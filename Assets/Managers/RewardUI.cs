using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RewardUI : MonoBehaviour
{
    private BattleManager manager;
    private GameObject skipHoverIndicator;
    private bool skipHoverConfigured;

    [SerializeField] private Text goldText;
    [SerializeField] private Button packButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private Transform cardParent;
    [Header("Reward Card Display")]
    [SerializeField] private bool useRewardCardScale = true;
    [SerializeField] private Vector3 rewardCardScale = new Vector3(0.8f, 0.8f, 1f);
    [SerializeField] private bool useRewardCardLayoutSize = false;
    [SerializeField] private Vector2 rewardCardPreferredSize = new Vector2(130f, 270f);

    private void Awake()
    {
        ConfigureSkipButtonHover();
        SetSkipHoverVisible(false);
    }

    private void OnDisable()
    {
        SetSkipHoverVisible(false);
    }

    public void Show(BattleManager bm, int goldReward, List<CardBase> cardChoices)
    {
        manager = bm;

        gameObject.SetActive(true);
        SetSkipHoverVisible(false);
        goldText.text = $"獲得 {goldReward} 金幣";

        packButton.gameObject.SetActive(true);
        cardParent.gameObject.SetActive(false);

        foreach (Transform child in cardParent)
            Destroy(child.gameObject);

        packButton.onClick.RemoveAllListeners();
        skipButton.onClick.RemoveAllListeners();

        packButton.onClick.AddListener(() => DisplayCardChoices(cardChoices));
        skipButton.onClick.AddListener(Close);
    }

    private void ConfigureSkipButtonHover()
    {
        if (skipHoverConfigured || skipButton == null)
        {
            return;
        }

        Transform hoverTransform = skipButton.transform.Find("h");
        if (hoverTransform == null)
        {
            return;
        }

        skipHoverIndicator = hoverTransform.gameObject;
        skipHoverIndicator.SetActive(false);
        Image hoverImage = skipHoverIndicator.GetComponent<Image>();
        if (hoverImage != null)
        {
            hoverImage.raycastTarget = false;
        }

        EventTrigger trigger = skipButton.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = skipButton.gameObject.AddComponent<EventTrigger>();
        }

        AddHoverEntry(trigger, EventTriggerType.PointerEnter, true);
        AddHoverEntry(trigger, EventTriggerType.PointerExit, false);
        skipHoverConfigured = true;
    }

    private void AddHoverEntry(EventTrigger trigger, EventTriggerType eventType, bool visible)
    {
        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(_ => SetSkipHoverVisible(visible));
        trigger.triggers.Add(entry);
    }

    private void SetSkipHoverVisible(bool visible)
    {
        if (skipHoverIndicator != null)
        {
            skipHoverIndicator.SetActive(visible);
        }
    }

    private void DisplayCardChoices(List<CardBase> cardChoices)
    {
        packButton.gameObject.SetActive(false);
        cardParent.gameObject.SetActive(true);
        foreach (var card in cardChoices)
        {
            GameObject cardGO = Instantiate(manager.cardPrefab, cardParent);
            if (useRewardCardScale)
            {
                cardGO.transform.localScale = rewardCardScale;
            }

            if (useRewardCardLayoutSize)
            {
                LayoutElement layoutElement = cardGO.GetComponent<LayoutElement>();
                if (layoutElement == null)
                    layoutElement = cardGO.AddComponent<LayoutElement>();

                layoutElement.preferredWidth = rewardCardPreferredSize.x;
                layoutElement.preferredHeight = rewardCardPreferredSize.y;
            }
            CardUI ui = cardGO.GetComponent<CardUI>();
            ui.SetupCard(card);
            ui.SetDisplayContext(CardUI.DisplayContext.Reward);

            if (!cardGO.TryGetComponent<Button>(out var b))
            {
                b = cardGO.AddComponent<Button>();
            }
            CardBase captured = card;
            b.onClick.AddListener(() => OnCardSelected(captured));
        }
    }

    private void OnCardSelected(CardBase card)
    {
        manager.player.deck.Add(Instantiate(card));
        Close();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        RunManager.Instance?.ReturnToRunSceneFromBattle();
    }
}
