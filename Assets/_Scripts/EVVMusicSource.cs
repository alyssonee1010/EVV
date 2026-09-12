using UnityEngine;

// Keeps a music AudioSource at its authored volume scaled by the music slider, and pins it to the
// highest priority. Unity mutes the lowest-priority voices first once the 32 real voices run out, and
// every sfx source in the prefabs sits at 80-128, so without this the music is the first thing to drop
// out when enough characters make noise at the same time.
[RequireComponent(typeof(AudioSource))]
public class EVVMusicSource : MonoBehaviour
{
    AudioSource audioSource;
    float authoredVolume;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.priority = 0;
        authoredVolume = audioSource.volume;
    }

    void OnEnable()
    {
        ApplyVolume();
        EVVAudioSettings.MusicVolumeChanged += ApplyVolume;
    }

    void OnDisable()
    {
        EVVAudioSettings.MusicVolumeChanged -= ApplyVolume;
    }

    void ApplyVolume()
    {
        audioSource.volume = authoredVolume * EVVAudioSettings.MusicVolume;
    }
}
