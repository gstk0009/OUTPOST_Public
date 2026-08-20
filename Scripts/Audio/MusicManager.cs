using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    [Header("Playback")]
    public AudioSource musicSourceA;
    public AudioSource musicSourceB;
    public AudioSource stingerSource;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float stingerVolume = 0.8f;
    public float crossfadeDuration = 1.4f;

    [Header("Music Library")]
    public AudioClip exploration;
    public AudioClip tension;
    public AudioClip combatLow;
    public AudioClip combatMedium;
    public AudioClip combatHigh;
    public AudioClip finalWave;
    public AudioClip boss;
    public AudioClip victory;
    public AudioClip defeat;

    [Header("Stingers")]
    public AudioClip waveStartStinger;
    public AudioClip defeatStinger;
    public AudioClip victoryStinger;

    public MusicState CurrentState { get; private set; } = MusicState.None;

    private AudioSource _activeSource;
    private AudioClip _requestedClip;
    private CancellationTokenSource _fadeCts;

    void Awake()
    {
        _activeSource = musicSourceA;
        ConfigureMusicSource(musicSourceA);
        ConfigureMusicSource(musicSourceB);
        if (stingerSource != null)
        {
            stingerSource.playOnAwake = false;
            stingerSource.loop = false;
            stingerSource.spatialBlend = 0f;
        }
    }

    public void SetMusicState(MusicState state, bool immediate = false)
    {
        AudioClip nextClip = GetClip(state);
        if (state == CurrentState && _requestedClip == nextClip)
            return;

        CurrentState = state;
        _requestedClip = nextClip;
        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _fadeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        CrossfadeAsync(nextClip, immediate ? 0f : crossfadeDuration, _fadeCts.Token).Forget();
    }

    public void PlayWaveStartStinger() => PlayStinger(waveStartStinger);
    public void PlayDefeatStinger() => PlayStinger(defeatStinger);
    public void PlayVictoryStinger() => PlayStinger(victoryStinger);

    public void PlayStinger(AudioClip clip)
    {
        if (clip != null && stingerSource != null)
            stingerSource.PlayOneShot(clip, stingerVolume);
    }

    async UniTaskVoid CrossfadeAsync(AudioClip nextClip, float duration, CancellationToken token)
    {
        AudioSource from = _activeSource;
        AudioSource to = from == musicSourceA ? musicSourceB : musicSourceA;
        if (to == null)
            return;

        to.Stop();
        to.loop = true;
        to.clip = nextClip;
        to.volume = 0f;
        if (nextClip != null)
            to.Play();

        if (duration <= 0f)
        {
            if (from != null) from.Stop();
            to.volume = nextClip != null ? musicVolume : 0f;
            _activeSource = to;
            return;
        }

        float fromStart = from != null ? from.volume : 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, t);
            to.volume = Mathf.Lerp(0f, nextClip != null ? musicVolume : 0f, t);
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (token.IsCancellationRequested) return;
        }

        if (from != null) from.Stop();
        _activeSource = to;
    }

    void Update()
    {
        if (_activeSource == null || _requestedClip == null || _activeSource.clip != _requestedClip) return;
        _activeSource.loop = true;
        if (!_activeSource.isPlaying) _activeSource.Play();
    }

    void OnDestroy()
    {
        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
    }

    AudioClip GetClip(MusicState state)
    {
        switch (state)
        {
            case MusicState.Exploration: return exploration;
            case MusicState.Tension: return tension != null ? tension : combatHigh;
            case MusicState.CombatLow: return combatLow;
            case MusicState.CombatMedium: return combatMedium;
            case MusicState.CombatHigh: return combatHigh;
            case MusicState.FinalWave: return finalWave;
            case MusicState.Boss: return boss;
            case MusicState.Victory: return victory != null ? victory : exploration;
            case MusicState.Defeat: return defeat;
            default: return null;
        }
    }

    static void ConfigureMusicSource(AudioSource source)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }
}
