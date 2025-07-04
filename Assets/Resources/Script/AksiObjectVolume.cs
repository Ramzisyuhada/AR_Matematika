using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AksiObjectVolume : MonoBehaviour
{
    private Vector2 touchStartPos;
    public float rotationSpeed = 0.1f;
    public Camera arCamera; // Drag ARCamera Vuforia ke sini lewat Inspector

    private float currentYRotation = 0f;
    private float currentXRotation = 0f;


    public AudioClip[] Suara;
    public AudioSource Audio;

    public GameObject[] Kubus;




    private GameObject[] UI;
    private bool isTouchingThisObject = false;

    private void Start()
    {
        List<GameObject> anakKubus = new List<GameObject>();

        foreach (Transform child in transform)
        {
            Debug.Log("Child: " + child.name);
            child.transform.localScale = Vector3.zero;
            anakKubus.Add(child.gameObject); 
        }

        Kubus = anakKubus.ToArray();
    }

    public void Volume()
    {
        StartCoroutine(AnimasiVolume());
    }

    public IEnumerator AnimasiVolume()
    {
        for (int i = 0; i < Kubus.Length; i++)
        {
            LeanTween.scale(Kubus[i],new Vector3(0.5f,0.5f,0.5f),1f).setEase(LeanTweenType.easeOutElastic);
            yield return new WaitForSeconds(0.2f);
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
    
}
