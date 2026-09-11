using UnityEngine;

// Shows a usable's UI once the defender that produces it is unlocked - on the selection screen and
// in level alike. Keyed on unlock rather than the level loadout: the loadout auto-fills to
// MaxLoadoutSize in catalog order and is not persisted, so a producer near the end of the catalog
// would never appear to be picked.
public class EVVUsableLoadoutVisibility : MonoBehaviour
{
    [System.Serializable]
    class ProducerGroup
    {
        [Tooltip("Defender id from EVVDefenderCatalog, e.g. potion_maker")]
        public string producerDefenderId = "";

        [Tooltip("UI shown only once that defender is unlocked")]
        public GameObject[] uiObjects;
    }

    [SerializeField] ProducerGroup[] groups;

    void OnEnable()
    {
        EVVDefenderUnlocks.UnlocksChanged += Apply;
        Apply();
    }

    void OnDisable()
    {
        EVVDefenderUnlocks.UnlocksChanged -= Apply;
    }

    void Apply()
    {
        if (groups == null)
        {
            return;
        }

        foreach (ProducerGroup group in groups)
        {
            if (group == null || group.uiObjects == null)
            {
                continue;
            }

            bool show = !string.IsNullOrEmpty(group.producerDefenderId)
                && EVVDefenderUnlocks.IsUnlocked(group.producerDefenderId);

            foreach (GameObject uiObject in group.uiObjects)
            {
                if (uiObject != null)
                {
                    uiObject.SetActive(show);
                }
            }
        }
    }

}
