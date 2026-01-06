using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Presents guide NPC dialogue using a shared DialogueBubbleUI and GuideDialogueDatabase.
/// Responsible for visual show/hide animation and invoking bubble playback.
/// </summary>
public class GuideNPCPresenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DialogueBubbleUI dialogueUI;
    [SerializeField] private GuideDialogueDatabase dialogueDatabase;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation")]
    [SerializeField] private bool animateVisibility = true;
    [SerializeField, Min(0f)] private float fadeDuration = 0.2f;
    [SerializeField] private Ease fadeEase = Ease.OutCubic;

    private Tween visibilityTween;

    public void AssignDialogueUI(DialogueBubbleUI ui)
    {
        dialogueUI = ui;
    }

    public void AssignDatabase(GuideDialogueDatabase database)
    {
        dialogueDatabase = database;
    }

    public bool Talk(string key)
    {
        IReadOnlyList<string> lines = dialogueDatabase != null ? dialogueDatabase.GetLines(key) : null;
        if (lines == null || lines.Count == 0)
            return false;

        TalkLines(lines);
        return true;
    }

    public void TalkLines(IEnumerable<string> lines)
    {
        if (dialogueUI == null)
            return;

        Show();
        dialogueUI.PlayLines(lines);
    }

    public void Show()
    {
        if (canvasGroup == null || !animateVisibility)
            return;

        KillVisibilityTween();
        canvasGroup.gameObject.SetActive(true);
        visibilityTween = canvasGroup.DOFade(1f, fadeDuration)
            .SetEase(fadeEase)
            .OnStart(() => canvasGroup.blocksRaycasts = true)
            .OnComplete(() => canvasGroup.alpha = 1f);
    }

    public void Hide()
    {
        if (canvasGroup == null || !animateVisibility)
            return;

        KillVisibilityTween();
        visibilityTween = canvasGroup.DOFade(0f, fadeDuration)
            .SetEase(fadeEase)
            .OnStart(() => canvasGroup.blocksRaycasts = false)
            .OnComplete(() =>
            {
                canvasGroup.alpha = 0f;
                canvasGroup.gameObject.SetActive(false);
            });
    }

    private void OnDestroy()
    {
        KillVisibilityTween();
    }

    private void KillVisibilityTween()
    {
        if (visibilityTween != null)
        {
            visibilityTween.Kill(false);
            visibilityTween = null;
        }
    }
}