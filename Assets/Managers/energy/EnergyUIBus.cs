using System.Collections.Generic;

public static class EnergyUIBus
{
    private static readonly List<EnergyPanelView> s_views = new List<EnergyPanelView>(8);

    public static void Register(EnergyPanelView v)
    {
        if (v != null && !s_views.Contains(v)) s_views.Add(v);
    }

    public static void Unregister(EnergyPanelView v)
    {
        if (v != null) s_views.Remove(v);
    }

    public static int ViewCount => s_views.Count;

    public static void RefreshAll(int current, int max)
    {
        for (int i = 0; i < s_views.Count; i++)
        {
            var v = s_views[i];
            if (v == null) continue;
            v.Refresh(current, max);
        }
    }
}
