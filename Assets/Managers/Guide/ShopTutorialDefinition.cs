using System;
using System.Collections.Generic;
using UnityEngine;

public enum ShopTutorialAnchor
{
    None,
    ShopTitle,
    GoldDisplay,
    CardsTabButton,
    RelicsTabButton,
    RemovalTabButton,
    PreviousPageButton,
    NextPageButton,
    RefreshRemovalButton,
    ReturnButton,
    RemovalCost,
    CardList,
    RelicList,
    RemovalList,
    CardsPanel,
    RelicsPanel,
    RemovalPanel,
    MessageText
}

public enum ShopTutorialAction
{
    None,
    OpenCardsTab,
    OpenRelicsTab,
    OpenRemovalTab,
    PreviousPage,
    NextPage,
    RefreshRemoval,
    ReturnToRunMap
}

[CreateAssetMenu(fileName = "ShopTutorialDefinition", menuName = "Guide/Shop Tutorial Definition")]
public sealed class ShopTutorialDefinition : ScriptableObject
{
    [Serializable]
    public sealed class Step
    {
        [SerializeField] private string stepId;
        [SerializeField, TextArea(2, 5)] private string dialogue;
        [SerializeField, TextArea(1, 3)] private string prompt;
        [SerializeField] private ShopTutorialAnchor focusAnchor = ShopTutorialAnchor.None;
        [SerializeField] private ShopTutorialAction requiredAction = ShopTutorialAction.None;
        [SerializeField] private bool selectTabBeforeStep;
        [SerializeField] private ShopUIManager.ShopTab tabToSelect = ShopUIManager.ShopTab.Cards;
        [SerializeField] private Sprite tutorialImage;
        [SerializeField, Range(0f, 1f)] private float tutorialImageAlpha = 1f;
        [SerializeField] private bool matchTargetRect = true;
        [SerializeField] private Vector2 imageSize = Vector2.zero;
        [SerializeField] private Vector2 imageOffset = Vector2.zero;

        public string StepId => stepId;
        public string Dialogue => dialogue;
        public string Prompt => prompt;
        public ShopTutorialAnchor FocusAnchor => focusAnchor;
        public ShopTutorialAction RequiredAction => requiredAction;
        public bool SelectTabBeforeStep => selectTabBeforeStep;
        public ShopUIManager.ShopTab TabToSelect => tabToSelect;
        public Sprite TutorialImage => tutorialImage;
        public float TutorialImageAlpha => tutorialImageAlpha;
        public bool MatchTargetRect => matchTargetRect;
        public Vector2 ImageSize => imageSize;
        public Vector2 ImageOffset => imageOffset;
    }

    [SerializeField] private string completionFlag = GuideKeys.TutorialShopIntro;
    [SerializeField] private List<Step> steps = new List<Step>();

    public string CompletionFlag
    {
        get
        {
            if (string.IsNullOrWhiteSpace(completionFlag))
                return GuideKeys.TutorialShopIntro;

            return completionFlag.Trim();
        }
    }

    public IReadOnlyList<Step> Steps => steps;
    public bool HasSteps => steps != null && steps.Count > 0;
}
