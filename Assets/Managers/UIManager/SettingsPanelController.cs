using UnityEngine;

public class SettingsPanelController : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;
    private UIFxController _fx;

    public void Initialize(UIFxController fx)
    {
        _fx = fx;
        if (settingsPanel) settingsPanel.SetActive(false);
    }

    public void Open()
    {
        if (settingsPanel) _fx?.ShowPanel(settingsPanel);
    }

    public void Close()
    {
        if (settingsPanel) _fx?.HidePanel(settingsPanel);
    }

    public void HideOthers(params GameObject[] others)
    {
        if (others == null) return;
        foreach (var go in others)
        {
            if (go != null) _fx?.HidePanel(go);
        }
    }
}