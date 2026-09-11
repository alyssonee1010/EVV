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

    EVVHealth health;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCollectPitchState()
    {
        lastCollectTime = -Mathf.Infinity;
        currentCollectPitch = -1f;
    }

    void Awake()
    {
        EnsureAudioSource();
        health = GetComponent<EVVHealth>();
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

        Play(clip, currentCollectPitch);
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

        Play(clip, Random.Range(pitchRange.x, pitchRange.y));
        return true;
    }

    void Play(AudioClip clip, float pitch)
    {
        // A dead owner is destroyed at the end of this frame together with its AudioSource, which would
        // cut the sound short, so death-time sounds go on a temporary source that outlives the owner.
        AudioSource source = health != null && !health.IsAlive ? CreateDetachedSource(clip, pitch) : audioSource;
        source.pitch = pitch;
        source.PlayOneShot(clip, EVVAudioSettings.SfxVolume);
    }

    AudioSource CreateDetachedSource(AudioClip clip, float pitch)
    {
        GameObject carrier = new GameObject(name + " Sound");
        carrier.transform.position = transform.position;

        AudioSource source = carrier.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.volume = audioSource.volume;
        source.priority = audioSource.priority;
        source.spatialBlend = audioSource.spatialBlend;
        source.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;

        Destroy(carrier, clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)));
        return source;
    }

    void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }
}
