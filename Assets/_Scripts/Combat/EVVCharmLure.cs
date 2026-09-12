using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;

// Defender role: a Viking that reaches this defender may be charmed instead of attacking it.
// A charmed Viking picks the lure up and carries it back off the board (EVVEnemyVikingWalker
// owns that walk). The lure owns the charm rule and how it looks while carried.
[RequireComponent(typeof(EVVDefender))]
public class EVVCharmLure : MonoBehaviour
{
    const string CarriedTriggerName = "Carried";
    const string CarriedPoseName = "Carried Pose";
    const string HeartName = "Charm Heart";
    const int HeartSortingOrder = 1000;
    const float HeartPopSeconds = 0.2f;

    [Header("Charm")]
    [Tooltip("Chance to charm a Viking that reaches the lure, before his EVVCharmResistance is applied.")]
    [SerializeField, Range(0f, 1f)] float charmChance = 1f;

    [Header("Carried Pose")]
    [Tooltip("Offset from the Viking's carry point, in world units, seen while he still faces left.")]
    [SerializeField] Vector3 carriedOffset = new Vector3(-0.1f, 0.05f, 0f);
    [Tooltip("Tilt while carried. -90 lays the lure flat with its head behind the Viking.")]
    [SerializeField] float carriedRotation = -80f;
    [Tooltip("Size while carried, relative to the board size.")]
    [SerializeField, Min(0.05f)] float carriedScale = 0.8f;
    [Tooltip("Sorting order inside the Viking's sorting group; higher draws in front of more of his parts.")]
    [SerializeField] int carriedSortingOrder = 200;
    [Tooltip("Objects switched off while carried, such as the ground shadow.")]
    [SerializeField] GameObject[] hiddenWhileCarried;

    [Header("Heart Popup")]
    [SerializeField] Sprite heartSprite;
    [Tooltip("World offset from the top-center of the Viking's sprites.")]
    [SerializeField] Vector2 heartOffset = new Vector2(0f, 0.2f);
    [SerializeField, Min(0.01f)] float heartScale = 0.45f;
    [SerializeField, Min(0f)] float heartRise = 0.35f;
    [SerializeField, Min(0.05f)] float heartSeconds = 0.9f;

    public bool IsCarried { get; private set; }

    // Called by a Viking that just reached the lure. Rolls the lure's chance against his
    // resistance; a charmed Viking gets a heart over his head and should then grab the lure.
    public bool TryCharm(GameObject viking)
    {
        if (viking == null || IsCarried)
        {
            return false;
        }

        float chance = charmChance;
        EVVCharmResistance resistance = viking.GetComponent<EVVCharmResistance>();
        if (resistance != null)
        {
            if (resistance.Immune)
            {
                return false;
            }

            chance *= 1f - resistance.ResistancePercent / 100f;
        }

        if (chance <= 0f || Random.value > chance)
        {
            return false;
        }

        SpawnHeart(viking.transform);
        return true;
    }

    // Moves the lure from the board onto the Viking's carry point. From here on it is no longer a
    // defender: it leaves EVVTargetRegistry, stops attacking, cannot be healed or removed, and its
    // cell frees up on the placement manager's next occupied-cell check.
    public void Grab(Transform carryPoint)
    {
        if (IsCarried || carryPoint == null)
        {
            return;
        }

        IsCarried = true;

        EVVDefender defender = GetComponent<EVVDefender>();
        if (defender != null)
        {
            defender.enabled = false;
        }

        EVVBoardMeleeAttacker attacker = GetComponent<EVVBoardMeleeAttacker>();
        if (attacker != null)
        {
            attacker.enabled = false;
        }

        EVVWorldHealthBar healthBar = GetComponent<EVVWorldHealthBar>();
        if (healthBar != null)
        {
            healthBar.Hide();
        }

        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        if (hiddenWhileCarried != null)
        {
            foreach (GameObject hidden in hiddenWhileCarried)
            {
                if (hidden != null)
                {
                    hidden.SetActive(false);
                }
            }
        }

        // The pose lives on a frame between the carry point and the lure, so the lure's own root
        // stays at identity: its animator may write the root transform every frame (the stun clip
        // animates the root scale). The carry point is world-sized (EVVEnemyVikingWalker), so the
        // offset is in world units and the scale is relative to the board size.
        Transform pose = new GameObject(CarriedPoseName).transform;
        pose.SetParent(carryPoint, false);
        pose.localPosition = carriedOffset;
        pose.localRotation = Quaternion.Euler(0f, 0f, carriedRotation);
        pose.localScale = Vector3.one * carriedScale;
        transform.SetParent(pose, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        SortingGroup sortingGroup = GetComponent<SortingGroup>();
        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = carriedSortingOrder;
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null && HasTrigger(animator, CarriedTriggerName))
        {
            animator.SetTrigger(CarriedTriggerName);
        }
    }

    void SpawnHeart(Transform viking)
    {
        if (heartSprite == null)
        {
            return;
        }

        GameObject heart = new GameObject(HeartName);
        heart.transform.SetParent(viking, false);
        heart.transform.position = new Vector3(viking.position.x + heartOffset.x, GetSpriteTop(viking) + heartOffset.y, viking.position.z);

        SpriteRenderer renderer = heart.AddComponent<SpriteRenderer>();
        renderer.sprite = heartSprite;
        EVVLaneDepth.ApplyGameplayRenderer(renderer);
        renderer.sortingOrder = HeartSortingOrder;

        heart.transform.SetLossyScale(Vector3.one * heartScale);
        Vector3 scale = heart.transform.localScale;

        Tween.Scale(heart.transform, Vector3.zero, scale, HeartPopSeconds, Ease.OutBack);
        Tween.PositionY(heart.transform, heart.transform.position.y + heartRise, heartSeconds, Ease.OutSine);
        Tween.Alpha(renderer, 0f, heartSeconds, Ease.InQuad)
            .OnComplete(heart, target => Destroy(target), warnIfTargetDestroyed: false);
    }

    static float GetSpriteTop(Transform root)
    {
        float top = root.position.y;
        foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>())
        {
            if (renderer.sprite != null)
            {
                top = Mathf.Max(top, renderer.bounds.max.y);
            }
        }

        return top;
    }

    static bool HasTrigger(Animator animator, string triggerName)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
            {
                return true;
            }
        }

        return false;
    }
}
