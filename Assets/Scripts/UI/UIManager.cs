using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Singleton;

public class UIManager : Singleton<UIManager>
{
    [SerializeField] private GameObject signInMenu;
    [SerializeField] private GameObject sessionsMenu;
    [SerializeField] private GameObject playersMenu;
    [SerializeField] private GameObject waitingScreen;
    [SerializeField] private UIStates initialState;
    private Dictionary<UIStates, GameObject> menus = new Dictionary<UIStates, GameObject>();

    protected override void Awake()
    {
        base.Awake();
        menus.Add(UIStates.lobbyMenu, signInMenu);
        menus.Add(UIStates.sessionsMenu, sessionsMenu);
        menus.Add(UIStates.playersMenu, playersMenu);
        menus.Add(UIStates.waitingScreen, waitingScreen);
        
        UpdateVisibility(initialState);
    }

    public void ShowLobbyMenu() => UpdateVisibility(UIStates.lobbyMenu);
    
    public void ShowSessionsMenu() => UpdateVisibility(UIStates.sessionsMenu);

    public void ShowPlayersMenu() => UpdateVisibility(UIStates.playersMenu);
    
    public void ShowWaitingScreen() => UpdateVisibility(UIStates.waitingScreen);
    
    private void UpdateVisibility(UIStates state)
    {
        foreach (var menu in menus)
        {
            if (!menu.Value) continue;
            menu.Value.SetActive(menu.Key == state);
        }
    }
}
