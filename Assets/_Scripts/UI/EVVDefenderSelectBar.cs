using System.Linq;
using UnityEngine;

public class EVVDefenderSelectBar : MonoBehaviour
{

    public Transform cardsContainer;
    public float slotWidth = 1f;
    public EVVDefenderCard cardPrefab;

    public Vector3 GetCardPosition(int slot)
    {
        var container = cardsContainer.transform;
        return container.position + new Vector3(slotWidth,0,0) * slot * container.localScale.x;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetupCards();
    }

    public void CloseGaps()
    {
        foreach (EVVDefenderCard card in GetComponentsInChildren<EVVDefenderCard>())
        {
            var index = EVVManager.Instance.SelectedDefenders.TakeWhile(it => it != card.defenderType).Count();
            card.transform.TweenTo(GetCardPosition(index));
        }
    }

    public void SetupCards()
    {
        ClearSlots();

        int i = 0;
        foreach (var slot in EVVManager.Instance.SelectedDefenders)
        {
            if (i >= 6)
                break;

            var card = Instantiate(cardPrefab, cardsContainer);
            card.transform.position = GetCardPosition(i);
            card.defenderType = EVVManager.Instance.SelectedDefenders[i];

            i += 1;
        }
    }

    void ClearSlots()
    {
        cardsContainer.DestroyChildren();
    }
}
