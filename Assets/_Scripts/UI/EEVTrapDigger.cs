using UnityEngine;
using UnityEngine.Events;

public class EEVTrapDigger : MonoBehaviour
{
    [SerializeField] float digDepth = 0.7f;
    [SerializeField] float digSeconds = 5f;
    [SerializeField] float jumpHeight = 0.2f;
    [SerializeField] Transform leaveHoleLandingPos;
    [SerializeField] UnityEvent onHoleDug;
    [SerializeField] UnityEvent onHoleLeft;
    [SerializeField] float walkSpeed = 1f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Sink().OnComplete(() => {
            if (this == null) return; // killed while digging
            onHoleDug.Invoke();
            // From here on the hole is the trap: it stops taking hits and swallows the next Vikings.
            EVVTrapHole hole = GetComponentInParent<EVVTrapHole>();
            if (hole != null) hole.Arm();
            LeaveHole().OnComplete(() => {
                if (this == null) return;
                onHoleLeft.Invoke();
                // No renderer on this object, so OnBecameInvisible never fires; leave once the walk is over.
                WalkLeftOffScreen().OnComplete(() => { if (this != null) Destroy(gameObject); });
            });
        });
    }
        
    private PrimeTween.Tween Sink()
    {
        return EVVTween.TweenTo(transform, transform.position - new Vector3(0,digDepth,0), digSeconds);
    }

    private PrimeTween.Tween LeaveHole()
    {
        var maxHeight = Mathf.Max(transform.position.y, leaveHoleLandingPos.position.y) + jumpHeight;
        return EVVTween.TweenArcTo(transform, leaveHoleLandingPos.position, maxHeight);
    }

    private PrimeTween.Tween WalkLeftOffScreen()
    {
        return EVVTween.TweenTo(transform, transform.position - new Vector3(20*walkSpeed,0,0), 20);
    }

    void OnBecameInvisible() {
        Destroy(gameObject);
    }

    // Killed mid-dig: stop the sink/walk tweens on this transform so their OnComplete callbacks are
    // not reported as lost.
    void OnDestroy() {
        PrimeTween.Tween.StopAll(onTarget: transform);
    }
}
