using UnityEngine;

public class EVVConstantRotation : MonoBehaviour
{
    [SerializeField] float degreesPerSecond = 60f;

    void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
    }
}
