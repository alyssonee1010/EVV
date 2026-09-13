using System.Collections.Generic;
using UnityEngine;

public class EVVAnimationSoundPlayer : MonoBehaviour
{
    enum CrowdChannel
    {
        Impact,
        Voice
    }

    enum SoundKind
    {
        Attack,
        Effort,
        Hit,
        AfterKill
    }

    [System.Serializable]
    sealed class CrowdControlSettings
    {
        [Tooltip("When enabled, sound players share total budgets, with an additional per-type voice limit.")]
        public bool enabled;

        [Header("Concurrency")]
        [Min(1)] public int maxImpacts = 10;
        [Min(0f)] public float impactCooldown = 0.04f;
        [Tooltip("Identifies this prefab for the per-type voice limit.")]
        public string voiceType;
        [Min(1)] public int maxVoices = 10;
        [Min(1)] public int maxVoicesPerType = 4;
        [Min(0f)] public float voiceCooldown = 0.12f;

        [Header("Playback Chance")]
        [Range(0f, 1f)] public float attackChance = 1f;
        [Range(0f, 1f)] public float effortChance = 0.3f;
        [Range(0f, 1f)] public float hitChance = 0.2f;
        [Range(0f, 1f)] public float afterKillChance = 1f;

        [Header("Relative Volume")]
        [Range(0f, 1f)] public float attackVolume = 1f;
        [Range(0f, 1f)] public float effortVolume = 0.55f;
        [Range(0f, 1f)] public float hitVolume = 0.55f;
        [Range(0f, 1f)] public float afterKillVolume = 0.7f;

        [Header("Board Stereo")]
        [Range(0f, 1f)] public float stereoPanAmount = 0.35f;
        [Min(0.01f)] public float stereoPanWorldHalfWidth = 10f;

        public void GetPlaybackSettings(
            SoundKind kind,
            out CrowdChannel channel,
            out int maxConcurrent,
            out float cooldown,
            out float chance,
            out float volume)
        {
            channel = kind == SoundKind.Attack ? CrowdChannel.Impact : CrowdChannel.Voice;
            maxConcurrent = channel == CrowdChannel.Impact ? maxImpacts : maxVoices;
            cooldown = channel == CrowdChannel.Impact ? impactCooldown : voiceCooldown;

            switch (kind)
            {
                case SoundKind.Attack:
                    chance = attackChance;
                    volume = attackVolume;
                    break;
                case SoundKind.Effort:
                    chance = effortChance;
                    volume = effortVolume;
                    break;
                case SoundKind.Hit:
                    chance = hitChance;
                    volume = hitVolume;
                    break;
                default:
                    chance = afterKillChance;
                    volume = afterKillVolume;
                    break;
            }
        }
    }

    sealed class CrowdState
    {
        public readonly List<float> activeEndTimes = new List<float>();
        public float lastPlayTime = -Mathf.Infinity;
    }

    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip[] attackSounds;
    [SerializeField] AudioClip[] effortSounds;
    [SerializeField] AudioClip[] hitSounds;
    [SerializeField] AudioClip[] mineSounds;
    [SerializeField] AudioClip[] collectSounds;
    [SerializeField] AudioClip[] afterKillSounds;
    [SerializeField] Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Header("Crowd Control")]
    [SerializeField] CrowdControlSettings crowdControl = new CrowdControlSettings();

    [Header("Collect Pitch Ramp")]
    [SerializeField] float collectPitchStart = 1f;
    [SerializeField] float collectPitchMax = 2.5f;
    [SerializeField] float collectPitchStep = 0.1f;
    [SerializeField] float collectPitchResetInterval = 0.4f;

    static float lastCollectTime = -Mathf.Infinity;
    static float currentCollectPitch = -1f;
    static readonly CrowdState impactCrowdState = new CrowdState();
    static readonly CrowdState voiceCrowdState = new CrowdState();
    static readonly Dictionary<string, CrowdState> voiceCrowdStatesByType = new Dictionary<string, CrowdState>();

    EVVHealth health;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCollectPitchState()
    {
        lastCollectTime = -Mathf.Infinity;
        currentCollectPitch = -1f;
        ResetCrowdState(impactCrowdState);
        ResetCrowdState(voiceCrowdState);
        voiceCrowdStatesByType.Clear();
    }

    void Awake()
    {
        EnsureAudioSource();
        health = GetComponent<EVVHealth>();
    }

    public void PlayAttackSounds()
    {
        PlayRandom(attackSounds, SoundKind.Attack);
    }

    public void PlayEffortSounds()
    {
        PlayRandom(effortSounds, SoundKind.Effort);
    }

    public void PlayHitSounds()
    {
        PlayRandom(hitSounds, SoundKind.Hit);
    }

    public void PlayAfterKillSounds()
    {
        PlayRandom(afterKillSounds, SoundKind.AfterKill);
    }

    public void PlayMineSounds()
    {
        PlayRandom(mineSounds, SoundKind.Attack, false);
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

        Play(clip, currentCollectPitch, 1f);
    }

    bool PlayRandom(AudioClip[] clips, SoundKind kind, bool useCrowdControl = true)
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

        float pitch = Random.Range(pitchRange.x, pitchRange.y);
        float volume = 1f;
        if (useCrowdControl && CrowdControlEnabled)
        {
            crowdControl.GetPlaybackSettings(
                kind,
                out CrowdChannel channel,
                out int maxConcurrent,
                out float cooldown,
                out float chance,
                out volume);

            if (!TryReserveCrowdSound(channel, maxConcurrent, cooldown, chance, clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch))))
            {
                return false;
            }
        }

        Play(clip, pitch, volume);
        return true;
    }

    bool TryReserveCrowdSound(CrowdChannel channel, int maxConcurrent, float cooldown, float chance, float duration)
    {
        if (chance <= 0f || (chance < 1f && Random.value > chance))
        {
            return false;
        }

        float now = Time.unscaledTime;
        float endTime = now + Mathf.Max(0.01f, duration);
        if (channel == CrowdChannel.Impact)
        {
            if (!CanReserve(impactCrowdState, now, maxConcurrent, cooldown))
            {
                return false;
            }

            Reserve(impactCrowdState, now, endTime);
            return true;
        }

        string voiceType = string.IsNullOrWhiteSpace(crowdControl.voiceType) ? "Default" : crowdControl.voiceType;
        if (!voiceCrowdStatesByType.TryGetValue(voiceType, out CrowdState typeState))
        {
            typeState = new CrowdState();
            voiceCrowdStatesByType.Add(voiceType, typeState);
        }

        // The total budget prevents all Viking types together from overwhelming the mix. The cooldown
        // is per type so different Viking types can still speak at the same moment.
        if (!CanReserve(voiceCrowdState, now, maxConcurrent, 0f)
            || !CanReserve(typeState, now, crowdControl.maxVoicesPerType, cooldown))
        {
            return false;
        }

        Reserve(voiceCrowdState, now, endTime);
        Reserve(typeState, now, endTime);
        return true;
    }

    static bool CanReserve(CrowdState state, float now, int maxConcurrent, float cooldown)
    {
        for (int i = state.activeEndTimes.Count - 1; i >= 0; i--)
        {
            if (state.activeEndTimes[i] <= now)
            {
                state.activeEndTimes.RemoveAt(i);
            }
        }

        if (now - state.lastPlayTime < Mathf.Max(0f, cooldown)
            || state.activeEndTimes.Count >= Mathf.Max(1, maxConcurrent))
        {
            return false;
        }

        return true;
    }

    static void Reserve(CrowdState state, float now, float endTime)
    {
        state.lastPlayTime = now;
        state.activeEndTimes.Add(endTime);
    }

    void Play(AudioClip clip, float pitch, float volume)
    {
        ApplyStereoPan();

        // A dead owner is destroyed at the end of this frame together with its AudioSource, which would
        // cut the sound short, so death-time sounds go on a temporary source that outlives the owner.
        AudioSource source = health != null && !health.IsAlive ? CreateDetachedSource(clip, pitch) : audioSource;
        source.pitch = pitch;
        source.PlayOneShot(clip, EVVAudioSettings.SfxVolume * Mathf.Clamp01(volume));
    }

    void ApplyStereoPan()
    {
        if (audioSource == null || !CrowdControlEnabled || crowdControl.stereoPanAmount <= 0f)
        {
            return;
        }

        float normalizedBoardPosition = transform.position.x / Mathf.Max(0.01f, crowdControl.stereoPanWorldHalfWidth);
        audioSource.panStereo = Mathf.Clamp(normalizedBoardPosition, -1f, 1f) * crowdControl.stereoPanAmount;
    }

    bool CrowdControlEnabled => crowdControl != null && crowdControl.enabled;

    static void ResetCrowdState(CrowdState state)
    {
        state.activeEndTimes.Clear();
        state.lastPlayTime = -Mathf.Infinity;
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
        source.panStereo = audioSource.panStereo;
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
