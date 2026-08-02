using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CharacterButton : MonoBehaviour
{
    public event UnityAction<int> OnSelected;

    private CharacterProperties _myCharacter;

    [SerializeField]
    private Image _btnSpr;
    [SerializeField]
    private Button _btn;
    [SerializeField]
    private TMP_Text _name;
    [SerializeField]
    private TMP_Text _spawnName;

    public int? MyCharacterID => _myCharacter ? _myCharacter.CharacterID : null;

    public void Setup(CharacterProperties character)
    {
        _myCharacter = character;

        _btnSpr.color = character.characterColor;

        _name.text = $"Name: {character.characterName}";
        _spawnName.text = $"Spawns: {(character.spawnObject ? character.spawnObject.name : "None")}";
    }

    public void OnClick()
    {
        if (!_myCharacter) return;
        
        OnSelected?.Invoke(_myCharacter.CharacterID);
    }

    public void SetEnabled(bool enabled)
    {
        _btn.interactable = enabled;
    }
}
