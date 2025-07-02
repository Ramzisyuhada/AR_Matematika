using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class BiDirectionalDraggableSnackbar : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform snackbarPanel;
    public TMP_Text snackbarText; // Gunakan TMP_Text jika pakai TextMeshPro
    public float showDuration = 3f;
    public float dismissThreshold = 150f;
    public float returnDuration = 0.2f;
    public Vector2 shownPos = new Vector2(0, 100);
    public Vector2 hiddenPos = new Vector2(0, -300);

    private Vector2 dragStartPos;
    private bool isShowing = false;

    void Start()
    {
        snackbarPanel.anchoredPosition = hiddenPos;
    }

    public void ShowSnackbar(string message)
    {
        StopAllCoroutines();
        snackbarText.text = message;
        isShowing = true;
        StartCoroutine(Slide(snackbarPanel.anchoredPosition, shownPos, 0.3f));
        StartCoroutine(AutoHide());
    }

    IEnumerator AutoHide()
    {
        yield return new WaitForSeconds(showDuration);
        if (isShowing)
        {
            Dismiss();
        }
    }

    public void Dismiss()
    {
        isShowing = false;
        StopAllCoroutines();
        StartCoroutine(Slide(snackbarPanel.anchoredPosition, hiddenPos, 0.3f));
    }

    IEnumerator Slide(Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            snackbarPanel.anchoredPosition = Vector2.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        snackbarPanel.anchoredPosition = to;
    }

    // ================= DRAG HANDLING =================
    public void OnBeginDrag(PointerEventData eventData)
    {
        dragStartPos = snackbarPanel.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 dragDelta = eventData.delta;
        snackbarPanel.anchoredPosition += new Vector2(0, dragDelta.y);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float dragDistance = snackbarPanel.anchoredPosition.y - shownPos.y;

        // Jika ditarik cukup jauh ke bawah → dismiss
        if (dragDistance < -dismissThreshold)
        {
            Dismiss();
        }
        else
        {
            // Balik ke posisi semula
            StartCoroutine(Slide(snackbarPanel.anchoredPosition, shownPos, returnDuration));
        }
    }
}
