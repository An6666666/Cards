using System.Collections.Generic;
using UnityEngine;

public interface IEnemySkillCooldownProvider
{
    List<(Sprite icon, int cd)> GetSkillCooldowns();
}
