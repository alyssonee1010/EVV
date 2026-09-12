using TMPro;
using UnityEngine;
using PrimeTween;
using UnityEngine.EventSystems;

public class EVVDefenderCard : MonoBehaviour, IPointerClickHandler
{
    public TextMeshPro priceTag;
    public EVVDefender defenderType;
    public Transform previewContainer;

    [SerializeField] float selectedScaleMultiplier = 1.2f;

    Vector3 baseScale;
    bool hasBaseScale;

    public GameObject CharacterPrefab => defenderType.gameObject;
    public int Cost => defenderType.GetComponent<EVVDefender>().cost;

    void ToggleSelect()
    {
        if (EVVManager.Instance.SelectedDefenders.Contains(defenderType))
        {
            EVVManager.Instance.SelectedDefenders.Remove(defenderType);
            var targetPos = EVVUiWidgetRefs.Instance.defenderSelectionUi.GetDefenderPosition(defenderType);
            transform.SetParent(EVVUiWidgetRefs.Instance.defenderSelectionUi.transform);
            EVVUiWidgetRefs.Instance.defenderSelectionTopBar.CloseGaps();
            transform.TweenTo(targetPos, 0.5f);
        } else
        {
            var selectedCount = EVVManager.Instance.SelectedDefenders.Count;
            if (selectedCount >= EVVManager.MaxDefenderTypes)
                return;
        
            EVVManager.Instance.SelectedDefenders.Add(defenderType);
            var targetPos = EVVUiWidgetRefs.Instance.defenderSelectionTopBar.GetCardPosition(selectedCount);
            transform.SetParent(EVVUiWidgetRefs.Instance.defenderSelectionTopBar.cardsContainer);
            transform.TweenTo(targetPos, 0.5f);
        }

    }

    void Start()
    {
        var preview = Instantiate(defenderType, previewContainer, false);
        preview.transform.localScale *= 0.67f;
        priceTag.text = Cost.ToString();

        CaptureBaseScale();
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        CaptureBaseScale();
        transform.localScale = selected ? baseScale * selectedScaleMultiplier : baseScale;
    }

    void CaptureBaseScale()
    {
        if (hasBaseScale)
        {
            return;
        }

        baseScale = transform.localScale;
        hasBaseScale = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (EVVManager.Instance.MenuIsOpen){
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                ToggleSelect();
            }
        }
    }
}
