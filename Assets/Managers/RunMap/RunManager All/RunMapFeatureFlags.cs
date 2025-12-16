public sealed class RunMapGenerationFeatureFlags
{
    public static RunMapGenerationFeatureFlags Default { get; } = new RunMapGenerationFeatureFlags();

    public RunMapGenerationFeatureFlags(
        bool enableEliteStartFloorFix = false,
        bool enableMinSideMergesPerFloorFix = false)
    {
        EnableEliteStartFloorFix = enableEliteStartFloorFix;
        EnableMinSideMergesPerFloorFix = enableMinSideMergesPerFloorFix;
    }

    public bool EnableEliteStartFloorFix { get; }
    public bool EnableMinSideMergesPerFloorFix { get; }
}