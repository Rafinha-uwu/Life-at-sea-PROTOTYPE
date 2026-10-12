using UnityEngine;

public class CannonFireTrigger : MonoBehaviour
{
    public CannonFire cannonFire;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Lighter") || other.transform.root.CompareTag("Lighter"))
            cannonFire.Fire();
    }
}
