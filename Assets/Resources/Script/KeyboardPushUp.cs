using UnityEngine;

public class KeyboardPushAdaptive : MonoBehaviour
{
    public RectTransform panel;   // panel login
    public float extraPadding = 20f;

    private Vector2 originalPos;
    private Rect lastSafeArea;

    void Start()
    {
        originalPos = panel.anchoredPosition;
        lastSafeArea = Screen.safeArea;
    }

    void Update()
    {
        Rect safe = Screen.safeArea;

        // Jika safe area berubah → artinya keyboard muncul / hilang
        if (safe != lastSafeArea)
        {
            AdjustPanel(safe);
            lastSafeArea = safe;
        }
    }

    void AdjustPanel(Rect safeArea)
    {
        float screenHeight = Screen.height;
        float keyboardHeight = screenHeight - (safeArea.y + safeArea.height);

        // Tidak ada keyboard
        if (keyboardHeight <= 0)
        {
            panel.anchoredPosition = originalPos;
            return;
        }

        // Keyboard muncul → geser panel sesuai tinggi keyboard
        panel.anchoredPosition = new Vector2(
            originalPos.x,
            originalPos.y + keyboardHeight + extraPadding
        );
    }
}
