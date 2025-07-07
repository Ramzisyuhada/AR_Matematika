using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AksiObject : MonoBehaviour
{
    private Vector2 touchStartPos;
    public float rotationSpeed = 0.1f;
    public Camera arCamera; // Drag ARCamera Vuforia ke sini lewat Inspector

    private float currentYRotation = 0f;
    private float currentXRotation = 0f;

    private Animator anim;

    public AudioClip[] Suara;
    public AudioSource Audio;

    public GameObject[] Alas;
    private Vector3[] InitAlas;
    private Material originalMaterial;
    private Color originalColor;

    [Header("UI Text")]
    [SerializeField] GameObject[] Text; 

    private GameObject[] UI;
    private bool isTouchingThisObject = false;
    

    private void Awake()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        arCamera = Camera.main;
        if (sceneName.Equals("ARBalok"))
        {
            Debug.Log(sceneName);
            Screen.orientation = ScreenOrientation.LandscapeRight;

        }
    }
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
        if (Screen.orientation == ScreenOrientation.LandscapeLeft || Screen.orientation == ScreenOrientation.LandscapeRight)
        {
            Debug.Log("Sedang Landscape");
        }

        // Untuk mobile (touch)
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            Ray ray = arCamera.ScreenPointToRay(touch.position);
            RaycastHit hit;

            if (touch.phase == TouchPhase.Began)
            {
                if (Physics.Raycast(ray, out hit))
                {
                    if (hit.transform == transform)
                    {
                        isTouchingThisObject = true;
                        touchStartPos = touch.position;
                    }
                }
            }
            else if (touch.phase == TouchPhase.Moved && isTouchingThisObject)
            {
                float deltaX = touch.deltaPosition.x;
                float deltaY = touch.deltaPosition.y;

                currentYRotation -= deltaX * rotationSpeed;
                currentXRotation += deltaY * rotationSpeed;
                currentXRotation = Mathf.Clamp(currentXRotation, -80f, 80f);

                transform.rotation = Quaternion.Euler(currentXRotation, currentYRotation, 0f);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isTouchingThisObject = false;
            }
        }

        // Untuk PC (mouse)
        if (Input.GetMouseButtonDown(0))
        {

            Ray ray = arCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {

                if (hit.transform == transform)
                {
                    isTouchingThisObject = true;
                    touchStartPos = Input.mousePosition;
                }
            }

        }
        else if (Input.GetMouseButton(0) && isTouchingThisObject)
        {
            Vector2 mouseDelta = (Vector2)Input.mousePosition - touchStartPos;

            currentYRotation -= mouseDelta.x * rotationSpeed;
            currentXRotation += mouseDelta.y * rotationSpeed;
            currentXRotation = Mathf.Clamp(currentXRotation, -80f, 80f);

            transform.rotation = Quaternion.Euler(currentXRotation, currentYRotation, 0f);

            touchStartPos = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isTouchingThisObject = false;
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
        LeanTween.scale(gameObject,new Vector3(0.8f,0.8f,0.8f),1f).setEase(LeanTweenType.easeOutElastic);
        GameObject Volume = GameObject.Find("Volume");
        LeanTween.scale(Volume, new Vector3(0f, 0f, 0f), 1f).setEase(LeanTweenType.easeOutElastic);
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        StartCoroutine(JalankanLuasTegakBerurutan());
    }

    public void LuasAlas()
    {
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        LeanTween.scale(gameObject, new Vector3(0.8f, 0.8f, 0.8f), 1f).setEase(LeanTweenType.easeOutElastic);
        GameObject Volume = GameObject.Find("Volume");
        LeanTween.scale(Volume, new Vector3(0f, 0f, 0f), 1f).setEase(LeanTweenType.easeOutElastic);

        StartCoroutine(JalankanLuasAlas());
    }

    public void LuasTotal()
    {
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        LeanTween.scale(gameObject, new Vector3(0.8f, 0.8f, 0.8f), 1f).setEase(LeanTweenType.easeOutElastic);
        GameObject Volume = GameObject.Find("Volume");
        LeanTween.scale(Volume, new Vector3(0f, 0f, 0f), 1f).setEase(LeanTweenType.easeOutElastic);

        StartCoroutine(JalankanLuasTotal());
    }


    private IEnumerator JalankanLuasTotal()
    {
        yield return StartCoroutine(AnimasiLuasTotal());
        yield return StartCoroutine(AnimasiLuasTotalTutup());

    }

    private IEnumerator AnimasiLuasTotal()
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


            Vector3 target = alas.transform.localPosition + targetOffsets[i];
            LeanTween.moveLocal(alas, target, 1f);
            LeanTween.scale(UI[i], new Vector3(1f, 1f, 1f), 1f);

            Renderer[] renderers = alas.GetComponentsInChildren<Renderer>();
            // LeanTween.scale(Text[i], new Vector3(1f, 1f, 1f), 1f);
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
    
    private IEnumerator JalankanLuasAlas()
    {
        yield return StartCoroutine (AnimasiLuasAlas());
        yield return StartCoroutine(AnimasiLuasAlasTutup());

    }
    private IEnumerator  AnimasiLuasAlas()
    {
        Vector3 offset = new Vector3(0f, 0.5f, 0f);
        Color warna = new Color(1f, 0f, 1f, 0.5f);

        for (int i = 0; i < Alas.Length; i++)
        {
            GameObject alas = Alas[i];
            if (alas != null && alas.name == "Alas_Atas")
            {
                Vector3 target = alas.transform.localPosition + offset;
                LeanTween.moveLocal(alas, target, 1f);
                LeanTween.scale(UI[i], Vector3.one, 1f);

                foreach (Renderer rend in alas.GetComponentsInChildren<Renderer>())
                {
                    rend.enabled = true;
                    rend.material = new Material(rend.material);
                    LeanTween.value(alas, rend.material.color, warna, 1f).setOnUpdate((Color val) =>
                    {
                        rend.material.color = val;
                    });
                }
                yield return new WaitForSeconds(3f);
            }
        }
    }

    private IEnumerator AnimasiLuasAlasTutup()
    {
       

        for (int i = 0; i < Alas.Length; i++)
        {
            GameObject alas = Alas[i];
            if (alas != null && alas.name == "Alas_Atas")
            {
                LeanTween.moveLocal(alas, InitAlas[i], 1f);
                LeanTween.scale(UI[i], Vector3.zero, 1f);

                Renderer renderers = alas.GetComponentInChildren<Renderer>();
              
                    if (renderers == null) continue;

                    Color startColor = renderers.material.color;
                    Color endColor = originalColor;

                    LeanTween.value(alas, startColor, endColor, 1f).setOnUpdate((Color val) =>
                    {
                        renderers.material.color = val;
                    });
                
                yield return new WaitForSeconds(3f);
            }
        }
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

        for (int i = 0; i < Alas.Length - 2 && i < targetOffsets.Length - 2 && i < warnaAlas.Length - 2; i++)
        {
            GameObject alas = Alas[i];
            if (alas == null) continue;

            
            Vector3 target = alas.transform.localPosition + targetOffsets[i];
            LeanTween.moveLocal(alas, target, 1f);
            LeanTween.scale(UI[i], new Vector3(1f, 1f, 1f), 1f);

            Renderer[] renderers = alas.GetComponentsInChildren<Renderer>();
           // LeanTween.scale(Text[i], new Vector3(1f, 1f, 1f), 1f);
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
        for (int i = 0; i < Alas.Length - 2 && i < InitAlas.Length - 2; i++)
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


    IEnumerator AnimasiLuasTotalTutup()
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
