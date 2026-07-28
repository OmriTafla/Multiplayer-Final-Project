using Fusion;
using TMPro;
using UnityEngine;

public class Player : NetworkBehaviour, IHitable
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

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

    private CharacterProperties character;
    private string cachedNickname;
    private bool controlsLocalCamera;
    private MaterialPropertyBlock materialPropertyBlock;

    public override void Spawned()
    {
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

    public override void FixedUpdateNetwork()
    {
        if (!GetInput<GameplayInput>(out var input))
        {
            ProcessRespawn();
            return;
        }

        if (IsDead)
        {
            ProcessRespawn();
            PreviousButtons = input.Buttons;
            return;
        }

        Move(input.Move);
        ProcessFire(input);
        ProcessAim(input);
        PreviousButtons = input.Buttons;
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

        var direction = new Vector3(
            movementInput.x,
            0f,
            movementInput.y);

        var nextPosition = transform.position +
                           direction *
                           movementSpeed *
                           Runner.DeltaTime;

        if (rigidBody != null)
        {
            rigidBody.MovePosition(nextPosition);
            return;
        }

        transform.position = nextPosition;
    }

    private void ProcessFire(GameplayInput input)
    {
        if (!input.Buttons.IsSet(GameplayButton.Fire))
            return;

        if (!ShootCooldownTimer.ExpiredOrNotRunning(Runner))
            return;

        if (!Object.HasStateAuthority)
            return;

        ShootCooldownTimer = TickTimer.CreateFromSeconds(Runner, shootingCooldown);

        var placementManager = FindAnyObjectByType<PlacementManager>();

        if (placementManager == null)
            return;

        placementManager.SpawnProjectile(
            Object,
            transform.position,
            transform.forward.normalized);
    }

    public void OnHit(DamageData data)
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
        var spawnPosition = MatchManager.Instance.GetRandomSpawnPosition();

        if (rigidBody != null)
        {
            rigidBody.position = spawnPosition;
            rigidBody.linearVelocity = Vector3.zero;
            rigidBody.angularVelocity = Vector3.zero;
        }
        else
        {
            transform.position = spawnPosition;
        }

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
        if (modelRenderer == null)
            return;

        var displayColor = TeamColor.a > 0f
            ? TeamColor
            : character != null
                ? character.characterColor
                : Color.white;

        materialPropertyBlock ??= new MaterialPropertyBlock();

        modelRenderer.GetPropertyBlock(materialPropertyBlock);
        materialPropertyBlock.SetColor(BaseColorId, displayColor);
        materialPropertyBlock.SetColor(ColorId, displayColor);
        modelRenderer.SetPropertyBlock(materialPropertyBlock);
        miniMapIconColor.color = TeamColor;
    }

    private void OnHpChanged()
    {
        if (hpLabel != null)
            hpLabel.text = $"Health: {Hp:F0}";
    }

    private void OnDeathStateChanged()
    {
        if (modelRenderer != null)
            modelRenderer.enabled = !IsDead;

        if (hitCollider != null)
            hitCollider.enabled = !IsDead;

        if (playerUI != null)
            playerUI.gameObject.SetActive(!IsDead && Object.HasInputAuthority);

        if (controlsLocalCamera && !IsDead)
            LocalPlayerCamera.Instance?.SnapToTarget();
    }

    private void ConfigureLocalPresentation()
    {
        if (playerUI != null)
            playerUI.gameObject.SetActive(Object.HasInputAuthority && !IsDead);
    }

    private string GetNickname()
    {
        var dataObject = Runner.GetPlayerObject(Object.InputAuthority);

        if (dataObject == null)
            return $"Player {Object.InputAuthority.PlayerId}";

        var playerData = dataObject.GetComponent<UI.PlayerData>();

        return playerData != null
            ? playerData.NickName.ToString()
            : $"Player {Object.InputAuthority.PlayerId}";
    }
}
