using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScaneEngine : MonoBehaviour
{
    [SerializeField] AudioSource click;
    public void Scane(string name)
    {

        StartCoroutine(PlaySoundAndLoadScene(name));

    }
    private IEnumerator PlaySoundAndLoadScene(string sceneName)
    {
        click.Play();

        yield return new WaitWhile(() => click.isPlaying);

        SceneManager.LoadScene(sceneName);
    }

    public void Scane1(string name)
    {
        if (name == "Latihan")
        {
            PlayerPrefs.SetString("SubmissionId", "S001");
            PlayerPrefs.SetString("assessment_id", "A_001");

            PlayerPrefs.Save();
        }           

        PlayerPrefs.Save();
        StartCoroutine(PlaySoundAndLoadScene(name));

    }

}
