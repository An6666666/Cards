using UnityEngine;
using UnityEngine.UI;

namespace DeckUI
{
    public class CardIconItem : MonoBehaviour
    {
        [SerializeField] private Image icon;

        void Awake()
        {
            if (icon == null) icon = GetComponent<Image>();
        }

        public void SetSprite(Sprite sprite)
        {
            if (icon == null) return;
            if (sprite != null)
            {
                icon.enabled = true;
                icon.sprite = sprite;
                icon.color = Color.white;
            }
            else
            {
                icon.enabled = true;
                icon.sprite = null;
                icon.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            }
        }
    }
}
