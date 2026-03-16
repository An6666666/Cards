using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Run/Encounter Pool", fileName = "EncounterPool")]
public class EncounterPool : ScriptableObject
{
    [Serializable]
    private sealed class FloorEncounterBand
    {
        [SerializeField] private string label = "Zone";
        [SerializeField, Range(0f, 1f)] private float startRatio = 0f;
        [SerializeField, Range(0f, 1f)] private float endRatio = 1f;
        [SerializeField] private List<RunEncounterDefinition> encounters = new List<RunEncounterDefinition>();

        public IReadOnlyList<RunEncounterDefinition> Encounters => encounters;

        public bool Matches(float progress)
        {
            float min = Mathf.Min(startRatio, endRatio);
            float max = Mathf.Max(startRatio, endRatio);
            return progress >= min && progress <= max;
        }
    }

    [SerializeField] private List<RunEncounterDefinition> encounters = new List<RunEncounterDefinition>();
    [SerializeField] private List<FloorEncounterBand> floorBands = new List<FloorEncounterBand>();

    public IReadOnlyList<RunEncounterDefinition> Encounters => encounters;

    public RunEncounterDefinition GetRandomEncounter()
    {
        return GetRandomEncounterFromList(encounters);
    }

    public RunEncounterDefinition GetRandomEncounter(int floorIndex, int totalFloors)
    {
        float progress = GetFloorProgress(floorIndex, totalFloors);

        if (floorBands != null)
        {
            for (int i = 0; i < floorBands.Count; i++)
            {
                FloorEncounterBand band = floorBands[i];
                if (band == null || !band.Matches(progress))
                    continue;

                RunEncounterDefinition encounter = GetRandomEncounterFromList(band.Encounters);
                if (encounter != null)
                    return encounter;
            }
        }

        return GetRandomEncounter();
    }

    private static float GetFloorProgress(int floorIndex, int totalFloors)
    {
        if (totalFloors <= 1)
            return 0f;

        return Mathf.Clamp01(floorIndex / (float)(totalFloors - 1));
    }

    private static RunEncounterDefinition GetRandomEncounterFromList(IReadOnlyList<RunEncounterDefinition> source)
    {
        if (source == null || source.Count == 0)
            return null;

        int index = UnityEngine.Random.Range(0, source.Count);
        return source[index];
    }
}
