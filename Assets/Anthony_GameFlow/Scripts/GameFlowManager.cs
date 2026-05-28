using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class GameFlowManager : MonoBehaviour
{
    public string victorySceneName = "victory_screen";
    public string defeatSceneName = "defeat_screen";

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            LoadVictory();
        }

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            LoadDefeat();
        }
    }

    public void LoadVictory()
    {
        SceneManager.LoadScene(victorySceneName);
    }

    public void LoadDefeat()
    {
        SceneManager.LoadScene(defeatSceneName);
    }
}