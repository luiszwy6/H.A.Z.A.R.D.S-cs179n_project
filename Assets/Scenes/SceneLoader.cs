using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public string mainMenuSceneName = "main_menu";
    public string gameSceneName = "Level 2";
    public string victorySceneName = "victory_screen";
    public string defeatSceneName = "defeat_screen";

    public void LoadMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void LoadGame()
    {
        GameFlowBootstrapper.AllowNextGameSceneLoad();
        SceneManager.LoadScene(gameSceneName);
    }

    public void LoadVictory()
    {
        SceneManager.LoadScene(victorySceneName);
    }

    public void LoadDefeat()
    {
        SceneManager.LoadScene(defeatSceneName);
    }

    public void LoadRetry()
    {
        string scene = GameFlowBootstrapper.RetrySceneName;
        if (string.IsNullOrEmpty(scene))
            scene = gameSceneName;

        GameFlowBootstrapper.AllowNextGameSceneLoad();
        SceneManager.LoadScene(scene);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
