using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class CrosshairUI : MonoBehaviour
{
    public RectTransform[] crosshairLines;
    public float baseGap = 10f;
    public float maxGap = 60f;
    public float lerpSpeed = 10f;

    private float _currentGap;
    private float _targetGap;
    private RectTransform _hitMarkerRoot;
    private Image[] _hitMarkerLines;
    private CancellationTokenSource _hitCts;
    private CancellationTokenSource _shotCts;
    private AudioSource _feedbackAudio;
    private static AudioClip _bodyClip;
    private static AudioClip _headClip;
    private static AudioClip _killClip;

    void Awake()
    {
        _currentGap = baseGap;
        _targetGap = baseGap;
        BuildHitMarker();
        TryGetComponent(out _feedbackAudio);
    }

    void Update()
    {
        _currentGap = Mathf.Lerp(_currentGap, _targetGap, lerpSpeed * Time.deltaTime);
        UpdateLines();
    }

    public void SetSpread(float spreadFactor) =>
        _targetGap = Mathf.Lerp(baseGap, maxGap, Mathf.Clamp01(spreadFactor));

    public void PulseShot(float strength = 0.4f)
    {
        RenewToken(ref _shotCts);
        ShotPulseAsync(Mathf.Clamp01(strength), _shotCts.Token).Forget();
    }

    public void ShowHitMarker(bool headshot, bool killed)
    {
        if (_hitMarkerRoot == null) BuildHitMarker();
        RenewToken(ref _hitCts);
        HitMarkerPulseAsync(headshot, killed, _hitCts.Token).Forget();
        if (_feedbackAudio != null)
            _feedbackAudio.PlayOneShot(GetFeedbackClip(headshot, killed), killed ? 0.42f : headshot ? 0.34f : 0.22f);
    }

    static AudioClip GetFeedbackClip(bool headshot, bool killed)
    {
        if (killed && _killClip == null) _killClip = BuildTone("Kill Confirm", 520f, 0.09f, true);
        if (headshot && _headClip == null) _headClip = BuildTone("Headshot Confirm", 1420f, 0.07f, false);
        if (_bodyClip == null) _bodyClip = BuildTone("Hit Confirm", 880f, 0.045f, false);
        return killed ? _killClip : headshot ? _headClip : _bodyClip;
    }

    static AudioClip BuildTone(string name, float frequency, float duration, bool descending)
    {
        const int sampleRate = 22050;
        int length = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[length];
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)length;
            float hz = descending ? Mathf.Lerp(frequency * 1.35f, frequency * 0.7f, t) : frequency;
            phase += hz / sampleRate;
            samples[i] = Mathf.Sin(phase * Mathf.PI * 2f) * (1f - t) * (1f - t);
        }
        AudioClip clip = AudioClip.Create(name, length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public void HideCrosshair(bool hidden)
    {
        if (crosshairLines == null) return;
        foreach (RectTransform line in crosshairLines)
            if (line != null) line.gameObject.SetActive(!hidden);
    }

    void UpdateLines()
    {
        if (crosshairLines == null || crosshairLines.Length < 4) return;
        crosshairLines[0].anchoredPosition = new Vector2(0f, _currentGap);
        crosshairLines[1].anchoredPosition = new Vector2(0f, -_currentGap);
        crosshairLines[2].anchoredPosition = new Vector2(-_currentGap, 0f);
        crosshairLines[3].anchoredPosition = new Vector2(_currentGap, 0f);
    }

    void BuildHitMarker()
    {
        GameObject root = new GameObject("HitMarker", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        _hitMarkerRoot = root.GetComponent<RectTransform>();
        _hitMarkerRoot.anchorMin = _hitMarkerRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _hitMarkerRoot.anchoredPosition = Vector2.zero;
        _hitMarkerRoot.sizeDelta = new Vector2(54f, 54f);

        _hitMarkerLines = new Image[4];
        Vector2[] positions = { new(-12f, 12f), new(12f, 12f), new(-12f, -12f), new(12f, -12f) };
        float[] rotations = { -45f, 45f, 45f, -45f };
        for (int i = 0; i < 4; i++)
        {
            GameObject line = new GameObject("MarkerLine_" + i, typeof(RectTransform), typeof(Image));
            line.transform.SetParent(_hitMarkerRoot, false);
            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = positions[i];
            rect.sizeDelta = new Vector2(3f, 16f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotations[i]);
            _hitMarkerLines[i] = line.GetComponent<Image>();
            _hitMarkerLines[i].raycastTarget = false;
        }
        root.SetActive(false);
    }

    async UniTaskVoid ShotPulseAsync(float strength, CancellationToken token)
    {
        _targetGap = Mathf.Max(_targetGap, Mathf.Lerp(baseGap + 8f, maxGap * 0.72f, strength));
        bool cancelled = await UniTask.Delay(70, true, cancellationToken: token).SuppressCancellationThrow();
        if (cancelled) return;
        _targetGap = baseGap;
    }

    async UniTaskVoid HitMarkerPulseAsync(bool headshot, bool killed, CancellationToken token)
    {
        Color color = killed ? new Color(1f, 0.3f, 0.12f) : headshot ? new Color(1f, 0.82f, 0.2f) : Color.white;
        _hitMarkerRoot.gameObject.SetActive(true);
        _hitMarkerRoot.localScale = Vector3.one * (killed ? 1.35f : headshot ? 1.18f : 1f);
        float elapsed = 0f;
        const float duration = 0.16f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            Color faded = color;
            faded.a = 1f - Mathf.Clamp01(elapsed / duration);
            foreach (Image line in _hitMarkerLines) line.color = faded;
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (token.IsCancellationRequested) return;
        }
        _hitMarkerRoot.gameObject.SetActive(false);
    }

    void RenewToken(ref CancellationTokenSource source)
    {
        source?.Cancel();
        source?.Dispose();
        source = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
    }

    void OnDestroy()
    {
        _hitCts?.Cancel();
        _hitCts?.Dispose();
        _shotCts?.Cancel();
        _shotCts?.Dispose();
    }
}
