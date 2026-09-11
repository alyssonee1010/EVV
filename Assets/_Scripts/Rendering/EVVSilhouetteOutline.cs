using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks the sprites of this character for the outline that EVVSilhouetteOutlineFeature
/// draws. Add it to a character whose art has no drawn outline. Health bar sprites are
/// left out.
///
/// Sprites that are not listed in a group form the body and share one outline. Each
/// group is a limb (an arm with its joint pieces, a leg, the head) that keeps its own
/// outline where it moves in front of or behind other groups of the same character, while
/// the pieces inside a group never outline each other. A group that lies on the body
/// without being a separate limb (the legs of the shorts on the waist piece) can be marked
/// seamless with the body, so it only outlines against other groups.
/// </summary>
[DisallowMultipleComponent]
public class EVVSilhouetteOutline : MonoBehaviour
{
    [System.Serializable]
    public class Group
    {
        public string name;
        public SpriteRenderer[] renderers;
        [Tooltip("No outline where this group meets the body, only where it meets other groups.")]
        public bool seamlessWithBody;
    }

    // The key texture stores the id in 8 bits: 31 character slots x 8 groups.
    const int MaxGroups = 7;
    const int CharacterSlots = 31;
    const string HealthBarRootName = "Health Bar";

    static readonly int OutlineIdId = Shader.PropertyToID("_OutlineId");
    static readonly int OutlineOrderId = Shader.PropertyToID("_OutlineOrder");
    static readonly int OutlineSeamlessId = Shader.PropertyToID("_OutlineSeamless");
    static int nextCharacterSlot;

    [Tooltip("Limbs that keep their own outline where they cross other parts of this character. Sprites not listed here form the body.")]
    [SerializeField] Group[] groups;

    // Enabled markers, for the renderer feature: it skips its passes while nothing is
    // outlined (menus, loadout screen) and only draws the outline around these characters.
    public static readonly List<EVVSilhouetteOutline> Active = new List<EVVSilhouetteOutline>();

    readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    readonly List<MaterialPropertyBlock> blocks = new List<MaterialPropertyBlock>();
    readonly List<int> ids = new List<int>();
    readonly List<bool> seamless = new List<bool>();
    readonly List<int> orders = new List<int>();

    void OnEnable()
    {
        Active.Add(this);

        int characterSlot = 1 + nextCharacterSlot % CharacterSlots;
        nextCharacterSlot++;

        Dictionary<SpriteRenderer, Group> groupsByRenderer = new Dictionary<SpriteRenderer, Group>();
        Dictionary<SpriteRenderer, int> groupIndices = new Dictionary<SpriteRenderer, int>();
        int groupCount = groups != null ? Mathf.Min(groups.Length, MaxGroups) : 0;
        for (int i = 0; i < groupCount; i++)
        {
            if (groups[i]?.renderers == null)
            {
                continue;
            }

            foreach (SpriteRenderer renderer in groups[i].renderers)
            {
                if (renderer != null)
                {
                    groupIndices[renderer] = i + 1;
                    groupsByRenderer[renderer] = groups[i];
                }
            }
        }

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (IsHealthBarRenderer(renderer))
            {
                continue;
            }

            groupIndices.TryGetValue(renderer, out int groupIndex);
            groupsByRenderer.TryGetValue(renderer, out Group group);
            renderers.Add(renderer);
            blocks.Add(new MaterialPropertyBlock());
            ids.Add(characterSlot * (MaxGroups + 1) + groupIndex);
            seamless.Add(group != null && group.seamlessWithBody);
            orders.Add(int.MinValue);
        }

        PushProperties();
    }

    void OnDisable()
    {
        Active.Remove(this);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.SetPropertyBlock(null);
            }
        }

        renderers.Clear();
        blocks.Clear();
        ids.Clear();
        seamless.Clear();
        orders.Clear();
    }

    // After the animator has updated the sorting orders of the parts.
    void LateUpdate()
    {
        PushProperties();
    }

    /// <summary>World bounds of the outlined sprites; false while none is visible.</summary>
    public bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || renderer.sprite == null)
            {
                continue;
            }

            if (found)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            else
            {
                bounds = renderer.bounds;
                found = true;
            }
        }

        return found;
    }

    void PushProperties()
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sortingOrder == orders[i])
            {
                continue;
            }

            orders[i] = renderer.sortingOrder;
            MaterialPropertyBlock block = blocks[i];
            block.SetFloat(OutlineIdId, ids[i]);
            block.SetFloat(OutlineOrderId, orders[i]);
            block.SetFloat(OutlineSeamlessId, seamless[i] ? 1f : 0f);
            renderer.SetPropertyBlock(block);
        }
    }

    bool IsHealthBarRenderer(SpriteRenderer renderer)
    {
        Transform candidate = renderer.transform;
        while (candidate != null && candidate != transform)
        {
            if (candidate.name == HealthBarRootName)
            {
                return true;
            }

            candidate = candidate.parent;
        }

        return false;
    }
}
