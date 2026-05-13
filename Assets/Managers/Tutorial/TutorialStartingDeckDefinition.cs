using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialStartingDeckDefinition", menuName = "Tutorial/Starting Deck Definition")]
public class TutorialStartingDeckDefinition : ScriptableObject
{
    [SerializeField] private List<CardBase> cards = new List<CardBase>();

    public bool HasCards => cards != null && cards.Any(PlayerRunSnapshot.ShouldPersistCard);

    public List<CardBase> BuildDeck()
    {
        return cards != null
            ? new List<CardBase>(cards.Where(PlayerRunSnapshot.ShouldPersistCard))
            : new List<CardBase>();
    }
}
