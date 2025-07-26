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
}
