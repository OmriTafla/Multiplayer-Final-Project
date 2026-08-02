using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;

[CreateAssetMenu(fileName = "CharacterProperties", menuName = "Scriptable Objects/CharacterProperties")]
public class CharacterProperties : ScriptableObject
{
    [Tooltip("Auto-generated unique ID. Do not touch.")]
    public int CharacterID {get => _id; private set => _id = value;}
    [SerializeField, ReadOnly]
    private int _id;

    [Space]
    public string characterName;
    public Color characterColor = Color.white;
    public GameObject spawnObject;
    public GameObject bullet;

    private static Dictionary<int, CharacterProperties> _characterRegistry;

    public static void InitializeRegistry(IEnumerable<CharacterProperties> characters)
    {
        _characterRegistry = new();

        if (characters is null)
            return;

        foreach (var character in characters)
            Register(character);
    }

    public static void Register(CharacterProperties character)
    {
        if (!character)
            return;

        _characterRegistry ??= new Dictionary<int, CharacterProperties>();

        if (_characterRegistry.ContainsKey(character.CharacterID)) return;

        _characterRegistry.Add(character.CharacterID, character);
    }

    public static CharacterProperties GetByID(int id)
    {
        if (_characterRegistry is not null && _characterRegistry.TryGetValue(id, out CharacterProperties character))
        {
            return character;
        }
        
        return null;
    }

#if UNITY_EDITOR

    [ContextMenu("Generate Unique ID")]
    private void GenerateID()
    {
        string path = UnityEditor.AssetDatabase.GetAssetPath(this);
        
        if (string.IsNullOrEmpty(path)) return;

        string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            
        CharacterID = Animator.StringToHash(guid);

        UnityEditor.EditorUtility.SetDirty(this);
    }
    private void OnValidate()
    {
        string path = UnityEditor.AssetDatabase.GetAssetPath(this);
        
        if (string.IsNullOrEmpty(path)) return;

        string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            
        CharacterID = Animator.StringToHash(guid);
    }
#endif
}
