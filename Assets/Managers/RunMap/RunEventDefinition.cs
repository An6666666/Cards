using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Run/Event Definition", fileName = "RunEvent")]
public class RunEventDefinition : ScriptableObject
{
    [SerializeField] private string eventId;
    [SerializeField] private string title;
    [TextArea] [SerializeField] private string description;
    [SerializeField] private List<RunEventOption> options = new List<RunEventOption>();

    public string EventId => string.IsNullOrEmpty(eventId) ? name : eventId;
    public string Title => title;
    public string Description => description;
    public IReadOnlyList<RunEventOption> Options => options;
}

[System.Serializable]
public class RunEventOption
{
    public string optionLabel;
    public int goldDelta;
    public int hpDelta;
    public List<CardBase> rewardCards = new List<CardBase>();
    public List<RelicBase> rewardRelics = new List<RelicBase>();
}
