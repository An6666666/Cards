using UnityEngine;
using UnityEngine.UI;

public class CardScopeDisplay : MonoBehaviour
{
    [Header("Scope UI")]
    [SerializeField] private GameObject scopeRoot;
    [SerializeField] private Image scopeImage;

    private CardUI cardUI;
    private bool hasScope;
    private bool isVisible;

    public void Initialize(CardUI ui)
    {
        cardUI = ui;
        ResolveReferences();
        SetCardData(cardUI != null ? cardUI.cardData : null);
        Hide();
    }

    public void SetCardData(CardBase data)
    {
        ResolveReferences();

        Sprite sprite = data is AttackCardBase attackData ? attackData.scopeImage : null;
        hasScope = sprite != null;

        if (scopeImage != null)
        {
            scopeImage.sprite = sprite;
            scopeImage.enabled = hasScope;
        }

        if (!hasScope)
        {
            Hide();
            return;
        }

        if (scopeRoot != null)
        {
            scopeRoot.SetActive(isVisible);
        }
    }

    public void Show()
    {
        ResolveReferences();
        isVisible = true;

        if (scopeRoot != null)
        {
            scopeRoot.SetActive(hasScope);
        }
    }

    public void Hide()
    {
        ResolveReferences();
        isVisible = false;

        if (scopeRoot != null)
        {
            scopeRoot.SetActive(false);
        }
    }

    private void ResolveReferences()
    {
        if (scopeRoot == null)
        {
            Transform scopeTransform = transform.Find("CardVisual/Scope");
            if (scopeTransform != null)
            {
                scopeRoot = scopeTransform.gameObject;
            }
        }

        if (scopeImage == null)
        {
            Transform scopeImageTransform = transform.Find("CardVisual/Scope/ScopeIM");
            if (scopeImageTransform != null)
            {
                scopeImage = scopeImageTransform.GetComponent<Image>();
            }
        }
    }
}
