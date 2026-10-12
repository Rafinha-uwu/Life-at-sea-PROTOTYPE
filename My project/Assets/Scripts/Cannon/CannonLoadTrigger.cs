using UnityEngine;

public class CannonLoadTrigger : MonoBehaviour
{
    public CannonLoader cannonLoader;

    private void OnTriggerEnter(Collider other)
    {
        if (cannonLoader != null)
            cannonLoader.TryLoad(other.gameObject);
    }
}
