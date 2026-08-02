using System;
using System.Collections;
using System.Collections.Generic;
using Abb2kTools;
using DG.Tweening;
using Fusion;
using Fusion.Addons.Physics;
using TMPro;
using UnityEngine;

public class Player : NetworkBehaviour, IHitable
{
    #region Shader Property IDs
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    #endregion

    #region Inspector References - Core
    [SerializeField] private Renderer[] modelRenderers;
    [SerializeField] private Collider[] collisionColliders;
    [SerializeField] private Rigidbody rigidBody;
    [SerializeField] private Animator animator;
    private readonly int animWalkId = Animator.StringToHash("IsWalking");
    [SerializeField] private Animator cannonAnimator;
    private readonly int animShootId = Animator.StringToHash("Shoot");
    [SerializeField] private SpriteRenderer miniMapIconColor;
    [SerializeField] private CamShakeData hurtShake;
    [SerializeField] private AudioSource hurtSource;
    [SerializeField] private AudioSource shootSource;
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
    [SerializeField] private Vector3 overheadOffset = new Vector3(0f, 1f, 1f);
    [SerializeField] private Vector3 overheadEulerAngles = new Vector3(90f, 0f, 0f);
    #endregion

    #region Inspector References - Local Movement Prediction
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float visualLeadStrength = 1f;
    [SerializeField] private float visualCatchUpSpeed = 10f;
    #endregion

    #region Inspector Tunables - Gameplay
    [SerializeField] private float startingHp = 10f;
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float shootingCooldown = 0.5f;
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

    [Networked] private TickTimer ShootCooldownTimer { get; set; }
    [Networked] private TickTimer RespawnTimer { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked] public Vector3 LastFireDirection { get; private set; }

    public int LastFireTick { get; private set; }
    #endregion

    #region Private Runtime State
    private static readonly Dictionary<PlayerRef, Player> SpawnedPlayers = new();

    private CharacterProperties character;
    private string cachedNickname;
    private bool controlsLocalCamera;
    private MaterialPropertyBlock materialPropertyBlock;
    private int[] modelMaterialSlotCounts;
    private PlayerRef registeredPlayer = PlayerRef.None;

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

        var rawInput = FusionInputProvider.Instance
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

        overheadUIRoot.SetPositionAndRotation(
            transform.position + overheadOffset,
            Quaternion.Euler(overheadEulerAngles));
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
        CacheMaterialSlotCounts();
        registeredPlayer = Object.InputAuthority;

        if (registeredPlayer != PlayerRef.None)
            SpawnedPlayers[registeredPlayer] = this;

        OnCharacterIdChanged();

        if (Object.HasStateAuthority)
        {
            Hp = startingHp;
            IsDead = false;
            ShootCooldownTimer = TickTimer.None;
            RespawnTimer = TickTimer.None;
        }

        StartCoroutine(ResolveNicknameWhenReady());

        OnTeamColorChanged();
        OnHpChanged();
        OnDeathStateChanged();
        ConfigureLocalPresentation();

        controlsLocalCamera = Object.HasInputAuthority;

        var localCamera = LocalPlayerCamera.Instance;

        if (controlsLocalCamera && localCamera)
            localCamera.SetTarget(transform);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Unregister();

        var localCamera = LocalPlayerCamera.Instance;

        if (controlsLocalCamera && localCamera)
            localCamera.ClearTarget(transform);
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

    public override void Render()
    {
        animator.SetBool(animWalkId, _direction.sqrMagnitude > 0.0001f);
    }
    #endregion

    #region Public API
    public void SetCharacter(CharacterProperties newCharacter)
    {
        if (!Object.HasStateAuthority || !newCharacter)
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
        return collisionColliders;
    }

    public static bool TryGet(PlayerRef player, out Player playerAvatar)
    {
        if (SpawnedPlayers.TryGetValue(player, out playerAvatar) &&
            playerAvatar &&
            playerAvatar.Object &&
            playerAvatar.Object.IsValid)
        {
            return true;
        }

        SpawnedPlayers.Remove(player);
        playerAvatar = null;
        return false;
    }

    public bool TryReceiveHit(PlayerRef attacker, DamageData data)
    {
        if (!Object.HasStateAuthority || IsDead)
            return false;

        var matchManager = MatchManager.Instance;
        var teamsManager = matchManager ? matchManager.TeamsManager : null;

        if (teamsManager && !teamsManager.CanDamage(attacker, Object.InputAuthority))
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
        {
            if (hitBy.HasValue)
                Die(hitBy.Value);
            else
                Die();
        }
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
        // if (Runner.IsForward)
        // {
        //     cannonAnimator.SetTrigger(animShootId);
        //     shootSource.Stop();
        //     shootSource.Play();
        // }
        //
        // if (Object.HasStateAuthority)
        // {
        //     PlayShootEffectsRPC();
        //
        //     var matchManager = MatchManager.Instance;
        //     var placementManager = matchManager
        //         ? matchManager.PlacementManager
        //         : null;
        //
        //     if (placementManager)
        //     {
        //         placementManager.SpawnProjectile(
        //             Object,
        //             transform.position,
        //             LastFireDirection);
        //     }
        // }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void PlayShootEffectsRPC()
    {
        if (cannonAnimator) 
            cannonAnimator.SetTrigger(animShootId);

        if (shootSource)
        {
            shootSource.Stop();
            shootSource.Play();
        }
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

        var scoreManager = ScoreManager.Instance;

        if (scoreManager)
        {
            scoreManager.AddScoreForKillingPlayer(killer, Object.InputAuthority);
            scoreManager.ResetPlayerScore(Object.InputAuthority);
        }

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
        _pendingRespawnPosition = MatchManager.Instance.GetSpawnPosition(Object.InputAuthority);
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

        PostProcessingEffectPlayer.Instance.RunVignetteEffect(.1f, 0, 1.5f, .3f, Color.red);
        PostProcessingEffectPlayer.Instance.RunFilmGrainEffect(.1f, 0, 1.5f, .7f);

        hurtSource.Stop();
        hurtSource.Play();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ApplyBodyFlashRPC()
    {
        Color ogColor = TeamColor.a > 0f
            ? TeamColor
            : (character ? character.characterColor : Color.white);

        for (var modelIndex = 0; modelIndex < modelRenderers.Length; modelIndex++)
        {
            var model = modelRenderers[modelIndex];

            if (!model)
                continue;

            var materialSlotCount = modelMaterialSlotCounts[modelIndex];

            DOTween.Kill(model);

            DOTween.Sequence()
                .Append(DOTween.To(() => ogColor, x => UpdateHitColor(model, materialSlotCount, x), Color.white, .05f))
                .Append(DOTween.To(() => Color.white, x => UpdateHitColor(model, materialSlotCount, x), Color.red, .1f))
                .Append(DOTween.To(() => Color.red, x => UpdateHitColor(model, materialSlotCount, x), ogColor, .15f))
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
        if (modelRenderers is null || modelRenderers.Length == 0)
            return;

        CacheMaterialSlotCounts();

        var displayColor = TeamColor.a > 0f
            ? TeamColor
            : character
                ? character.characterColor
                : Color.white;

        materialPropertyBlock ??= new MaterialPropertyBlock();

        for (var rendererIndex = 0; rendererIndex < modelRenderers.Length; rendererIndex++)
        {
            var renderer = modelRenderers[rendererIndex];

            if (!renderer)
                continue;

            var materialSlotCount = modelMaterialSlotCounts[rendererIndex];

            if (materialSlotCount == 0)
            {
                materialPropertyBlock.Clear();
                renderer.GetPropertyBlock(materialPropertyBlock);
                materialPropertyBlock.SetColor(BaseColorId, displayColor);
                materialPropertyBlock.SetColor(ColorId, displayColor);
                materialPropertyBlock.SetColor(TintColorId, displayColor);
                renderer.SetPropertyBlock(materialPropertyBlock);
                continue;
            }

            for (var materialIndex = 0; materialIndex < materialSlotCount; materialIndex++)
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

    private void UpdateHitColor(
        Renderer renderer,
        int materialSlotCount,
        Color color)
    {
        if (!renderer) return;

        materialPropertyBlock ??= new MaterialPropertyBlock();

        if (materialSlotCount == 0)
        {
            renderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetColor(BaseColorId, color);
            materialPropertyBlock.SetColor(ColorId, color);
            materialPropertyBlock.SetColor(TintColorId, color);
            renderer.SetPropertyBlock(materialPropertyBlock);
            return;
        }

        for (var i = 0; i < materialSlotCount; i++)
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
        if (modelRenderers is not null)
        {
            foreach (var renderer in modelRenderers)
            {
                if (renderer)
                    renderer.enabled = !IsDead;
            }
        }

        if (collisionColliders is not null)
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

        var localCamera = LocalPlayerCamera.Instance;

        if (controlsLocalCamera && !IsDead && localCamera)
            localCamera.SnapToTarget();
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
        return UI.PlayerData.TryGet(Object.InputAuthority, out var playerData)
            ? playerData.NickName.ToString()
            : $"Player {Object.InputAuthority.PlayerId}";
    }
    #endregion

    #region Shared Helpers
    private void OnDestroy()
    {
        Unregister();
    }

    private void CacheMaterialSlotCounts()
    {
        if (modelRenderers is null)
        {
            modelMaterialSlotCounts = Array.Empty<int>();
            return;
        }

        if (modelMaterialSlotCounts is not null &&
            modelMaterialSlotCounts.Length == modelRenderers.Length)
        {
            return;
        }

        modelMaterialSlotCounts = new int[modelRenderers.Length];

        for (var index = 0; index < modelRenderers.Length; index++)
        {
            var renderer = modelRenderers[index];
            modelMaterialSlotCounts[index] = renderer
                ? renderer.sharedMaterials.Length
                : 0;
        }
    }

    private void Unregister()
    {
        if (registeredPlayer == PlayerRef.None)
            return;

        if (SpawnedPlayers.TryGetValue(registeredPlayer, out var playerAvatar) &&
            playerAvatar == this)
        {
            SpawnedPlayers.Remove(registeredPlayer);
        }

        registeredPlayer = PlayerRef.None;
    }
    #endregion
}
