using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadScene()
    {


        SceneManager.LoadSceneAsync(1);
    }

    public void LoadMenu()
    {


        SceneManager.LoadSceneAsync(0);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void ReloadGame()
    {


        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
    }
}