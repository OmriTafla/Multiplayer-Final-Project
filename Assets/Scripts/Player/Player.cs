using System;
using System.Collections;
using Abb2kTools;
using Abb2kTools.Projectiles;
using DG.Tweening;
using Fusion;
using Managers;
using TMPro;
using UnityEngine;

public class Player : NetworkBehaviour, IHitable
{
    private const int SCORE_FOR_KILL = 10;

    #region Shader Property IDs
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    #endregion

    #region Inspector References - Core
    [SerializeField] private Renderer modelRenderer;
    [SerializeField] private Collider hitCollider;
    [SerializeField] private Rigidbody rigidBody;
    [SerializeField] private SpriteRenderer miniMapIconColor;
    [SerializeField] private CamShakeData hurtShake;
    [SerializeField] private Shooter shooter;
    #endregion

    #region Inspector References - Owner HUD
    [SerializeField] private Canvas playerUI;
    [SerializeField] private TMP_Text hpLabel;
    #endregion

    #region Inspector References - Overhead UI
    [SerializeField] private Canvas overheadUI;
    [SerializeField] private TMP_Text overheadNicknameLabel;
    [SerializeField] private TMP_Text overheadHpLabel;
    [SerializeField] private Transform overheadUIRoot;
    private Vector3 overheadUIFixedEulerAngles = new Vector3(90f, 0f, 0f);
    #endregion

    #region Inspector References - Local Movement Prediction
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float visualLeadStrength = 1f;
    [SerializeField] private float visualCatchUpSpeed = 10f;
    #endregion

    #region Inspector Tunables - Gameplay
    [SerializeField] private float startingHp = 10f;
    [SerializeField] private float movementSpeed = 5f;
    // [SerializeField] private float shootingCooldown = 0.5f;
    [SerializeField] private float respawnDelay = 3f;
    #endregion

    #region Networked State
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

    // [Networked] private TickTimer ShootCooldownTimer { get; set; }
    [Networked] private TickTimer RespawnTimer { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    // [Networked] public Vector3 LastFireDirection { get; private set; }

    public int LastFireTick { get; private set; }
    #endregion

    #region Private Runtime State
    private CharacterProperties character;
    private string cachedNickname;
    private bool controlsLocalCamera;
    private MaterialPropertyBlock materialPropertyBlock;
    private Renderer[] modelRenderers;
    private Collider[] collisionColliders;
    private TeamsManager teamsManager;

    private Vector3 _direction;
    private bool _pendingRespawn;
    private Vector3 _pendingRespawnPosition;
    private Vector3 visualLeadOffset;
    #endregion

    #region Unity Lifecycle
    private void Update()
    {
        if (!Object || !Object.HasInputAuthority || IsDead || !visualRoot)
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

    private void LateUpdate()
    {
        if (!overheadUIRoot)
            return;

        overheadUIRoot.position = transform.position + new Vector3(0, 1, 1);
    }

    private void FixedUpdate()
    {
        if (_pendingRespawn && rigidBody)
        {
            rigidBody.position = _pendingRespawnPosition;
            rigidBody.linearVelocity = Vector3.zero;
            rigidBody.angularVelocity = Vector3.zero;
            _pendingRespawn = false;
        }

        if (rigidBody && !IsDead)
        {
            rigidBody.AddForce(_direction * movementSpeed - rigidBody.linearVelocity, ForceMode.VelocityChange);
        }
    }
    #endregion

    #region Fusion Lifecycle
    public override void Spawned()
    {
        CacheReferences();
        OnCharacterIdChanged();

        if (Object.HasStateAuthority)
        {
            Hp = startingHp;
            IsDead = false;
            RespawnTimer = TickTimer.None;
        }

        StartCoroutine(ResolveNicknameWhenReady());

        if (overheadUIRoot)
        {
            overheadUIRoot.SetParent(null, true);
            overheadUIRoot.rotation = Quaternion.Euler(overheadUIFixedEulerAngles);
        }

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

        Move(input.Move);
        ProcessFire(input);
        ProcessAim(input);
        PreviousButtons = input.Buttons;
    }
    #endregion

    #region Public API
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
            if (hitBy.HasValue)
                Die(hitBy.Value);
            else
                Die();
        else
        {
            HurtShakeRPC();
            ApplyBodyFlashRPC();
        }
    }
    #endregion

    #region Movement / Aim / Fire
    private void Move(Vector2 movementInput)
    {
        movementInput = Vector2.ClampMagnitude(movementInput, 1f);

        _direction = new Vector3(
            movementInput.x,
            0f,
            movementInput.y);
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

    private void ProcessFire(GameplayInput input)
    {
        if (!input.Buttons.IsSet(GameplayButton.Fire))
            return;

        shooter.TryShoot();
        
        // if (!ShootCooldownTimer.ExpiredOrNotRunning(Runner))
        //     return;
        //
        // ShootCooldownTimer = TickTimer.CreateFromSeconds(Runner, shootingCooldown);
        // LastFireTick = Runner.Tick;
        // LastFireDirection = transform.forward.normalized;
        //
        // if (Object.HasStateAuthority)
        // {
        //     var placementManager = FindAnyObjectByType<PlacementManager>();
        //     placementManager?.SpawnProjectile(Object, transform.position, LastFireDirection);
        // }
    }
    #endregion

    #region Death / Respawn
    private void Die()
    {
        if (!Object.HasStateAuthority)
            return;

        IsDead = true;
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
        Debug.Log($"{cachedNickname} died");
    }

    private void Die(PlayerRef killer)
    {
        if (!Object.HasStateAuthority)
            return;
        
        ScoreManager.Instance?.AddScoreForKillingPlayer(killer, Object.InputAuthority);
        ScoreManager.Instance?.ResetPlayerScore(Object.InputAuthority);
        
        Die();
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
    #endregion

    #region RPCs - Hit Feedback
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void HurtShakeRPC()
    {
        CameraShaker.Instance.Shake(hurtShake);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ApplyBodyFlashRPC()
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
    #endregion

    #region Visual Presentation - Model Color / Hit Flash
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
    #endregion

    #region Visual Presentation - HP / Death State / UI Visibility
    private void OnHpChanged()
    {
        if (hpLabel)
            hpLabel.text = $"Health: {Hp:F0}";

        if (overheadHpLabel)
            overheadHpLabel.text = $"{Hp:F0}";
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

        if (overheadUI)
            overheadUI.gameObject.SetActive(!IsDead);

        if (controlsLocalCamera && !IsDead)
            LocalPlayerCamera.Instance?.SnapToTarget();
    }

    private void ConfigureLocalPresentation()
    {
        if (playerUI)
            playerUI.gameObject.SetActive(Object.HasInputAuthority && !IsDead);
    }
    #endregion

    #region Nickname Logic
    private IEnumerator ResolveNicknameWhenReady()
    {
        string resolved;
        var attempts = 0;

        do
        {
            resolved = GetNickname();
            attempts++;

            if (!string.IsNullOrEmpty(resolved))
                break;

            yield return new WaitForSeconds(0.1f);
        }
        while (attempts < 50);

        cachedNickname = string.IsNullOrEmpty(resolved)
            ? $"Player {Object.InputAuthority.PlayerId}"
            : resolved;

        if (overheadNicknameLabel)
            overheadNicknameLabel.text = cachedNickname;
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
    #endregion

    #region Shared Helpers
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
    #endregion
}