using UnityEngine;

// Rebinds the scene's fixed EVVPlacementCharacterSlot objects to whichever defenders are
// currently in the player's loadout (EVVDefenderUnlocks), in loadout order. Slots beyond the
// loadout size are hidden. Call Rebind() whenever the loadout changes or a level starts.
public class EVVPlacementSlotBinder : MonoBehaviour
{
    [SerializeField] EVVPlacementCharacterSlot[] slots;

    void OnEnable()
    {
        EVVDefenderUnlocks.LoadoutChanged += Rebind;
        Rebind();
    }

    void OnDisable()
    {
        EVVDefenderUnlocks.LoadoutChanged -= Rebind;
    }

    public void Rebind()
    {
        if (slots == null || EVVDefenderCatalog.Instance == null)
        {
            return;
        }

        var loadout = EVVDefenderUnlocks.GetLoadout();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                continue;
            }

            if (i >= loadout.Count)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            EVVDefenderCatalog.Entry entry = EVVDefenderCatalog.Instance.FindById(loadout[i]);
            if (entry == null || entry.prefab == null)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            slots[i].gameObject.SetActive(true);
        }
    }
}
