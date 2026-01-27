using System.Collections;
using UnityEngine;
using TMPro;

public class Talk : MonoBehaviour
{
    [Header("References")]
    public Transform worldTextAnchor;   // above NPC head
    public Transform cameraTextAnchor;  // below camera
    public Transform playerCamera;

    public Canvas dialogueCanvas;
    public TextMeshProUGUI dialogueText;

    [Header("Look Settings")]
    public float lookAngleThreshold = 35f;

    void Start()
    {
        dialogueText.text = "";
        StartCoroutine(IntroDialogue());
    }

    void Update()
    {
        UpdateTextPosition();
    }

    void UpdateTextPosition()
    {
        Vector3 directionToNPC = (transform.position - playerCamera.position).normalized;
        float angle = Vector3.Angle(playerCamera.forward, directionToNPC);

        if (angle < lookAngleThreshold)
        {
            // Looking at NPC → text above him
            dialogueCanvas.transform.position = worldTextAnchor.position;
            dialogueCanvas.transform.rotation =
                Quaternion.LookRotation(dialogueCanvas.transform.position - playerCamera.position);
        }
        else
        {
            // Looking away → text follows camera
            dialogueCanvas.transform.position = cameraTextAnchor.position;
            dialogueCanvas.transform.rotation = cameraTextAnchor.rotation;
        }
    }

    IEnumerator IntroDialogue()
    {
        yield return SayLine("Ah… Finalmente apareces marujo", 4f);

        yield return SayLine(
            "Estava a ver que te tinha esquecido do teu trabalho",
            4f
        );

        yield return SayLine(
            "Vamos lá.. escolhe uma tarefa na mesa para começar...",
            4f
        );

    }

    IEnumerator SayLine(string text, float duration)
    {
        dialogueText.text = text;

        yield return new WaitForSeconds(duration);

        yield return new WaitForSeconds(0.2f);
    }
}
