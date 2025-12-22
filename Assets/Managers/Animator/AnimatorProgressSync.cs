using UnityEngine;

public class AnimatorProgressSync : MonoBehaviour
{
    [Header("主動畫（本體）")]
    public Animator masterAnimator;

    [Header("要同步的 Animator（紅底 / 白底）")]
    public Animator[] slaveAnimators;

    [Tooltip("允許的誤差，越小同步越硬")]
    public float tolerance = 0.01f;

    void LateUpdate()
    {
        if (masterAnimator == null) return;

        var masterState = masterAnimator.GetCurrentAnimatorStateInfo(0);

        // normalizedTime 可能 >1（loop），所以取小數部分
        float masterProgress = masterState.normalizedTime % 1f;

        foreach (var slave in slaveAnimators)
        {
            if (slave == null || !slave.gameObject.activeInHierarchy)
                continue;

            var slaveState = slave.GetCurrentAnimatorStateInfo(0);

            float slaveProgress = slaveState.normalizedTime % 1f;

            // 如果進度差距超過容許值，就強制同步
            if (Mathf.Abs(masterProgress - slaveProgress) > tolerance)
            {
                slave.Play(
                    slaveState.fullPathHash,
                    0,
                    masterProgress
                );
            }
        }
    }
}
