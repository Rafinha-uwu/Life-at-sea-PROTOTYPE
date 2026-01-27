using UnityEngine;

public class Fall : MonoBehaviour
{
    [Header("Fall Settings")]
    public float minimumFallHeight = 2.0f; // meters before triggering
    public bool isClimbing;               // set this from your ladder system

    public GameObject Fade;
    public GameObject Target1;

    private float highestPoint;
    private bool wasGrounded;

    private CharacterController controller;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        highestPoint = transform.position.y;
        wasGrounded = controller.isGrounded;
    }

    void Update()
    {
        // Ignore fall logic while climbing
        if (isClimbing)
        {
            highestPoint = transform.position.y;
            wasGrounded = true;
            return;
        }

        bool isGrounded = controller.isGrounded;

        // While in air, track highest point
        if (!isGrounded)
        {
            highestPoint = Mathf.Max(highestPoint, transform.position.y);
        }

        // Just landed
        if (!wasGrounded && isGrounded)
        {
            float fallDistance = highestPoint - transform.position.y;

            if (fallDistance >= minimumFallHeight)
            {
                this.transform.position = Target1.transform.position;
                GetComponent<AudioSource>().Play();
                Fade.GetComponent<Animator>().Play("Fall");
            }

            highestPoint = transform.position.y;
        }

        wasGrounded = isGrounded;
    }

    public void On()
    {
        isClimbing = true;
    }

    public void Off()
    {
        isClimbing = false;
    }
}
