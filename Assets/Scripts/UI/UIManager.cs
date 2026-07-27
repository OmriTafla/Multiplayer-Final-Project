#if UNITY_SERVER
#define DEDICATED_SERVER
#endif

using Singleton;
using UnityEngine;

public class UIManager : Singleton<UIManager>
{
    [SerializeField] private GameObject signInMenu;
    [SerializeField] private GameObject sessionsMenu;
    [SerializeField] private GameObject playersMenu;
    [SerializeField] private GameObject waitingScreen;

    protected override void Awake()
    {
        base.Awake();

#if DEDICATED_SERVER
        SetActive(signInMenu, false);
        SetActive(sessionsMenu, false);
        SetActive(playersMenu, false);
        SetActive(waitingScreen, false);
#else
        ShowLobbyMenu();
#endif
    }

    public void ShowLobbyMenu()
    {
        SetState(true, false);
    }

    public void ShowWaitingScreen()
    {
        SetState(false, true);
    }

    public void ShowSessionsMenu()
    {
        ShowLobbyMenu();
    }

    public void ShowPlayersMenu()
    {
        ShowLobbyMenu();
    }

    private void SetState(bool showLobby, bool showWaiting)
    {
        SetActive(signInMenu, showLobby);
        SetActive(waitingScreen, showWaiting);
        SetActive(sessionsMenu, false);
        SetActive(playersMenu, false);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}
