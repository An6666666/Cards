using System.Collections.Generic;

public interface IEnemySkillCooldownProvider
{
    List<EnemyBattleSkillDisplayData> GetSkillCooldowns();
}
