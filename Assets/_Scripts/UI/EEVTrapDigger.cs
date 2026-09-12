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


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Sink().OnComplete(() => {
            onHoleDug.Invoke();
            LeaveHole().OnComplete(() => {
                onHoleLeft.Invoke();
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
}
