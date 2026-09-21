using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.Events;

// The Trap Digger's hole. While he digs, the tile is an ordinary defender the Vikings attack. Once
// the hole is dug (EEVTrapDigger.Arm) it is covered and nobody attacks it: the next Viking to walk
// onto it falls in, plus any arriving within the group window, sinking behind the same sprite mask
// the digger sank behind. From then on the hole is open and fills itself back up: for the
// regeneration time the cell stays taken, and the Vikings see the open hole and jump over it. The
// digger dying mid-dig leaves an open hole that regenerates the same way. Regenerated, the trap is
// gone and the cell is free again.
[RequireComponent(typeof(EVVDefender))]
[RequireComponent(typeof(EVVHealth))]
public class EVVTrapHole : MonoBehaviour
{
    enum State { Digging, Armed, Sprung, Regenerating }

    // Sorting orders the mask is limited to while things fall in, so it clips only the ones falling
    // and not a digger working in a lane below (the stretched mask reaches down that far).
    const int FallingSortingOrderOffset = 100;

    [Header("Hole")]
    [SerializeField] SpriteMask holeMask;
    [SerializeField] SpriteRenderer holeSprite;
    [Tooltip("The leaves and sticks covering the finished hole. They drop into it with the first Viking.")]
    [SerializeField] GameObject cover;

    [Header("Falling in")]
    [Tooltip("Half width of the strip over the hole (world units) a Viking's feet have to enter to fall.")]
    [SerializeField, Min(0.01f)] float triggerHalfWidth = 0.35f;
    [Tooltip("How far a Viking sinks before he is gone. Keep it bigger than the tallest Viking.")]
    [SerializeField, Min(0.1f)] float fallDepth = 3.2f;
    [SerializeField, Min(0.05f)] float fallSeconds = 0.55f;
    [Tooltip("How long the hole stays open after the first Viking falls, so a group falls in together.")]
    [SerializeField, Min(0f)] float groupWindowSeconds = 0.2f;
    [Tooltip("Depth below the rim the mask is stretched to while Vikings fall.")]
    [SerializeField, Min(0.1f)] float maskDepthWhileFalling = 4.5f;
    [Tooltip("Animator trigger fired on a falling Viking; the fallback is used when a rig has no such trigger.")]
    [SerializeField] string fallTrigger = "Fall";
    [SerializeField] string fallbackFallTrigger = "Stun";

    [Header("Open hole")]
    [Tooltip("Seconds the open hole takes to fill itself back up. The cell stays taken until then.")]
    [SerializeField, Min(0f)] float regenerationSeconds = 60f;
    [Tooltip("A Viking walking into the open hole jumps it: takes off this far before the hole's centre...")]
    [SerializeField, Min(0.05f)] float jumpStartOffset = 0.8f;
    [Tooltip("...and lands this far past it.")]
    [SerializeField, Min(0.05f)] float jumpLandOffset = 0.8f;
    [SerializeField, Min(0.01f)] float jumpHeight = 0.7f;
    [SerializeField, Min(0.05f)] float jumpSeconds = 0.5f;
    [Tooltip("Animator trigger fired on a jumping Viking, if the rig has it.")]
    [SerializeField] string jumpTrigger = "Jump";

    [Header("Events")]
    [SerializeField] UnityEvent onArmed;
    [SerializeField] UnityEvent onSprung;
    [SerializeField] UnityEvent onRegenerationStarted;

    EVVDefender defender;
    EVVHealth health;
    State state = State.Digging;
    float windowRemaining;
    float lastFallEnds;
    float regenerationRemaining;
    Color holeColor;
    readonly List<MonoBehaviour> candidates = new List<MonoBehaviour>();
    readonly Dictionary<MonoBehaviour, float> lastWalkerX = new Dictionary<MonoBehaviour, float>();
    readonly List<MonoBehaviour> staleWalkers = new List<MonoBehaviour>();

    public bool IsArmed => state == State.Armed;
    public bool IsOpen => state == State.Regenerating;

    void Awake()
    {
        defender = GetComponent<EVVDefender>();
        health = GetComponent<EVVHealth>();
        if (holeSprite != null)
        {
            holeColor = holeSprite.color;
        }
    }

    void OnEnable()
    {
        health.Died += OnDied;
    }

    void OnDisable()
    {
        health.Died -= OnDied;
    }

    // The hole is finished: nobody attacks it any more, the next Viking falls in.
    public void Arm()
    {
        if (state != State.Digging)
        {
            return;
        }

        state = State.Armed;
        defender.SetTargetable(false);
        onArmed.Invoke();
    }

    void Update()
    {
        switch (state)
        {
            case State.Armed:
                ScanForFallers();
                break;
            case State.Sprung:
                windowRemaining -= Time.deltaTime;
                if (windowRemaining > 0f)
                {
                    ScanForFallers();
                }
                else if (Time.time >= lastFallEnds)
                {
                    StartRegeneration();
                }
                break;
            case State.Regenerating:
                TickRegeneration();
                break;
        }
    }

    // ---------------------------------------------------------------- falling in

    void ScanForFallers()
    {
        float holeX = HoleCenter.x;
        candidates.Clear();
        IReadOnlyList<IEVVEnemyLaneWalker> enemies = EVVTargetRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            IEVVEnemyLaneWalker walker = enemies[i];
            if (!IsWalkingHere(walker, out MonoBehaviour behaviour))
            {
                continue;
            }

            if (Mathf.Abs(behaviour.transform.position.x - holeX) <= triggerHalfWidth)
            {
                candidates.Add(behaviour);
            }
        }

        // Swallowing disables the walker, which drops it from the list being scanned.
        for (int i = 0; i < candidates.Count; i++)
        {
            Swallow(candidates[i]);
        }
    }

    // A live, enabled walker in this lane (a disabled one is already falling or jumping).
    bool IsWalkingHere(IEVVEnemyLaneWalker walker, out MonoBehaviour behaviour)
    {
        behaviour = walker as MonoBehaviour;
        if (behaviour == null || !behaviour.enabled || walker.Health == null || !walker.Health.IsAlive)
        {
            return false;
        }

        if (defender.HasCell)
        {
            return walker.LaneIndex == defender.Cell.y;
        }

        return EVVLaneDepth.IsSameDepth(transform, behaviour.transform);
    }

    Vector3 HoleCenter => holeSprite != null ? holeSprite.bounds.center : transform.position;

    // Out of the fight (disabling the walker leaves the target registry), then down the hole
    // behind the mask, the way the digger went in.
    void Swallow(MonoBehaviour walker)
    {
        if (state == State.Armed)
        {
            Spring();
        }

        walker.enabled = false;
        foreach (Collider2D collider in walker.GetComponentsInChildren<Collider2D>())
        {
            collider.enabled = false;
        }

        EVVSilhouetteOutline outline = walker.GetComponent<EVVSilhouetteOutline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        Transform body = walker.transform;
        MaskForFalling(walker.gameObject);
        Animator animator = walker.GetComponent<Animator>();
        if (animator != null && !TrySetTrigger(animator, fallTrigger))
        {
            TrySetTrigger(animator, fallbackFallTrigger);
        }

        Vector3 bottom = new Vector3(HoleCenter.x, body.position.y - fallDepth, body.position.z);
        GameObject victim = walker.gameObject;
        lastFallEnds = Mathf.Max(lastFallEnds, Time.time + fallSeconds);
        EVVTween.TweenTo(body, bottom, fallSeconds, Ease.InQuad).OnComplete(() =>
        {
            if (victim != null)
            {
                Destroy(victim);
            }
        }, warnIfTargetDestroyed: false);
    }

    // The first Viking is in: the cover goes down with him and the hole stays open for the group window.
    void Spring()
    {
        state = State.Sprung;
        windowRemaining = groupWindowSeconds;
        StretchMask();
        DropCover();
        onSprung.Invoke();
    }

    // The digger's mask only reaches a little below the rim. Stretch it down with the rim edge kept in
    // place so a whole Viking fits behind it, restrict it to the falling sprites' sorting orders, and
    // take it out of the trap's sorting group: inside one, a mask only clips the group's own sprites
    // (and even that proved unreliable), while a free mask clips every sprite in its range.
    void StretchMask()
    {
        if (holeMask == null)
        {
            return;
        }

        Transform maskTransform = holeMask.transform;
        maskTransform.SetParent(null, true);
        float top = holeMask.bounds.max.y;
        float height = holeMask.bounds.size.y;
        if (height > 0f)
        {
            Vector3 scale = maskTransform.localScale;
            scale.y *= maskDepthWhileFalling / height;
            maskTransform.localScale = scale;
            Vector3 position = maskTransform.position;
            position.y = top - maskDepthWhileFalling * 0.5f;
            maskTransform.position = position;
        }

        int layer = SortingLayer.NameToID(EVVLaneDepth.GameplaySortingLayerName);
        holeMask.isCustomRangeActive = true;
        holeMask.frontSortingLayerID = layer;
        holeMask.backSortingLayerID = layer;
        holeMask.backSortingOrder = FallingSortingOrderOffset - 10;
        holeMask.frontSortingOrder = FallingSortingOrderOffset * 2 + 10;
    }

    void DropCover()
    {
        if (cover == null || !cover.activeInHierarchy)
        {
            return;
        }

        MaskForFalling(cover);
        foreach (SpriteRenderer piece in cover.GetComponentsInChildren<SpriteRenderer>())
        {
            Transform pieceTransform = piece.transform;
            EVVTween.TweenTo(pieceTransform, pieceTransform.position + Vector3.down * (fallDepth * 0.5f), fallSeconds, Ease.InQuad);
        }

        GameObject coverObject = cover;
        Tween.Delay(fallSeconds, () =>
        {
            if (coverObject != null)
            {
                coverObject.SetActive(false);
            }
        });
    }

    static void MaskForFalling(GameObject target)
    {
        foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            renderer.sortingOrder += FallingSortingOrderOffset;
        }
    }

    // ---------------------------------------------------------------- the open hole

    // The digger died in the hole: the hole stays and fills itself back up like a sprung one. It keeps
    // one hit point so the board still counts the cell as taken (a dead defender frees its cell).
    // Any later death is the player's remove tool.
    void OnDied(EVVHealth deadHealth)
    {
        if (state == State.Digging)
        {
            health.SetMaxHealth(1);
            defender.SetTargetable(false);
            EEVTrapDigger digger = GetComponentInChildren<EEVTrapDigger>();
            if (digger != null)
            {
                Destroy(digger.gameObject);
            }

            StartRegeneration();
            return;
        }

        Remove();
    }

    void StartRegeneration()
    {
        state = State.Regenerating;
        regenerationRemaining = regenerationSeconds;
        lastWalkerX.Clear();
        onRegenerationStarted.Invoke();
    }

    void TickRegeneration()
    {
        // Somebody placed a defender on this cell (the remove tool frees a cell before the hole is
        // gone): the hole is filled in, or the Vikings would jump over the new defender.
        if (AnotherDefenderOnThisCell())
        {
            Remove();
            return;
        }

        regenerationRemaining -= Time.deltaTime;
        if (holeSprite != null && regenerationSeconds > 0f)
        {
            Color color = holeColor;
            color.a = holeColor.a * Mathf.Clamp01(regenerationRemaining / regenerationSeconds);
            holeSprite.color = color;
        }

        if (regenerationRemaining <= 0f)
        {
            Remove();
            return;
        }

        ScanForJumpers();
    }

    bool AnotherDefenderOnThisCell()
    {
        if (!defender.HasCell)
        {
            return false;
        }

        IReadOnlyList<EVVDefender> defenders = EVVTargetRegistry.Defenders;
        for (int i = 0; i < defenders.Count; i++)
        {
            EVVDefender other = defenders[i];
            if (other != null && other != defender && other.HasCell && other.Cell == defender.Cell)
            {
                return true;
            }
        }

        return false;
    }

    // A Viking that walks up to the open hole jumps it. Only walkers crossing the take-off line
    // from the far side count, so one standing near it fighting something is left alone.
    void ScanForJumpers()
    {
        float takeOffX = HoleCenter.x + jumpStartOffset;
        candidates.Clear();
        IReadOnlyList<IEVVEnemyLaneWalker> enemies = EVVTargetRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            IEVVEnemyLaneWalker walker = enemies[i];
            if (!IsWalkingHere(walker, out MonoBehaviour behaviour))
            {
                continue;
            }

            float x = behaviour.transform.position.x;
            if (lastWalkerX.TryGetValue(behaviour, out float previousX) && previousX > takeOffX && x <= takeOffX && x < previousX)
            {
                candidates.Add(behaviour);
            }

            lastWalkerX[behaviour] = x;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            Jump(candidates[i]);
        }

        staleWalkers.Clear();
        foreach (KeyValuePair<MonoBehaviour, float> entry in lastWalkerX)
        {
            if (entry.Key == null)
            {
                staleWalkers.Add(entry.Key);
            }
        }

        for (int i = 0; i < staleWalkers.Count; i++)
        {
            lastWalkerX.Remove(staleWalkers[i]);
        }
    }

    // The walker is paused for the arc (it would drag the Viking back onto its lane line every
    // frame) and resumes its walk on landing.
    void Jump(MonoBehaviour walker)
    {
        walker.enabled = false;
        lastWalkerX.Remove(walker);
        Animator animator = walker.GetComponent<Animator>();
        if (animator != null)
        {
            TrySetTrigger(animator, jumpTrigger);
        }

        Transform body = walker.transform;
        Vector3 landing = new Vector3(HoleCenter.x - jumpLandOffset, body.position.y, body.position.z);
        EVVTween.TweenArcTo(body, landing, body.position.y + jumpHeight, jumpSeconds).OnComplete(() =>
        {
            if (walker != null)
            {
                walker.enabled = true;
            }
        });
    }

    // ---------------------------------------------------------------- gone

    // The digger may still be walking off, so he is left to it. The mask comes along if it was set free.
    void Remove()
    {
        EEVTrapDigger digger = GetComponentInChildren<EEVTrapDigger>();
        if (digger != null)
        {
            digger.transform.SetParent(null, true);
        }

        if (holeMask != null && !holeMask.transform.IsChildOf(transform))
        {
            Destroy(holeMask.gameObject);
        }

        Destroy(gameObject);
    }

    static bool TrySetTrigger(Animator animator, string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(parameterName);
                return true;
            }
        }

        return false;
    }
}
