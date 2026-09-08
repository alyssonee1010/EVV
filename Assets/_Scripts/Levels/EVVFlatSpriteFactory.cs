using UnityEngine;

// Generates simple flat-color sprites used by procedural world-space UI (level select cards,
// level progress bar) so those components don't need hand-authored art assets.
public static class EVVFlatSpriteFactory
{
    static Sprite centeredWhiteSprite;
    static Sprite leftAnchoredWhiteSprite;

    public static Sprite GetWhiteSprite()
    {
        if (centeredWhiteSprite == null)
        {
            centeredWhiteSprite = CreateSprite(new Vector2(0.5f, 0.5f));
        }

        return centeredWhiteSprite;
    }

    public static Sprite GetLeftAnchoredWhiteSprite()
    {
        if (leftAnchoredWhiteSprite == null)
        {
            leftAnchoredWhiteSprite = CreateSprite(new Vector2(0f, 0.5f));
        }

        return leftAnchoredWhiteSprite;
    }

    static Sprite CreateSprite(Vector2 pivot)
    {
        Texture2D texture = new Texture2D(4, 4);
        Color32[] pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), pivot, 4f);
    }
}
