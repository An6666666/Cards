using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class EnemySortingController : MonoBehaviour
{
    private Enemy enemy;
    private SpriteRenderer[] cachedSpriteRenderers;
    private int[] cachedSpriteBaseOrders;
    private SortingGroup sortingGroup;
    private int sortingGroupBaseOrder;
    private Vector3 lastWorldPosition;
    private bool hasLastWorldPosition;
    private int lastAppliedOrderOffset;
    private bool hasLastAppliedOrder;

    public void Init(Enemy owner)
    {
        enemy = owner;
    }

    public void HandleAwake()
    {
        CacheSortingComponents();
        UpdateNow();
    }

    public void HandleOnEnable()
    {
        if (cachedSpriteRenderers == null || cachedSpriteRenderers.Length == 0)
        {
            CacheSortingComponents();
        }
        UpdateNow();
    }

    public void HandleLateUpdate()
    {
        Vector3 currentPosition = enemy.transform.position;
        bool positionChanged = !hasLastWorldPosition || (currentPosition - lastWorldPosition).sqrMagnitude > 0.0001f;

        if (enemy.transform.hasChanged || positionChanged || !hasLastAppliedOrder)
        {
            UpdateNow();
        }

        enemy.transform.hasChanged = false;
    }

    public void UpdateNow()
    {
        EnsureSortingComponents();

        int order = enemy.sortingOrderBase + Mathf.RoundToInt(-enemy.transform.position.y * enemy.sortingOrderMultiplier);

        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = sortingGroupBaseOrder + order;
        }

        if (cachedSpriteRenderers != null)
        {
            for (int i = 0; i < cachedSpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = cachedSpriteRenderers[i];
                if (renderer != null)
                {
                    int baseOrder = (cachedSpriteBaseOrders != null && i < cachedSpriteBaseOrders.Length)
                        ? cachedSpriteBaseOrders[i]
                        : 0;
                    renderer.sortingOrder = baseOrder + order;
                }
            }
        }

        lastAppliedOrderOffset = order;
        hasLastAppliedOrder = true;
        lastWorldPosition = enemy.transform.position;
        hasLastWorldPosition = true;
    }

    private void CacheSortingComponents()
    {
        sortingGroup = GetComponentInChildren<SortingGroup>(true);
        sortingGroupBaseOrder = sortingGroup != null ? sortingGroup.sortingOrder : 0;

        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (allRenderers != null && allRenderers.Length > 0)
        {
            List<SpriteRenderer> filteredRenderers = new List<SpriteRenderer>(allRenderers.Length);
            List<int> filteredBaseOrders = new List<int>(allRenderers.Length);

            foreach (SpriteRenderer renderer in allRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.GetComponent<SpriteSortingByParentY>() != null)
                {
                    continue;
                }

                filteredRenderers.Add(renderer);
                filteredBaseOrders.Add(renderer.sortingOrder);
            }

            cachedSpriteRenderers = filteredRenderers.ToArray();
            cachedSpriteBaseOrders = filteredBaseOrders.ToArray();
        }
        else
        {
            cachedSpriteRenderers = System.Array.Empty<SpriteRenderer>();
            cachedSpriteBaseOrders = System.Array.Empty<int>();
        }
    }

    private bool IsRendererOwnedByThis(SpriteRenderer renderer)
    {
        return renderer != null && renderer.transform != null && (renderer.transform == enemy.transform || renderer.transform.IsChildOf(enemy.transform));
    }

    private bool IsSortingGroupOwnedByThis(SortingGroup group)
    {
        if (group == null)
        {
            return false;
        }

        Transform groupTransform = group.transform;
        return groupTransform == enemy.transform || groupTransform.IsChildOf(enemy.transform);
    }

    private void EnsureSortingComponents()
    {
        bool needsRefresh =
            cachedSpriteRenderers == null ||
            cachedSpriteBaseOrders == null ||
            cachedSpriteRenderers.Length == 0 ||
            cachedSpriteBaseOrders.Length != cachedSpriteRenderers.Length;

        if (!needsRefresh)
        {
            for (int i = 0; i < cachedSpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = cachedSpriteRenderers[i];
                if (renderer == null || !IsRendererOwnedByThis(renderer))
                {
                    needsRefresh = true;
                    break;
                }
            }
        }

        if (!needsRefresh && sortingGroup != null && !IsSortingGroupOwnedByThis(sortingGroup))
        {
            needsRefresh = true;
        }

        if (needsRefresh)
        {
            CacheSortingComponents();
        }
        else
        {
            if (sortingGroup == null)
            {
                sortingGroup = GetComponentInChildren<SortingGroup>(true);
            }

            if (sortingGroup != null)
            {
                sortingGroupBaseOrder = sortingGroup.sortingOrder;
            }
        }
    }
}
