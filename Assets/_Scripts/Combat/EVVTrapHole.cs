using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

// The Trap Digger's hole. While he digs and then lays the cover, the tile is an ordinary defender
// the Vikings attack. Once the cover is down (EEVTrapDigger arms the hole) nobody attacks it: the
// next Viking to walk onto it falls in, plus any arriving within the group window, sinking behind
// the same sprite mask the digger sank behind. From then on the hole is open and fills itself back
// up: for the regeneration time the cell stays taken, and the Vikings see the open hole and jump
// over it, coming or going. The digger dying before the cover is done leaves an open hole that
// regenerates the same way. Regenerated, the trap is gone and the cell is free again.
[RequireComponent(typeof(EVVDefender))]
[RequireComponent(typeof(EVVHealth))]
public class EVVTrapHole : MonoBehaviour
{
    enum State { Digging, Armed, Sprung, Regenerating }

    [Header("Hole")]
    [SerializeField] SpriteMask holeMask;
    [SerializeField] SpriteRenderer holeSprite;
    [Tooltip("The leaves and sticks covering the finished hole. They drop into it with the first Viking.")]
    [SerializeField] GameObject cover;

    [Header("Falling in")]
    [Tooltip("Half width of the strip over the hole (world units) a Viking's feet have to enter to fall.")]
    [SerializeField, Min(0.01f)] float triggerHalfWidth = 0.35f;
    [Tooltip("A Viking sinks until his top is this far below the mask's top edge (the rim dips about 0.3 below it in the middle), and the mask is extended this far below his feet.")]
    [SerializeField, Min(0f)] float hideMargin = 0.6f;
    [SerializeField, Min(0.05f)] float fallSeconds = 0.55f;
    [Tooltip("How long the hole stays open after the first Viking falls, so a group falls in together.")]
    [SerializeField, Min(0f)] float groupWindowSeconds = 0.2f;
    [Tooltip("Animator trigger fired on a falling Viking, if his rig has it.")]
    [SerializeField] string fallTrigger = "Stun";

    [Header("Open hole")]
    [Tooltip("Seconds the open hole takes to fill itself back up. The cell stays taken until then.")]
    [SerializeField, Min(0f)] float regenerationSeconds = 60f;
    [Tooltip("A Viking walking into the open hole jumps it: takes off this far from the hole's centre and lands as far past it.")]
    [SerializeField, Min(0.05f)] float jumpHalfWidth = 0.8f;
    [SerializeField, Min(0.01f)] float jumpHeight = 0.7f;
    [SerializeField, Min(0.05f)] float jumpSeconds = 0.5f;

    EVVDefender defender;
    EVVHealth health;
    State state = State.Digging;
    float windowRemaining;
    float lastFallEnds;
    float regenerationRemaining;
    // The mask's top edge (the rim) and how far down the masked area currently reaches; a plain
    // rectangle mask is hung below the digger's mask when a faller needs more.
    float rimY;
    float maskBottom;
    SpriteMask maskExtension;
    static Sprite squareSprite;
    Color holeColor;
    readonly List<MonoBehaviour> candidates = new List<MonoBehaviour>();
    readonly Dictionary<MonoBehaviour, float> lastWalkerX = new Dictionary<MonoBehaviour, float>();
    readonly List<MonoBehaviour> staleWalkers = new List<MonoBehaviour>();

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

    // The digger is out of the hole: the leaves and sticks come down over it. Returns how long
    // that takes; the digger arms the hole after the wait, so until every piece has landed it is
    // still an ordinary, attackable defender and dies like one.
    public float PlaceCover()
    {
        if (state != State.Digging || cover == null)
        {
            return 0f;
        }

        cover.SetActive(true);
        float seconds = 0f;
        foreach (EEVScatterInTween scatter in cover.GetComponentsInChildren<EEVScatterInTween>(true))
        {
            seconds = Mathf.Max(seconds, scatter.TotalSeconds);
        }

        return seconds;
    }

    // The hole is finished and covered: nobody attacks it any more, the next Viking falls in.
    public void Arm()
    {
        if (state != State.Digging)
        {
            return;
        }

        state = State.Armed;
        defender.SetTargetable(false);
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

    // Anyone stepping onto the cover goes in, a Viking carrying the lure back out just the same.
    void ScanForFallers()
    {
        candidates.Clear();
        CollectFallers(EVVTargetRegistry.Enemies);
        CollectFallers(EVVTargetRegistry.CharmedEnemies);

        // Swallowing disables the walker, which drops it from the list being scanned.
        for (int i = 0; i < candidates.Count; i++)
        {
            Swallow(candidates[i]);
        }
    }

    void CollectFallers(IReadOnlyList<IEVVEnemyLaneWalker> walkers)
    {
        float holeX = HoleCenter.x;
        for (int i = 0; i < walkers.Count; i++)
        {
            if (IsWalkingHere(walkers[i], out MonoBehaviour behaviour)
                && Mathf.Abs(behaviour.transform.position.x - holeX) <= triggerHalfWidth)
            {
                candidates.Add(behaviour);
            }
        }
    }

    // A live, enabled walker in this lane (a disabled one is already falling in).
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

    // Out of the fight (the lure he holds gets away first; disabling the walker leaves the target
    // registry), then down the hole behind the mask, the way the digger went in. He only sinks
    // until the mask hides all of him and is gone the moment it does, so no part of him ever shows
    // up lower on the board.
    void Swallow(MonoBehaviour walker)
    {
        if (state == State.Armed)
        {
            Spring();
        }

        ((IEVVEnemyLaneWalker)walker).LetGo();
        walker.enabled = false;
        foreach (Collider2D collider in walker.GetComponentsInChildren<Collider2D>())
        {
            collider.enabled = false;
        }

        foreach (EVVHitRecoil recoil in walker.GetComponents<EVVHitRecoil>())
        {
            recoil.enabled = false;
        }

        EVVSilhouetteOutline outline = walker.GetComponent<EVVSilhouetteOutline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        EVVWorldHealthBar healthBar = walker.GetComponentInChildren<EVVWorldHealthBar>();
        if (healthBar != null)
        {
            healthBar.Hide();
        }

        Transform body = walker.transform;
        float sink = 2.5f;
        if (TryGetSpriteBounds(walker.gameObject, out Bounds bodyBounds))
        {
            sink = Mathf.Max(0.5f, bodyBounds.max.y - rimY + hideMargin);
            EnsureMaskReaches(bodyBounds.min.y - sink);
        }

        MaskForFalling(walker.gameObject);
        Animator animator = walker.GetComponent<Animator>();
        if (animator != null)
        {
            TrySetTrigger(animator, fallTrigger);
        }

        Vector3 bottom = new Vector3(HoleCenter.x, body.position.y - sink, body.position.z);
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
        FreeMask();
        DropCover();
    }

    // The digger's mask sits inside the trap's sorting group, where it only clips the group's own
    // sprites (and even that proved unreliable). Set free at the scene root it clips every sprite
    // set to hide inside it, whatever sorting order the Viking's animator gives his parts: nothing
    // but a falling Viking, the dropping cover and a digger still down a hole is ever set that way.
    void FreeMask()
    {
        rimY = transform.position.y;
        maskBottom = rimY;
        if (holeMask == null)
        {
            return;
        }

        holeMask.transform.SetParent(null, true);
        holeMask.isCustomRangeActive = false;
        Bounds maskBounds = holeMask.bounds;
        rimY = maskBounds.max.y;
        maskBottom = maskBounds.min.y;
    }

    // Extends the masked area down to below the given height with a plain rectangle under the
    // digger's mask, whose rim shape is left as it is.
    void EnsureMaskReaches(float bottomY)
    {
        float wanted = bottomY - hideMargin;
        if (holeMask == null || wanted >= maskBottom)
        {
            return;
        }

        Bounds maskBounds = holeMask.bounds;
        if (maskExtension == null)
        {
            GameObject extension = new GameObject(holeMask.name + " extension");
            extension.transform.SetParent(holeMask.transform, false);
            maskExtension = extension.AddComponent<SpriteMask>();
            maskExtension.sprite = SquareSprite;
            maskExtension.isCustomRangeActive = false;
        }

        // Overlaps the mask's bottom edge a little so no seam shows between the two.
        float top = maskBounds.min.y + 0.05f;
        Transform extensionTransform = maskExtension.transform;
        Vector3 parentScale = holeMask.transform.lossyScale;
        extensionTransform.position = new Vector3(maskBounds.center.x, (top + wanted) * 0.5f, holeMask.transform.position.z);
        extensionTransform.localScale = new Vector3(maskBounds.size.x / parentScale.x, (top - wanted) / parentScale.y, 1f);
        maskBottom = wanted;
    }

    // A 1 x 1 unit solid square for the extension mask.
    static Sprite SquareSprite
    {
        get
        {
            if (squareSprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                squareSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            }

            return squareSprite;
        }
    }

    // The leaves and sticks sink out of sight below the rim. Pieces still to come down (the digger
    // died while laying them) stay away.
    void DropCover()
    {
        if (cover == null || !cover.activeInHierarchy)
        {
            return;
        }

        foreach (EEVScatterInTween scatter in cover.GetComponentsInChildren<EEVScatterInTween>())
        {
            scatter.StopAllCoroutines();
        }

        float drop = 0.5f;
        if (TryGetSpriteBounds(cover, out Bounds coverBounds))
        {
            drop = Mathf.Max(drop, coverBounds.max.y - rimY + hideMargin);
            EnsureMaskReaches(coverBounds.min.y - drop);
        }

        MaskForFalling(cover);
        foreach (SpriteRenderer piece in cover.GetComponentsInChildren<SpriteRenderer>())
        {
            Transform pieceTransform = piece.transform;
            Tween.StopAll(onTarget: pieceTransform);
            EVVTween.TweenTo(pieceTransform, pieceTransform.position + Vector3.down * drop, fallSeconds, Ease.InQuad);
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
        }
    }

    // World bounds of the object's visible sprites.
    static bool TryGetSpriteBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!renderer.enabled || renderer.sprite == null)
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

    // Nothing sinks any more: a mask set free is done with.
    void ReleaseMask()
    {
        if (holeMask != null && !holeMask.transform.IsChildOf(transform))
        {
            Destroy(holeMask.gameObject);
        }
    }

    // ---------------------------------------------------------------- the open hole

    // The digger died before the trap was ready: the hole stays and fills itself back up like a
    // sprung one, a half-laid cover falling in first. It keeps one hit point so the board still
    // counts the cell as taken (a dead defender frees its cell). Any later death is the player's
    // remove tool.
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

            if (cover != null && cover.activeInHierarchy)
            {
                state = State.Sprung;
                windowRemaining = 0f;
                lastFallEnds = Time.time + fallSeconds;
                FreeMask();
                DropCover();
                return;
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
        ReleaseMask();
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

    // A Viking that walks up to the open hole jumps it, coming in or going back out with the lure.
    // Only walkers crossing a take-off line towards the hole count, so one standing near it
    // fighting something is left alone.
    void ScanForJumpers()
    {
        candidates.Clear();
        CollectJumpers(EVVTargetRegistry.Enemies);
        CollectJumpers(EVVTargetRegistry.CharmedEnemies);
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

    void CollectJumpers(IReadOnlyList<IEVVEnemyLaneWalker> walkers)
    {
        float holeX = HoleCenter.x;
        float rightLine = holeX + jumpHalfWidth;
        float leftLine = holeX - jumpHalfWidth;
        for (int i = 0; i < walkers.Count; i++)
        {
            if (!IsWalkingHere(walkers[i], out MonoBehaviour behaviour))
            {
                continue;
            }

            float x = behaviour.transform.position.x;
            if (lastWalkerX.TryGetValue(behaviour, out float previousX)
                && ((previousX > rightLine && x <= rightLine) || (previousX < leftLine && x >= leftLine)))
            {
                candidates.Add(behaviour);
            }

            lastWalkerX[behaviour] = x;
        }
    }

    // The walker is held still for the arc (it would drag the Viking back onto its lane line every
    // frame) and walks on from where he lands, on whichever side he was heading for.
    void Jump(MonoBehaviour walker)
    {
        lastWalkerX.Remove(walker);
        ((IEVVEnemyLaneWalker)walker).PauseWalk(jumpSeconds);
        Transform body = walker.transform;
        float side = body.position.x < HoleCenter.x ? 1f : -1f;
        Vector3 landing = new Vector3(HoleCenter.x + side * jumpHalfWidth, body.position.y, body.position.z);
        EVVTween.TweenArcTo(body, landing, body.position.y + jumpHeight, jumpSeconds);
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

        ReleaseMask();
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
