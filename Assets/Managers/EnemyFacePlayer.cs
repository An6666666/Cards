using UnityEngine;

public class EnemyFacePlayer : MonoBehaviour
{
    [Header("Which part to flip")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool faceRightByDefault = true;

    [Header("Position Offset (Optional)")]
    [SerializeField] private Vector3 leftOffset = Vector3.zero;
    [SerializeField] private Vector3 rightOffset = Vector3.zero;

    [Header("Stability")]
    [Tooltip("dx 小於這個值視為正上/正下，不改面向（避免抖動）")]
    [SerializeField] private float verticalDeadZone = 0.15f;

    [Tooltip("方向改變至少要超過這個差值才翻面（額外防抖）")]
    [SerializeField] private float flipHysteresis = 0.05f;

    private Transform playerTf;
    private bool facingRight;

    private void Awake()
    {
        // 1) Auto find Visual Root if not assigned
        if (visualRoot == null)
        {
            // Your hierarchy looks like: Enemy/Visual/body
            Transform t = transform.Find("Visual/body");
            if (t != null)
            {
                visualRoot = t;
            }
            else
            {
                t = transform.Find("Visual");
                if (t != null) visualRoot = t;
            }
        }

        // 2) Find player transform
        Player player = FindObjectOfType<Player>();
        if (player != null)
        {
            playerTf = player.transform;
        }
        else
        {
            // Fallback: try tag if you use it
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) playerTf = go.transform;
        }

        // 3) Initialize facing based on default (since scale is normalized to 1)
        facingRight = faceRightByDefault;

        // Apply once at start
        if (visualRoot != null)
            ApplyFacingAndOffset(facingRight);
    }

    private void LateUpdate()
    {
        if (visualRoot == null || playerTf == null) return;

        // ✅ Use ROOT as reference (stable). Don't use visualRoot because it moves with offset.
        float dx = playerTf.position.x - transform.position.x;

        // 1) Dead zone: keep facing to avoid jitter
        if (Mathf.Abs(dx) <= verticalDeadZone)
        {
            ApplyFacingAndOffset(facingRight);
            return;
        }

        // 2) Hysteresis
        if (facingRight)
        {
            if (dx < -flipHysteresis) facingRight = false;
        }
        else
        {
            if (dx > flipHysteresis) facingRight = true;
        }

        ApplyFacingAndOffset(facingRight);
    }

        private void ApplyFacingAndOffset(bool shouldFaceRight)
    {
        // 保留原本大小，只改正負號
        Vector3 s = visualRoot.localScale;

        float absX = Mathf.Abs(s.x);
        float absY = Mathf.Abs(s.y);
        float absZ = Mathf.Abs(s.z);

        float sign = faceRightByDefault
            ? (shouldFaceRight ? 1f : -1f)
            : (shouldFaceRight ? -1f : 1f);

        s.x = absX * sign;
        s.y = absY;
        s.z = absZ;

        visualRoot.localScale = s;

        // 偏移（如果你不需要，left / right Offset 請保持 Vector3.zero）
        visualRoot.localPosition = shouldFaceRight ? rightOffset : leftOffset;
    }
}
