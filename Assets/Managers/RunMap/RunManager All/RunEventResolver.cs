using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RunEventResolver
{
    private RunEventUIManager eventUIManager;
    private Player player;
    private PlayerRunSnapshot initialPlayerSnapshot;
    private PlayerRunSnapshot currentRunSnapshot;

    public RunEventResolver(RunEventUIManager eventUIManager)
    {
        this.eventUIManager = eventUIManager;
    }

    public RunEventUIManager EventUIManager
    {
        get => eventUIManager;
        set => eventUIManager = value;
    }

    public Player Player
    {
        get => player;
        set => player = value;
    }

    public PlayerRunSnapshot InitialPlayerSnapshot
    {
        get => initialPlayerSnapshot;
        set => initialPlayerSnapshot = value;
    }

    public PlayerRunSnapshot CurrentRunSnapshot
    {
        get => currentRunSnapshot;
        set => currentRunSnapshot = value;
    }

    public void HandleEventNode(MapNodeData node, Action onComplete)
    {
        if (node == null)
            return;

        bool callbackInvoked = false;
        Action safeComplete = () =>
        {
            if (callbackInvoked)
                return;
            callbackInvoked = true;
            onComplete?.Invoke();
        };

        RunEventDefinition eventDefinition = node.Event;
        if (eventDefinition == null)
        {
            Debug.LogWarning($"RunEventResolver: Event node {node.NodeId} has no definition.");
            safeComplete();
            return;
        }

        if (eventUIManager == null)
        {
            eventUIManager = UnityEngine.Object.FindObjectOfType<RunEventUIManager>(includeInactive: true);
        }

        if (eventUIManager == null)
        {
            Debug.LogWarning("RunEventResolver: Event UI Manager is not assigned; completing event immediately.");
            safeComplete();
            return;
        }

        eventUIManager.ShowEvent(eventDefinition, option =>
        {
            ApplyEventOption(option);
            safeComplete();
        });
    }

    private void ApplyEventOption(RunEventOption option)
    {
        if (option == null)
            return;

        if (player != null)
        {
            ApplyEventOptionToPlayer(option, player);
            currentRunSnapshot = PlayerRunSnapshot.Capture(player);
        }
        else
        {
            ApplyEventOptionToSnapshot(option);
        }
    }

    private void ApplyEventOptionToPlayer(RunEventOption option, Player target)
    {
        if (option.goldDelta != 0)
        {
            target.gold += option.goldDelta;
        }

        if (option.hpDelta != 0)
        {
            int clampedHp = Mathf.Clamp(target.currentHP + option.hpDelta, 0, target.maxHP);
            target.currentHP = clampedHp;
        }

        if (option.rewardCards != null && option.rewardCards.Count > 0)
        {
            target.deck.AddRange(option.rewardCards.Where(card => card != null));
        }

        if (option.rewardRelics != null && option.rewardRelics.Count > 0)
        {
            target.relics.AddRange(option.rewardRelics.Where(card => card != null));
        }
    }

    private void ApplyEventOptionToSnapshot(RunEventOption option)
    {
        EnsureRunSnapshotExists();

        if (currentRunSnapshot == null)
            return;

        if (option.goldDelta != 0)
        {
            currentRunSnapshot.gold += option.goldDelta;
        }

        if (option.hpDelta != 0)
        {
            int clampedHp = Mathf.Clamp(currentRunSnapshot.currentHP + option.hpDelta, 0, currentRunSnapshot.maxHP);
            currentRunSnapshot.currentHP = clampedHp;
        }

        if (option.rewardCards != null && option.rewardCards.Count > 0)
        {
            currentRunSnapshot.deck.AddRange(option.rewardCards.Where(card => card != null));
        }

        if (option.rewardRelics != null && option.rewardRelics.Count > 0)
        {
            currentRunSnapshot.relics.AddRange(option.rewardRelics.Where(card => card != null));
        }
    }

    private void EnsureRunSnapshotExists()
    {
        if (currentRunSnapshot != null)
            return;

        if (initialPlayerSnapshot != null)
        {
            currentRunSnapshot = initialPlayerSnapshot.Clone();
            return;
        }

        currentRunSnapshot = new PlayerRunSnapshot
        {
            maxHP = player != null ? player.maxHP : 0,
            currentHP = player != null ? player.currentHP : 0,
            gold = player != null ? player.gold : 0,
            deck = new List<CardBase>(),
            relics = new List<CardBase>(),
            exhaustPile = new List<CardBase>()
        };
    }
}
