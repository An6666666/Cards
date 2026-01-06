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

    public bool IsTyping => isTyping;

    private void Awake()
    {
        WireNextButton(nextButton);
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
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ShowNextLine();
    }

    public void PlayLines(IEnumerable<string> lines)
    {
        KillCurrentTween();
        queuedLines.Clear();

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
                    queuedLines.Enqueue(line);
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

        PlayLines(new[] { text });
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
            return;
        }

        PlayTypewriter(queuedLines.Dequeue());
    }

    public void SetUIReferences(GameObject newBubbleRoot, Text newDialogueText, Button newNextButton)
    {
        bubbleRoot = newBubbleRoot;
        dialogueText = newDialogueText;

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(ShowNextLine);
        }

        nextButton = newNextButton;
        WireNextButton(nextButton);
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
            });

        UpdateBubbleVisibility();
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
}