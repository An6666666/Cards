using UnityEngine;

public class PlayerLowHpFeedbackController : MonoBehaviour
{
    private static readonly int IsLowHpHash = Animator.StringToHash("IsLowHp");

    [SerializeField] private GameObject lowHpOverlayRoot;
    [SerializeField] private Animator lowHpAnimator;
    [SerializeField] private float lowHpThreshold = 0.2f;

    private bool isLowHpActive;

    private void Awake()
    {
        lowHpThreshold = Mathf.Clamp01(lowHpThreshold);

        if (lowHpAnimator != null)
        {
            if (lowHpOverlayRoot != null && !lowHpOverlayRoot.activeSelf)
            {
                lowHpOverlayRoot.SetActive(true);
            }

            lowHpAnimator.SetBool(IsLowHpHash, false);
            isLowHpActive = false;
            return;
        }

        if (lowHpOverlayRoot != null)
        {
            lowHpOverlayRoot.SetActive(false);
        }
    }

    public void RefreshLowHpState(int currentHp, int maxHp)
    {
        bool shouldBeLowHp = ShouldBeLowHp(currentHp, maxHp);
        if (shouldBeLowHp == isLowHpActive)
        {
            return;
        }

        isLowHpActive = shouldBeLowHp;

        if (lowHpAnimator != null)
        {
            if (lowHpOverlayRoot != null && !lowHpOverlayRoot.activeSelf)
            {
                lowHpOverlayRoot.SetActive(true);
            }

            lowHpAnimator.SetBool(IsLowHpHash, shouldBeLowHp);
            return;
        }

        if (lowHpOverlayRoot != null)
        {
            lowHpOverlayRoot.SetActive(shouldBeLowHp);
        }
    }

    private bool ShouldBeLowHp(int currentHp, int maxHp)
    {
        if (maxHp <= 0)
        {
            return false;
        }

        float threshold = Mathf.Clamp01(lowHpThreshold);
        return currentHp <= maxHp * threshold;
    }

    private void OnDisable()
    {
        isLowHpActive = false;

        if (lowHpAnimator != null)
        {
            lowHpAnimator.SetBool(IsLowHpHash, false);
        }

        if (lowHpOverlayRoot != null && lowHpAnimator == null)
        {
            lowHpOverlayRoot.SetActive(false);
        }
    }
}
