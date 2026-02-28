using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

/// <summary>
/// Presents guide NPC dialogue using a shared DialogueBubbleUI and GuideDialogueDatabase.
/// Responsible for visual show/hide animation and invoking bubble playback.
/// </summary>
public class GuideNPCPresenter : MonoBehaviour
{
    public event Action DialogueLinesFinished;
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
    private static readonly List<DialogueBubbleUI> dialogueUiCandidates = new List<DialogueBubbleUI>();
    private static readonly List<GuideDialogueDatabase> dialogueDatabaseCandidates = new List<GuideDialogueDatabase>();
    private DialogueBubbleUI subscribedDialogueUI;
    private Graphic[] selfGraphics;

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
        ResolveSceneReferencesIfNeeded();
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
        IReadOnlyList<string> lines = null;
        if (!TryResolveDialogueLines(trimmedKey, out lines))
        {
            string databaseName = dialogueDatabase != null ? dialogueDatabase.name : "null";
            Debug.LogWarning($"[GuideNPCPresenter] Talk aborted: no dialogue lines found for key '{trimmedKey}' (database: {databaseName}).", this);
            return false;
        }

        if (lines == null || lines.Count == 0)
        {
            string databaseName = dialogueDatabase != null ? dialogueDatabase.name : "null";
            Debug.LogWarning($"[GuideNPCPresenter] Talk aborted: no dialogue lines found for key '{trimmedKey}' (database: {databaseName}).", this);
            return false;
        }

        TalkLines(lines);
        return true;
    }
    private bool TryResolveDialogueLines(string key, out IReadOnlyList<string> lines)
    {
        lines = null;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        string trimmedKey = key.Trim();
        if (dialogueDatabase != null)
        {
            lines = dialogueDatabase.GetLines(trimmedKey);
            if (lines != null && lines.Count > 0)
            {
                return true;
            }
        }

        dialogueDatabaseCandidates.Clear();
        dialogueDatabaseCandidates.AddRange(Resources.FindObjectsOfTypeAll<GuideDialogueDatabase>());

        for (int i = 0; i < dialogueDatabaseCandidates.Count; i++)
        {
            GuideDialogueDatabase candidate = dialogueDatabaseCandidates[i];
            if (candidate == null || candidate == dialogueDatabase)
                continue;

            IReadOnlyList<string> candidateLines = candidate.GetLines(trimmedKey);
            if (candidateLines == null || candidateLines.Count == 0)
                continue;

            dialogueDatabase = candidate;
            lines = candidateLines;
            return true;
        }

        return false;
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
        ResolveSceneReferencesIfNeeded();
        if (dialogueUI == null)
            return;

        Show();
        dialogueUI.PlayLines(lines);
    }

    public void Show()
    {
        ResolveSceneReferencesIfNeeded();
        KillDelayedHideTween();
        SetSelfGraphicsVisible(true);
        if (canvasGroup == null)
        {
            return;
        }

        KillVisibilityTween();
        canvasGroup.gameObject.SetActive(true);
        if (!animateVisibility)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            return;
        }
        canvasGroup.alpha = 0f;
        visibilityTween = canvasGroup.DOFade(1f, fadeDuration)
            .SetEase(fadeEase)
            .OnStart(() => canvasGroup.blocksRaycasts = true)
            .OnComplete(() => canvasGroup.alpha = 1f);
    }

    public void Hide()
    {
        KillDelayedHideTween();
        SetSelfGraphicsVisible(false);
        if (canvasGroup == null)
        {
            return;
        }

        KillVisibilityTween();
        if (!animateVisibility)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.gameObject.SetActive(false);
            return;
        }

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
        ResolveSceneReferencesIfNeeded();
    }
    private void OnEnable()
    {
        ResolveSceneReferencesIfNeeded();
    }
    private void OnDestroy()
    {
        UnsubscribeDialogueEvents();
        KillDelayedHideTween();
        KillVisibilityTween();
    }
    private void OnDialogueLinesFinished()
    {
        DialogueLinesFinished?.Invoke();
        if (!hideAfterDialogueEnds)
            return;

        KillDelayedHideTween();
        delayedHideTween = DOVirtual.DelayedCall(hideDelaySeconds, Hide);
    }

    private void SubscribeDialogueEvents()
    {
        if (dialogueUI != null && subscribedDialogueUI != dialogueUI)
        {
            dialogueUI.LinesFinished += OnDialogueLinesFinished;
            subscribedDialogueUI = dialogueUI;
        }
    }

    private void UnsubscribeDialogueEvents()
    {
        if (subscribedDialogueUI != null)
        {
            subscribedDialogueUI.LinesFinished -= OnDialogueLinesFinished;
            subscribedDialogueUI = null;
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
    private void SetSelfGraphicsVisible(bool visible)
    {
        if (selfGraphics == null || selfGraphics.Length == 0)
        {
            selfGraphics = GetComponents<Graphic>();
        }

        for (int i = 0; i < selfGraphics.Length; i++)
        {
            Graphic graphic = selfGraphics[i];
            if (graphic == null)
                continue;

            graphic.enabled = visible;
        }
    }
    private void ResolveSceneReferencesIfNeeded()
    {
        if (dialogueUI == null)
        {
            DialogueBubbleUI foundDialogueUI = FindBestDialogueUI();
            if (foundDialogueUI != null)
            {
                AssignDialogueUI(foundDialogueUI);
            }
        }

        //if (dialogueDatabase == null)
        //{
        //    dialogueDatabase = FindObjectOfType<GuideDialogueDatabase>(true);
        //}

        if (canvasGroup == null && dialogueUI != null)
        {
            canvasGroup = dialogueUI.GetComponent<CanvasGroup>();
        }
        if (canvasGroup == null && dialogueUI != null)
        {
            canvasGroup = dialogueUI.GetComponentInParent<CanvasGroup>(true);
        }
        if (canvasGroup == null && dialogueUI != null)
        {
            canvasGroup = dialogueUI.GetComponentInChildren<CanvasGroup>(true);
        }
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        SubscribeDialogueEvents();
    }
    private DialogueBubbleUI FindBestDialogueUI()
    {
        dialogueUiCandidates.Clear();
        #if UNITY_2023_1_OR_NEWER
        dialogueUiCandidates.AddRange(FindObjectsByType<DialogueBubbleUI>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        #else
        dialogueUiCandidates.AddRange(Resources.FindObjectsOfTypeAll<DialogueBubbleUI>());
        #endif

        Scene currentScene = gameObject.scene;
        DialogueBubbleUI fallback = null;

        for (int i = 0; i < dialogueUiCandidates.Count; i++)
        {
            DialogueBubbleUI candidate = dialogueUiCandidates[i];
            if (candidate == null)
                continue;

            Scene candidateScene = candidate.gameObject.scene;
            if (!candidateScene.IsValid() || !candidateScene.isLoaded)
                continue;

            if (fallback == null)
            {
                fallback = candidate;
            }

            if (candidateScene != currentScene)
                continue;

            if (candidate.gameObject.activeInHierarchy)
                return candidate;

            if (fallback == null)
            {
                fallback = candidate;
            }
        }

        return fallback;
    }
}
