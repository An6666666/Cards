using System.Collections.Generic; // 使用泛型集合（List、Queue、IEnumerable 等）
using UnityEngine; // Unity 核心 API（MonoBehaviour、GameObject、SerializeField 等）
using UnityEngine.UI; // Unity UI（Text、Button）

/// <summary>
/// 元素選擇教學觸發器：橋接元素選擇事件與通用對話系統。
/// </summary>
public class ElementSelectionGuideNPC : SceneGuideTriggerBase
{
    [System.Serializable]
    public class ElementDialogue
    {
        public ElementType element;
        [TextArea(2, 4)]
        public List<string> lines = new List<string>();
        public string dialogueKey;
    }

    [Header("References")]
    [SerializeField] private StartingElementSelectionUI selectionUI;
    [SerializeField] private DialogueBubbleUI dialogueBubbleUI;
    [SerializeField] private GuideNPCPresenter npcPresenter;
    [SerializeField] private GuideDialogueDatabase dialogueDatabase;

    [Header("Intro")]
    [SerializeField] private string introKey;
    [TextArea(2, 4)]
    [SerializeField] private List<string> introLines = new List<string>();

    [Header("Element Lines")]
    [SerializeField] private List<ElementDialogue> elementDialogues = new List<ElementDialogue>();

    [Header("Legacy UI Binding (for automatic wiring)")]
    [SerializeField] private GameObject bubbleRoot;
    [SerializeField] private Text dialogueText;
    [SerializeField] private Button nextButton;
    [SerializeField] private bool hideBubbleWhenEmpty = true;
    [SerializeField, Min(0f)] private float typewriterDurationPerChar = 0.03f;
    [SerializeField, Min(0f)] private float minTypewriterDuration = 0.15f;
    [SerializeField] private DG.Tweening.Ease typewriterEase = DG.Tweening.Ease.Linear;

    private void Awake()
    {
        WireSceneReferences();

        if (selectionUI != null)
        {
            selectionUI.ElementSelected += HandleElementSelected;
        }
    }

    private void Start()
    {
        if (!string.IsNullOrWhiteSpace(introKey))
        {
            TryTalk(npcPresenter, introKey, introLines);
        }
        else if (introLines.Count > 0)
        {
            npcPresenter?.TalkLines(introLines);
        }
    }

    private void OnDestroy()
    {
        if (selectionUI != null)
        {
            selectionUI.ElementSelected -= HandleElementSelected;
        }
    }

    private void HandleElementSelected(ElementType element)
    {
        ElementDialogue dialogue = elementDialogues.Find(d => d != null && d.element == element);
        if (dialogue == null)
            return;

        if (!string.IsNullOrWhiteSpace(dialogue.dialogueKey))
        {
            TryTalk(npcPresenter, dialogue.dialogueKey, dialogue.lines);
            return;
        }

        if (dialogue.lines != null && dialogue.lines.Count > 0)
        {
            npcPresenter?.TalkLines(dialogue.lines);
        }
    }

    private void WireSceneReferences()
    {
        if (dialogueBubbleUI == null && (bubbleRoot != null || dialogueText != null || nextButton != null))
        {
            dialogueBubbleUI = gameObject.GetComponent<DialogueBubbleUI>();
            if (dialogueBubbleUI == null)
            {
                dialogueBubbleUI = gameObject.AddComponent<DialogueBubbleUI>();
            }

            dialogueBubbleUI.SetUIReferences(bubbleRoot, dialogueText, nextButton);
            dialogueBubbleUI.SetHideWhenEmpty(hideBubbleWhenEmpty);
            dialogueBubbleUI.SetTypewriterSettings(typewriterDurationPerChar, minTypewriterDuration, typewriterEase);
        }

        if (npcPresenter == null)
        {
            npcPresenter = gameObject.GetComponent<GuideNPCPresenter>();
            if (npcPresenter == null)
            {
                npcPresenter = gameObject.AddComponent<GuideNPCPresenter>();
            }
        }

        if (npcPresenter != null)
        {
            npcPresenter.AssignDialogueUI(dialogueBubbleUI);
            if (dialogueDatabase != null)
            {
                npcPresenter.AssignDatabase(dialogueDatabase);
            }
        }
    }
}
