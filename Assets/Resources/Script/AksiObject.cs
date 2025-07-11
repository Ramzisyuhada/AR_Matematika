using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    public AudioClip[] SuaraHeader;
    public AudioSource Audio;

    private Vector3[] InitAlas;
    private Material originalMaterial;
    private Color originalColor;

    [Header("UI Text")]
    [SerializeField] GameObject Text;
    [SerializeField] GameObject ButtomSheet;
    [SerializeField] GameObject RumusText;
    [SerializeField] GameObject PersamaanText;
    [SerializeField] GameObject HeaderText;

    [SerializeField] TMP_Text Header;
    private GameObject[] UI;
    private bool isTouchingThisObject = false;

    public static bool IsAnimasiPlay;

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

    private void SetTextHeader(string name)
    {

        HeaderText.SetActive(false);

        HeaderText.SetActive(true);

        HeaderText.GetComponent<TMP_Text>().text = name;
        LeanTween.scale(HeaderText.transform.parent.gameObject, Vector3.zero, 1f).setEase(LeanTweenType.easeOutElastic);
        LeanTween.scale(HeaderText.transform.parent.gameObject, new Vector3(1f, 1f, 1f), 1f).setEase(LeanTweenType.easeOutElastic);
    }

    public void SetActiveFalseVolume()
    {
        GameObject Volume = GameObject.Find("Volume");
        for (int i = 0; i < Volume.transform.childCount; i++)
        {
            Volume.transform.GetChild(i).localScale = Vector3.zero;
        }
    }
    public void LuasTegak()
    {
        if (IsAnimasiPlay) return;
        DestroyTextPersamaan();
        SetTextHeader("Plane Side Area");

        IsAnimasiPlay = true;
        Header.text = "Plane Side Area";
        DestroyText();
        DestroyTextRumus();
        ButtomSheet.SetActive(false);
        LeanTween.scale(gameObject,new Vector3(0.8f,0.8f,0.8f),1f).setEase(LeanTweenType.easeOutElastic);
        GameObject Volume = GameObject.Find("Volume");
        LeanTween.scale(Volume, new Vector3(0f, 0f, 0f), 1f).setEase(LeanTweenType.easeOutElastic);
        SetActiveFalseVolume();
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        StartCoroutine(JalankanLuasTegakBerurutan());
    }
    void DestroyTextPersamaan()
    {
        PersamaanCounter = 0;
        Transform parent = PersamaanText.transform.parent;
        int childCount = parent.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.gameObject.active) Destroy(child.gameObject);
        }

    }
    public void LuasAlas()
    {
        if (IsAnimasiPlay) return;
        DestroyTextPersamaan();
        IsAnimasiPlay = true;
        Header.text = "Base Area";
        SetTextHeader("Base Area");

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
        SetActiveFalseVolume();


        StartCoroutine(JalankanLuasAlas());
    }

    public void LuasTotal()
    {
        if (IsAnimasiPlay) return;
        DestroyTextPersamaan();

        IsAnimasiPlay = true;
        SetTextHeader("Total Area");

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
        SetActiveFalseVolume();

        StartCoroutine(JalankanLuasTotal());
    }


    private IEnumerator JalankanLuasTotal()
    {
                rumusCounter = 0;


        Audio.clip = SuaraHeader[2];
        Audio.Play();
        yield return new WaitWhile(() => Audio.isPlaying);
        yield return StartCoroutine(AnimasiLuasTotal());
        Audio.clip = Suara[7];


        Audio.Play();
        yield return new WaitWhile(() => Audio.isPlaying);

        yield return StartCoroutine(AnimasiLuasTotalTutup());
        Audio.clip = SuaraRumus[6];
        Audio.Play();

        yield return new WaitWhile(() => Audio.isPlaying);

        yield return StartCoroutine(AnimasiPersamaanTotal());

    }

    private IEnumerator AnimasiLuasTotal()
    {
        string[] Penjelasan = new string[] { "Luas 1", "Luas 2", "Luas 3", "Luas 4","Luas 5" , "Luas 6" };
        int[] isi = new int[] { 0, 1, 2, 3,4,6   };

        int[] UrutanIndexAnimasi = new int[] {4,5,1,0,2,3};
        Vector3[] targetOffsets = new Vector3[]
       {
            new Vector3(0f, 0.5f, 0f),
            new Vector3(0f,  -0.5f, 0f),
            new Vector3(0f, 0f, -0.5f),
            new Vector3(0f, 0f, 0.5f),
            new Vector3(-0.5f, 0f, 0f),
            new Vector3(0.5f, 0f, 0f),
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

            GameObject alas = Alas[UrutanIndexAnimasi[i]];
            if (alas == null) continue;

            InitAlas[i] = alas.transform.localPosition;
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
    [SerializeField] Transform PersamaanContainer;
    int PersamaanCounter = 0;
    private void SetTextPersamaan(string name)
    {
        if (PersamaanText == null || PersamaanContainer == null)
        {
            Debug.LogError("PersamaanText atau PersamaanContainer belum di-assign di Inspector!");
            return;
        }

        // Buat row baru setiap 3 teks
        if (PersamaanCounter % 3 == 0 || currentRowGroup == null)
        {
            currentRowGroup = new GameObject("HorizontalGroup_" + (PersamaanCounter / 3));
            currentRowGroup.transform.SetParent(PersamaanContainer, false);

            var layout = currentRowGroup.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = currentRowGroup.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var rect = currentRowGroup.AddComponent<RectTransform>();
            //rect.localScale = Vector3.one;
        }

        GameObject teks = Instantiate(PersamaanText);
        teks.transform.SetParent(currentRowGroup.transform, false);
        teks.SetActive(true);
        teks.GetComponent<TMP_Text>().text = name;

        // Animasi teks persamaan masuk
        teks.transform.localScale = Vector3.zero;
        LeanTween.scale(teks, Vector3.one, 0.8f).setEase(LeanTweenType.easeOutElastic);

        PersamaanCounter++;
    }

    private IEnumerator JalankanLuasAlas()
    {
        rumusCounter = 0;

        Audio.clip = SuaraHeader[0];
        Audio.Play();
        yield return new WaitWhile(() => Audio.isPlaying);

        yield return StartCoroutine (AnimasiLuasAlas());
        Audio.clip = Suara[7];


        Audio.Play();
        yield return new WaitWhile(() => Audio.isPlaying);


        yield return StartCoroutine(AnimasiLuasAlasTutup());
        Audio.clip = SuaraRumus[6];
        Audio.Play();
        yield return new WaitWhile(() => Audio.isPlaying);

        yield return StartCoroutine(AnimasiPersamaanAlas());


    }
    Transform parent;
    private IEnumerator  AnimasiLuasAlas()
    {
        Vector3 offset = new Vector3(0f, 0.5f, 0f);
        Color warna = new Color(1f, 0f, 1f, 0.5f);

        for (int i = 0; i < Alas.Length; i++)
        {
            GameObject alas = Alas[i];
            if (alas != null && alas.name == "1")
            {
                InitAlas[i] = alas.transform.localPosition;
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
            if (alas != null && alas.name == "1")
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
        InitAlas = new Vector3[Alas.Length];
        IsAnimasiPlay = false;
    }

    private GameObject currentRowGroup;
    private int rumusCounter = 0;

    [SerializeField] private Transform RumusContainer;

    private void SetTextRumus(string name)
    {
        if (RumusText == null)
        {
            Debug.LogError("RumusText belum di-assign di Inspector!");
            return;
        }

        if (rumusCounter % 3 == 0)
        {
            currentRowGroup = new GameObject("HorizontalGroup_" + (rumusCounter / 3));
            currentRowGroup.transform.SetParent(RumusContainer, false);

            var layout = currentRowGroup.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = currentRowGroup.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform rect = currentRowGroup.AddComponent<RectTransform>();
           // rect.localScale = Vector3.one;
        }

        GameObject teks = Instantiate(RumusText);
        teks.GetComponent<TMP_Text>().text = name;
        teks.transform.SetParent(currentRowGroup.transform, false);
        teks.SetActive(true);

        LeanTween.scale(teks, Vector3.one, 1f).setEase(LeanTweenType.easeOutElastic);

        rumusCounter++;
    }
    private IEnumerator JalankanLuasTegakBerurutan()
    {
        rumusCounter = 0;


        Audio.clip = SuaraHeader[1];
        Audio.Play();
        yield return new WaitWhile(() => Audio.isPlaying);

        yield return StartCoroutine(AnimasiLuasTegak());
        Audio.clip = Suara[7];
        Audio.Play();
        yield return new WaitWhile(() => Audio.isPlaying);

        yield return StartCoroutine(AnimasiLuasTegakTutup());
        Audio.clip = SuaraRumus[6];
        Audio.Play();
        yield return new WaitWhile(() => Audio.isPlaying);

        yield return StartCoroutine(AnimasiPersamaanTegak());
    }
    IEnumerator AnimasiPersamaanAlas()
    {

        string[] PenjelasanPersamaan = new string[] { "2 x", "[ (P x L) ]" };
        int[] IndexSuara = new int[] { 7, 0 };
        for (int i = 0; i < PenjelasanPersamaan.Length; i++)
        {

            Audio.clip = SuaraRumus[IndexSuara[i]];
            Audio.Play();
            SetTextPersamaan(PenjelasanPersamaan[i]);
            yield return new WaitWhile(() => Audio.isPlaying);

        }


    }
    IEnumerator AnimasiPersamaanTotal()
    {
        string[] Penjelasan = new string[] { "Luas 3", "Luas 4", "Luas 5", "Luas 6" };
        string[] PenjelasanRumus = new string[] { "= (P x T) +", "(P x T) +", "(P x L)+", "(P x L) +", "(L x T) +", "(L x T)" };

        string[] PenjelasanPersamaan = new string[] { "2 x", "[ (P x T) +", "(P x L) +", "(L x T) ]" };
        int[] IndexSuara = new int[] { 7, 5, 1,2 };
        for (int i = 0; i < PenjelasanPersamaan.Length; i++)
        {

            Audio.clip = SuaraRumus[IndexSuara[i]];
            Audio.Play();
            SetTextPersamaan(PenjelasanPersamaan[i]);
            yield return new WaitWhile(() => Audio.isPlaying);

        }


    }
    IEnumerator AnimasiPersamaanTegak()
    {
        string[] Penjelasan = new string[] { "Luas 3", "Luas 4", "Luas 5", "Luas 6" };
        string[] PenjelasanPersamaan = new string[] { "2 x","[ (L x T) +", "(P x T) ] " };
        int[] IndexSuara = new int[] { 7, 3, 4 };
        for (int i = 0; i < PenjelasanPersamaan.Length ; i++)
        {

            Audio.clip = SuaraRumus[IndexSuara[i]];
            Audio.Play();   
            SetTextPersamaan(PenjelasanPersamaan[i]);
            yield return new WaitWhile(() => Audio.isPlaying);

        }


    }

    IEnumerator AnimasiLuasTegak()
    {
        int[] isi = new int[] { 2, 3, 4, 6 };
        int[] UrutanAnimasi = new int[] { 1, 0, 2, 3 };

        string[] Penjelasan = new string[] { "Luas 3", "Luas 4", "Luas 5", "Luas 6" };

        Vector3[] targetOffsets = new Vector3[]
        {
            new Vector3(0f, 0f, -0.5f),
            new Vector3(0f, 0f, 0.5f),
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
            GameObject alas = Alas[UrutanAnimasi[i]];
            if (alas == null) continue;

            InitAlas[i] = alas.transform.localPosition;
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

        int[] UrutanAnimasi = new int[] { 1, 0, 2, 3 };
        string[] Penjelasan = new string[] { "Luas 3", "Luas 4", "Luas 5", "Luas 6" };
        string[] PenjelasanRumus = new string[] { "=(L x T) +"," (L x T) +","(P x T) +"," (P x T)" };

        for (int i = 0; i < Alas.Length - 2 && i < InitAlas.Length - 2; i++)
        {
            GameObject alas = Alas[UrutanAnimasi[i]];
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
            yield return new WaitForSeconds(1.9f);
        }
        InitAlas = new Vector3[Alas.Length];
        IsAnimasiPlay = false;
    }

    int j = 0;

    IEnumerator AnimasiLuasTotalTutup()
    {
        int[] isi = new int[] { 5,5,1, 1, 3, 2 };
        int[] UrutanIndexAnimasi = new int[] { 4, 5, 1, 0, 2, 3 };

        //string[] Penjelasan = new string[] { "Luas 4", "Luas 3", "Luas 5", "Luas 6", "Luas 1", "Luas 2" };
        string[] Penjelasan = new string[] { "Luas 1", "Luas 2", "Luas 3", "Luas 4", "Luas 5", "Luas 6" };

        string[] PenjelasanRumus = new string[] {"= (P x T) +","(P x T) +" ,"(P x L)+", "(P x L) +", "(L x T) +", "(L x T)" };
        //string[] PenjelasanRumus = new string[] { "= (P x T) +", "(P x T) +" , "(P x L) +" , "(P x L) +" + "(L x T) +", "(L x T)" };
        for (int i = 0; i < Alas.Length && i < InitAlas.Length; i++)
        {
            GameObject alas = Alas[UrutanIndexAnimasi[i]];
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

            yield return new WaitForSeconds(1.9f);
        }
        InitAlas = new Vector3[Alas.Length];
        IsAnimasiPlay = false;  

    }

}
