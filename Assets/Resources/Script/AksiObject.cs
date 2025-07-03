using System.Collections;
using UnityEngine;

public class AksiObject : MonoBehaviour
{
    private Vector2 touchStartPos;
    public float rotationSpeed = 0.1f;

    private float currentYRotation = 0f;
    private float currentXRotation = 0f;

    private Animator anim;

    public AudioClip[] Suara;
    public AudioSource Audio;

    public GameObject[] Alas;
    private Vector3[] InitAlas;
    private Material originalMaterial;
    private Color originalColor;


    private GameObject[] UI;
   
    private void Start()
    {
        InitAlas = new Vector3[Alas.Length];
        UI = new GameObject[Alas.Length];

        for (int i = 0; i < Alas.Length; i++)
        {
            if (Alas[i] != null)
            {
                InitAlas[i] = Alas[i].transform.localPosition;

                Canvas canvas = Alas[i].GetComponentInChildren<Canvas>();
                if (canvas != null)
                {
                    UI[i] = canvas.gameObject;
                }
            }
        }

        Renderer rend = Alas[0].GetComponent<Renderer>();
        if (rend != null)
        {
            originalMaterial = rend.sharedMaterial;
            originalColor = rend.sharedMaterial.color;
        }
    }

    void Update()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                touchStartPos = touch.position;
            }
            else if (touch.phase == TouchPhase.Moved)
            {
                float deltaX = touch.deltaPosition.x;
                float deltaY = touch.deltaPosition.y;

                currentYRotation -= deltaX * rotationSpeed;
                currentXRotation += deltaY * rotationSpeed;
                currentXRotation = Mathf.Clamp(currentXRotation, -80f, 80f);

                transform.rotation = Quaternion.Euler(currentXRotation, currentYRotation, 0f);
            }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            touchStartPos = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0))
        {
            Vector2 mouseDelta = (Vector2)Input.mousePosition - touchStartPos;

            currentYRotation -= mouseDelta.x * rotationSpeed;
            currentXRotation += mouseDelta.y * rotationSpeed;
            currentXRotation = Mathf.Clamp(currentXRotation, -80f, 80f);

            transform.rotation = Quaternion.Euler(currentXRotation, currentYRotation, 0f);

            touchStartPos = Input.mousePosition;
        }
    }

    public void LuasBalok()
    {
        if (anim != null)
        {
            anim.SetTrigger("LuasAlas");
        }

        if (Suara != null && Suara.Length > 0)
        {
            Audio.clip = Suara[0];
            Audio.Play();
        }
    }

    public void LuasTegak()
    {
        StartCoroutine(JalankanLuasTegakBerurutan());
    }

    private IEnumerator JalankanLuasTegakBerurutan()
    {
        yield return StartCoroutine(AnimasiLuasTegak());
        yield return StartCoroutine(AnimasiLuasTegakTutup());
    }

    IEnumerator AnimasiLuasTegak()
    {
        Vector3[] targetOffsets = new Vector3[]
        {
            new Vector3(0f, 0f, 0.5f),
            new Vector3(0f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0f),
            new Vector3(0.5f, 0f, 0f),
            new Vector3(0f, 0.5f, 0f),
            new Vector3(0f, -0.5f, 0f),
        };

        Color[] warnaAlas = new Color[]
        {
            new Color(1f, 0f, 0f, 0.5f),   // Merah
            new Color(0f, 1f, 0f, 0.5f),   // Hijau
            new Color(0f, 0f, 1f, 0.5f),   // Biru
            new Color(1f, 1f, 0f, 0.5f),   // Kuning
            new Color(1f, 0f, 1f, 0.5f),   // Magenta
            new Color(0f, 1f, 1f, 0.5f),   // Cyan
        };

        for (int i = 0; i < Alas.Length && i < targetOffsets.Length && i < warnaAlas.Length; i++)
        {
            GameObject alas = Alas[i];
            if (alas == null) continue;

            // Gerakan posisi
            Vector3 target = alas.transform.localPosition + targetOffsets[i];
            LeanTween.moveLocal(alas, target, 1f);
            LeanTween.scale(UI[i], new Vector3(1f, 1f, 1f), 1f);

            Renderer[] renderers = alas.GetComponentsInChildren<Renderer>();

            foreach (Renderer rend in renderers)
            {
                if (rend == null) continue;

                rend.enabled = true;

                rend.material = new Material(rend.material);

                Color start = rend.material.color;
                Color end = warnaAlas[i];

                LeanTween.value(alas, start, end, 1f).setOnUpdate((Color val) =>
                {
                    rend.material.color = val;
                });
            }

            yield return new WaitForSeconds(1.2f);
        }


    }

    IEnumerator AnimasiLuasTegakTutup()
    {
        for (int i = 0; i < Alas.Length && i < InitAlas.Length; i++)
        {
            GameObject alas = Alas[i];
            if (alas == null) continue;

            LeanTween.moveLocal(alas, InitAlas[i], 1f);
            LeanTween.scale(UI[i], new Vector3(0f, 0f, 0f), 1f);

            Renderer[] renderers = alas.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                if (rend == null) continue;

                Color startColor = rend.material.color;
                Color endColor = originalColor;

                LeanTween.value(alas, startColor, endColor, 1f).setOnUpdate((Color val) =>
                {
                    rend.material.color = val;
                });
            }
            yield return new WaitForSeconds(1.2f);
        }
    }
}
