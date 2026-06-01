using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class GameFlowManager : MonoBehaviour
{
    public string victorySceneName = "victory_screen";
    public string defeatSceneName = "defeat_screen";
    public bool enableDebugKeys = false;

    private bool gameEnded;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        EnsurePlayerDeathHook();
    }

    private void Start()
    {
        EnsurePlayerDeathHook();
    }

    private void Update()
    {
        if (!enableDebugKeys || gameEnded)
            return;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.kKey.wasPressedThisFrame)
            LoadVictory();

        if (keyboard.lKey.wasPressedThisFrame)
            LoadDefeat();
#else
        if (Input.GetKeyDown(KeyCode.K))
            LoadVictory();

        if (Input.GetKeyDown(KeyCode.L))
            LoadDefeat();
#endif
    }

    private void OnDestroy()
    {
        if (playerHealth != null && playerHealth.onDeath != null)
            playerHealth.onDeath.RemoveListener(LoadDefeat);
    }

    public void LoadVictory()
    {
        LoadEndScene(victorySceneName);
    }

    public void LoadDefeat()
    {
        LoadEndScene(defeatSceneName);
    }

    private void LoadEndScene(string sceneName)
    {
        if (gameEnded)
            return;

        gameEnded = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private void EnsurePlayerDeathHook()
    {
        if (playerHealth != null)
            return;

        playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth == null || playerHealth.onDeath == null)
            return;

        playerHealth.onDeath.RemoveListener(LoadDefeat);
        playerHealth.onDeath.AddListener(LoadDefeat);
    }
}
