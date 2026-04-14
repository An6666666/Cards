using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BattleEndSummaryController : MonoBehaviour
{
    [Serializable]
    private sealed class TextBinding
    {
        [SerializeField] private Text legacyText;
        [SerializeField] private TMP_Text tmpText;

        public void Set(string value)
        {
            if (legacyText != null)
            {
                legacyText.text = value ?? string.Empty;
            }

            if (tmpText != null)
            {
                tmpText.text = value ?? string.Empty;
            }
        }
    }

    [Header("Visibility")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private bool hideWhenNoSummary = true;

    [Header("Result Visual")]
    [SerializeField] private Image resultImage;
    [SerializeField] private Sprite victorySprite;
    [SerializeField] private Sprite defeatSprite;

    [Header("Result Text")]
    [SerializeField] private TextBinding resultText;
    [SerializeField] private string victoryLabel = "\u52dd\u5229";
    [SerializeField] private string defeatLabel = "\u5931\u6557";

    [Header("Navigation")]
    [SerializeField] private Button returnToTitleButton;
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("Battle Summary")]
    [SerializeField] private TextBinding elementsText;
    [SerializeField] private TextBinding relicCountText;
    [SerializeField] private TextBinding relicNamesText;
    [SerializeField] private TextBinding cardCountText;
    [SerializeField] private TextBinding cardNamesText;
    [SerializeField] private TextBinding damageDealtText;
    [SerializeField] private TextBinding damageTakenText;
    [SerializeField] private TextBinding enemiesDefeatedText;
    [SerializeField] private TextBinding goldRewardText;

    private void Awake()
    {
        if (panelRoot == null)
        {
            Transform panelTransform = transform.Find("Panel");
            panelRoot = panelTransform != null ? panelTransform.gameObject : gameObject;
        }

        BindReturnButton();
        RefreshFromStoredSummary();
    }

    public void RefreshFromStoredSummary()
    {
        if (!BattleEndSummaryStore.TryGetLastSummary(out BattleEndSummaryData summary))
        {
            Debug.Log("BattleEndSummaryController: no stored summary found.");
            if (hideWhenNoSummary && panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            return;
        }

        Debug.Log($"BattleEndSummaryController: applying summary, victory={summary.IsVictory}, kills={summary.EnemiesDefeated}, gold={summary.GoldReward}");
        Apply(summary);
    }

    public void Apply(BattleEndSummaryData summary)
    {
        if (summary == null)
        {
            if (hideWhenNoSummary && panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            return;
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (resultImage != null)
        {
            resultImage.sprite = summary.IsVictory ? victorySprite : defeatSprite;
            resultImage.enabled = resultImage.sprite != null;
        }

        resultText?.Set(summary.IsVictory ? victoryLabel : defeatLabel);
        elementsText?.Set($"\u5143\u7d20\uff1a{summary.ElementsDisplay}");
        relicCountText?.Set($"\u6cd5\u5668\u6578\uff1a{summary.RelicCount}");
        relicNamesText?.Set($"\u6cd5\u5668\uff1a{summary.RelicNamesDisplay}");
        cardCountText?.Set($"\u5361\u7247\u6578\uff1a{summary.CardCount}");
        cardNamesText?.Set($"\u5361\u7247\uff1a{summary.CardNamesDisplay}");
        damageDealtText?.Set($"\u9020\u6210\u50b7\u5bb3\uff1a{summary.TotalDamageDealt}");
        damageTakenText?.Set($"\u53d7\u5230\u50b7\u5bb3\uff1a{summary.TotalDamageTaken}");
        enemiesDefeatedText?.Set($"\u64ca\u6bba\u6578\uff1a{summary.EnemiesDefeated}");
        goldRewardText?.Set($"\u91d1\u5e63\uff1a{summary.GoldReward}");
    }

    private void BindReturnButton()
    {
        if (returnToTitleButton == null)
        {
            Transform buttonTransform = transform.Find("backButton");
            if (buttonTransform != null)
            {
                returnToTitleButton = buttonTransform.GetComponent<Button>();
            }
        }

        if (returnToTitleButton == null)
        {
            return;
        }

        returnToTitleButton.onClick.RemoveListener(ReturnToTitleScene);
        returnToTitleButton.onClick.AddListener(ReturnToTitleScene);
    }

    private void ReturnToTitleScene()
    {
        if (string.IsNullOrWhiteSpace(titleSceneName))
        {
            Debug.LogWarning("BattleEndSummaryController: titleSceneName is not configured.");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() &&
            string.Equals(activeScene.name, titleSceneName, StringComparison.Ordinal))
        {
            BattleEndSummaryStore.ClearLastSummary();
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            return;
        }

        SceneManager.LoadScene(titleSceneName);
    }
}

[Serializable]
public sealed class BattleEndSummaryData
{
    public bool IsVictory;
    public string ElementsDisplay;
    public int RelicCount;
    public string RelicNamesDisplay;
    public int CardCount;
    public string CardNamesDisplay;
    public int TotalDamageDealt;
    public int TotalDamageTaken;
    public int EnemiesDefeated;
    public int GoldReward;
}

public static class BattleEndSummaryStore
{
    private static BattleEndSummaryRuntime runtime;
    private static BattleEndSummaryData lastSummary;
    private static int cumulativeDamageDealt;
    private static int cumulativeDamageTaken;
    private static int cumulativeEnemiesDefeated;
    private static int cumulativeGoldEarned;

    private sealed class BattleEndSummaryRuntime
    {
        public int totalDamageDealt;
        public int totalDamageTaken;
        public int enemiesDefeated;
        public int fallbackGoldReward;
    }

    public static void BeginBattle()
    {
        lastSummary = null;
        runtime = new BattleEndSummaryRuntime();
    }

    public static void ResetRunTotals()
    {
        cumulativeDamageDealt = 0;
        cumulativeDamageTaken = 0;
        cumulativeEnemiesDefeated = 0;
        cumulativeGoldEarned = 0;
        runtime = null;
    }

    public static void RegisterGoldEarned(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        cumulativeGoldEarned += amount;
    }

    public static void RegisterPlayerDamageDealt(int amount)
    {
        if (runtime == null || amount <= 0)
        {
            return;
        }

        runtime.totalDamageDealt += amount;
        cumulativeDamageDealt += amount;
    }

    public static void RegisterPlayerDamageTaken(int amount)
    {
        if (runtime == null || amount <= 0)
        {
            return;
        }

        runtime.totalDamageTaken += amount;
        cumulativeDamageTaken += amount;
    }

    public static void RegisterEnemyDefeated(Enemy enemy)
    {
        if (runtime == null || enemy == null)
        {
            return;
        }

        int goldReward = Mathf.Max(0, enemy.GoldReward);
        if (goldReward <= 0)
        {
            return;
        }

        runtime.enemiesDefeated++;
        runtime.fallbackGoldReward += goldReward;
        cumulativeEnemiesDefeated++;
    }

    public static BattleEndSummaryData Capture(bool isVictory, Player player, int resolvedGoldReward = -1)
    {
        int battleGoldEarned = ResolveBattleGoldEarned(resolvedGoldReward);
        if (isVictory && battleGoldEarned > 0)
        {
            cumulativeGoldEarned += battleGoldEarned;
        }

        lastSummary = BuildSummary(isVictory, player, resolvedGoldReward);

        Debug.Log(
            $"BattleEndSummaryStore.Capture: " +
            $"victory={lastSummary.IsVictory}, " +
            $"damageDealt={lastSummary.TotalDamageDealt}, " +
            $"damageTaken={lastSummary.TotalDamageTaken}, " +
            $"enemiesDefeated={lastSummary.EnemiesDefeated}, " +
            $"gold={lastSummary.GoldReward}");

        runtime = null;
        return lastSummary;
    }

    public static bool TryGetLastSummary(out BattleEndSummaryData summary)
    {
        summary = lastSummary;
        return summary != null;
    }

    public static void ClearLastSummary()
    {
        lastSummary = null;
        runtime = null;
    }

    private static BattleEndSummaryData BuildSummary(bool isVictory, Player player, int resolvedGoldReward)
    {
        List<string> elementNames = new List<string>();
        if (StartingDeckSelection.TryGetRunSelectedElements(out IReadOnlyList<ElementType> selectedElements) &&
            selectedElements != null)
        {
            for (int i = 0; i < selectedElements.Count; i++)
            {
                elementNames.Add(GetElementDisplayName(selectedElements[i]));
            }
        }

        List<string> relicNames = new List<string>();
        if (player != null && player.relics != null)
        {
            for (int i = 0; i < player.relics.Count; i++)
            {
                RelicBase relic = player.relics[i];
                if (relic == null)
                {
                    continue;
                }

                string name = !string.IsNullOrWhiteSpace(relic.cardName) ? relic.cardName : relic.name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    relicNames.Add(name);
                }
            }
        }

        List<CardBase> allRunCards = GetAllRunCards(player);
        List<string> cardNames = BuildCountedCardNames(allRunCards);

        int fallbackGoldReward = runtime != null ? runtime.fallbackGoldReward : 0;

        return new BattleEndSummaryData
        {
            IsVictory = isVictory,
            ElementsDisplay = JoinOrFallback(elementNames, "\u7121"),
            RelicCount = relicNames.Count,
            RelicNamesDisplay = JoinOrFallback(relicNames, "\u7121"),
            CardCount = allRunCards.Count,
            CardNamesDisplay = JoinOrFallback(cardNames, "\u7121"),
            TotalDamageDealt = cumulativeDamageDealt,
            TotalDamageTaken = cumulativeDamageTaken,
            EnemiesDefeated = cumulativeEnemiesDefeated,
            GoldReward = cumulativeGoldEarned
        };
    }

    private static int ResolveBattleGoldEarned(int resolvedGoldReward)
    {
        int fallbackGoldReward = runtime != null ? runtime.fallbackGoldReward : 0;
        return resolvedGoldReward >= 0
            ? Mathf.Max(resolvedGoldReward, fallbackGoldReward)
            : fallbackGoldReward;
    }

    private static List<CardBase> GetAllRunCards(Player player)
    {
        PlayerRunSnapshot snapshot = RunManager.Instance != null ? RunManager.Instance.CurrentRunSnapshot : null;
        if (snapshot != null && snapshot.deck != null && snapshot.deck.Count > 0)
        {
            return new List<CardBase>(snapshot.deck);
        }

        PlayerRunSnapshot capturedSnapshot = PlayerRunSnapshot.Capture(player);
        return capturedSnapshot != null && capturedSnapshot.deck != null
            ? capturedSnapshot.deck
            : new List<CardBase>();
    }

    private static List<string> BuildCountedCardNames(List<CardBase> cards)
    {
        List<string> orderedNames = new List<string>();
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);

        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                CardBase card = cards[i];
                if (card == null)
                {
                    continue;
                }

                string name = !string.IsNullOrWhiteSpace(card.cardName) ? card.cardName : card.name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (counts.ContainsKey(name))
                {
                    counts[name]++;
                    continue;
                }

                counts.Add(name, 1);
                orderedNames.Add(name);
            }
        }

        List<string> result = new List<string>(orderedNames.Count);
        for (int i = 0; i < orderedNames.Count; i++)
        {
            string name = orderedNames[i];
            result.Add($"{name}*{counts[name]}");
        }

        return result;
    }

    private static string JoinOrFallback(List<string> values, string fallback)
    {
        if (values == null || values.Count == 0)
        {
            return fallback;
        }

        return string.Join(" / ", values);
    }

    private static string GetElementDisplayName(ElementType element)
    {
        return element switch
        {
            ElementType.Fire => "\u706b",
            ElementType.Water => "\u6c34",
            ElementType.Wood => "\u6728",
            ElementType.Ice => "\u51b0",
            ElementType.Thunder => "\u96f7",
            _ => element.ToString()
        };
    }
}
