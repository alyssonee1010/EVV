using UnityEngine;

public class EVVAnimationSoundPlayer : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip[] attackSounds;
    [SerializeField] AudioClip[] effortSounds;
    [SerializeField] AudioClip[] hitSounds;
    [SerializeField] AudioClip[] mineSounds;
    [SerializeField] AudioClip[] collectSounds;
    [SerializeField] AudioClip[] afterKillSounds;
    [SerializeField] Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Header("Collect Pitch Ramp")]
    [SerializeField] float collectPitchStart = 1f;
    [SerializeField] float collectPitchMax = 2.5f;
    [SerializeField] float collectPitchStep = 0.1f;
    [SerializeField] float collectPitchResetInterval = 0.4f;

    static float lastCollectTime = -Mathf.Infinity;
    static float currentCollectPitch = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCollectPitchState()
    {
        lastCollectTime = -Mathf.Infinity;
        currentCollectPitch = -1f;
    }

    void Awake()
    {
        EnsureAudioSource();
    }

    public void PlayAttackSounds()
    {
        PlayRandom(attackSounds);
    }

    public void PlayEffortSounds()
    {
        PlayRandom(effortSounds);
    }

    public void PlayHitSounds()
    {
        PlayRandom(hitSounds);
    }

    public void PlayAfterKillSounds()
    {
        PlayRandom(afterKillSounds);
    }

    public void PlayMineSounds()
    {
        PlayRandom(mineSounds);
    }

    public void PlayCollectSounds()
    {
        if (collectSounds == null || collectSounds.Length == 0)
        {
            return;
        }

        EnsureAudioSource();
        if (audioSource == null)
        {
            return;
        }

        AudioClip clip = collectSounds[Random.Range(0, collectSounds.Length)];
        if (clip == null)
        {
            return;
        }

        float timeSinceLastCollect = Time.unscaledTime - lastCollectTime;
        bool didReset = timeSinceLastCollect >= collectPitchResetInterval;
        currentCollectPitch = didReset
            ? collectPitchStart
            : Mathf.Min(currentCollectPitch + collectPitchStep, collectPitchMax);
        lastCollectTime = Time.unscaledTime;

        audioSource.pitch = currentCollectPitch;
        audioSource.volume = EVVAudioSettings.SfxVolume;
        audioSource.PlayOneShot(clip);
    }

    bool PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            return false;
        }

        EnsureAudioSource();
        if (audioSource == null)
        {
            return false;
        }

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
        {
            return false;
        }

        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.volume = EVVAudioSettings.SfxVolume;
        audioSource.PlayOneShot(clip);
        return true;
    }

    void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }
}
