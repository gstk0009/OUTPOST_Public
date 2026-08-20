using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class PlayerDamageFeedback : MonoBehaviour
{
    public Image damageOverlay;
    public AudioSource audioSource;
    public AudioClip[] damageSounds;
    public float peakAlpha = 0.52f;
    public float fadeDuration = 0.42f;
    private CancellationTokenSource _feedbackCts;

    void Awake()
    {
        if (damageOverlay != null)
        {
            Color color = damageOverlay.color;
            color.a = 0f;
            damageOverlay.color = color;
            damageOverlay.raycastTarget = false;
        }
    }

    public void Play(float damage)
    {
        if (audioSource != null && damageSounds != null && damageSounds.Length > 0)
        {
            audioSource.pitch = Random.Range(0.94f, 1.03f);
            audioSource.PlayOneShot(damageSounds[Random.Range(0, damageSounds.Length)], 0.72f);
        }
        RecoilSystem recoil = Camera.main != null ? Camera.main.GetComponent<RecoilSystem>() : null;
        recoil?.AddRecoil(Mathf.Clamp(damage * 0.045f, 0.35f, 1.25f), Random.Range(-0.45f, 0.45f));
        _feedbackCts?.Cancel();
        _feedbackCts?.Dispose();
        _feedbackCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        FadeAsync(_feedbackCts.Token).Forget();
    }

    async UniTaskVoid FadeAsync(CancellationToken token)
    {
        if (damageOverlay == null) return;
        Color color = damageOverlay.color;
        color.a = peakAlpha;
        damageOverlay.color = color;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(peakAlpha, 0f, elapsed / fadeDuration);
            damageOverlay.color = color;
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (token.IsCancellationRequested) return;
        }
        color.a = 0f;
        damageOverlay.color = color;
    }

    void OnDestroy()
    {
        _feedbackCts?.Cancel();
        _feedbackCts?.Dispose();
    }
}
