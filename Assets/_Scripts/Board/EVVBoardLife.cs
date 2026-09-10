using TMPro;
using UnityEngine;

[RequireComponent(typeof(EVVHealth))]
[RequireComponent(typeof(EVVWorldHealthBar))]
public class EVVBoardLife : MonoBehaviour
{
    [SerializeField, Min(1)] int startingLife = 5;
    [SerializeField, Min(1)] int defaultDamagePerEnemy = 1;
    [SerializeField] bool pauseOnGameOver = true;
    [SerializeField] string gameOverMessage = "GAME OVER";
    [SerializeField] Vector3 gameOverWorldOffset = new Vector3(0f, 3.5f, 0f);
    [SerializeField] float gameOverFontSize = 1.2f;
    [SerializeField] Color gameOverColor = new Color(1f, 0.12f, 0.08f, 1f);

    EVVHealth health;
    TextMeshPro gameOverText;
    bool isGameOver;

    public static EVVBoardLife Instance { get; private set; }
    public bool IsGameOver => isGameOver;

    public void ResetLife()
    {
        isGameOver = false;
        SetGameOverTextVisible(false);
        Time.timeScale = 1f;

        if (health != null)
        {
            health.SetMaxHealth(startingLife);
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        health = GetComponent<EVVHealth>();
        health.SetMaxHealth(startingLife);
        health.Died += OnBoardLifeDepleted;
        EnsureGameOverText();
        SetGameOverTextVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (health != null)
        {
            health.Died -= OnBoardLifeDepleted;
        }
    }

    public static bool TryDamageActiveBoardLife(int damage)
    {
        EVVBoardLife boardLife = Instance != null ? Instance : FindAnyObjectByType<EVVBoardLife>();
        if (boardLife == null)
        {
            return false;
        }

        boardLife.TakeEnemyLeakDamage(damage);
        return true;
    }

    public void TakeEnemyLeakDamage(int damage)
    {
        if (isGameOver || health == null)
        {
            return;
        }

        health.TakeDamage(damage > 0 ? damage : defaultDamagePerEnemy);
    }

    void OnBoardLifeDepleted(EVVHealth depletedHealth)
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;
        SetGameOverTextVisible(true);
        Debug.Log("Game Over: the chest life reached 0.");

        if (pauseOnGameOver)
        {
            Time.timeScale = 0f;
        }
    }

    void EnsureGameOverText()
    {
        if (gameOverText != null)
        {
            return;
        }

        GameObject textObject = new GameObject("Game Over Text");
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = gameOverWorldOffset;
        textObject.transform.localRotation = Quaternion.identity;
        textObject.transform.localScale = Vector3.one;

        gameOverText = textObject.AddComponent<TextMeshPro>();
        gameOverText.text = gameOverMessage;
        gameOverText.alignment = TextAlignmentOptions.Center;
        gameOverText.fontSize = gameOverFontSize;
        gameOverText.color = gameOverColor;
        gameOverText.sortingOrder = 20000;
    }

    void SetGameOverTextVisible(bool visible)
    {
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(visible);
        }
    }
}
