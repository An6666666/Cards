using System.Collections.Generic;
using UnityEngine;

internal static class RunMapConnectionSampling
{
    public static int SampleByWeight(List<int> candidates, List<float> weights)
    {
        if (candidates == null || weights == null || candidates.Count == 0 || weights.Count != candidates.Count)
            return -1;

        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            total += Mathf.Max(0f, weights[i]);
        }

        if (total <= 0f)
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];

        float roll = UnityEngine.Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += Mathf.Max(0f, weights[i]);
            if (roll <= cumulative)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }
}