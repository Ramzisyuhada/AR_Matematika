using UnityEngine;

public class AksiObject : MonoBehaviour
{
    private Vector2 touchStartPos;
    public float rotationSpeed = 0.1f;

    private float currentYRotation = 0f;
    private float currentXRotation = 0f;
    private Animator anim;

    private void Start()
    {
        anim = GetComponent<Animator>();
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

                // Batasi rotasi X agar tidak kebalik
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

            // Batasi rotasi X agar tidak kebalik
            currentXRotation = Mathf.Clamp(currentXRotation, -80f, 80f);

            transform.rotation = Quaternion.Euler(currentXRotation, currentYRotation, 0f);

            touchStartPos = Input.mousePosition;
        }
    }

    public void LuasBalok()
    {
        anim.SetTrigger("LuasAlas");
    }
}
