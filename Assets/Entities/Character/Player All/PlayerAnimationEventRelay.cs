using UnityEngine;

public class PlayerAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private Player player;

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponentInParent<Player>();
        }
    }

    public void OnTeleportDisappearEvent()
    {
        player?.OnTeleportDisappearEvent();
    }
}
