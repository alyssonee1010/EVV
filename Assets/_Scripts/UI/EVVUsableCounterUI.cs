using TMPro;
using UnityEngine;

public class EVVUsableCounterUI : MonoBehaviour
{
    enum CounterResource { Diamonds, HealingPotions, SpeedPotions }

    [SerializeField] EVVUsableWallet wallet;
    [SerializeField] CounterResource resource = CounterResource.Diamonds;
    [SerializeField] TMP_Text diamondText;
    [SerializeField] string prefix = "";

    public static Transform DiamondTarget { get; private set; }
    public static Transform HealingPotionTarget { get; private set; }
    public static Transform SpeedPotionTarget { get; private set; }

    void Awake()
    {
        if (IsPlacementSlotPrice())
        {
            enabled = false;
            return;
        }

        if (wallet == null)
        {
            wallet = EVVUsableWallet.Instance != null ? EVVUsableWallet.Instance : FindAnyObjectByType<EVVUsableWallet>();
        }

        if (diamondText == null)
        {
            diamondText = GetComponentInChildren<TMP_Text>();
        }
    }

    void OnEnable()
    {
        if (IsPlacementSlotPrice())
        {
            enabled = false;
            return;
        }

        if (wallet == null)
        {
            wallet = EVVUsableWallet.Instance != null ? EVVUsableWallet.Instance : FindAnyObjectByType<EVVUsableWallet>();
        }

        if (wallet != null)
        {
            if (resource == CounterResource.HealingPotions)
            {
                wallet.HealingPotionsChanged += UpdateCounter;
                UpdateCounter(wallet.HealingPotions);
                HealingPotionTarget = transform;
            }
            else if (resource == CounterResource.SpeedPotions)
            {
                wallet.SpeedPotionsChanged += UpdateCounter;
                UpdateCounter(wallet.SpeedPotions);
                SpeedPotionTarget = transform;
            }
            else
            {
                wallet.DiamondsChanged += UpdateCounter;
                UpdateCounter(wallet.Diamonds);
                DiamondTarget = transform;
            }
        }
    }

    void OnDisable()
    {
        if (wallet != null)
        {
            wallet.DiamondsChanged -= UpdateCounter;
            wallet.HealingPotionsChanged -= UpdateCounter;
            wallet.SpeedPotionsChanged -= UpdateCounter;
        }

        if (DiamondTarget == transform)
        {
            DiamondTarget = null;
        }

        if (HealingPotionTarget == transform)
        {
            HealingPotionTarget = null;
        }

        if (SpeedPotionTarget == transform)
        {
            SpeedPotionTarget = null;
        }
    }

    void UpdateCounter(int diamonds)
    {
        if (diamondText != null)
        {
            diamondText.text = prefix + diamonds;
        }
    }

    bool IsPlacementSlotPrice()
    {
        return name == "Price" && GetComponentInParent<EVVPlacementCharacterSlot>() != null;
    }
}
