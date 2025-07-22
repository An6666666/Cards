using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro; // �p��TextMeshPro�A�Y���Ϋh��^UnityEngine.UI.Text

public class CardUI: MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI �Ѧ�")]
    public Text cardNameText;
    public Text costText;
    public Text descriptionText;
    public Image cardImage;
    public Image cardBackground;

    [Header("�����Ѽ�")]
    public CardBase cardData; // �o�i�d�ҹ����� ScriptableObject
    public Transform originalParent;
    private Canvas canvas;     // ���F���T�즲(��U�p�⹫�Ц�m)
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = FindObjectOfType<Canvas>();
    }

    /// <summary>
    /// �]�w���d�P�����
    /// </summary>
    public void SetupCard(CardBase data)
    {
        cardData = data;
        if (cardNameText) cardNameText.text = data.cardName;
        if (costText) costText.text = data.cost.ToString();
        if (descriptionText) descriptionText.text = data.description;
        if (cardImage && data.cardImage) cardImage.sprite = data.cardImage;

        // �]�i�ھ� cardType �Ӥ����I���C��
        switch (data.cardType)
        {
            case CardType.Attack:
                if (cardBackground) cardBackground.color = Color.red;
                break;
            case CardType.Skill:
                if (cardBackground) cardBackground.color = Color.blue;
                break;
            case CardType.Movement:
                if (cardBackground) cardBackground.color = Color.green;
                break;
            case CardType.Relic:
                if (cardBackground) cardBackground.color = Color.yellow;
                break;
        }
    }

    #region �즲�ƥ�
    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        transform.SetParent(FindObjectOfType<Canvas>().transform);
         BattleManager bm = FindObjectOfType<BattleManager>();
        if (bm != null)
        {
            if (cardData.cardType == CardType.Attack)
            {
                bm.StartAttackSelect(cardData);
            }
            else if (cardData.cardType == CardType.Movement)
            {
                bm.UseMovementCard(cardData);
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        BattleManager bm = FindObjectOfType<BattleManager>();
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(eventData.position);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
        bool used = false;
        if (bm != null)
        {
            if (cardData.cardType == CardType.Attack)
            {
                if (hit.collider != null)
                {
                    Enemy e = hit.collider.GetComponent<Enemy>();
                    if (e != null)
                    {
                         used = bm.OnEnemyClicked(e);
                    }
                }
                if (!used)
                {
                    bm.EndAttackSelect();
                }
            }
            else if (cardData.cardType == CardType.Movement)
            {
                if (hit.collider != null)
                {
                    BoardTile tile = hit.collider.GetComponent<BoardTile>();
                    if (tile != null)
                    {
                        used = bm.OnTileClicked(tile);
                    }
                }
                if (!used)
                {
                    bm.CancelMovementSelection();
                }
            }
        }
        if (used)
        {
            Destroy(gameObject);
        }
        else
        {
            ReturnToHand();
        }
    }


    // �����P�G�u������ Enemy �ɤ~Ĳ�o�����î����A�_�h�h�^��P

    #endregion

    // ���ʵP�G�u�n�S��^��P�A�NĲ�o���ʮĪG�î���
    // �Y�P�w����HandPanel, �N�^��P
    private void HandleMovementCard(PointerEventData eventData)
    {
        RaycastHit2D hit2D = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(eventData.position), Vector2.zero);
        // �ˬd�O�_���� HandPanel�]�i�� Tag / �W�� / ���c���ѧO�^
        if (hit2D.collider != null)
        {
            HandPanelMarker handPanel = hit2D.collider.GetComponent<HandPanelMarker>();

            if (handPanel != null)
            {
                // ���ܥ�� HandPanel => �^��P
                ReturnToHand();
                return;
            }
        }

        // �p�G���O HandPanel => Ĳ�o���ʵP
        BattleManager bm = FindObjectOfType<BattleManager>();
        bm.UseMovementCard(cardData); // �i�J���򪺿��Tile�y�{

        // �P��UI����(�קK�d�d�b��P)
        Destroy(gameObject);

    }


    private void ReturnToHand()
    {
        transform.SetParent(originalParent);
        rectTransform.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// �ˬd�d�P�즲�����ɪ����I
    /// </summary>
    private void CheckDropTarget(PointerEventData eventData)
    {
        // ��UI����GraphicRaycast or Physics Raycast (2D)
        // �o��²��: 
        //   - �Y���a��� Enemy UI �W => ����
        //   - �Y��� Board Tile => ����
        //   - �_�h�^���P
        RaycastHit2D hit2D = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(eventData.position), Vector2.zero);
        if (hit2D.collider != null)
        {
            // �ˬd�O�_�O Enemy
            Enemy e = hit2D.collider.GetComponent<Enemy>();
            if (e != null)
            {
                // ���� or �ޯ��ĤH
                UseCardOnEnemy(e);
                return;
            }
        }

        // �Y�W�����S�ǰt => �^��P
        transform.SetParent(originalParent);
        rectTransform.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// ��ĤH�ϥΦ��d
    /// </summary>
    private void UseCardOnEnemy(Enemy enemyTarget)
    {
        // �V BattleManager �o�e "PlayCard" 
        BattleManager bm = FindObjectOfType<BattleManager>();
        if (bm != null)
        {
            bm.PlayCard(cardData);
        }
        // �d�i��|�Q�����P��, UI��������s
    }
}
