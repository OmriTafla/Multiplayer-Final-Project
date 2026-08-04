#if UNITY_SERVER
#define DEDICATED_SERVER
#endif

using DG.Tweening;
using EasyTextEffects.Editor.MyBoxCopy.Extensions;
using Singleton;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UIManager : Singleton<UIManager>
{
    [FormerlySerializedAs("signInMenu")]
    [SerializeField] private GameObject startMenu;
    [SerializeField] private GameObject leaderboards;
    [SerializeField] private GameObject waitingScreen;
    [SerializeField] private TMP_Text waitingText;

    [Header("Entry Fade")]
    [SerializeField] private Image fgImage;
    [SerializeField] private float fadeInTime;
    [SerializeField] private Ease fadeInEase;
    [SerializeField] private float flashesDelay;
    [SerializeField] private float flashesOffsetDelay;
    [SerializeField] private FlashTransition[] flashes;
    

    protected override void Awake()
    {
        base.Awake();

        if (fgImage)
        {
            fgImage.color = Color.black;
            fgImage.DOFade(0, fadeInTime).SetEase(fadeInEase);
        }

        flashes.ForEach(x => x.InstantExit());

        DOTween.Sequence()
            .AppendInterval(flashesDelay).AppendInterval(flashesDelay)
            .AppendCallback(RunFlashes)
            .SetLink(gameObject)
            .SetTarget(this);

#if DEDICATED_SERVER
        HideAll();
#else
        ShowStartMenu();
#endif
    }

    void RunFlashes()
    {
        if (flashes.Length <= 0) return;

        for (int i = 0; i < flashes.Length; i++)
        {
            int index = i;
            float delay = flashesOffsetDelay * index;

            DOTween.Sequence()
                .AppendInterval(delay)
                .AppendCallback(() => flashes[index].Enter());
        }
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
