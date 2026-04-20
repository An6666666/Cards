using UnityEngine;
using TMPro;

public class EnemyIntentController : MonoBehaviour
{
    private Enemy enemy;
    private Enemy subscribedEnemy;
    private Player cachedPlayer;
    private Vector2Int lastKnownPlayerPos;
    private bool hasLastKnownPlayerPos = false;
    private int lastKnownFrozenTurns = int.MinValue;
    private int lastKnownBlock = int.MinValue;

    public void Init(Enemy owner)
    {
        if (ReferenceEquals(enemy, owner))
        {
            BindStatusListener();
            return;
        }

        UnbindStatusListener();
        enemy = owner;
        BindStatusListener();
    }

    public void HandleAwake()
    {
        if (TryEnsureCachedPlayer())
        {
            RecalculateIntent(cachedPlayer);
        }
    }

    public void HandleLateUpdate()
    {
        if (cachedPlayer == null)
        {
            if (TryEnsureCachedPlayer())
            {
                RecalculateIntent(cachedPlayer);
            }
        }
        else
        {
            Vector2Int currentPlayerPos = cachedPlayer.position;
            if (!hasLastKnownPlayerPos || currentPlayerPos != lastKnownPlayerPos)
            {
                lastKnownPlayerPos = currentPlayerPos;
                hasLastKnownPlayerPos = true;
                RecalculateIntent(cachedPlayer);
            }
        }

        if (enemy != null)
        {
            bool statusChanged =
                enemy.frozenTurns != lastKnownFrozenTurns ||
                enemy.block != lastKnownBlock;

            if (statusChanged)
            {
                RecalculateIntent(cachedPlayer);
            }
        }
    }

    public void DecideNextIntent(Player player)
    {
        if (enemy == null)
        {
            return;
        }

        enemy.DecideNextIntent(player);
        CacheStatusCounters();
    }

    public void UpdateIntentIcon()
    {
        if (enemy == null) return;
        if (enemy.intentIconRenderer == null)
            return;

        Sprite icon = null;
        switch (enemy.nextIntent.type)
        {
            case EnemyIntentType.Attack:
                icon = enemy.intentAttackSprite;
                break;
            case EnemyIntentType.Move:
                icon = enemy.intentMoveSprite;
                break;
            case EnemyIntentType.Defend:
                icon = enemy.intentDefendSprite;
                break;
            case EnemyIntentType.Skill:
                icon = enemy.intentSkillSprite != null ? enemy.intentSkillSprite : enemy.intentAttackSprite;
                break;
            default:
                icon = enemy.intentIdleSprite;
                break;
        }

        if (enemy.ForceHideIntent)
        {
            enemy.intentIconRenderer.enabled = false;
            if (enemy.intentValueText != null)
                enemy.intentValueText.gameObject.SetActive(false);
            return;
        }

        enemy.intentIconRenderer.sprite = icon;
        enemy.intentIconRenderer.enabled = (icon != null);

        if (enemy.intentValueText != null)
        {
            if (enemy.nextIntent.type == EnemyIntentType.Attack && enemy.nextIntent.value > 0)
            {
                enemy.intentValueText.gameObject.SetActive(true);
                enemy.intentValueText.text = enemy.nextIntent.value.ToString();
            }
            else
            {
                enemy.intentValueText.gameObject.SetActive(false);
            }
        }
    }

    private void RecalculateIntent(Player player)
    {
        if (enemy == null) return;

        enemy.DecideNextIntent(player);
        CacheStatusCounters();
    }

    private void CacheStatusCounters()
    {
        if (enemy == null) return;

        lastKnownFrozenTurns = enemy.frozenTurns;
        lastKnownBlock = enemy.block;
    }

    private bool TryEnsureCachedPlayer()
    {
        if (cachedPlayer != null)
        {
            return true;
        }

        cachedPlayer = FindObjectOfType<Player>();
        if (cachedPlayer == null)
        {
            return false;
        }

        lastKnownPlayerPos = cachedPlayer.position;
        hasLastKnownPlayerPos = true;
        return true;
    }

    private void HandleEnemyStatusChanged()
    {
        if (enemy == null)
        {
            return;
        }

        TryEnsureCachedPlayer();
        RecalculateIntent(cachedPlayer);
    }

    private void BindStatusListener()
    {
        if (enemy == null || subscribedEnemy == enemy)
        {
            return;
        }

        UnbindStatusListener();
        enemy.OnStatusChanged += HandleEnemyStatusChanged;
        subscribedEnemy = enemy;
    }

    private void UnbindStatusListener()
    {
        if (subscribedEnemy == null)
        {
            return;
        }

        subscribedEnemy.OnStatusChanged -= HandleEnemyStatusChanged;
        subscribedEnemy = null;
    }

    private void OnEnable()
    {
        BindStatusListener();
    }

    private void OnDisable()
    {
        UnbindStatusListener();
    }
}
