using System.Collections;                     // 雿輻???賊?憿嚗Enumerator 蝑?
using System.Collections.Generic;             // 雿輻瘜???嚗ist<T> 蝑?
using UnityEngine;                            // 雿輻 Unity 撘??詨? API

public class BattleEncounterLoader            // 鞎痊???交擛仿?韏瑕?雿蔭???鈭箝??批憿?
{
    private readonly BattleManager battleManager;          // ?圈洛蝞∠??剁??詨?蝮賜恣嚗??怎摰嗉絲憪?蝵桃?閮剖?嚗?
    private readonly Board board;                          // 璉嚗靘?敺??摮漣璅?嚗?
    private readonly Player player;                        // ?拙振?拐辣
    private readonly List<Enemy> enemies;                  // ?港??萎犖皜嚗 BattleManager ????銵剁?
    private readonly List<EnemySpawnConfig> enemySpawnConfigs; // ?萎犖??閮剖?皜嚗refab + ?賊?嚗?
    private readonly BattleStateMachine stateMachine;      // ?圈洛???嚗???PlayerTurn / EnemyTurn 蝑?
    private readonly TutorialBattleController tutorialController;

    private bool isSelectingStartTile = false;             // ?臬甇??豢??拙振韏瑕??潛???

    public BattleEncounterLoader(                         // 撱箸?摮??其?瘜典???閬??訾??拐辣
        BattleManager battleManager,
        Board board,
        Player player,
        List<Enemy> enemies,
        List<EnemySpawnConfig> enemySpawnConfigs,
        BattleStateMachine stateMachine,
        TutorialBattleController tutorialController)
    {
        this.battleManager = battleManager;                // ???圈洛蝞∠???
        this.board = board;                                // ??璉
        this.player = player;                              // ???拙振
        this.enemies = enemies;                            // ???萎犖?”嚗?鋡怠‵皛選?
        this.enemySpawnConfigs = enemySpawnConfigs;        // ???萎犖??閮剖??”
        this.stateMachine = stateMachine;                  // ???圈洛???
        this.tutorialController = tutorialController;      // ???飛?批?剁??舐 null嚗?
    }

    public void LoadEncounterFromRunManager()
    {
        var runManager = RunManager.Instance;              // 敺靘?敺?RunManager
        var encounter = runManager?.ActiveNode?.Encounter; // 敺???暺?敺?Encounter 閮剖?嚗?賜 null嚗?

        if (encounter == null)                             // ?交??身摰?Encounter
            return;                                        // ?湔蝯?嚗?頛?萎犖?蔭嚗?

        if (enemySpawnConfigs == null)                     // ?亙??冽??策??閮剖??”
            return;                                        // ?湔蝯?

        enemySpawnConfigs.Clear();                         // ??蝛箏??祉???閮剖?

        var enemyGroups = encounter.EnemyGroups;           // 敺?Encounter ???萎犖蝢斤?鞈?嚗?蝯?enemyPrefab + count嚗?
        if (enemyGroups == null)                           // 瘝??停銝???
            return;

        foreach (var group in enemyGroups)                 // 撠?銝蝯鈭箇黎蝯脰???
        {
            if (group == null)                             // ?脣?嚗黎蝯?賜 null
                continue;

            var configCopy = new EnemySpawnConfig          // 撱箇?銝???EnemySpawnConfig 銴
            {
                enemyPrefab = group.enemyPrefab,           // ???萎犖?ˊ??
                count = group.count                        // ??閬????賊?
            };

            enemySpawnConfigs.Add(configCopy);             // ??唳?啁???閮剖?皜
        }
    }

    public IEnumerator GameStartRoutine()
    {
        if (board != null)                                 // ?交?璉摮
            {
            Vector2Int startPos;
            bool overrideStart = false;
            if (tutorialController != null)
            {
                overrideStart = tutorialController.TryGetPlayerStartPosition(out startPos);
            }
            else
            {
                startPos = default;
            }
            if (overrideStart)
            {
                battleManager.playerStartPos = startPos;
                SetupPlayer();
            }
            else
            {
                yield return battleManager.StartCoroutine(SelectPlayerStartTile());
                // 鈭斤策 BattleManager ????嚗??拙振?豢?韏瑕?蝡??澆?嚗?敺摰園??賂?
            }
        }

        SpawnInitialEnemies();                             // ?冽??支??????萎犖
        battleManager.RefreshEnemiesFromScene();

        battleManager.SetBattleStarted(true);              // ?迄 BattleManager嚗擛交迤撘?憪?
        battleManager.SetEndTurnButtonInteractable(false); // ???雿??踹??飛撠店???舀???????
        tutorialController?.HandleBattleStarted();
        while (tutorialController != null && tutorialController.IsWaitingForOpeningDialogue)
        {
            yield return null;
        }

        stateMachine.ChangeState(new PlayerTurnState(battleManager));
        // 撠擛亦?????摰嗅?????PlayerTurnState嚗?
    }

    private IEnumerator SelectPlayerStartTile()
    {
        yield return battleManager.ShowBattlePhaseHintAndWait("選擇落點回合");
        isSelectingStartTile = true;                       // 閮剜??迤?券?絲憪??

        List<Vector2Int> positions = board.GetAllPositions(); // ??璉銝??摮?摨扳?
        foreach (var pos in positions)                        // 撠?銝?摮脰???
        {
            BoardTile t = board.GetTileAt(pos);               // ??閰脖?蝵桀??? BoardTile
            if (t.GetComponent<BoardTileSelectable>() == null)
                t.gameObject.AddComponent<BoardTileSelectable>();
            // ?亥府?潭???BoardTileSelectable ?辣嚗停??嚗?摰隞亥◤暺??豢?

            if (t.GetComponent<BoardTileHoverHighlight>() == null)
                t.gameObject.AddComponent<BoardTileHoverHighlight>();
            // ?亥府?潭???曌??鈭桀?隞塚?撠勗?銝?霈?曌宏?憿舐內擃漁
        }

        while (isSelectingStartTile)                        // ??isSelectingStartTile ??true嚗??芷摰?
            yield return null;                              // 瘥?蝑?嚗?唳??? isSelectingStartTile 閮剔 false嚗?

        foreach (var pos in positions)                      // ?拙振撌脩??詨?韏瑕??潘?皜??冽???
        {
            BoardTile t = board.GetTileAt(pos);             // ?活??瘥??摮?
            BoardTileHoverHighlight hover = t.GetComponent<BoardTileHoverHighlight>();
            if (hover) Object.Destroy(hover);               // ?交? HoverHighlight嚗停?芣?甇文?隞?
            t.SetHighlight(false);                          // ???澆?擃漁
        }

        SetupPlayer();                                      // 撠摰嗆?圈憟賜?韏瑕??潔?蝵桐?
        board.ResetAllTilesSelectable();                    // ?蔭??摮??舫????蝘駁 BoardTileSelectable 銝??詨????
    }

    private void SetupPlayer()
    {
        if (player == null || board == null) return;        // ?亦摰嗆?璉銝??典停?湔頝喳

        BoardTile tile = board.GetTileAt(battleManager.playerStartPos);
        // 敺?BattleManager 銝? playerStartPos ??撠??摮?

        if (tile != null)                                   // ?亥府?澆?摮
        {
            player.MoveToPosition(battleManager.playerStartPos);
            // ?摰嗥??摩摨扳?嚗ridPosition嚗宏?閰脖?蝵殷?銝血?甇亙祕??transform.position
        }
    }

    private void SpawnInitialEnemies()
    {
        if (board == null || enemySpawnConfigs == null) return;
        // ?交??斗??萎犖??閮剖??”銝??剁?撠曹????萎犖
        if (tutorialController != null && tutorialController.IsActive)
        {
            tutorialController.SpawnEnemiesForCurrentStep();
            return;
        }
        List<Vector2Int> positions = board.GetAllPositions();   // ?踹????支?蝵?
        positions.Remove(battleManager.playerStartPos);         // ?摰嗉絲憪?蝵桀??舐雿蔭銝剔宏?歹??踹??萎犖蝡?拙振?凋?嚗?

        foreach (var config in enemySpawnConfigs)               // 撠?銝蝔格鈭粹?蝵桅脰?餈游?
        {
            if (config == null || config.enemyPrefab == null) continue;
            // ?仿?蝵桐?摮???? Prefab嚗停頝喲?

            int spawnCount = Mathf.Max(0, config.count);        // ?脣?嚗????撠 0
            for (int i = 0; i < spawnCount && positions.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, positions.Count);
                // ?函?擗??舐雿蔭銝剝璈銝??index

                Vector2Int pos = positions[idx];                // ???詨?漣璅?
                positions.RemoveAt(idx);                        // 敺?銵其葉蝘駁??蝵殷??踹?????嚗?

                BoardTile tile = board.GetTileAt(pos);          // ??閰脣漣璅??摮?
                if (tile == null) continue;                     // ?交摮?摮撠梯歲??

                Enemy e = Object.Instantiate(
                    config.enemyPrefab,                         // 閬????萎犖 Prefab
                    tile.transform.position,                    // ?曉閰脫摮?銝?摨扳?雿蔭
                    Quaternion.identity);                       // 銝?頧????身?孵?嚗?

                e.gridPosition = pos;                           // 閮剖??萎犖??頛舀摮漣璅?
            }

            if (positions.Count == 0)                           // 憒?撌脩?瘝??舐雿蔭?臭誑?暹鈭?
                break;                                          // 蝯?憭惜餈游?嚗????憭鈭?
        }
    }

    public bool HandleTileSelection(BoardTile tile)
    {
        if (!isSelectingStartTile) return false;                // ?亦???臬?貉絲憪?挾嚗?亙蕭??
        battleManager.playerStartPos = tile.gridPosition;       // 撠摰嗉絲憪?蝵株身?箄◤暺??
        isSelectingStartTile = false;                           // 蝯??詨?瘚?嚗electPlayerStartTile ??while ?歲?綽?
        return true;                                            // ? true 銵函內?活暺??◤??嚗??暺?
    }
}
