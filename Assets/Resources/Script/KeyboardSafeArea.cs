using UnityEngine;
using UnityEngine.UI;

public class KeyboardSafeArea : MonoBehaviour
{
    RectTransform rt;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (TouchScreenKeyboard.visible)
        {
            // tinggi keyboard
            var kbArea = TouchScreenKeyboard.area;
            float height = kbArea.height;

            // konversi ke canvas scale
            float scaledHeight = height / Screen.height * rt.rect.height;

            rt.anchoredPosition = new Vector2(0, scaledHeight);
        }
        else
        {
            // turun kembali
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
