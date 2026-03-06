using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles dialogue bubble presentation: queuing lines, typewriter animation, next/skip behavior.
/// This component owns only the UI concerns and does not depend on any specific scene logic.
/// </summary>
public class DialogueBubbleUI : MonoBehaviour, IPointerClickHandler
{
    public event Action LinesFinished;
    [Header("References")]
    [SerializeField] private GameObject bubbleRoot;
    [SerializeField] private Text dialogueText;
    [SerializeField] private Button nextButton;

    [Header("Options")]
    [SerializeField] private bool hideBubbleWhenEmpty = true;

    [Header("Typewriter")]
    [SerializeField, Min(0f)] private float typewriterDurationPerChar = 0.03f;
    [SerializeField, Min(0f)] private float minTypewriterDuration = 0.15f;
    [SerializeField] private Ease typewriterEase = Ease.Linear;

    private readonly Queue<string> queuedLines = new Queue<string>();
    private Tween dialogueTween;
    private bool isTyping;
    private bool linesFinishedInvoked;
    private DialogueBubbleClickRelay bubbleClickRelay;
    public bool IsTyping => isTyping;

    private void Awake()
    {
        WireNextButton(nextButton);
        WireBubbleRootClickRelay();
    }

    private void OnDestroy()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(ShowNextLine);
        }

        if (dialogueTween != null && dialogueTween.IsActive())
        {
            dialogueTween.Kill(false);
        }

        ClearBubbleRootClickRelayOwner();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isTyping && queuedLines.Count == 0)
        {
            HideBubbleImmediately();
            return;
        }
        ShowNextLine();
    }

    public void PlayLines(IEnumerable<string> lines)
    {
        KillCurrentTween();
        queuedLines.Clear();
        linesFinishedInvoked = false;

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        if (lines != null)
        {
            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    queuedLines.Enqueue(line.Trim());
                }
            }
        }

        ShowNextLine();
    }

    public void PlayText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            PlayLines(null);
            return;
        }

        PlayLines(new[] { text.Trim() });
    }

    public void ShowNextLine()
    {
        if (dialogueText == null)
            return;

        if (dialogueTween != null && dialogueTween.IsActive() && dialogueTween.IsPlaying())
        {
            dialogueTween.Complete();
            return;
        }

        KillCurrentTween();

        if (queuedLines.Count == 0)
        {
            isTyping = false;
            dialogueText.text = string.Empty;
            UpdateBubbleVisibility();
            NotifyLinesFinished();
            return;
        }

        PlayTypewriter(queuedLines.Dequeue());
    }

    public void SetUIReferences(GameObject newBubbleRoot, Text newDialogueText, Button newNextButton)
    {
        ClearBubbleRootClickRelayOwner();
        bubbleRoot = newBubbleRoot;
        dialogueText = newDialogueText;

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(ShowNextLine);
        }

        nextButton = newNextButton;
        WireNextButton(nextButton);
        WireBubbleRootClickRelay();
        UpdateBubbleVisibility();
    }

    public void SetTypewriterSettings(float perCharDuration, float minimumDuration, Ease ease)
    {
        typewriterDurationPerChar = Mathf.Max(0f, perCharDuration);
        minTypewriterDuration = Mathf.Max(0f, minimumDuration);
        typewriterEase = ease;
    }

    public void SetHideWhenEmpty(bool hide)
    {
        hideBubbleWhenEmpty = hide;
        UpdateBubbleVisibility();
    }

    public void ForceHideBubble()
    {
        KillCurrentTween();
        queuedLines.Clear();
        linesFinishedInvoked = true;
        HideBubbleImmediately();

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(false);
        }
    }

    public CanvasGroup GetOrAddBubbleCanvasGroup()
    {
        if (bubbleRoot == null)
            return null;

        CanvasGroup group = bubbleRoot.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = bubbleRoot.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private void KillCurrentTween()
    {
        if (dialogueTween != null)
        {
            dialogueTween.Kill(false);
            dialogueTween = null;
        }

        isTyping = false;
    }

    private void PlayTypewriter(string line)
    {
        isTyping = true;
        dialogueText.text = string.Empty;

        float duration = Mathf.Max(minTypewriterDuration, line.Length * typewriterDurationPerChar);
        dialogueTween = dialogueText
            .DOText(line, duration, true, ScrambleMode.None)
            .SetEase(typewriterEase)
            .OnComplete(() =>
            {
                isTyping = false;
                UpdateBubbleVisibility();
                if (queuedLines.Count == 0)
                {
                    NotifyLinesFinished();
                }
            });
        UpdateBubbleVisibility();
    }
    private void HideBubbleImmediately()
    {
        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        if (bubbleRoot != null && bubbleRoot.activeSelf)
        {
            bubbleRoot.SetActive(false);
        }
    }
    private void UpdateBubbleVisibility()
    {
        bool hasMoreLines = queuedLines.Count > 0;

        if (nextButton != null)
        {
            bool canAdvanceOrSkip = hasMoreLines || isTyping;
            nextButton.gameObject.SetActive(canAdvanceOrSkip);
        }

        if (hideBubbleWhenEmpty && bubbleRoot != null)
        {
            bool hasContent = hasMoreLines || isTyping || !string.IsNullOrEmpty(dialogueText?.text);
            bubbleRoot.SetActive(hasContent);
        }
    }

    private void WireNextButton(Button button)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(ShowNextLine);
    }

    public void HandleBubbleRootPointerClick(PointerEventData eventData)
    {
        if (!isTyping && queuedLines.Count == 0)
        {
            HideBubbleImmediately();
        }
    }

    private void WireBubbleRootClickRelay()
    {
        if (bubbleRoot == null || bubbleRoot == gameObject)
            return;

        bubbleClickRelay = bubbleRoot.GetComponent<DialogueBubbleClickRelay>();
        if (bubbleClickRelay == null)
        {
            bubbleClickRelay = bubbleRoot.AddComponent<DialogueBubbleClickRelay>();
        }

        bubbleClickRelay.SetOwner(this);
    }

    private void ClearBubbleRootClickRelayOwner()
    {
        if (bubbleClickRelay != null)
        {
            bubbleClickRelay.SetOwner(null);
            bubbleClickRelay = null;
        }
    }
    private void NotifyLinesFinished()
    {
        if (linesFinishedInvoked)
            return;

        linesFinishedInvoked = true;
        LinesFinished?.Invoke();
    }
}

public sealed class DialogueBubbleClickRelay : MonoBehaviour, IPointerClickHandler
{
    private DialogueBubbleUI owner;

    public void SetOwner(DialogueBubbleUI dialogueBubbleUI)
    {
        owner = dialogueBubbleUI;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        owner?.HandleBubbleRootPointerClick(eventData);
    }
}
