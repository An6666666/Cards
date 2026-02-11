using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

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
    [SerializeField] private Image reactionImageView;
    [SerializeField] private VideoPlayer reactionVideoPlayer;

    [Header("Animation")]
    [SerializeField] private bool animateVisibility = true;
    [SerializeField, Min(0f)] private float fadeDuration = 0.2f;
    [SerializeField] private Ease fadeEase = Ease.OutCubic;
    [SerializeField] private bool hideAfterDialogueEnds = true;
    [SerializeField, Min(0f)] private float hideDelaySeconds = 2f;

    private Tween visibilityTween;
    private Tween delayedHideTween;

    public void AssignDialogueUI(DialogueBubbleUI ui)
    {
        if (dialogueUI == ui)
        return;

        UnsubscribeDialogueEvents();
        dialogueUI = ui;
        SubscribeDialogueEvents();
    }

    public void AssignDatabase(GuideDialogueDatabase database)
    {
        dialogueDatabase = database;
    }

    public bool Talk(string key)
    {
        if (dialogueUI == null)
        {
            Debug.LogWarning("[GuideNPCPresenter] Talk aborted: dialogueUI is null.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogWarning("[GuideNPCPresenter] Talk aborted: dialogue key is null or empty.", this);
            return false;
        }

        string trimmedKey = key.Trim();
        IReadOnlyList<string> lines = dialogueDatabase != null ? dialogueDatabase.GetLines(trimmedKey) : null;
        if (lines == null || lines.Count == 0)
        {
            Debug.LogWarning($"[GuideNPCPresenter] Talk aborted: no dialogue lines found for key '{trimmedKey}'.", this);
            return false;
        }

        TalkLines(lines);
        return true;
    }
    public void ShowReactionVisual(Sprite image, VideoClip video)
    {
        if (reactionImageView != null)
        {
            reactionImageView.sprite = image;
            reactionImageView.enabled = image != null;
        }

        if (reactionVideoPlayer != null)
        {
            reactionVideoPlayer.Stop();
            reactionVideoPlayer.clip = video;

            bool hasVideo = video != null;
            if (reactionVideoPlayer.gameObject.activeSelf != hasVideo)
            {
                reactionVideoPlayer.gameObject.SetActive(hasVideo);
            }

            if (hasVideo)
            {
                reactionVideoPlayer.Play();
            }
        }
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
        KillDelayedHideTween();
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
        KillDelayedHideTween();
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
    private void Awake()
    {
        SubscribeDialogueEvents();
    }
    private void OnDestroy()
    {
        UnsubscribeDialogueEvents();
        KillDelayedHideTween();
        KillVisibilityTween();
    }
    private void OnDialogueLinesFinished()
    {
        if (!hideAfterDialogueEnds)
            return;

        KillDelayedHideTween();
        delayedHideTween = DOVirtual.DelayedCall(hideDelaySeconds, Hide);
    }

    private void SubscribeDialogueEvents()
    {
        if (dialogueUI != null)
        {
            dialogueUI.LinesFinished += OnDialogueLinesFinished;
        }
    }

    private void UnsubscribeDialogueEvents()
    {
        if (dialogueUI != null)
        {
            dialogueUI.LinesFinished -= OnDialogueLinesFinished;
        }
    }
    private void KillVisibilityTween()
    {
        if (visibilityTween != null)
        {
            visibilityTween.Kill(false);
            visibilityTween = null;
        }
    }
    private void KillDelayedHideTween()
    {
        if (delayedHideTween != null)
        {
            delayedHideTween.Kill(false);
            delayedHideTween = null;
        }
    }
}