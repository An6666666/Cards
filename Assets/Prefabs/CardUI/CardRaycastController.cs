using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class CardRaycastController : MonoBehaviour
{
    [Header("互動權限")]
    [SerializeField] private bool interactable = true;

    private CardUI cardUI;
    private CardAnimationController animationController;
    private CanvasGroup canvasGroup;
    private BattleManager battleManager;

    public bool Interactable => interactable;
    public bool BlocksRaycasts => canvasGroup != null && canvasGroup.blocksRaycasts;

    public void Initialize(CardUI ui, CardAnimationController animation)
    {
        cardUI = ui;
        animationController = animation;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = true;
    }

    public void HandleCardEnabled()
    {
        EnsureBattleManager();
        bool desired = battleManager == null ? interactable : !battleManager.IsCardInteractionLocked;
        SetInteractable(desired);
    }

    public void SetInteractable(bool value)
    {
        EnsureBattleManager();

        if (battleManager != null && battleManager.IsCardInteractionLocked)
            value = false;

        interactable = value;
        ApplyToCanvasGroup(value);
    }

    public void SetBlocksRaycasts(bool value)
    {
        if (canvasGroup == null) return;
        canvasGroup.blocksRaycasts = value;
    }

    public bool IsCardInteractionLocked()
    {
        EnsureBattleManager();
        return battleManager != null && battleManager.IsCardInteractionLocked;
    }

    private void ApplyToCanvasGroup(bool value)
    {
        if (canvasGroup == null) return;

        canvasGroup.interactable = value;
    }
    private void EnsureBattleManager()
    {
        if (battleManager == null)
            battleManager = FindObjectOfType<BattleManager>();
    }
}