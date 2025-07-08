using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AksiObject : MonoBehaviour
{
    private Vector2 touchStartPos;
    public float rotationSpeed = 0.1f;
    public Camera arCamera; // Drag ARCamera Vuforia ke sini lewat Inspector
    public GameObject[] Alas;

    private float currentYRotation = 0f;
    private float currentXRotation = 0f;

    private Animator anim;
    [Header("Audio")]
    public AudioClip[] Suara;
    public AudioClip[] SuaraRumus;

    public AudioSource Audio;

    private Vector3[] InitAlas;
    private Material originalMaterial;
    private Color originalColor;

    [Header("UI Text")]
    [SerializeField] GameObject Text;
    [SerializeField] GameObject ButtomSheet;
    [SerializeField] GameObject RumusText;
    [SerializeField] TMP_Text Header;
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
        parent = Text.transform.parent;


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

                transform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
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

            transform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);

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

    void DestroyText()
    {
        Transform parent = Text.transform.parent;
        int childCount = parent.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.gameObject.active) Destroy(child.gameObject);
        }
    }

    void DestroyTextRumus()
    {
        Transform parent = RumusText.transform.parent;
        int childCount = parent.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.gameObject.active) Destroy(child.gameObject);
        }
        
    }
    public void PrefScane()
    {
        SceneManager.LoadScene("Halaman Pilihan Materi");
    }
    public void LuasTegak()
    {
        Header.text = "Plane Side Area";
        DestroyText();
        DestroyTextRumus();
        ButtomSheet.SetActive(false);
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
        Header.text = "Base Area";

        DestroyTextRumus();

        DestroyText();
        ButtomSheet.SetActive(false);

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
        Header.text = "Total Area";

        DestroyTextRumus();

        DestroyText();
        ButtomSheet.SetActive(false);

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
        Audio.clip = Suara[7];


        Audio.Play();
        yield return new WaitWhile(() => Audio.isPlaying);
        yield return StartCoroutine(AnimasiLuasTotalTutup());

    }

    private IEnumerator AnimasiLuasTotal()
    {
        string[] Penjelasan = new string[] { "Luas 4", "Luas 3", "Luas 5", "Luas 6","Luas 1" , "Luas 2" };
        int[] isi = new int[] { 3, 2, 4, 5,0,8   };


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
            Audio.clip = Suara[isi[i]];
            Audio.Play();
            SetText(Penjelasan[i]);
            yield return new WaitForSeconds(1.2f);
        }
    }
    
    private IEnumerator JalankanLuasAlas()
    {


        yield return StartCoroutine (AnimasiLuasAlas());
        Audio.clip = Suara[7];


        Audio.Play();
        yield return new WaitWhile(() => Audio.isPlaying);

        yield return StartCoroutine(AnimasiLuasAlasTutup());

    }
    Transform parent;
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
                Audio.clip = Suara[0];
                Audio.Play();
                SetText("Luas 1");

                yield return new WaitForSeconds(3f);
            }
        }
    }
    private void SetText(string name)
    {
        GameObject texs = Instantiate(Text);
        texs.GetComponent<TMP_Text>().text = name;
        texs.SetActive(true);
        texs.transform.SetParent(Text.transform.parent, false);
        LeanTween.scale(texs, Vector3.one, 1f).setEase(LeanTweenType.easeOutElastic);

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
                Audio.clip = SuaraRumus[0];
                Audio.Play();
                SetTextRumus("P X L");

                yield return new WaitForSeconds(3f);
            }
        }
    }

    private void SetTextRumus(string name)
    {
        GameObject texs = Instantiate(RumusText);
        texs.GetComponent<TMP_Text>().text = name;
        texs.SetActive(true);
        texs.transform.SetParent(RumusText.transform.parent, false);
        LeanTween.scale(texs, Vector3.one, 1f).setEase(LeanTweenType.easeOutElastic);
    }
    private IEnumerator JalankanLuasTegakBerurutan()
    {
        yield return StartCoroutine(AnimasiLuasTegak());
        Audio.clip = Suara[7];
        Audio.Play();
        yield return StartCoroutine(AnimasiLuasTegakTutup());
    }

    IEnumerator AnimasiLuasTegak()
    {
        int[] isi = new int[] { 3, 2, 4, 6 };

        string[] Penjelasan = new string[] {"Luas 4" , "Luas 3", "Luas 5" , "Luas 6"};
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
            Audio.clip = Suara[isi[i]];
            Audio.Play();
            SetText(Penjelasan[i]);
            yield return new WaitForSeconds(1.2f);
        }


    }

    IEnumerator AnimasiLuasTegakTutup()
    {
        int[] isi = new int[] { 3, 3, 5, 4 };


        string[] Penjelasan = new string[] { "Luas 4", "Luas 3", "Luas 5", "Luas 6" };
        string[] PenjelasanRumus = new string[] { "=(L x T) +","(L x T) +","(P x T) +","(P x T)" };

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
            Audio.clip = SuaraRumus[isi[i]];
            Audio.Play();
            SetTextRumus(PenjelasanRumus[i]);
            yield return new WaitForSeconds(1.2f);
        }
    }


    IEnumerator AnimasiLuasTotalTutup()
    {
        int[] isi = new int[] { 1,1,3, 3, 5, 4 };

        string[] Penjelasan = new string[] { "Luas 4", "Luas 3", "Luas 5", "Luas 6", "Luas 1", "Luas 2" };

        string[] PenjelasanRumus = new string[] {"= (P x L) +","(P x L) +" ,"(L x T) +", "(L x T) +", "(P x T) +", "(P x T)" };
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

            Audio.clip = SuaraRumus[isi[i]];
            Audio.Play();
            SetTextRumus(PenjelasanRumus[i]);
            yield return new WaitForSeconds(1.2f);
        }
    }
}
