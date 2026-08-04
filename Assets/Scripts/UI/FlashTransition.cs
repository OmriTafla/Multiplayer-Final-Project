using DG.Tweening;
using EasyTextEffects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FlashTransition : MonoBehaviour
{
    [SerializeField] private Color flashColor;
    [SerializeField] private Image flashImage;
    [SerializeField] private float flashInDuration;
    [SerializeField] private float flashOutDuration;
    [SerializeField] private Ease flashOutEase;

    [Space]

    [SerializeField] private RectTransform scalerParent;
    [SerializeField] private float XScaleInDuration;
    [SerializeField] private Ease XScaleInEase;
    [SerializeField] private float YScaleInDuration;
    [SerializeField] private Ease YScaleInEase;

    [Space]

    [SerializeField] private Graphic[] toFadeGraphics;

    private Vector3? originalScale;
    private bool isOff = false;

    void Start()
    {
        InitOGScale(); 
    }

    void InitOGScale()
    {
        if (!originalScale.HasValue)
            originalScale = scalerParent.localScale;
    }

    public void Enter()
    {
        if (!isOff) return;

        KillActiveTweens();
        InitOGScale();

        if (flashImage)
        {
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0);
            
            DOTween.Sequence()
                .Append(flashImage.DOFade(1, flashInDuration))
                .AppendCallback(StartManualTextEffects)
                .Append(flashImage.DOFade(0, flashOutDuration).SetEase(flashOutEase))
                .SetLink(gameObject)
                .SetTarget(this);

            scalerParent.localScale = Vector3.zero;

            DOTween.Sequence()
                .Append(scalerParent.DOScaleX(originalScale.Value.x, XScaleInDuration).SetEase(XScaleInEase))
                .Join(scalerParent.DOScaleY(originalScale.Value.y, YScaleInDuration).SetEase(YScaleInEase))
                .SetLink(gameObject)
                .SetTarget(this);

            foreach (var graphic in toFadeGraphics)
            {
                if (graphic)
                {
                    graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, 0);
                    graphic.DOFade(1, Mathf.Min(XScaleInDuration, YScaleInDuration)).SetLink(gameObject).SetTarget(this);
                }
            }
        }

        isOff = false;
    }

    public void Exit()
    {
        if (isOff) return;

        KillActiveTweens();
        InitOGScale();

        if (flashImage)
        {
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0);
            
            DOTween.Sequence()
                .Append(flashImage.DOFade(1, flashInDuration))
                .AppendCallback(StopManualTextEffects)
                .Append(flashImage.DOFade(0, flashOutDuration).SetEase(flashOutEase))
                .SetLink(gameObject)
                .SetTarget(this);

            scalerParent.localScale = originalScale.Value;

            DOTween.Sequence()
                .Append(scalerParent.DOScaleX(0, YScaleInDuration).SetEase(XScaleInEase))
                .Join(scalerParent.DOScaleY(0, XScaleInDuration).SetEase(YScaleInEase))
                .SetLink(gameObject)
                .SetTarget(this);

            foreach (var graphic in toFadeGraphics)
            {
                if (graphic)
                    graphic.DOFade(0, Mathf.Min(XScaleInDuration, YScaleInDuration)).SetLink(gameObject).SetTarget(this);
            }
        }

        isOff = true;
    }

    public void InstantExit()
    {
        if (isOff) return;

        KillActiveTweens();
        InitOGScale();

        if (flashImage)
        {
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0);
            scalerParent.localScale = Vector3.zero;

            foreach (var graphic in toFadeGraphics)
            {
                if (graphic)
                    graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, 0);
            }

            StopManualTextEffects();
        }

        isOff = true;
    }

    private void KillActiveTweens()
    {
        DOTween.Kill(this);
        if (scalerParent) scalerParent.DOKill();
        if (flashImage) flashImage.DOKill();
        
        foreach (var graphic in toFadeGraphics)
        {
            if (graphic) graphic.DOKill();
        }
    }

    void StartManualTextEffects()
    {
        foreach (var graphic in toFadeGraphics)
        {
            if (graphic.TryGetComponent(out TextEffect effect))
            {
                effect.StartManualTagEffects();
            }
        }
    }

    void StopManualTextEffects()
    {
        foreach (var graphic in toFadeGraphics)
        {
            if (graphic.TryGetComponent(out TextEffect effect))
            {
                effect.StopManualTagEffects();
            }
        }
    }
}
