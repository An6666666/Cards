using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Watches a dialogue text component and swaps an NPC portrait when the text changes.
/// Behavior priority is fixed:
/// 1. If messageRoot is hidden, or the watched text is inactive/empty, restore defaultSprite.
/// 2. While text is still changing, delay sprite swapping until the content settles.
/// 3. If a typewriter state is available, it must also be idle before swapping.
/// 4. If randomSprites has no valid sprite entries, keep the current sprite unchanged.
/// </summary>
public class NpcDialogueSpriteSwitcher : MonoBehaviour
{
    [Header("NPC")]
    [SerializeField] private Image npcImage;
    [SerializeField] private Sprite defaultSprite;

    [Header("Dialogue Source")]
    [SerializeField] private GameObject messageRoot;
    [SerializeField] private Text uiText;
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private DialogueBubbleUI dialogueBubbleUI;
    [SerializeField] private GuideNPCPresenter guideNPCPresenter;

    [Header("Behavior")]
    [SerializeField] private bool restoreDefaultWhenMessageHidden = true;
    [SerializeField, Min(0f)] private float textChangeSettleSeconds = 0.05f;

    [Header("Random Sprite Pool")]
    [SerializeField] private List<Sprite> randomSprites = new List<Sprite>();

    private string lastObservedText = string.Empty;
    private string lastAppliedDialogueText = string.Empty;
    private bool lastMessageHidden = true;
    private bool lastWasTyping;
    private Sprite lastAppliedSprite;
    private bool warnedAboutAutoAssignedNpcImage;
    private bool npcImageWasAutoAssigned;
    private float lastTextChangeTime = float.NegativeInfinity;

    private void Awake()
    {
        ResolveReferencesIfNeeded();
        CacheDefaultSpriteIfNeeded();
    }

    private void OnEnable()
    {
        ForceRefresh();
    }

    private void Update()
    {
        ResolveReferencesIfNeeded();

        string currentText = GetCurrentText();
        bool messageHidden = IsMessageHidden(currentText);
        bool isTyping = IsTyping();
        bool textAlreadyApplied = string.Equals(lastAppliedDialogueText, currentText, System.StringComparison.Ordinal);

        if (!string.Equals(lastObservedText, currentText, System.StringComparison.Ordinal))
        {
            lastTextChangeTime = Time.unscaledTime;
        }

        if (string.Equals(lastObservedText, currentText, System.StringComparison.Ordinal) &&
            lastMessageHidden == messageHidden &&
            lastWasTyping == isTyping &&
            (messageHidden || textAlreadyApplied))
        {
            return;
        }

        RefreshSprite(currentText, messageHidden, isTyping);
    }

    [ContextMenu("Refresh Sprite")]
    public void ForceRefresh()
    {
        ResolveReferencesIfNeeded();
        string currentText = GetCurrentText();
        RefreshSprite(currentText, IsMessageHidden(currentText), IsTyping());
    }

    private void RefreshSprite(string currentText, bool messageHidden, bool isTyping)
    {
        CacheDefaultSpriteIfNeeded();

        if (guideNPCPresenter != null && !guideNPCPresenter.AreSelfGraphicsVisible)
        {
            if (npcImage != null)
            {
                npcImage.enabled = false;
                npcImage.raycastTarget = false;
            }

            lastAppliedDialogueText = string.Empty;
            lastObservedText = currentText ?? string.Empty;
            lastMessageHidden = messageHidden;
            lastWasTyping = isTyping;
            return;
        }

        if (restoreDefaultWhenMessageHidden && messageHidden)
        {
            ApplySprite(defaultSprite);
            lastAppliedDialogueText = string.Empty;
        }
        else
        {
            bool hasTypingSource = dialogueBubbleUI != null;
            bool textSettled = (Time.unscaledTime - lastTextChangeTime) >= textChangeSettleSeconds;
            bool canApplyForCurrentText = !string.Equals(lastAppliedDialogueText, currentText, System.StringComparison.Ordinal);

            if (!isTyping && (!hasTypingSource ? textSettled : true) && canApplyForCurrentText)
            {
                if (TryPickRandomSprite(out Sprite randomSprite))
                {
                    ApplySprite(randomSprite);
                    lastAppliedDialogueText = currentText ?? string.Empty;
                }
            }
        }

        lastObservedText = currentText ?? string.Empty;
        lastMessageHidden = messageHidden;
        lastWasTyping = isTyping;
    }

    private void ApplySprite(Sprite sprite)
    {
        if (npcImage == null)
            return;

        bool shouldEnable = sprite != null;
        if (lastAppliedSprite == sprite && npcImage.sprite == sprite && npcImage.enabled == shouldEnable)
        {
            npcImage.raycastTarget = false;
            return;
        }

        npcImage.sprite = sprite;
        npcImage.enabled = shouldEnable;
        npcImage.raycastTarget = false;
        lastAppliedSprite = sprite;
    }

    private bool TryPickRandomSprite(out Sprite sprite)
    {
        if (randomSprites == null || randomSprites.Count == 0)
        {
            sprite = null;
            return false;
        }

        List<Sprite> validSprites = null;
        for (int i = 0; i < randomSprites.Count; i++)
        {
            Sprite candidate = randomSprites[i];
            if (candidate == null)
                continue;

            if (validSprites == null)
                validSprites = new List<Sprite>();

            validSprites.Add(candidate);
        }

        if (validSprites == null || validSprites.Count == 0)
        {
            sprite = null;
            return false;
        }

        sprite = validSprites[Random.Range(0, validSprites.Count)];
        return true;
    }

    private string GetCurrentText()
    {
        if (tmpText != null)
            return tmpText.text ?? string.Empty;

        if (uiText != null)
            return uiText.text ?? string.Empty;

        return string.Empty;
    }

    private bool IsMessageHidden(string currentText)
    {
        if (messageRoot != null && !messageRoot.activeInHierarchy)
            return true;

        if (tmpText != null && !tmpText.gameObject.activeInHierarchy)
            return true;

        if (uiText != null && !uiText.gameObject.activeInHierarchy)
            return true;

        return string.IsNullOrWhiteSpace(currentText);
    }

    private bool IsTyping()
    {
        return dialogueBubbleUI != null && dialogueBubbleUI.IsTyping;
    }

    private void ResolveReferencesIfNeeded()
    {
        if (guideNPCPresenter == null)
        {
            guideNPCPresenter = GetComponent<GuideNPCPresenter>();
            if (guideNPCPresenter == null)
            {
                guideNPCPresenter = GetComponentInParent<GuideNPCPresenter>(true);
            }
        }

        if (npcImage == null)
        {
            npcImage = GetComponent<Image>();
            if (npcImage == null)
            {
                npcImage = GetComponentInChildren<Image>(true);
            }

            if (npcImage != null)
            {
                npcImageWasAutoAssigned = true;
            }
        }
        else
        {
            npcImageWasAutoAssigned = false;
        }

        if (npcImageWasAutoAssigned && !warnedAboutAutoAssignedNpcImage)
        {
            Debug.LogWarning(
                "[NpcDialogueSpriteSwitcher] npcImage was auto-assigned. Inspector binding is recommended because UI hierarchies often contain multiple Image components.",
                this);
            warnedAboutAutoAssignedNpcImage = true;
        }

        if (dialogueBubbleUI == null)
        {
            if (messageRoot != null)
            {
                dialogueBubbleUI = messageRoot.GetComponentInParent<DialogueBubbleUI>(true);
            }

            if (dialogueBubbleUI == null && tmpText != null)
            {
                dialogueBubbleUI = tmpText.GetComponentInParent<DialogueBubbleUI>(true);
            }

            if (dialogueBubbleUI == null && uiText != null)
            {
                dialogueBubbleUI = uiText.GetComponentInParent<DialogueBubbleUI>(true);
            }
        }
    }

    private void CacheDefaultSpriteIfNeeded()
    {
        if (defaultSprite == null && npcImage != null)
        {
            defaultSprite = npcImage.sprite;
        }
    }
}
