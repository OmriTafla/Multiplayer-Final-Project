#if UNITY_SERVER
#define DEDICATED_SERVER
#endif

using Singleton;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class UIManager : Singleton<UIManager>
{
    [FormerlySerializedAs("signInMenu")]
    [SerializeField] private GameObject startMenu;
    [SerializeField] private GameObject leaderboards;
    [SerializeField] private GameObject waitingScreen;
    [SerializeField] private TMP_Text waitingText;

    protected override void Awake()
    {
        base.Awake();

#if DEDICATED_SERVER
        HideAll();
#else
        ShowStartMenu();
#endif
    }

    public void ShowStartMenu()
    {
        SetActive(startMenu, true);
        SetActive(leaderboards, true);
        SetActive(waitingScreen, false);
    }

    public void ShowStatus(string message)
    {
        if (waitingText)
            waitingText.text = message;

        SetActive(startMenu, false);
        SetActive(leaderboards, false);
        SetActive(waitingScreen, true);
    }

    private void HideAll()
    {
        SetActive(startMenu, false);
        SetActive(leaderboards, false);
        SetActive(waitingScreen, false);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target)
            target.SetActive(active);
    }
}
