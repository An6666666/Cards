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
            CardUI ui = cardGO.GetComponent<CardUI>();
            ui.SetupCard(card);
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
    }
}
