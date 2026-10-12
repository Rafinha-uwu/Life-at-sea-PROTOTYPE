using UnityEngine;

// Shows one menu page at a time. Page 0 is the one the menu opens on.
public class MenuPages : MonoBehaviour
{
    [Tooltip("Page 0 is the main menu. Page 1 is usually Options.")]
    public GameObject[] pages;

    public bool AnyPageOpen
    {
        get
        {
            if (pages == null) return false;

            foreach (GameObject page in pages)
            {
                if (page != null && page.activeSelf) return true;
            }

            return false;
        }
    }

    public void ShowPage(int index)
    {
        if (pages == null) return;

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
                pages[i].SetActive(i == index);
        }
    }

    // Hooked to buttons
    public void ShowMain() => ShowPage(0);
    public void ShowSettings() => ShowPage(1);

    public void CloseAll() => ShowPage(-1);
}
