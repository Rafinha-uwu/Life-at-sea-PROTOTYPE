using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ReturnZone : MonoBehaviour
{
    [SerializeField] private VRCollectMinigame minigame;

    private void Reset()
    {
        minigame = GetComponent<VRCollectMinigame>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (minigame == null) return;

        CollectItemData item = other.GetComponentInParent<CollectItemData>();

        if (item != null)
            minigame.TrySubmitItem(item);
    }
}
