using System.Collections.Generic;
using System.Linq;
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;

public class EVVBumpChildren : MonoBehaviour, IPointerClickHandler
{
    List<Transform> _children = new();
    Vector3[] _childrenOriginalPositions;

    [SerializeField] float duration = 0.4f;
    [SerializeField] float height = 0.4f;

    int tweensInProgress = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        foreach (Transform child in transform){
            _children.Add(child);
        }
        _childrenOriginalPositions = _children.Select(t => t.localPosition).ToArray();
    }

    public void Bump()
    {
        if (tweensInProgress > 0)
            return;

        for (var i = 0; i<_children.Count; i++)
        {
            var child = _children[i];
            var originalPos = _childrenOriginalPositions[i];
            var targetPos = child.localPosition;
            var h = Random.Range(0, height);
            targetPos.y *= 1 + h;
            tweensInProgress += 1;
            EVVTween.TweenTo(child, transform.position + targetPos, duration * h * Mathf.Sqrt(2), PrimeTween.Ease.OutQuad, 2, CycleMode.Rewind)
                .OnComplete(() => tweensInProgress -= 1, warnIfTargetDestroyed: false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("Click Pile Of Rocks");
        Bump();
    }
}
