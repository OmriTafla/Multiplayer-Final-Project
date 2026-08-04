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

    private Vector2? originalScale;

    void Awake()
    {
        originalScale = scalerParent.sizeDelta;
    }

    public void Enter()
    {
        if (originalScale == null)
            originalScale = scalerParent.sizeDelta;

        if (flashImage)
        {
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0);
            DOTween.Sequence()
                .Append(flashImage.DOFade(1, flashInDuration))
                .AppendCallback(StartManualTextEffects)
                .Append(flashImage.DOFade(0, flashOutDuration).SetEase(flashOutEase))
                .SetLink(gameObject)
                .SetTarget(this);

            scalerParent.sizeDelta = Vector2.zero;

            DOTween.Sequence()
                .Append(DOTween.To(() => scalerParent.sizeDelta.x, x => scalerParent.sizeDelta = new Vector2(x, scalerParent.sizeDelta.y), originalScale.Value.x, XScaleInDuration).SetEase(XScaleInEase))
                .Join(DOTween.To(() => scalerParent.sizeDelta.y, y => scalerParent.sizeDelta = new Vector2(scalerParent.sizeDelta.x, y), originalScale.Value.y, YScaleInDuration).SetEase(YScaleInEase))
                .SetLink(gameObject)
                .SetTarget(this);

            foreach (var graphic in toFadeGraphics)
            {
                graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, 0);

                if (graphic)
                    graphic.DOFade(1, Mathf.Min(XScaleInDuration, YScaleInDuration)).SetLink(gameObject).SetTarget(this);
            }
        }
    }

    public void Exit()
    {
        if (flashImage)
        {
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0);
            DOTween.Sequence()
                .Append(flashImage.DOFade(1, flashInDuration))
                .AppendCallback(StopManualTextEffects)
                .Append(flashImage.DOFade(0, flashOutDuration).SetEase(flashOutEase))
                .SetLink(gameObject)
                .SetTarget(this);

            scalerParent.sizeDelta = originalScale.Value;

            DOTween.Sequence()
                .Append(DOTween.To(() => scalerParent.sizeDelta.x, x => scalerParent.sizeDelta = new Vector2(x, scalerParent.sizeDelta.y), 0, XScaleInDuration).SetEase(XScaleInEase))
                .Join(DOTween.To(() => scalerParent.sizeDelta.y, y => scalerParent.sizeDelta = new Vector2(scalerParent.sizeDelta.x, y), 0, YScaleInDuration).SetEase(YScaleInEase))
                .SetLink(gameObject)
                .SetTarget(this);

            foreach (var graphic in toFadeGraphics)
            {
                if (graphic)
                    graphic.DOFade(0, Mathf.Min(XScaleInDuration, YScaleInDuration)).SetLink(gameObject).SetTarget(this);
            }
        }
    }

    public void InstantExit()
    {
        if (flashImage)
        {
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0);
            scalerParent.sizeDelta = Vector2.zero;

            foreach (var graphic in toFadeGraphics)
            {
                if (graphic)
                    graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, 0);
            }

            StopManualTextEffects();
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
