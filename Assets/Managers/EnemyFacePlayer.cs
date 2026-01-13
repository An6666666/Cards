using UnityEngine;

public class EnemyFacePlayer : MonoBehaviour
{
    [Header("Which part to flip")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool faceRightByDefault = true;

    [Header("Position Offset")]
    [SerializeField] private Vector3 leftOffset = Vector3.zero;
    [SerializeField] private Vector3 rightOffset = Vector3.zero;

    [Header("Stability")]
    [Tooltip("dx 小於這個值視為正上/正下，不改面向（避免抖動）")]
    [SerializeField] private float verticalDeadZone = 0.15f;

    [Tooltip("方向改變至少要超過這個差值才翻面（額外防抖）")]
    [SerializeField] private float flipHysteresis = 0.05f;

    private Transform playerTf;
    private bool facingRight = true;  // 記住上一次方向

    private void Awake()
    {
        if (visualRoot == null)
        {
            var t = transform.Find("Visual");
            if (t != null) visualRoot = t;
        }

        var player = FindObjectOfType<Player>();
        if (player != null) playerTf = player.transform;

        // 初始化 facingRight：用目前 visual 的 scale 判斷
        if (visualRoot != null)
            facingRight = visualRoot.localScale.x >= 0f;
    }

    private void LateUpdate()
    {
        if (visualRoot == null || playerTf == null) return;

        float dx = playerTf.position.x - visualRoot.position.x;

        // 1) 正上/正下：維持原方向（不翻）
        if (Mathf.Abs(dx) <= verticalDeadZone)
        {
            ApplyFacingAndOffset(facingRight);
            return;
        }

        // 2) 加一點 hysteresis，避免剛好在界線附近跳來跳去
        //    - 如果目前朝右，要翻到左，必須 dx < -flipHysteresis
        //    - 如果目前朝左，要翻到右，必須 dx > +flipHysteresis
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
        // 翻轉 Visual（整套動畫一起翻）
        var s = visualRoot.localScale;
        float absX = Mathf.Abs(s.x);

        // 注意：localScale.x 的正負不一定等於「面向右」
        // faceRightByDefault=true 表示 absX(正) = 面向右
        float sign = faceRightByDefault
            ? (shouldFaceRight ? 1f : -1f)
            : (shouldFaceRight ? -1f : 1f);

        s.x = absX * sign;
        visualRoot.localScale = s;

        // 偏移
        visualRoot.localPosition = shouldFaceRight ? rightOffset : leftOffset;
    }
}
