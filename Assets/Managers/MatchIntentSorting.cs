using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class MatchIntentSorting : MonoBehaviour
{
    public SpriteRenderer referenceIcon; // 拖 Enemy 裡的 IntentIcon 進來

    void LateUpdate()
    {
        if (referenceIcon == null) return;

        var mr = GetComponent<MeshRenderer>();
        mr.sortingLayerID = referenceIcon.sortingLayerID;
        mr.sortingOrder   = referenceIcon.sortingOrder + 1; // 比 icon 再高一層
    }
}
