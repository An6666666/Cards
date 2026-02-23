using System;                                // 雿輻 System ?賢?蝛粹?嚗?游???蝑蝷???
using System.Collections;                   // 雿輻?????? IEnumerator嚗?蝔?嚗?
using System.Collections.Generic;           // 雿輻瘜???嚗ist<T> 蝑?
using UnityEngine;                          // 雿輻 Unity 撘??詨? API
using UnityEngine.UI;                       // 雿輻 Unity UI嚗ext?utton 蝑?

[Serializable]                              // 璅?甇日??臬???嚗靘踹 Inspector 憿舐內/蝺刻摩
public class EnemySpawnConfig               // ?萎犖???身摰???Prefab + ?賊?嚗?
{
    public Enemy enemyPrefab;               // 閬????萎犖?ˊ??
    public int count = 1;                   // ??甇斗鈭箸???身 1
}

public class BattleManager : MonoBehaviour  // ?圈洛蝞∠??剁??游?圈洛?葉璅?穿???湔?拐辣嚗?
{
    [Header("Core References")]             // Inspector ?憛?憿??詨???
    public Player player;                   // ?港??摰嗥隞嗅???
    [NonSerialized] public List<Enemy> enemies = new List<Enemy>();
    // ?港???鈭箇??”嚗?摨????踹?蝺刻摩?刻蕭頩?runtime ?拐辣嚗?

    public Board board;                     // 璉 Board ?拐辣嚗恣?摮?

    [Header("Cards & UI")]                  // Inspector ?憛?憿??∠???UI
    public GameObject cardPrefab;           // ?∠? UI ??鋆賜
    public Transform handPanel;             // UI嚗??????嗥隞?
    public Transform deckPile;              // UI嚗?摨恍＊蝷箏????虜?芷＊蝷箸摮?
    public Transform discardPile;           // UI嚗???憿舐內????虜?芷＊蝷箸摮?
    public Text energyText;                 // UI嚗＊蝷箇摰嗉?? Text
    [SerializeField] private Button endTurnButton;
    // UI嚗???????蝘?雿??Inspector ??嚗?

    [Header("Initial Setup")]               // Inspector ?憛?憿???閮剖?
    public List<EnemySpawnConfig> enemySpawnConfigs = new List<EnemySpawnConfig>();
    // ???圈洛?剝??鈭箇???蝵殷??臬 Inspector 閮剖?嚗?

    public Vector2Int playerStartPos = Vector2Int.zero;
    // ?拙振?冽??支??絲憪摮漣璅??身 0,0嚗?
    [Header("Tutorial")]
    [SerializeField] private TutorialBattleController tutorialController;
    private readonly BattleStateMachine stateMachine = new BattleStateMachine();
    // ?圈洛???嚗?鞎祉恣??PlayerTurn/EnemyTurn/Victory/Defeat 蝑???

    [Header("Guaranteed Cards")]            // Inspector ?憛?憿?靽??∴?靘?靽?銝撘萇宏?嚗?
    public Move_YiDong guaranteedMovementCard;
    // 靽??策?拙振??撘萸宏??見?選?ScriptableObject嚗?

    private Move_YiDong guaranteedMovementCardInstance;
    // ?券?圈洛銝剔?甇?蝙?函???霅宏??祕靘?敺見??Clone嚗?

    [Header("Rewards")]                     // Inspector ?憛?憿??啣???
    public List<CardBase> allCardPool = new List<CardBase>();
    // ???賭??箏??拍??萇??⊥??”

    public RewardUI rewardUIPrefab;         // ?敺＊蝷箇??萇? UI ?ˊ??

    [Header("Timings")]                     // Inspector ?憛?憿????賊?閮剖?
    public float cardUseDelay = 0f;         // 雿輻?∠?敺?閫???∠?鈭??辣?脩???

    private bool battleStarted = false;     // ?圈洛?臬撌脩???嚗?憪?銝孛?澆?鞎摰?

    private BattleEncounterLoader encounterLoader;      // 鞎痊頛?圈洛?剝?/???萎犖/?貉絲憪??嗅
    private BattleTurnController turnController;        // 蝞∠???瘚?嚗摰嗅????萎犖??嚗??批??
    private BattleHandUIController handUIController;    // 蝞∠??? UI ??＊蝷箇??批??
    private MovementSelectionController movementSelectionController;
    // 蝞∠??宏???潸?蝘餃??摩??嗅

    private AttackSelectionController attackSelectionController;
    // 蝞∠?????鈭箇璅??批??

    private BattleRewardController rewardController;
    private BattleRuntimeContext runtimeContext;
    private IEnemyQueryService enemyQueryService;
    private PlayerDeckController playerDeckController;
    // 蝞∠??萎犖甇颱滿閮???拍???UI ??嗅

    public bool BattleStarted => battleStarted;
    // ?祇??航?撅祆改??桀??圈洛?臬撌脤?憪?

    public BattleStateMachine StateMachine => stateMachine;
    // ?祇??航?撅祆改???憭摮??圈洛???嚗?憒???瘀?
    public TutorialBattleController TutorialController => tutorialController;
    public bool IsProcessingEnemyTurnStart => turnController != null && turnController.IsProcessingEnemyTurnStart;
    // ?臬甇????菜????嚗?靘策?嗡?蝟餌絞?斗???剁?

    public bool IsCardInteractionLocked => handUIController != null && handUIController.IsCardInteractionLocked;
    public BattleRuntimeContext RuntimeContext => runtimeContext;
    public IEnemyQueryService EnemyQueryService => enemyQueryService;
    // ?臬?桀??∠?鈭?鋡恍?摰?蝯?CardUI ?郊鈭?甈??剁?

    void Awake()
    {
        ResolveTutorialController();
        // 靘?桀? Run 蝭暺??剝?鞈?嚗捱摰?圈洛?臬??飛璅∪?
        InitializeControllers();                            // ????蝔桀??批?剁?Hand?ovement?ttack?eward?ncounter?urn嚗?
        handUIController.SetEndTurnButtonInteractable(false);
        // 銝???????????身?箔??舫?嚗??圈洛甇??????嚗?

        encounterLoader.LoadEncounterFromRunManager();
        ConfigureTutorialForActiveEncounter();
        // 敺?RunManager ?桀?蝭暺??仿?身摰?憛?enemySpawnConfigs
    }
    private void ResolveTutorialController()
    {
        if (tutorialController != null)
        {
            return;
        }

        tutorialController = GetComponentInChildren<TutorialBattleController>(true);
        if (tutorialController == null)
        {
            tutorialController = FindObjectOfType<TutorialBattleController>();
        }
    }
    private void ConfigureTutorialForActiveEncounter()
    {
        ResolveTutorialController();

        if (tutorialController == null)
        {
            return;
        }
        // 瘝??飛?批?典停銝????圈洛蝬剜?銝?祆芋撘?

        RunEncounterDefinition encounter = RunManager.Instance?.ActiveNode?.Encounter;
        bool enableTutorial = encounter != null && encounter.UseTutorialBattle;
        TutorialBattleDefinition definition = enableTutorial ? encounter.TutorialBattleDefinition : null;
        // ?梢??蝢拇捱摰?血??冽?摮賂?銝血葆?交?湔擛亥?雿輻??摮詨摰?
        tutorialController.ConfigureForBattle(enableTutorial, definition);
        // ??Battle ????????蝵殷?敺? Encounter/Turn ?批?典?湔霈 IsActive
    }
    void Start()
    {
        StartCoroutine(encounterLoader.GameStartRoutine());
        // ???圈洛韏瑕???嚗?拙振韏瑕????????萎犖 ?????啁摰嗅???
    }

    void Update()
    {
        stateMachine.Update();                              // 瘥??湔?圈洛???嚗??嗅????璈??瑁??摩嚗?

        if (!battleStarted) return;                         // ?交擛亙??芷?憪?銝脰????斗

        enemies.RemoveAll(e => e == null);
        // 蝘駁?”銝剖歇鋡?Destroy ?鈭綽??踹? Null 撘嚗?

        bool allDead = enemies.Count == 0 || enemies.TrueForAll(e => e.currentHP <= 0);
        // allDead 璇辣嚗?) ?萎犖?賊???0嚗? 2) ??鈭?currentHP <= 0

        if (allDead && !(stateMachine.Current is VictoryState))
        {
            stateMachine.ChangeState(new VictoryState(this));
            // ?亙?冽鈭箸香鈭∴?銝???臬??拍?????????VictoryState
        }

        if (player.currentHP <= 0 && !(stateMachine.Current is DefeatState))
        {
            stateMachine.ChangeState(new DefeatState(this));
            // ?亦摰嗉??飛 0嚗??桀?銝憭望??????????DefeatState
        }
    }

    public void StartPlayerTurn()
    {
        turnController.StartPlayerTurn();                   // 憪? BattleTurnController ???摰嗅???憪?
    }

    public void EndPlayerTurn()
    {
        turnController.EndPlayerTurn();                     // 憪? BattleTurnController ???摰嗅?????????萎犖????
    }

    public IEnumerator EnemyTurnCoroutine()
    {
        return turnController.EnemyTurnCoroutine();         // ??BattleTurnController ???萎犖????
    }

    public void UseMovementCard(CardBase movementCard)
    {
        movementSelectionController.UseMovementCard(movementCard);
        // 憪? MovementSelectionController嚗?憪宏???潭?蝔?
    }

    public void CancelMovementSelection()
    {
        movementSelectionController.CancelMovementSelection();
        // ???桀??宏??潭?蝔???擃漁??豢?閮?
    }
    
    public bool OnTileClicked(BoardTile tile)
    {
        return movementSelectionController.OnTileClicked(tile);
        // 璉?澆?鋡恍???嚗漱蝯?MovementSelectionController ??嚗??怨絲憪?豢??宏??潘?
    }

    public void StartAttackSelect(CardBase attackCard)
    {
        if (!TryCreateAttackSelectionRequest(attackCard, out AttackSelectionRequest request))
        {
            return;
        }

        StartAttackSelect(request);
    }

    public void StartAttackSelect(AttackSelectionRequest request)
    {
        attackSelectionController.StartAttackSelect(request);
    }

    public bool OnEnemyClicked(Enemy e)
    {
        return attackSelectionController.OnEnemyClicked(e);
        // ?萎犖鋡恍???嚗漱??AttackSelectionController ?斗?臬?箸???璅蒂?瑁???
    }

    public void UpdateAttackHover(Vector2 worldPosition)
    {
        attackSelectionController.UpdateAttackHover(worldPosition);
        // ??餅??⊥?嚗?唳?曌?皞璅?擃漁????
    }

    public void EndAttackSelect()
    {
        attackSelectionController.EndAttackSelect();
        // 憭?臬?怠撥?嗥???????擃漁嚗?
    }

    private bool TryCreateAttackSelectionRequest(CardBase attackCard, out AttackSelectionRequest request)
    {
        request = default;

        if (attackCard == null || player == null || player.Hand == null)
        {
            return false;
        }

        if (!player.Hand.Contains(attackCard))
        {
            return false;
        }

        int finalCost = CalculateCardEnergyCost(attackCard);
        if (player.energy < finalCost)
        {
            Debug.Log("Not enough energy");
            return false;
        }

        request = new AttackSelectionRequest(attackCard, finalCost, Time.unscaledTime);
        return true;
    }

    private int CalculateCardEnergyCost(CardBase cardData)
    {
        if (cardData == null || player == null)
        {
            return 0;
        }

        int finalCost = cardData.cost + player.GetCardCostModifier(cardData);

        if (cardData.cardType == CardType.Attack)
        {
            finalCost += player.buffs.nextAttackCostModify;
        }

        if (cardData.cardType == CardType.Movement)
        {
            finalCost += player.buffs.movementCostModify;
        }

        return Mathf.Max(0, finalCost);
    }

    public void OnEnemyDefeated(Enemy e)
    {
        rewardController.OnEnemyDefeated(e);
        // ?鈭箸香鈭⊥??澆嚗? BattleRewardController 蝝舐??捏?貉??馳
    }

    public void ShowVictoryRewards()
    {
        rewardController.ShowVictoryRewards();
        // ?圈洛???恬?憿舐內? UI嚗??馳 + ?詨?嚗?
    }

    /// <summary>
    /// ?拙嚗??祥?具銵??????UI
    /// </summary>
    public bool PlayCard(CardBase cardData)
    {
        if (!(stateMachine.Current is PlayerTurnState)) return false;
        // ?芣??函摰嗅????賢???血?憭望?

        if (cardData == null) return false;                 // ?喳?∠?鞈??箇征嚗?亙仃??
        if (player == null || player.Hand == null) return false;
        // ?拙振????銵其?摮嚗?亙仃??

        if (!player.Hand.Contains(cardData)) return false;
        // ?撐?∩??冽??嚗?賭??芸??對?嚗?亙仃??

        int finalCost = CalculateCardEnergyCost(cardData);

        if (player.energy < finalCost)
        {
            Debug.Log("Not enough energy");                // ?賡?銝雲嚗瘜蝙?冽迨??
            return false;
        }

        if (cardData is Skill_ZhiJiao && !player.HasExhaustableCardInHand(cardData))
        {
            Debug.Log("No exhaustable cards in hand for ?脩?");
            return false;
        }

        Enemy target = enemies.Find(e => e != null && e.currentHP > 0);
        // ?ㄐ蝪∪?豢?蝚砌???摮暑?鈭箔??箇璅??亙???芸楛???格?嚗?
        if (target != null)
        {
            FaceUtils.Face(player.gameObject, target.transform);        // ???
        }
        List<ElementType> targetElementsBefore = null;
        if (target != null)
        {
            targetElementsBefore = new List<ElementType>(target.GetElementTags());
        }
        cardData.ExecuteEffect(player, target);
        // ?瑁??∠???嚗?⊥頨怠祕雿?憓?霅瑞???瑕拿?????嚗?
        List<ElementType> targetElementsAfter = null;
        if (target != null)
        {
            targetElementsAfter = new List<ElementType>(target.GetElementTags());
        }
        if (cardData.cardType == CardType.Attack && player.buffs.nextAttackPlus > 0)
        {
            player.buffs.nextAttackPlus = 0;               // ?冽?銝甈⊥????嚗? nextAttackPlus 甇賊
        }

        // ?交??葉隞甇文嚗?蝘餉璉???
        bool isGuaranteedMovement = IsGuaranteedMovementCard(cardData);
        // ?斗?撐?⊥?衣??霅宏???

        bool removedFromHand = player.Hand.Remove(cardData);
        // ????銝剔宏?日撐?∴??亙??剁?

        if (removedFromHand)
        {
            player.ClearCardCostModifier(cardData);
            // ??????蝘駁???斗迨?∠?鞎餌靽格迤蝝??

            if (isGuaranteedMovement)
            {
                RemoveGuaranteedMovementCardFromPiles();
                // ?交迨?⊥靽?蝘餃??∴?敺?摨怨?璉??葉蝘駁???YiDong嚗??銴?
            }
            else if (cardData.exhaustOnUse)
            {
                player.ExhaustCard(cardData);
                // 銝甈⊥批嚗蝙?典???Exhaust ?嚗擛乩葉銝?餈?嚗?
            }
            else
            {
                player.discardPile.Add(cardData);
                // 銝?砍嚗蝙?典???拙振璉???
            }
        }
        else if (isGuaranteedMovement)
        {
            RemoveGuaranteedMovementCardFromPiles();
            // 璆萇垢?瘜???鋆⊥???堆?雿??臭?霅宏? ??靽???澈/璉?銝剔? YiDong
        }

        player.UseEnergy(finalCost);                       // ???拙振?賡?
        GameEvents.RaiseCardPlayed(cardData);              // 閫貊??歇鋡急??箝?隞塚??嫣噶?嗡?蝟餌絞??
        GameEvents.RaiseCardPlayedWithContext(
        new CardPlayContext(cardData, target, targetElementsBefore, targetElementsAfter));
        if (removedFromHand)
        handUIController.UpdateHandMetaUI();          // ?湔?賡???摨?璉?憿舐內
        return true;                                       // ????∠?
    }
    public void HandleCardUsedUI(CardUI usedUI)
    {
        handUIController.HandleCardUsedUI(usedUI);
    }
    public void RefreshHandUI(bool playDrawAnimation = false)
    {
        handUIController.RefreshHandUI(playDrawAnimation);
        // 撠?蝯血??函嚗??HandUIController ??渡???嚗?豢?行?賜??嚗?
    }

    internal void EnsureMovementCardInHand()
    {
        if (player == null) return;                        // 瘝??拙振撠曹??刻???

        Move_YiDong movementCard = GetGuaranteedMovementCardInstance();
        // ?踹靽?蝘餃??∠?撖虫?嚗??撱箇???Instantiate 銝??

        if (movementCard == null) return;                  // ?交???蝵格見?選??湔餈?

        RemoveGuaranteedMovementCardFromPiles();
        // ???澈????蝘駁???YiDong嚗??銴?

        int removedDuplicateCount = 0;
        for (int i = player.Hand.Count - 1; i >= 0; i--)
        {
            CardBase card = player.Hand[i];
            if (card is Move_YiDong && !ReferenceEquals(card, movementCard))
            {
                player.Hand.RemoveAt(i);                   // 蝘駁??銝剖摰?YiDong 撖虫?嚗靽??臭???撐嚗?
                removedDuplicateCount++;                   // 閮?鋡怎宏?斤??賊?
            }
        }

        if (!player.Hand.Contains(movementCard))
        {
            player.Hand.Add(movementCard);                 // ?交??葉瘝??撐靽??∴?撠望?脫???
        }

        if (removedDuplicateCount > 0)
        {
            player.DrawCards(removedDuplicateCount);
            // ?亦鈭?宏?支?憭撐 YiDong嚗停鋆????嗡??策?拙振
        }
    }

    internal void DiscardAllHand()
    {
        Move_YiDong movementCard = guaranteedMovementCardInstance;
        // ??靽?蝘餃??∠?撖虫?嚗??

        if (movementCard != null)
        {
            player.Hand.Remove(movementCard);
            // ????銝剔宏?支?霅宏?嚗????剁?

            player.discardPile.Remove(movementCard);
            // 蝣箔?璉??葉銝????撐靽???
        }

        player.discardPile.AddRange(player.Hand);
        // ?擗???其??唳???銝?

        player.Hand.Clear();
        // 皜征???”

        RemoveGuaranteedMovementCardFromPiles();
        // ?皜??澈????銝剜???YiDong嚗甇Ｘ???

        handUIController.RefreshHandUI();
        // ?湔 UI嚗＊蝷箸??????
    }

    internal void SetBattleStarted(bool value)
    {
        battleStarted = value;                             // ?勗??刻身摰擛交?血歇??
    }

    internal void SetEndTurnButtonInteractable(bool value)
    {
        handUIController.SetEndTurnButtonInteractable(value);
        // ?? HandUIController ?批????????血鈭?
    }
    public void RefreshEnemiesFromScene()
    {
        Enemy.FillActiveEnemies(enemies);
    }
    internal bool IsGuaranteedMovementCard(CardBase card)
    {
        if (card == null)
            return false;                                  // 蝛箏銝摰???

        Move_YiDong instance = GetGuaranteedMovementCardInstance();
        if (instance == null)
            return false;                                  // ?交?祆?閮剖?靽??⊥見?選?銋?亙 false

        if (ReferenceEquals(card, instance))
            return true;                                   // ?亙停?舫?祕靘??靽?蝘餃???

        if (guaranteedMovementCard != null && ReferenceEquals(card, guaranteedMovementCard))
            return true;                                   // ?????航?湔撘璅??祈澈

        return false;                                     // ?嗡????賭??臭?霅宏?
    }

    internal void RemoveGuaranteedMovementCardFromPiles()
    {
        if (player == null) return;                       // 瘝摰嗅停銝??

        player.deck.RemoveAll(card => card is Move_YiDong);
        // 敺?摨怎宏?斗???YiDong 憿??

        player.discardPile.RemoveAll(card => card is Move_YiDong);
        // 敺???蝘駁???YiDong 憿??
    }

    private Move_YiDong GetGuaranteedMovementCardInstance()
    {
        if (guaranteedMovementCardInstance == null)
        {
            if (guaranteedMovementCard == null)
            {
                Debug.LogWarning("Guaranteed movement card template is not assigned.");
                // ?交?? Inspector ??璅?嚗停霅血?銝銝?
                return null;
            }

            guaranteedMovementCardInstance = Instantiate(guaranteedMovementCard);
            // 隞交見??Instantiate ?箔??祕?擛亦??ScriptableObject 撖虫?
        }

        return guaranteedMovementCardInstance;            // ?甇文祕靘?銋??賢?券?隞踝?
    }

    private void InitializeControllers()
    {
        runtimeContext = new BattleRuntimeContext(this, player, board, enemies);
        runtimeContext.Activate();
        enemyQueryService = new BattleEnemyQueryService(runtimeContext);

        handUIController = new BattleHandUIController(
            this,                                          // ?喳 BattleManager ?祈澈
            player,                                        // ?喳?拙振
            cardPrefab,                                    // ?∠? UI ?ˊ??
            handPanel,                                     // ?? UI 摰孵
            deckPile,                                      // ?澈憿舐內?
            discardPile,                                   // 璉??＊蝷箏?
            energyText,                                    // ?賡???
            endTurnButton,                                 // 蝯?????
            cardUseDelay);                                 // 雿輻?∠?敺辣?脩???

        movementSelectionController = new MovementSelectionController(
            this,                                          // BattleManager
            player,                                        // ?拙振
            board,                                         // 璉
            handUIController);                             // ?其??函宏???湔 UI ???∩???

        attackSelectionController = new AttackSelectionController(
            player,                                        // ?拙振
            board,                                         // 璉嚗????鈭殷?
            handUIController,
            this,
            enemyQueryService
            );                             // ?其??冽???湔?? UI

        rewardController = new BattleRewardController(
            this,                                          // BattleManager
            player,                                        // ?拙振嚗??馳嚗?
            allCardPool,                                   // ????萄瘙?
            rewardUIPrefab,                                // ? UI ?ˊ??
            handPanel);                                    // 隞交?????Canvas ?箇撅斤???UI

        encounterLoader = new BattleEncounterLoader(
            this,                                          // BattleManager
            board,                                         // 璉
            player,                                        // ?拙振
            enemies,                                       // ?萎犖?”嚗?敺?鋡怠‵?亙銝鈭綽?
            enemySpawnConfigs,                             // ???萎犖?身摰?銵?
            stateMachine,                                  // ?圈洛???嚗?撅敺???PlayerTurn嚗?
            tutorialController);                           // ?飛?批?剁??舐 null嚗?

        movementSelectionController.SetEncounterLoader(encounterLoader);
        // ?迄 MovementSelectionController嚗???閬??絲憪?豢????摩

        turnController = new BattleTurnController(
            this,                                          // BattleManager
            player,                                        // ?拙振
            enemies,                                       // ?萎犖?”
            stateMachine,                                  // ?圈洛???
            handUIController,                              // ?閬?嗆???UI嚗?憒??～??????
            tutorialController);                           // ?飛?批?剁??舐 null嚗?

        PlayerDeckController deckController = player != null ? player.GetComponent<PlayerDeckController>() : null;
        playerDeckController = deckController;
        if (playerDeckController != null)
        {
            playerDeckController.ConfigureBattleRuntime(this, runtimeContext);
            playerDeckController.HandChanged -= HandlePlayerHandChanged;
            playerDeckController.HandChanged += HandlePlayerHandChanged;
            playerDeckController.DeckChanged -= HandlePlayerDeckChanged;
            playerDeckController.DeckChanged += HandlePlayerDeckChanged;
        }
    }

    private void OnDestroy()
    {
        runtimeContext?.DeactivateIfOwner(this);

        if (player != null)
        {
            if (playerDeckController == null)
            {
                playerDeckController = player.GetComponent<PlayerDeckController>();
            }

            if (playerDeckController != null)
            {
                playerDeckController.HandChanged -= HandlePlayerHandChanged;
                playerDeckController.DeckChanged -= HandlePlayerDeckChanged;
                playerDeckController.ClearBattleRuntime(this);
            }
        }
    }

    private void HandlePlayerHandChanged()
    {
        handUIController?.RefreshHandUI();
    }

    private void HandlePlayerDeckChanged()
    {
        handUIController?.UpdateDeckDiscardUI();
    }
}


