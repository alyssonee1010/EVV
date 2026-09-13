using UnityEngine;
using UnityEngine.Rendering;

public class EVVDisableChildObjectsComponents : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {        
        foreach (Transform child in transform) {
            var components = child.GetComponentsInChildren<Behaviour>();
            foreach (var component in components)
            {
                if (component is SortingGroup)
                    continue;
                component.enabled = false;
            }
        }
    }
}
