using System.Collections.Generic;

/// <summary>
/// Optional interface for attack cards that preview/resolve multiple enemies
/// from a selected primary target.
/// </summary>
public interface IAreaTargetingCard
{
    void GetPreviewTargets(Enemy primaryTarget, IReadOnlyList<Enemy> aliveEnemies, List<Enemy> results);
    void GetResolveTargets(Enemy primaryTarget, IReadOnlyList<Enemy> aliveEnemies, List<Enemy> results);
}

