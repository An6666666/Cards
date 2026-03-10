using UnityEngine;

public class YingGeAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private YingGe yingGe;

    private void Awake()
    {
        if (yingGe == null)
        {
            yingGe = GetComponentInParent<YingGe>();
        }
    }

    public void OnStoneFeatherTakeOffEvent()
    {
        yingGe?.OnStoneFeatherTakeOffEvent();
    }
}
