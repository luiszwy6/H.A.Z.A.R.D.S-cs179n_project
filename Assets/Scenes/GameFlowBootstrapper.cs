using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameFlowBootstrapper
{
    private const string MainMenuSceneName = "main_menu";
    private const string GameSceneName = "Level 1";
    private const string VictorySceneName = "victory_screen";
    private const string DefeatSceneName = "defeat_screen";
    private static bool allowNextGameSceneLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoaded()
    {
        allowNextGameSceneLoad = false;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public static void AllowNextGameSceneLoad()
    {
        allowNextGameSceneLoad = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void HandleInitialSceneLoaded()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == GameSceneName)
        {
            if (!ConsumeGameSceneLoadPermission())
            {
                SceneManager.LoadScene(MainMenuSceneName);
                return;
            }

            EnsureGameplayFlowObjects();
            return;
        }

        if (scene.name == MainMenuSceneName ||
            scene.name == VictorySceneName ||
            scene.name == DefeatSceneName)
        {
            UnlockCursor();
        }
    }

    private static bool ConsumeGameSceneLoadPermission()
    {
        if (!allowNextGameSceneLoad)
            return false;

        allowNextGameSceneLoad = false;
        return true;
    }

    private static void EnsureGameplayFlowObjects()
    {
        GameFlowManager manager = Object.FindObjectOfType<GameFlowManager>();

        if (manager == null)
        {
            GameObject managerObject = new GameObject("GameFlowManager");
            manager = managerObject.AddComponent<GameFlowManager>();
        }

        EnemySquadGenerator squadGenerator = Object.FindObjectOfType<EnemySquadGenerator>();
        EnemyVictoryChecker checker = Object.FindObjectOfType<EnemyVictoryChecker>();

        if (squadGenerator == null && checker == null)
        {
            GameObject checkerObject = new GameObject("EnemyVictoryChecker");
            checkerObject.AddComponent<EnemyVictoryChecker>();
        }
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
    }
}
