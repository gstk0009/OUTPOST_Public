using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GunAudio : MonoBehaviour
{
    [Header("Clips")]
    public AudioClip fireSound;
    public AudioClip reloadSound;
    public AudioClip emptyClickSound;
    public AudioClip drawSound;
    public AudioClip holsterSound;

    [Header("Volume")]
    public float fireVolume = 1f;
    public float reloadVolume = 0.8f;

    private AudioSource _audioSource;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    public void PlayFireSound()
    {
        if (fireSound) _audioSource.PlayOneShot(fireSound, fireVolume);
    }

    public void PlayReloadSound()
    {
        if (reloadSound) _audioSource.PlayOneShot(reloadSound, reloadVolume);
    }

    public void PlayEmptyClick()
    {
        if (emptyClickSound) _audioSource.PlayOneShot(emptyClickSound, 0.5f);
    }

    public void PlayDraw()
    {
        if (drawSound) _audioSource.PlayOneShot(drawSound, 0.8f);
    }

    public void PlayHolster()
    {
        if (holsterSound) _audioSource.PlayOneShot(holsterSound, 0.8f);
    }

    public void PlayUtilitySound(AudioClip clip, float volume = 0.8f)
    {
        if (clip) _audioSource.PlayOneShot(clip, volume);
    }
}
