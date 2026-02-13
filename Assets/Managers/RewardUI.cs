using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RewardUI : MonoBehaviour
{
    private BattleManager manager;

    [SerializeField] private Text goldText;
    [SerializeField] private Button packButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private Transform cardParent;
    [Header("Reward Card Display")]
    [SerializeField] private bool useRewardCardScale = true;
    [SerializeField] private Vector3 rewardCardScale = new Vector3(0.8f, 0.8f, 1f);
    [SerializeField] private bool useRewardCardLayoutSize = false;
    [SerializeField] private Vector2 rewardCardPreferredSize = new Vector2(130f, 270f);

    public void Show(BattleManager bm, int goldReward, List<CardBase> cardChoices)
    {
        manager = bm;

        gameObject.SetActive(true);
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
