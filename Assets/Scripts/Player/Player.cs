using System;
using System.Collections;
using System.Collections.Generic;
using Abb2kTools;
using DG.Tweening;
using Fusion;
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
    [SerializeField] private NetworkTransform networkTransform;
    [SerializeField] private float collisionSkin = 0.02f;
    [SerializeField] private Animator animator;
    private readonly int animWalkId = Animator.StringToHash("IsWalking");
    [SerializeField] private SpriteRenderer miniMapIconColor;
    [SerializeField] private CamShakeData hurtShake;
    [SerializeField] private AudioSource hurtSource;
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
    [SerializeField] private Vector3 overheadOffset = new Vector3(0f, 1f, 1f);
    [SerializeField] private Vector3 overheadEulerAngles = new Vector3(90f, 0f, 0f);
    #endregion

    #region Inspector Tunables - Gameplay
    [SerializeField] private float startingHp = 10f;
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField, Min(0f)] private float healthRegenerationDelay = 5f;
    [SerializeField, Min(0.05f)] private float healthRegenerationInterval = 1f;
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

    [Networked] private TickTimer RespawnTimer { get; set; }
    [Networked] private TickTimer HealthRegenerationDelayTimer { get; set; }
    [Networked] private TickTimer HealthRegenerationIntervalTimer { get; set; }
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
    #endregion

    #region Unity Lifecycle
    private void LateUpdate()
    {
        if (!overheadUIRoot)
            return;

        overheadUIRoot.SetPositionAndRotation(
            transform.position + overheadOffset,
            Quaternion.Euler(overheadEulerAngles));
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
            RespawnTimer = TickTimer.None;
            HealthRegenerationDelayTimer = TickTimer.None;
            HealthRegenerationIntervalTimer = TickTimer.None;
        }

        StartCoroutine(ResolveNicknameWhenReady());

        OnTeamColorChanged();
        OnHpChanged();
        OnDeathStateChanged();
        ConfigureLocalPresentation();
        ConfigureRigidbody();

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
        ProcessRespawn();
        ProcessHealthRegeneration();
        SetCollisionCollidersEnabled(!IsDead);

        if (IsDead)
        {
            _direction = Vector3.zero;
            return;
        }

        if (!GetInput<GameplayInput>(out var input))
        {
            _direction = Vector3.zero;
            return;
        }

        Move(input.Move);
        ProcessAim(input);

        if (Object.HasStateAuthority)
            ProcessFire(input);
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

        var previousHp = Hp;
        Hp = Mathf.Max(0f, Hp - data.damage);

        if (Hp >= previousHp)
            return;

        if (Hp <= 0f)
        {
            if (hitBy.HasValue)
                Die(hitBy.Value);
            else
                Die();
        }
        else
        {
            RestartHealthRegeneration();
            HurtShakeRPC();
            ApplyBodyFlashRPC();
        }
    }
    #endregion

    #region Movement / Aim / Fire
    private void Move(Vector2 movementInput)
    {
        if (!IsFinite(movementInput.x) || !IsFinite(movementInput.y))
            movementInput = Vector2.zero;

        movementInput = Vector2.ClampMagnitude(movementInput, 1f);

        _direction = new Vector3(
            movementInput.x,
            0f,
            movementInput.y);

        var displacement = _direction * movementSpeed * Runner.DeltaTime;

        if (!rigidBody || displacement.sqrMagnitude <= 0.000001f)
            return;

        Physics.SyncTransforms();

        for (var iteration = 0; iteration < 2; iteration++)
        {
            var distance = displacement.magnitude;

            if (distance <= 0.0001f)
                break;

            var direction = displacement / distance;

            if (!rigidBody.SweepTest(
                    direction,
                    out var hit,
                    distance + collisionSkin,
                    QueryTriggerInteraction.Ignore))
            {
                transform.position += displacement;
                break;
            }

            var moveDistance = Mathf.Clamp(
                hit.distance - collisionSkin,
                0f,
                distance);

            transform.position += direction * moveDistance;

            var normal = hit.normal;
            normal.y = 0f;

            if (normal.sqrMagnitude <= 0.0001f)
                break;

            displacement = Vector3.ProjectOnPlane(
                displacement - direction * moveDistance,
                normal.normalized);

            if (displacement.sqrMagnitude <= 0.000001f)
                break;

            Physics.SyncTransforms();
        }

        Physics.SyncTransforms();
    }

    private void ProcessAim(GameplayInput input)
    {
        if (!IsFinite(input.AimPosition.x) ||
            !IsFinite(input.AimPosition.y) ||
            !IsFinite(input.AimPosition.z))
        {
            return;
        }

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
    }
    #endregion

    #region Death / Respawn
    private void Die()
    {
        if (!Object.HasStateAuthority)
            return;

        IsDead = true;
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
        HealthRegenerationDelayTimer = TickTimer.None;
        HealthRegenerationIntervalTimer = TickTimer.None;
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
        var respawnPosition = MatchManager.Instance.GetSpawnPosition(
            Object.InputAuthority);

        if (networkTransform)
            networkTransform.Teleport(respawnPosition, transform.rotation);
        else
            transform.position = respawnPosition;

        _direction = Vector3.zero;
        Physics.SyncTransforms();

        Hp = startingHp;
        IsDead = false;
        RespawnTimer = TickTimer.None;
        HealthRegenerationDelayTimer = TickTimer.None;
        HealthRegenerationIntervalTimer = TickTimer.None;

        Debug.Log($"{cachedNickname} respawned");
    }
    #endregion

    #region Health Regeneration
    private void RestartHealthRegeneration()
    {
        HealthRegenerationDelayTimer = TickTimer.CreateFromSeconds(
            Runner,
            healthRegenerationDelay);
        HealthRegenerationIntervalTimer = TickTimer.None;
    }

    private void ProcessHealthRegeneration()
    {
        if (!Object.HasStateAuthority || IsDead)
            return;

        if (Hp >= startingHp)
        {
            HealthRegenerationDelayTimer = TickTimer.None;
            HealthRegenerationIntervalTimer = TickTimer.None;
            return;
        }

        if (!HealthRegenerationDelayTimer.ExpiredOrNotRunning(Runner))
            return;

        if (HealthRegenerationDelayTimer.Expired(Runner))
        {
            HealthRegenerationDelayTimer = TickTimer.None;
            RegenerateHealthPoint();
            return;
        }

        if (!HealthRegenerationIntervalTimer.ExpiredOrNotRunning(Runner))
            return;

        RegenerateHealthPoint();
    }

    private void RegenerateHealthPoint()
    {
        Hp = Mathf.Min(startingHp, Hp + 1f);

        HealthRegenerationIntervalTimer = Hp < startingHp
            ? TickTimer.CreateFromSeconds(
                Runner,
                Mathf.Max(healthRegenerationInterval, Runner.DeltaTime))
            : TickTimer.None;
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

    private void SetCollisionCollidersEnabled(bool value)
    {
        if (collisionColliders is null)
            return;

        foreach (var collider in collisionColliders)
        {
            if (collider && collider.enabled != value)
                collider.enabled = value;
        }
    }

    private void ConfigureRigidbody()
    {
        if (!rigidBody)
            return;

        rigidBody.useGravity = false;
        rigidBody.isKinematic = true;
        rigidBody.interpolation = RigidbodyInterpolation.None;
        rigidBody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion
}
