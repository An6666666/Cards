using UnityEngine;

public static class FaceUtils
{
    // faceRightByDefault=true 代表貼圖原本臉朝右（常見）
    public static void Face(GameObject actor, Transform target, bool faceRightByDefault = true)
    {
        if (actor == null || target == null) return;

        var sr = actor.GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null) return;

        float dx = target.position.x - actor.transform.position.x;
        bool shouldFaceRight = dx > 0f;

        // 原圖朝右：面向右 -> flipX false；面向左 -> flipX true
        sr.flipX = faceRightByDefault ? !shouldFaceRight : shouldFaceRight;
    }

    // 給敵人用：面向玩家（可在 Update/LateUpdate 呼叫）
    public static void Face(GameObject actor, Vector3 targetPos, bool faceRightByDefault = true)
    {
        if (actor == null) return;
        var sr = actor.GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null) return;

        float dx = targetPos.x - actor.transform.position.x;
        bool shouldFaceRight = dx > 0f;

        sr.flipX = faceRightByDefault ? !shouldFaceRight : shouldFaceRight;
    }
}
