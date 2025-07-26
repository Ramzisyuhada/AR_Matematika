using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FlipBook : MonoBehaviour
{
    public Sprite[] bookPages;
    public GameObject Button;
    public Book book;
    public GameObject Materi;


    public GameObject ButtonAR;
    public GameObject Header;
    void Start()
    {
        for (int i = 0; i < bookPages.Length; i++)
        {
            GameObject buttonObj = Instantiate(Button);
            buttonObj.transform.SetParent(transform, false); // pastikan layout tetap

            Image img = buttonObj.GetComponent<Image>();
            img.sprite = bookPages[i];

            int index = i; // local copy to avoid closure issue
            buttonObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                JumpToPage(index);
            });
        }

        Button.SetActive(false);
    }

    void JumpToPage(int targetPage)
    {
        Debug.Log(bookPages.Length);
        if (targetPage >= bookPages.Length - 1)
        {
            ButtonAR.SetActive(true);
            Header.SetActive(false);
        }
        else
        {
            Header.SetActive(true);
            ButtonAR.SetActive(false);
        }
           Materi.GetComponent<Image>().sprite = bookPages[targetPage];
    }

 
}
