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
    [Tooltip("Half width of the strip over the hole (world units) a Viking's feet have to enter to break the cover.")]
    [SerializeField, Min(0.01f)] float triggerHalfWidth = 0.35f;
    [Tooltip("Once the cover breaks, everyone whose feet are this close to the hole's centre goes down with him.")]
    [SerializeField, Min(0.01f)] float groupHalfWidth = 0.8f;
    [Tooltip("How far a Viking sinks while he shrinks away to nothing. Keep it within the digger's mask (1.6 deep), which hides everything below the rim.")]
    [SerializeField, Min(0.1f)] float fallDepth = 1f;
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
    Color holeColor;
    readonly List<MonoBehaviour> candidates = new List<MonoBehaviour>();
    readonly Dictionary<MonoBehaviour, float> lastWalkerX = new Dictionary<MonoBehaviour, float>();
    readonly List<MonoBehaviour> staleWalkers = new List<MonoBehaviour>();
    // Walkers that were already over the hole when it opened (the one who just killed the digger).
    readonly List<MonoBehaviour> standingOnHole = new List<MonoBehaviour>();

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
                ScanForFallers(triggerHalfWidth);
                // The cover just broke: everyone else standing over the hole goes down with him.
                if (state == State.Sprung)
                {
                    ScanForFallers(groupHalfWidth);
                }
                break;
            case State.Sprung:
                windowRemaining -= Time.deltaTime;
                if (windowRemaining > 0f)
                {
                    ScanForFallers(groupHalfWidth);
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

    // Anyone within reach of the hole's centre goes in, a Viking carrying the lure back out just the same.
    void ScanForFallers(float reach)
    {
        candidates.Clear();
        CollectWithinReach(EVVTargetRegistry.Enemies, reach);
        CollectWithinReach(EVVTargetRegistry.CharmedEnemies, reach);

        // Swallowing disables the walker, which drops it from the list being scanned.
        for (int i = 0; i < candidates.Count; i++)
        {
            Swallow(candidates[i]);
        }
    }

    void CollectWithinReach(IReadOnlyList<IEVVEnemyLaneWalker> walkers, float reach)
    {
        float holeX = HoleCenter.x;
        for (int i = 0; i < walkers.Count; i++)
        {
            if (IsWalkingHere(walkers[i], out MonoBehaviour behaviour)
                && Mathf.Abs(behaviour.transform.position.x - holeX) <= reach)
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
    // registry), then down the hole behind the mask, the way the digger went in. He shrinks away
    // to nothing as he sinks, so a shallow mask hides all of him and the mask art is never
    // stretched; he is destroyed the moment he is gone.
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
        MaskForFalling(walker.gameObject);
        Animator animator = walker.GetComponent<Animator>();
        if (animator != null)
        {
            TrySetTrigger(animator, fallTrigger);
        }

        Vector3 bottom = new Vector3(HoleCenter.x, body.position.y - fallDepth, body.position.z);
        GameObject victim = walker.gameObject;
        lastFallEnds = Mathf.Max(lastFallEnds, Time.time + fallSeconds);
        Tween.Scale(body, Vector3.zero, fallSeconds, Ease.InQuad);
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
        if (holeMask == null)
        {
            return;
        }

        holeMask.transform.SetParent(null, true);
        holeMask.isCustomRangeActive = false;
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

        MaskForFalling(cover);
        foreach (SpriteRenderer piece in cover.GetComponentsInChildren<SpriteRenderer>())
        {
            Transform pieceTransform = piece.transform;
            Tween.StopAll(onTarget: pieceTransform);
            EVVTween.TweenTo(pieceTransform, pieceTransform.position + Vector3.down * fallDepth, fallSeconds, Ease.InQuad);
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
        // Whoever is standing over the hole as it opens jumps it once he walks on, instead of
        // strolling across; a newcomer has to cross a take-off line.
        candidates.Clear();
        CollectWithinReach(EVVTargetRegistry.Enemies, jumpHalfWidth);
        CollectWithinReach(EVVTargetRegistry.CharmedEnemies, jumpHalfWidth);
        standingOnHole.Clear();
        standingOnHole.AddRange(candidates);
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
            if (lastWalkerX.TryGetValue(behaviour, out float previousX))
            {
                bool crossedIn = (previousX > rightLine && x <= rightLine) || (previousX < leftLine && x >= leftLine);
                bool walkedOn = standingOnHole.Contains(behaviour) && Mathf.Abs(x - holeX) < Mathf.Abs(previousX - holeX);
                if (crossedIn || walkedOn)
                {
                    candidates.Add(behaviour);
                }
            }

            lastWalkerX[behaviour] = x;
        }
    }

    // The walker is held still for the arc (it would drag the Viking back onto its lane line every
    // frame) and walks on from where he lands, on whichever side he was heading for.
    void Jump(MonoBehaviour walker)
    {
        lastWalkerX.Remove(walker);
        standingOnHole.Remove(walker);
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
