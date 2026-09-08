using UnityEngine;

public class EVVUiWidgetRefs : MonoBehaviour
{
    [field: SerializeField] 
    public EVVDefenderSelectBar defenderSelectionTopBar { get; private set; }

    [field: SerializeField] 
    public EVVDefenderSelectionUi defenderSelectionUi { get; private set; }

    public static EVVUiWidgetRefs Instance { get; private set; }


    private void Awake()
    {
        // 2. Check if an instance already exists in the scene
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Destroy duplicate managers
            return;
        }

        // 3. Set this object as the definitive instance
        Instance = this;

        // 4. Optional: Keep this object alive when changing scenes
        DontDestroyOnLoad(gameObject);
    }

}
