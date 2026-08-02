using Singleton;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

public class PostProcessingEffectPlayer : Singleton<PostProcessingEffectPlayer>
{
    [SerializeField] private Volume globalVolume;

    private Vignette vignette;
    private FilmGrain filmGrain;

    protected override void Awake()
    {
        base.Awake();

        if (!globalVolume) return;

        if (globalVolume.profile.TryGet(out vignette))
        {
            vignette.intensity.overrideState = true;
            vignette.color.overrideState = true;
        }

        if (globalVolume.profile.TryGet(out filmGrain))
        {
            filmGrain.intensity.overrideState = true;
        }
    }

    public void RunVignetteEffect(float fadeInTime, float holdTime, float fadeOutTime, float targetIntensity, Color color)
    {
        vignette.color.value = color;
        
        DOTween.Kill(vignette);

        DOTween.Sequence()
            .Append(DOTween.To(() => vignette.intensity.value, x => vignette.intensity.value = x, targetIntensity, fadeInTime))
            .AppendInterval(holdTime)
            .Append(DOTween.To(() => vignette.intensity.value, x => vignette.intensity.value = x, 0f, fadeOutTime))
            .SetLink(gameObject)
            .SetTarget(vignette);
    }

    public void RunFilmGrainEffect(float fadeInTime, float holdTime, float fadeOutTime, float targetIntensity)
    {
        DOTween.Kill(filmGrain);

        DOTween.Sequence()
            .Append(DOTween.To(() => filmGrain.intensity.value, x => filmGrain.intensity.value = x, targetIntensity, fadeInTime))
            .AppendInterval(holdTime)
            .Append(DOTween.To(() => filmGrain.intensity.value, x => filmGrain.intensity.value = x, 0f, fadeOutTime))
            .SetLink(gameObject)
            .SetTarget(filmGrain);
    }
}