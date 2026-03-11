using UnityEngine;
using UnityEngine.UI;

public class MonsterSkillItemBindings : MonoBehaviour
{
    [SerializeField, InspectorName("Icon")] private Image iconImage;
    [SerializeField, InspectorName("Skill Name Text")] private IllustratedBookPanelController.UITextBinding skillNameText;
    [SerializeField, InspectorName("Description Text")] private IllustratedBookPanelController.UITextBinding descriptionText;

    public void Fill(IllustratedBookPanelController.MonsterSkillData data)
    {
        if (data == null) return;

        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.enabled = data.Icon != null;
        }

        if (skillNameText != null) skillNameText.SetText(data.SkillName);
        if (descriptionText != null) descriptionText.SetText(data.Description);
    }
}
