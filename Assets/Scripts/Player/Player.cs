using System;
using Abb2kTools;
using DG.Tweening;
using Fusion;
using Managers;
using TMPro;
using UnityEngine;

public class Player : NetworkBehaviour, IHitable
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    [SerializeField] private Renderer modelRenderer;
    [SerializeField] private Collider hitCollider;
    [SerializeField] private Rigidbody rigidBody;
    [SerializeField] private Canvas playerUI;
    [SerializeField] private TMP_Text hpLabel;
    [SerializeField] private float startingHp = 10f;
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float shootingCooldown = 0.5f;
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private SpriteRenderer miniMapIconColor;
    [SerializeField] private CamShakeData hurtShake;
    
    [Networked, OnChangedRender(nameof(OnCharacterIdChanged))]
    public int CharacterID { get; private set; }

    [Networked]
    public int TeamId { get; private set; } = -1;

    [Networked, OnChangedRender(nameof(OnTeamColorChanged))]
    public Color TeamColor { get; private set; }

    [Networked, OnChangedRender(nameof(OnHpChanged))]
    public float Hp { get; private set; }

    [Networked, OnChangedRender(nameof(OnDeathStateChanged))]
    public NetworkBool IsDead { get; private set; }

    [Networked] private TickTimer ShootCooldownTimer { get; set; }
    [Networked] private TickTimer RespawnTimer { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    public int LastFireTick { get; private set; }

    [Networked] public Vector3 LastFireDirection { get; private set; }

    private CharacterProperties character;
    private string cachedNickname;
    private bool controlsLocalCamera;
    private MaterialPropertyBlock materialPropertyBlock;
    private Renderer[] modelRenderers;
    private Collider[] collisionColliders;
    private TeamsManager teamsManager;
    
    // Physics movement buffer
    private Vector3 _direction;
    private bool _pendingRespawn;
    private Vector3 _pendingRespawnPosition;
    
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float visualLeadStrength = 1f;
    [SerializeField] private float visualCatchUpSpeed = 10f;

    private Vector3 visualLeadOffset;

    private void Update()
    {
        if (!Object || !Object.HasInputAuthority || IsDead || visualRoot == null)
            return;

        var rawInput = FusionInputProvider.Instance != null
            ? FusionInputProvider.Instance.CurrentMoveInput
            : Vector2.zero;

        var desiredDirection = new Vector3(rawInput.x, 0f, rawInput.y);

        visualLeadOffset += desiredDirection * (visualLeadStrength * Time.deltaTime);

        visualLeadOffset = Vector3.Lerp(
            visualLeadOffset,
            Vector3.zero,
            visualCatchUpSpeed * Time.deltaTime);

        visualRoot.localPosition = visualLeadOffset;
    }
    
    public override void FixedUpdateNetwork()
    {
        if (!GetInput<GameplayInput>(out var input))
        {
            ProcessRespawn();
            _direction = Vector3.zero;
            return;
        }

        if (IsDead)
        {
            ProcessRespawn();
            PreviousButtons = input.Buttons;
            _direction = Vector3.zero;
            return;
        }

        // Cache direction from networked input to be processed in FixedUpdate
        Move(input.Move);
        ProcessFire(input);
        ProcessAim(input);
        PreviousButtons = input.Buttons;
    }

    private void FixedUpdate()
    {
        // Handle pending respawn position reset
        if (_pendingRespawn && rigidBody != null)
        {
            rigidBody.position = _pendingRespawnPosition;
            rigidBody.linearVelocity = Vector3.zero;
            rigidBody.angularVelocity = Vector3.zero;
            _pendingRespawn = false;
        }

        // Apply forces to Rigidbody strictly in Unity's FixedUpdate
        if (rigidBody != null && !IsDead)
        {
            rigidBody.AddForce(_direction * movementSpeed - rigidBody.linearVelocity, ForceMode.VelocityChange);
        }
    }

    public override void Spawned()
    {
        CacheReferences();
        OnCharacterIdChanged();

        if (Object.HasStateAuthority)
        {
            Hp = startingHp;
            IsDead = false;
            ShootCooldownTimer = TickTimer.None;
            RespawnTimer = TickTimer.None;
        }

        cachedNickname = GetNickname();

        OnTeamColorChanged();
        OnHpChanged();
        OnDeathStateChanged();
        ConfigureLocalPresentation();

        controlsLocalCamera = Object.HasInputAuthority;

        if (controlsLocalCamera)
            LocalPlayerCamera.Instance?.SetTarget(transform);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (controlsLocalCamera)
            LocalPlayerCamera.Instance?.ClearTarget(transform);
    }

    public void SetCharacter(CharacterProperties newCharacter)
    {
        if (!Object.HasStateAuthority || newCharacter == null)
            return;

        CharacterID = newCharacter.CharacterID;
        ApplyDisplayColor();
    }

    public void SetTeam(int teamId, Color teamColor)
    {
        if (!Object.HasStateAuthority)
            return;

        TeamId = teamId;
        TeamColor = teamColor;
        ApplyDisplayColor();
    }

    public Collider[] GetCollisionColliders()
    {
        CacheReferences();
        return collisionColliders;
    }

    public bool TryReceiveHit(PlayerRef attacker, DamageData data)
    {
        if (!Object.HasStateAuthority || IsDead)
            return false;

        teamsManager ??= FindAnyObjectByType<TeamsManager>();

        if (teamsManager != null && !teamsManager.CanDamage(attacker, Object.InputAuthority))
            return false;

        if (data.damage < DamageData.MIN_POSSIBLE_DAMAGE ||
            data.damage > DamageData.MAX_POSSIBLE_DAMAGE)
        {
            return false;
        }

        var previousHp = Hp;
        OnHit(data, attacker);
        return Hp < previousHp;
    }

    private void ProcessAim(GameplayInput input)
    {
        var aimPoint = new Vector3(
            input.AimPosition.x,
            transform.position.y,
            input.AimPosition.z);

        var aimDirection = aimPoint - transform.position;

        if (aimDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(aimDirection);
    }

    private void Move(Vector2 movementInput)
    {
        movementInput = Vector2.ClampMagnitude(movementInput, 1f);

        _direction = new Vector3(
            movementInput.x,
            0f,
            movementInput.y);
    }

    private void ProcessFire(GameplayInput input)
    {
        if (!input.Buttons.IsSet(GameplayButton.Fire))
            return;

        if (!ShootCooldownTimer.ExpiredOrNotRunning(Runner))
            return;

        ShootCooldownTimer = TickTimer.CreateFromSeconds(Runner, shootingCooldown);
        LastFireTick = Runner.Tick;
        LastFireDirection = transform.forward.normalized;

        if (Object.HasStateAuthority)
        {
            var placementManager = FindAnyObjectByType<PlacementManager>();
            placementManager?.SpawnProjectile(Object, transform.position, LastFireDirection);
        }
    }

    public void OnHit(DamageData data, PlayerRef? hitBy)
    {
        if (!Object.HasStateAuthority || IsDead)
            return;

        if (data.damage < DamageData.MIN_POSSIBLE_DAMAGE ||
            data.damage > DamageData.MAX_POSSIBLE_DAMAGE)
        {
            return;
        }

        Hp = Mathf.Max(0f, Hp - data.damage);

        if (Hp <= 0f)
            Die();
        else
        {
            HurtShakeRPC();
            ApplyBodyFlashRPC();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    void HurtShakeRPC()
    {
        CameraShaker.Instance.Shake(hurtShake);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void ApplyBodyFlashRPC()
    {
        Color ogColor = TeamColor.a > 0f 
            ? TeamColor 
            : (character != null ? character.characterColor : Color.white);

        foreach (var model in modelRenderers)
        {
            DOTween.Kill(model);

            DOTween.Sequence()
                .Append(DOTween.To(() => ogColor, x => UpdateHitColor(model, x), Color.white, .05f))
                .Append(DOTween.To(() => Color.white, x => UpdateHitColor(model, x), Color.red, .1f))
                .Append(DOTween.To(() => Color.red, x => UpdateHitColor(model, x), ogColor, .15f))
                .SetTarget(model)
                .SetLink(model.gameObject);
        }
    }

    private void UpdateHitColor(Renderer renderer, Color color)
    {
        if (!renderer) return;

        materialPropertyBlock ??= new MaterialPropertyBlock();
        var materials = renderer.sharedMaterials;

        if (materials.Length == 0)
        {
            renderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetColor(BaseColorId, color);
            materialPropertyBlock.SetColor(ColorId, color);
            materialPropertyBlock.SetColor(TintColorId, color);
            renderer.SetPropertyBlock(materialPropertyBlock);
            return;
        }

        for (var i = 0; i < materials.Length; i++)
        {
            renderer.GetPropertyBlock(materialPropertyBlock, i);
            materialPropertyBlock.SetColor(BaseColorId, color);
            materialPropertyBlock.SetColor(ColorId, color);
            materialPropertyBlock.SetColor(TintColorId, color);
            renderer.SetPropertyBlock(materialPropertyBlock, i);
        }
    }

    private void Die()
    {
        if (!Object.HasStateAuthority)
            return;

        IsDead = true;
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
        Debug.Log($"{cachedNickname} died");
    }

    private void ProcessRespawn()
    {
        if (!Object.HasStateAuthority || !IsDead)
            return;

        if (RespawnTimer.Expired(Runner))
            Respawn();
    }

    private void Respawn()
    {
        _pendingRespawnPosition = MatchManager.Instance.GetRandomSpawnPosition();
        _pendingRespawn = true;

        Hp = startingHp;
        IsDead = false;
        RespawnTimer = TickTimer.None;

        Debug.Log($"{cachedNickname} respawned");
    }

    private void OnCharacterIdChanged()
    {
        character = CharacterProperties.GetByID(CharacterID);
        ApplyDisplayColor();
    }

    private void OnTeamColorChanged()
    {
        ApplyDisplayColor();
    }

    private void ApplyDisplayColor()
    {
        CacheReferences();

        if (modelRenderers == null || modelRenderers.Length == 0)
            return;

        var displayColor = TeamColor.a > 0f
            ? TeamColor
            : character != null
                ? character.characterColor
                : Color.white;

        materialPropertyBlock ??= new MaterialPropertyBlock();

        foreach (var renderer in modelRenderers)
        {
            if (!renderer)
                continue;

            var materials = renderer.sharedMaterials;

            if (materials.Length == 0)
            {
                materialPropertyBlock.Clear();
                renderer.GetPropertyBlock(materialPropertyBlock);
                materialPropertyBlock.SetColor(BaseColorId, displayColor);
                materialPropertyBlock.SetColor(ColorId, displayColor);
                materialPropertyBlock.SetColor(TintColorId, displayColor);
                renderer.SetPropertyBlock(materialPropertyBlock);
                continue;
            }

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                materialPropertyBlock.Clear();
                renderer.GetPropertyBlock(materialPropertyBlock, materialIndex);
                materialPropertyBlock.SetColor(BaseColorId, displayColor);
                materialPropertyBlock.SetColor(ColorId, displayColor);
                materialPropertyBlock.SetColor(TintColorId, displayColor);
                renderer.SetPropertyBlock(materialPropertyBlock, materialIndex);
            }
        }
    }

    private void OnHpChanged()
    {
        if (hpLabel)
            hpLabel.text = $"Health: {Hp:F0}";
    }

    private void OnDeathStateChanged()
    {
        CacheReferences();

        if (modelRenderers != null)
        {
            foreach (var renderer in modelRenderers)
            {
                if (renderer)
                    renderer.enabled = !IsDead;
            }
        }

        if (collisionColliders != null)
        {
            foreach (var collider in collisionColliders)
            {
                if (collider)
                    collider.enabled = !IsDead;
            }
        }

        if (playerUI)
            playerUI.gameObject.SetActive(!IsDead && Object.HasInputAuthority);

        if (controlsLocalCamera && !IsDead)
            LocalPlayerCamera.Instance?.SnapToTarget();
    }

    private void ConfigureLocalPresentation()
    {
        if (playerUI)
            playerUI.gameObject.SetActive(Object.HasInputAuthority && !IsDead);
    }

    private void CacheReferences()
    {
        if (modelRenderers == null || modelRenderers.Length == 0)
            modelRenderers = GetComponentsInChildren<Renderer>(true);

        if (collisionColliders == null || collisionColliders.Length == 0)
            collisionColliders = GetComponentsInChildren<Collider>(true);

        if (modelRenderer == null && modelRenderers.Length > 0)
            modelRenderer = modelRenderers[0];

        if (hitCollider == null && collisionColliders.Length > 0)
            hitCollider = collisionColliders[0];

        teamsManager ??= FindAnyObjectByType<TeamsManager>();
    }

    private string GetNickname()
    {
        var dataObject = Runner.GetPlayerObject(Object.InputAuthority);

        if (!dataObject)
            return $"Player {Object.InputAuthority.PlayerId}";

        var playerData = dataObject.GetComponent<UI.PlayerData>();

        return playerData != null
            ? playerData.NickName.ToString()
            : $"Player {Object.InputAuthority.PlayerId}";
    }
}