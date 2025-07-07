using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DemoButtonController : MonoBehaviour
{
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private BottomSheetController bottomSheetPrefab;
    [SerializeField] private GameObject Canvas_Penjelasan;
    [SerializeField] private GameObject Balok;
    [SerializeField] private GameObject AnimateText;
    [SerializeField] private GameObject AudioSource;

    private BottomSheetController bottomSheetInstance;

    public void OnUserPressButton()
    {
        if (bottomSheetPrefab.gameObject.active)
        {
            bottomSheetPrefab.gameObject.SetActive(false);

        }
        else
        {
            bottomSheetPrefab.gameObject.SetActive(true);

        }

    }

}

   

