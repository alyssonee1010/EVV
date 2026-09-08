using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class EVVLevelMusicController : MonoBehaviour
{
    [SerializeField] AudioClip earlyLevelsMusic;
    [SerializeField] AudioClip laterLevelsMusic;

    AudioSource audioSource;
    EVVWaveDirector waveDirector;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.volume = EVVAudioSettings.MusicVolume;

        EVVAudioSettings.ApplySavedVolume();
        EVVAudioSettings.MusicVolumeChanged += ApplyMusicVolume;
        TrySubscribe();
    }

    void Start()
    {
        TrySubscribe();

        if (waveDirector != null && waveDirector.CurrentLevel != null)
        {
            PlayMusicFor(waveDirector.CurrentLevel);
        }
    }

    void OnDisable()
    {
        EVVAudioSettings.MusicVolumeChanged -= ApplyMusicVolume;
        if (waveDirector != null)
        {
            waveDirector.LevelStarted -= PlayMusicFor;
        }
    }

    void ApplyMusicVolume()
    {
        audioSource.volume = EVVAudioSettings.MusicVolume;
    }

    void TrySubscribe()
    {
        if (waveDirector != null)
        {
            return;
        }

        waveDirector = FindAnyObjectByType<EVVWaveDirector>();
        if (waveDirector != null)
        {
            waveDirector.LevelStarted += PlayMusicFor;
        }
    }

    void PlayMusicFor(EVVLevelDefinition level)
    {
        if (level == null)
        {
            return;
        }

        AudioClip selectedMusic = IsLastLevelInStage(level)
            ? laterLevelsMusic
            : earlyLevelsMusic;

        if (selectedMusic == null)
        {
            Debug.LogWarning($"No music is assigned for level {level.Id}.", this);
            return;
        }

        if (audioSource.clip != selectedMusic)
        {
            audioSource.Stop();
            audioSource.clip = selectedMusic;
        }

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    static bool IsLastLevelInStage(EVVLevelDefinition level)
    {
        foreach (EVVLevelDefinition candidate in EVVLevelLoader.DiscoverLevels())
        {
            if (candidate.Stage == level.Stage && candidate.Level > level.Level)
            {
                return false;
            }
        }

        return true;
    }
}
