using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimasiHome : MonoBehaviour
{
    public List<RectTransform> panels;

    public float duration = 0.5f;

    private int currentIndex = 0;
    private Vector2 centerPos;
    private Vector2 rightPos;
    private Vector2 leftPos;
    [SerializeField] private AudioSource Right;
    [SerializeField] private AudioSource Left;

    void Start()
    {
        centerPos = panels[0].anchoredPosition;
        rightPos = new Vector2(1500, centerPos.y);
        leftPos = new Vector2(-1500, centerPos.y);

        // Sembunyikan semua panel ke kanan, kecuali index 0
        for (int i = 0; i < panels.Count; i++)
        {
            panels[i].anchoredPosition = (i == 0) ? centerPos : rightPos;
        }
    }

    public void NextPanel()
    {
        Right.Play();
        if (currentIndex >= panels.Count - 1) return;

        LeanTween.move(panels[currentIndex], leftPos, duration).setEaseInExpo();

        currentIndex++;

        panels[currentIndex].anchoredPosition = rightPos;
        LeanTween.move(panels[currentIndex], centerPos, duration).setEaseOutExpo();
    }

    public void PrevPanel()
    {
        Left.Play();
        if (currentIndex <= 0) return;

        LeanTween.move(panels[currentIndex], rightPos, duration).setEaseInExpo();

        currentIndex--;

        panels[currentIndex].anchoredPosition = leftPos;
        LeanTween.move(panels[currentIndex], centerPos, duration).setEaseOutExpo();
    }
    private void Awake()
    {
        Screen.orientation = ScreenOrientation.Portrait;

    }

   
}
