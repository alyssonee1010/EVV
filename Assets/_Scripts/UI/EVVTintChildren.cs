using UnityEngine;

public class TintChildren : MonoBehaviour
{
    // The Animation Window can natively serialize, see, and record this field
    public Color tintColor = Color.white;

    private SpriteRenderer[] childSprites;

    void Start()
    {
        CacheSprites();
        ApplyTint();
    }

    // NATIVE UNITY CALLBACK: Fires instantly when the Animator updates fields 
    // on this GameObject during playback, previewing, or scrubbing the timeline.
    void OnDidApplyAnimationProperties()
    {
        ApplyTint();
    }

    private void ApplyTint()
    {
        // Fallback in case it runs in the editor before Start() caches things
        if (childSprites == null || childSprites.Length == 0)
        {
            CacheSprites();
        }

        for (int i = 0; i < childSprites.Length; i++)
        {
            if (childSprites[i] != null)
            {
                childSprites[i].color = tintColor;
            }
        }
    }

    private void CacheSprites()
    {
        childSprites = GetComponentsInChildren<SpriteRenderer>(true);
    }

    // Allows the tint to update instantly when tweaking values manually in the Inspector
    void OnValidate()
    {
        ApplyTint();
    }
}