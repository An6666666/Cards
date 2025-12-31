using UnityEngine;
using TMPro;

public class EnemyIntentController : MonoBehaviour
{
    private Enemy enemy;
    private Player cachedPlayer;
    private Vector2Int lastKnownPlayerPos;
    private bool hasLastKnownPlayerPos = false;

    public void Init(Enemy owner)
    {
        enemy = owner;
    }

    public void HandleAwake()
    {
        cachedPlayer = FindObjectOfType<Player>();
        if (cachedPlayer != null)
        {
            lastKnownPlayerPos = cachedPlayer.position;
            hasLastKnownPlayerPos = true;
            DecideNextIntent(cachedPlayer);
        }
    }

    public void HandleLateUpdate()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = FindObjectOfType<Player>();
            if (cachedPlayer != null)
            {
                lastKnownPlayerPos = cachedPlayer.position;
                hasLastKnownPlayerPos = true;
                DecideNextIntent(cachedPlayer);
            }
        }
        else
        {
            Vector2Int currentPlayerPos = cachedPlayer.position;
            if (!hasLastKnownPlayerPos || currentPlayerPos != lastKnownPlayerPos)
            {
                lastKnownPlayerPos = currentPlayerPos;
                hasLastKnownPlayerPos = true;
                DecideNextIntent(cachedPlayer);
            }
        }
    }

    public void DecideNextIntent(Player player)
    {
        if (enemy == null) return;
        if (player == null)
        {
            UpdateIntentIcon();
            return;
        }

        if (enemy.frozenTurns > 0 || enemy.buffs.stun > 0)
        {
            enemy.nextIntent.type = EnemyIntentType.Idle;
            enemy.nextIntent.value = 0;
            UpdateIntentIcon();
            return;
        }

        if (enemy.Movement.IsPlayerInRange(player))
        {
            enemy.nextIntent.type = EnemyIntentType.Attack;
            enemy.nextIntent.value = enemy.CalculateAttackDamage();
            UpdateIntentIcon();
            return;
        }

        if (enemy.canMove)
        {
            enemy.nextIntent.type = EnemyIntentType.Move;
            enemy.nextIntent.value = 0;
        }
        else
        {
            enemy.nextIntent.type = EnemyIntentType.Idle;
            enemy.nextIntent.value = 0;
        }

        UpdateIntentIcon();
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
}
