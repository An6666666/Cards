using UnityEngine;

/// <summary>
/// Forwards tile clicks to BattleManager unless a UI element is under the pointer.
/// </summary>
public class BoardTileSelectable : MonoBehaviour
{
    private void OnMouseDown()
    {
        if (PointerUiBlocker.IsPointerBlockedByUi())
            return;

        BattleManager bm = FindObjectOfType<BattleManager>();
        if (bm) bm.OnTileClicked(GetComponent<BoardTile>());
    }
}
