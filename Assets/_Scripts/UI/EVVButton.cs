using TMPro;
using UnityEngine;

public class EVVButton : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI textMeshPro;

    public void SetButtonText(string text)
    {
        textMeshPro.text = text;
    }

}
