using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Coklat : MonoBehaviour
{
    public Camera arCamera; // Kamera utama untuk raycast
    public float rotationSpeed = 0.1f; // Kecepatan rotasi

    private bool isTouchingThisObject = false;
    private Vector2 touchStartPos;
    private float currentXRotation = 0f;
    private float currentYRotation = 0f;

    [SerializeField] Transform PosisiMuncul;
    [SerializeField] GameObject Particle;
    private void Start()
    {
       // StartCoroutine(Animasi());
    }

    private IEnumerator Animasi()
    {
        // Mulai animasi scale
        int scaleTweenId = LeanTween.scale(gameObject, new Vector3(0.0507999994f, 0.0126999998f, 0.203199998f), 0.9f)
                                    .setEase(LeanTweenType.easeOutElastic)
                                    .id;

        // Mulai animasi move (dijalankan bersamaan)
        int moveTweenId = LeanTween.move(gameObject,
                                new Vector3(gameObject.transform.position.x, -15.73942f, gameObject.transform.position.z),
                                0.9f)
                            .setEase(LeanTweenType.easeOutElastic)
                            .id;
        Destroy(Instantiate(Particle, transform.position, Quaternion.identity), 2f);

        // Tunggu hingga kedua animasi selesai
        yield return new WaitUntil(() =>
            !LeanTween.isTweening(scaleTweenId) && !LeanTween.isTweening(moveTweenId)
        );
        Debug.Log("Animasi scale dan move selesai");
    }


    private void Awake()
    {

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName.Equals("Latihan"))
        {
            Debug.Log(sceneName);
            Screen.orientation = ScreenOrientation.LandscapeRight;

        }
    }
 
    void Update()
    {
        // Mengecek orientasi layar
        if (Screen.orientation == ScreenOrientation.LandscapeLeft || Screen.orientation == ScreenOrientation.LandscapeRight)
        {
            Debug.Log("Sedang Landscape");
        }

        //HandleTouchInput();
        //HandleMouseInput();
    }

    void HandleTouchInput()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            Ray ray = arCamera.ScreenPointToRay(touch.position);
            RaycastHit hit;

            if (touch.phase == TouchPhase.Began)
            {
                if (Physics.Raycast(ray, out hit) && hit.transform == transform)
                {
                    isTouchingThisObject = true;
                    touchStartPos = touch.position;
                }
            }
            else if (touch.phase == TouchPhase.Moved && isTouchingThisObject)
            {
                float deltaX = touch.deltaPosition.x;
                currentYRotation -= deltaX * rotationSpeed;

                transform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isTouchingThisObject = false;
            }
        }
    }

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = arCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit) && hit.transform == transform)
            {
                isTouchingThisObject = true;
                touchStartPos = Input.mousePosition;
            }
        }
        else if (Input.GetMouseButton(0) && isTouchingThisObject)
        {
            Vector2 mouseDelta = (Vector2)Input.mousePosition - touchStartPos;
            currentYRotation -= mouseDelta.x * rotationSpeed;

            transform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
            touchStartPos = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isTouchingThisObject = false;
        }
    }

}
