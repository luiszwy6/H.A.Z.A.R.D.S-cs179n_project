using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadMainMenu()
    {
        SceneManager.LoadScene("main_menu");
    }

    public void LoadGame()
    {
        SceneManager.LoadScene("gameflow");
    }

    public void LoadVictory()
    {
        SceneManager.LoadScene("victory_screen");
    }

    public void LoadDefeat()
    {
        SceneManager.LoadScene("defeat_screen");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}