using UnityEngine;

public class Rope : MonoBehaviour
{
    [Header("References")]
    public Transform rope;
    public Transform flag;

    [Header("Movement")]
    public float maxPullDistance = 2.0f;
    public float maxFlagHeight = 2.0f;

    public GameObject Game;

    private float ropeStartY;
    private float flagStartY;

    private float maxProgress; // 0 → 1, never decreases
    private bool reachedTop;

    Vector3 ropeStartPosition;


    void Start()
    {
        ropeStartY = rope.position.y;
        flagStartY = flag.position.y;

        maxProgress = 0f;

        // Start flag collapsed on X
        flag.localScale = new Vector3(0f, flag.localScale.y, flag.localScale.z);

        ropeStartPosition = rope.position;
    }

    void Update()
    {
        rope.position = new Vector3(ropeStartPosition.x,rope.position.y,ropeStartPosition.z);

        // How far the rope is pulled down
        float pullAmount = ropeStartY - rope.position.y;
        pullAmount = Mathf.Clamp(pullAmount, 0f, maxPullDistance);

        // Current pull progress (0 → 1)
        float currentProgress = Mathf.Clamp01(pullAmount / maxPullDistance);

        // 🔒 ONE-WAY: only allow progress to increase
        maxProgress = Mathf.Max(maxProgress, currentProgress);

        // Smooth it
        float t = Mathf.SmoothStep(0f, 1f, maxProgress);

        // Move flag up
        flag.position = new Vector3(
            flag.position.x,
            flagStartY + (t * maxFlagHeight),
            flag.position.z
        );

        // Scale flag on X (hard capped at 1)
        flag.localScale = new Vector3(
            Mathf.Min(t, 1f),
            flag.localScale.y,
            flag.localScale.z
        );

        // Reached the top (fires once)
        if (!reachedTop && maxProgress >= 1f)
        {
            GetComponent<AudioSource>().Play();
            Game.GetComponent<GAME>().Invoke("Hub", 5);

            reachedTop = true;
        }
    }
}
