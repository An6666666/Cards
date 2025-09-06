using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class Player : MonoBehaviour
{
    [Header("�ݩ�")]
    public int maxHP = 50;
    public int currentHP;
    public int energy = 5;
    public int block = 0;
    public int gold = 0;

    [Header("�d�P�޲z")]
    public List<CardBase> deck = new List<CardBase>();
    private List<CardBase> hand = new List<CardBase>();
    public List<CardBase> discardPile = new List<CardBase>();
    public List<CardBase> relics = new List<CardBase>();  // �����쪺��

    [Header("�^�X������")]
    public bool hasDiscardedThisTurn = false;  // �O�_��L�P
    public int discardCountThisTurn = 0;       // ��P��
    public int attackUsedThisTurn = 0;         // ���^�X�ϥΪ������P��

    // ²���������a��m(�Y��2D����a��)
    public Vector2Int position = new Vector2Int(0, 0);

    // Buff���c
    public PlayerBuffs buffs = new PlayerBuffs();

    public List<CardBase> Hand => hand;

    private void Awake()
    {
        currentHP = maxHP;
    }

    /// <summary>
    /// �^�X�}�l�ɽե�
    /// </summary>
    public void StartTurn()
    {
        block = 0;  // ���C���ݨD�A�Y�OSLAY THE SPIRE����A�^�X�����|�M��block
        energy = 5; // ���m��q(�]�i���C���W�h)
        hasDiscardedThisTurn = false;
        discardCountThisTurn = 0;
        attackUsedThisTurn = 0;
        DrawCards(5); // �Ҧp��5�i
        BattleManager bm = FindObjectOfType<BattleManager>();
        bm.RefreshHandUI();

        // �^�X�}�lbuff�B�z (�p movementCostModify�k�s, damageTakenRatio���m��)
        buffs.OnTurnStartReset();
        // �Y���� "�\�����" => OnTurnStart
        foreach (CardBase r in relics)
        {
            if (r is Relic_KuMuShuQian kk)
            {
                kk.OnTurnStart(this);
            }
        }
    }

    /// <summary>
    /// �^�X�����ɡ]�b BattleManager �̩I�s�^
    /// </summary>
    public void EndTurn()
    {
        // �Y�ϥί}�]­ => OnEndTurn
        foreach (CardBase r in relics)
        {
            if (r is Relic_PoMoXiao pmx)
            {
                pmx.OnEndTurn(this, attackUsedThisTurn);
            }
        }

        // ���]�u½�c���d�v�ݭn�b�^�X�����H����2 => 
        if (buffs.needRandomDiscardAtEnd > 0)
        {
            int n = buffs.needRandomDiscardAtEnd;
            buffs.needRandomDiscardAtEnd = 0;
            // �H���� n �i
            for (int i = 0; i < n; i++)
            {
                if (hand.Count > 0)
                {
                    int idx = Random.Range(0, hand.Count);
                    CardBase c = hand[idx];
                    hand.RemoveAt(idx);
                    discardPile.Add(c);
                    hasDiscardedThisTurn = true;
                    discardCountThisTurn++;
                }
            }
        }

        // �^�X�����A�N damageTakenRatio ��_1.0f �άݧA�]�p
        // buff���������B�z
        buffs.OnTurnEndReset();
    }

    /// <summary>
    /// ����q
    /// </summary>
    public void UseEnergy(int cost)
    {
        Debug.Log($"UseEnergy: deducting {cost} energy. Energy before={energy}");
        // �Ybuff.nextAttackCostModify��buff.movementCostModify���v�T�A���b PlayCard �ɳB�z
        energy -= cost;
        if (energy < 0) energy = 0;
    }

    /// <summary>
    /// �̷�buff�p��̲ק����ˮ`�A�î��Ӥ@���ʥ[��
    /// </summary>
    public int CalculateAttackDamage(int baseDamage)
    {
        int dmg = baseDamage + buffs.nextAttackPlus + buffs.nextTurnAllAttackPlus;
        if (dmg < 0) dmg = 0;
        buffs.nextAttackPlus = 0;
        return dmg;
    }

    /// <summary>
    /// �W�[���
    /// </summary>
    public void AddBlock(int amount)
    {
        block += amount;
        // Ĳ�o�Y�ǿ��ˬd(�p���q��)
        foreach (CardBase r in relics)
        {
            if (r is Relic_ZiDianJiao z)
            {
                z.OnAddBlock(this, amount);
            }
        }
    }

    /// <summary>
    /// ����(�Ҽ{buff: damageTakenRatio)
    /// </summary>
    public void TakeDamage(int dmg)
    {
        int reduced = dmg - buffs.meleeDamageReduce;
        if (reduced < 0) reduced = 0;
        float realDmgF = reduced * buffs.damageTakenRatio;
        int realDmg = Mathf.CeilToInt(realDmgF);

        int remain = realDmg - block;
        if (remain > 0)
        {
            block = 0;
            currentHP -= remain;
            if (currentHP <= 0)
            {
                currentHP = 0;
                // Player Die
            }
        }
        else
        {
            block -= realDmg;
        }
    }

    /// <summary>
    /// ��������(�L��block) - ���۴ݩίS���]�p
    /// </summary>
    public void TakeDamageDirect(int dmg)
    {
        currentHP -= dmg;
        if (currentHP <= 0) currentHP = 0;
    }

    public void AddGold(int amount)
    {
        gold += amount;
    }

    /// <summary>
    /// �� n �i�P
    /// </summary>
    public void DrawCards(int n)
    {
        for (int i = 0; i < n; i++)
        {
            if (deck.Count == 0)
            {
                // �P�w�� => ���լ~�P
                ReshuffleDiscardIntoDeck();
                // �Y�٬O�S�d => break
                if (deck.Count == 0) break;
            }
            CardBase top = deck[0];
            deck.RemoveAt(0);
            hand.Add(top);
        }
        FindObjectOfType<BattleManager>()?.RefreshHandUI();
    }

    public void DrawNewHand(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (deck.Count == 0)
            {
                if (discardPile.Count > 0)
                {
                    deck.AddRange(discardPile);
                    discardPile.Clear();
                    ShuffleDeck();
                }
                else
                {
                    break;
                }
            }

            if (deck.Count > 0)
            {
                CardBase drawn = deck[0];
                deck.RemoveAt(0);
                hand.Add(drawn);
            }
        }
    }


    public void ReshuffleDiscardIntoDeck()
    {
        // �X��
        deck.AddRange(discardPile);
        discardPile.Clear();
        // �~�P
        ShuffleDeck();
    }

    public void ShuffleDeck()
    {
        System.Random rnd = new System.Random();
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int r = rnd.Next(0, i + 1);
            CardBase temp = deck[i];
            deck[i] = deck[r];
            deck[r] = temp;
        }
    }

    /// <summary>
    /// �� n �i�P (²�ƳB�z: �q��P�̫�X�i���)
    /// </summary>
    public void DiscardCards(int n)
    {
        if (hand.Count < n) n = hand.Count;
        for (int i = 0; i < n; i++)
        {
            CardBase c = hand[hand.Count - 1];
            hand.RemoveAt(hand.Count - 1);
            discardPile.Add(c);
        }
        hasDiscardedThisTurn = true;
        discardCountThisTurn += n;

        // ���j��²Ĳ�o
        foreach (CardBase r in relics)
        {
            if (r is Relic_LunHuiZhuJian zhujian)
            {
                zhujian.OnPlayerDiscard(this, n);
            }
        }
    }

    /// <summary>
    /// ��1�i�P (���U�ɱٵ��ϥ�)
    /// </summary>
    public bool DiscardOneCard()
    {
        if (hand.Count == 0) return false;
        CardBase c = hand[hand.Count - 1];
        hand.RemoveAt(hand.Count - 1);
        discardPile.Add(c);

        hasDiscardedThisTurn = true;
        discardCountThisTurn++;

        // Ĳ�o���j��²
        foreach (CardBase r in relics)
        {
            if (r is Relic_LunHuiZhuJian zhujian)
            {
                zhujian.OnPlayerDiscard(this, 1);
            }
        }

        return true;
    }

    /// <summary>
    /// �ˬd�O�_�p�e��P(���j���@�޵���)
    /// ���ܽd������ hasDiscardedThisTurn ������
    /// </summary>
    public bool CheckDiscardPlan()
    {
        return hasDiscardedThisTurn;
    }

    /// <summary>
    /// ���ʦܫ��w��l(�Y��2D����)
    /// </summary>
    public void MoveToPosition(Vector2Int targetGridPos)
    {
        /// 1. 取得棋盤管理器
        Board board = FindObjectOfType<Board>();
        if (board == null)
        {
            Debug.LogWarning("Board not found!");
            return;
        }

        // 2. 檢查是否有敵人佔據該格
        if (board.IsTileOccupied(targetGridPos))
        {
            Debug.Log("Cannot move: tile occupied by enemy.");
            return;
        }

        // 3. 更新邏輯座標
        position = targetGridPos;

        // 4. 拿到這個格子的 BoardTile
        BoardTile tile = board.GetTileAt(targetGridPos);
        if (tile == null)
        {
            Debug.LogWarning($"No tile at {targetGridPos}");
            return;
        }

        // 5. 將玩家的世界座標設成該格子的 transform.position
        transform.position = tile.transform.position;
    }


    /// <summary>
    /// ���� (�Y�a���o�i��)
    /// </summary>
    public void TeleportToPosition(Vector2Int targetPos)
    {
        Board board = FindObjectOfType<Board>();
        if (board != null && board.IsTileOccupied(targetPos))
        {
            Debug.Log("Cannot teleport: tile occupied by enemy.");
            return;
        }

        position = targetPos;
        transform.position = new Vector3(targetPos.x, targetPos.y, 0f);
    }
}

/// <summary>
/// ���a���W��Buff�P�^�X���A
/// </summary>
[System.Serializable]
public class PlayerBuffs
{
    public float damageTakenRatio = 1.0f;   // ���˭��v(�����i =0.5)
    public int nextAttackPlus = 0;         // �U�������B�~�ˮ`
    public int nextDamageTakenUp = 0;      // �ĤH�U������+X (�]�i��b Enemy ��)
    public int nextAttackCostModify = 0;   // �U�������d�O�μW��
    public int movementCostModify = 0;     // ���^�X�Ҧ����ʵP�O�μW��
    public int nextTurnDrawChange = 0;     // �U�^�X��P�W��
    public int needRandomDiscardAtEnd = 0; // �^�X�����H����P
    public int meleeDamageReduce = 0;      // ��Զˮ`�T�w���
    public int weak = 0;                   // ��z�^�X��
    public int stun = 0;                   // �w�t�^�X(�L�k���)
    public int nextTurnAllAttackPlus = 0;  // �U�^�X�Ҧ�����+X

    /// <summary>
    /// �^�X�}�l���m�έp��
    /// </summary>
    public void OnTurnStartReset()
    {
        // �Ҧp�W�@�^�X���� nextTurnAllAttackPlus �i�b�o�^�X�ͮ�
        // damageTakenRatio�^�k1.0f? �����p
        // �o�̶ȥܽd
        if (stun > 0) stun--;

        // ��z�]����
        if (weak > 0) weak--;

        // nextAttackPlus �u�w��"�U�@��"����, �Ϋ�i�k0
        // �p�G�A�n�^�X�}�l�N�k0, �]�i

        // movementCostModify �i�k0
        movementCostModify = 0;
        // nextAttackCostModify �k0
        nextAttackCostModify = 0;
    }

    /// <summary>
    /// �^�X�����B�z
    /// </summary>
    public void OnTurnEndReset()
    {
        // damageTakenRatio �Y�u�b���^�X�ͮġA�^�X�����n���m
        damageTakenRatio = 1.0f;
        // nextAttackPlus �]�i�M�s (�Y�u�ͮĤ@��)
        // needRandomDiscardAtEnd �b�~���B�z���k0
    }
}

