using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

#region VeryPersonalAndImportantDontTouchOrOpen
[HelpURL("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=RDdQw4w9WgXcQ")]
#endregion
public class Player : NetworkBehaviour, IHitable
{
    private CharacterProperties _myCharacter;
    [SerializeField] private Renderer modelRenderer;
    [SerializeField] private Collider hitCollider;
    [SerializeField] private Canvas playerUI;
    [SerializeField] private TMP_Text hpLabel;

    [Networked, OnChangedRender(nameof(OnCharacterIdChanged))]
    public int CharacterID { get; set; }

    private CharacterProperties _character;
    private int _placeableAreaLayer;

    [Networked, OnChangedRender(nameof(OnHPChanged))]
    private float _hp { get; set; }

    [SerializeField] private float startingHp;
    [SerializeField] private float respawnDelay = 3f;

    [SerializeField] private float shootingCooldown;
    private float _nextShootTime;
    
    [Networked, OnChangedRender(nameof(OnDeathStateChanged))]
    private NetworkBool IsDead { get; set; }

    [Networked] private TickTimer RespawnTimer { get; set; }
    [Networked] private Vector3 SpawnPosition { get; set; }
    private string _cachedNickname;


    public override void Spawned()
    {
        base.Spawned();
        _cachedNickname = GetNickname();
        OnCharacterIdChanged();
        _placeableAreaLayer = LayerMask.NameToLayer("PlaceableArea");

        if (Object.HasStateAuthority)
        {
            SpawnPosition = transform.position;
            _hp = startingHp;
            IsDead = false;
        }

        if (playerUI) playerUI.gameObject.SetActive(Object.HasStateAuthority);

        OnDeathStateChanged();
    }

    public void SetCharacter(CharacterProperties character)
    {
        if (character == null) return;
        CharacterID = character.CharacterID;
        _character = character;
    }

    private void OnCharacterIdChanged()
    {        
        _myCharacter = CharacterProperties.GetByID(CharacterID);
        if (_myCharacter != null)
        {
            modelRenderer.material.color = _myCharacter.characterColor;
        }
    }

    private void OnHPChanged()
    {
        hpLabel.text = $"Health: {_hp:F2}";
    }
    
    private void OnDeathStateChanged()
    {
        if (modelRenderer) modelRenderer.enabled = !IsDead;
        if (hitCollider) hitCollider.enabled = !IsDead;
        if (playerUI) playerUI.gameObject.SetActive(!IsDead && Object.HasStateAuthority);
    }
    
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (!IsDead) return;

        if (RespawnTimer.Expired(Runner))
        {
            Respawn();
        }
    }

    void Update()
    {
        if (!Object.HasInputAuthority) return;
        
        if (Mouse.current.rightButton.wasPressedThisFrame && MatchManager.Instance)
        {            
            var screenPos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(screenPos);
            
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform.gameObject.layer == _placeableAreaLayer)
                {
                    MatchManager.Instance.RequestPlacePlaceable(CharacterID, hit.point);
                }
                else 
                {
                    // ik this is not optimal but it might just be 3am
                    var placeable = hit.transform.GetComponentInParent<PlaceableObject>();
                    if (placeable != null)
                    {
                        var netObj = placeable.GetComponent<NetworkObject>();
                        if (netObj != null)
                            MatchManager.Instance.RequestDeletePlaceable(netObj.Id);
                    }
                }
            }
        }

        if (Mouse.current.leftButton.isPressed && MatchManager.Instance && Time.time > _nextShootTime)
        {
            var screenPos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(screenPos);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 origin = transform.position;
                Vector3 direction = hit.point - origin;
                direction.y = 0f;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    MatchManager.Instance.RequestSpawnProjectile(CharacterID, origin, direction.normalized);
                    _nextShootTime = Time.time + shootingCooldown;
                }
            }
        }
    }

    public void OnHit(DamageData data)
    {
        Debug.Log($"[GUEST CHECK] OnHit called, HasStateAuthority: {Object.HasStateAuthority}, IsDead: {IsDead}, HP before: {_hp}, damage: {data.damage}");
        if (!Object.HasStateAuthority) return;
        if (IsDead) return;
    
        _hp -= data.damage;
        Debug.Log($"[GUEST CHECK] HP after: {_hp}");
    
        if (_hp <= 0)
        {
            _hp = 0;
            Die();
        }
    }

    private void Die()
    {
        IsDead = true;
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
        Debug.Log($"{_cachedNickname} has been killed!");
    }

    private void Respawn()
    {
        if (MatchManager.Instance)
            transform.position = MatchManager.Instance.GetRandomSpawnPosition();

        _hp = startingHp;
        IsDead = false;
        Debug.Log($"{_cachedNickname} respawned!");
    }
    
    private string GetNickname()
    {
        var dataObject = Runner.GetPlayerObject(Object.InputAuthority);
        if (!dataObject) return $"Player {CharacterID}";

        var playerData = dataObject.GetComponent<UI.PlayerData>();
        return playerData ? playerData.NickName.ToString() : $"Player {CharacterID}";
    }
}