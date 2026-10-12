using UnityEngine;

public class CollectItemData : MonoBehaviour
{
    [Header("Item Info")]
    [SerializeField] private string itemID;
    [SerializeField] private string itemNameText;

    [TextArea]
    [SerializeField] private string descriptionText;

    [Header("English")]
    [SerializeField] private string itemNameEnglish;

    [TextArea]
    [SerializeField] private string descriptionEnglish;

    public string ItemID => itemID;

    // Falls back to Portuguese if the English is left blank
    public string ItemNameText => GameSettings.Language == GameLanguage.English &&
                                  !string.IsNullOrWhiteSpace(itemNameEnglish)
        ? itemNameEnglish
        : itemNameText;

    public string DescriptionText => GameSettings.Language == GameLanguage.English &&
                                     !string.IsNullOrWhiteSpace(descriptionEnglish)
        ? descriptionEnglish
        : descriptionText;
}
