using System.Collections;
using PrimeTween;
using UnityEngine;

public class EEVScatterInTween : MonoBehaviour
{

    [SerializeField] float startDelay;
    [SerializeField] float fromHeight;
    [SerializeField] float fallTime = 0.5f;
    [SerializeField] float scatterDurationSeconds = 2f;
    [SerializeField] Ease easeFunciton = Ease.OutSine;

    void Start()
    {
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }
        StartCoroutine(Scatter());
    }

    IEnumerator Scatter()
    {   
        yield return new WaitForSeconds(startDelay);
        var interval = scatterDurationSeconds / Mathf.Max(transform.childCount-1, 1);
        foreach (Transform child in transform) {
            child.gameObject.SetActive(true);
            var originalPos = child.position;
            child.position += Vec3.Y(fromHeight);
            _ = EVVTween.TweenTo(child, originalPos, fallTime, ease: easeFunciton);
            yield return new WaitForSeconds(interval);
        }
    } 

}
