using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public sealed class ShopSceneGuideTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RunManager runManager;
    [SerializeField] private ShopUIManager shopUiManager;
    [SerializeField] private ShopNpcDialogueController shopNpcController;
    [SerializeField] private GuideNPCPresenter guidePresenter;
    [SerializeField] private GuideDialogueDatabase dialogueDatabase;
    [SerializeField] private ShopTutorialController tutorialController;
    [SerializeField] private ShopTutorialDefinition tutorialDefinition;

    [Header("Dialogue")]
    [SerializeField] private string tutorialShopIntroKey = GuideKeys.TutorialShopIntro;

    private bool hasResolved;
    private bool tutorialStarted;
    private bool subscribedToTutorialCompleted;

    private void Awake()
    {
        TryBindReferences();
        RequestGreetingSuppressionIfNeeded();
    }

    private void OnEnable()
    {
        TryBindReferences();
        RequestGreetingSuppressionIfNeeded();
    }

    private void OnDisable()
    {
        UnsubscribeTutorialCompleted();
    }

    private void Start()
    {
        TryResolveShopGuideFlow();
    }

    private void Update()
    {
        if (hasResolved)
            return;

        TryResolveShopGuideFlow();
    }

    private void TryResolveShopGuideFlow()
    {
        if (hasResolved)
            return;

        TryBindReferences();
        if (runManager == null)
            return;

        if (!ShouldPlayTutorialIntro())
        {
            hasResolved = true;
            return;
        }

        if (TryStartTutorialSequence())
        {
            tutorialStarted = true;
            hasResolved = true;
            return;
        }

        if (TryPlayWithGuidePresenter() || TryPlayWithShopNpc())
        {
            MarkGuideAsPlayed();
            return;
        }

        hasResolved = true;
    }

    private bool TryStartTutorialSequence()
    {
        if (tutorialStarted)
            return true;

        ShopTutorialDefinition definition = GetResolvedTutorialDefinition();
        if (definition == null || !definition.HasSteps)
            return false;

        if (tutorialController == null)
        {
            GameObject host = shopUiManager != null ? shopUiManager.gameObject : gameObject;
            tutorialController = host.GetComponent<ShopTutorialController>();
            if (tutorialController == null)
                tutorialController = host.AddComponent<ShopTutorialController>();
        }

        if (tutorialController == null)
            return false;

        SubscribeTutorialCompleted();

        if (tutorialController.IsRunning)
            return true;

        return tutorialController.Begin(definition);
    }

    private bool TryPlayWithGuidePresenter()
    {
        if (guidePresenter == null)
            return false;

        if (dialogueDatabase != null)
            guidePresenter.AssignDatabase(dialogueDatabase);

        string introKey = GetResolvedIntroKey();
        return !string.IsNullOrEmpty(introKey) && guidePresenter.Talk(introKey);
    }

    private bool TryPlayWithShopNpc()
    {
        if (shopNpcController == null)
            return false;

        string introLine = ResolveIntroLine();
        if (string.IsNullOrWhiteSpace(introLine))
            return false;

        shopNpcController.ShowScriptedLine(introLine);
        return true;
    }

    private string ResolveIntroLine()
    {
        string introKey = GetResolvedIntroKey();
        if (dialogueDatabase != null)
        {
            var lines = dialogueDatabase.GetLines(introKey);
            string firstDatabaseLine = lines?.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
            if (!string.IsNullOrWhiteSpace(firstDatabaseLine))
                return firstDatabaseLine.Trim();
        }

        return string.Empty;
    }

    private bool ShouldPlayTutorialIntro()
    {
        if (runManager == null || !runManager.IsTutorialRun)
            return false;

        if (runManager.ActiveNode?.NodeType != MapNodeType.Shop)
            return false;

        return !runManager.HasGuideFlag(GetResolvedGuideFlag());
    }

    private void RequestGreetingSuppressionIfNeeded()
    {
        if (runManager == null)
            return;

        if (!ShouldPlayTutorialIntro())
            return;

        runManager.RequestDefaultShopEntryDialogueSuppression();
    }

    private void TryBindReferences()
    {
        if (runManager == null)
            runManager = RunManager.Instance ?? FindObjectOfType<RunManager>(true);

        if (shopUiManager == null)
            shopUiManager = GetComponent<ShopUIManager>() ?? FindObjectOfType<ShopUIManager>(true);

        if (shopNpcController == null && shopUiManager != null)
            shopNpcController = shopUiManager.ShopNpcController;

        if (shopNpcController == null)
            shopNpcController = FindObjectOfType<ShopNpcDialogueController>(true);

        if (guidePresenter == null)
            guidePresenter = FindObjectOfType<GuideNPCPresenter>(true);

        if (dialogueDatabase == null)
            dialogueDatabase = ResolveDialogueDatabase(GetResolvedIntroKey());

        if (tutorialDefinition == null)
            tutorialDefinition = ResolveTutorialDefinition();
    }

    private GuideDialogueDatabase ResolveDialogueDatabase(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var databases = Resources.FindObjectsOfTypeAll<GuideDialogueDatabase>();
        for (int i = 0; i < databases.Length; i++)
        {
            GuideDialogueDatabase candidate = databases[i];
            if (candidate == null)
                continue;

            var lines = candidate.GetLines(key);
            if (lines != null && lines.Count > 0)
                return candidate;
        }

        return null;
    }

    private ShopTutorialDefinition ResolveTutorialDefinition()
    {
        ShopTutorialDefinition fromResources = Resources.Load<ShopTutorialDefinition>("Guide/ShopTutorial_Default");
        if (fromResources != null && fromResources.HasSteps)
            return fromResources;

        var definitions = Resources.FindObjectsOfTypeAll<ShopTutorialDefinition>();
        for (int i = 0; i < definitions.Length; i++)
        {
            ShopTutorialDefinition candidate = definitions[i];
            if (candidate != null && candidate.HasSteps)
                return candidate;
        }

        return null;
    }

    private ShopTutorialDefinition GetResolvedTutorialDefinition()
    {
        if (tutorialDefinition != null && tutorialDefinition.HasSteps)
            return tutorialDefinition;

        tutorialDefinition = ResolveTutorialDefinition();
        return tutorialDefinition;
    }

    private string GetResolvedGuideFlag()
    {
        ShopTutorialDefinition definition = GetResolvedTutorialDefinition();
        if (definition != null && definition.HasSteps)
            return definition.CompletionFlag;

        return GetResolvedIntroKey();
    }

    private string GetResolvedIntroKey()
    {
        return string.IsNullOrWhiteSpace(tutorialShopIntroKey)
            ? GuideKeys.TutorialShopIntro
            : tutorialShopIntroKey.Trim();
    }

    private void MarkGuideAsPlayed()
    {
        runManager.MarkGuideFlag(GetResolvedGuideFlag());
        hasResolved = true;
    }

    private void HandleTutorialCompleted()
    {
        MarkGuideAsPlayed();
        UnsubscribeTutorialCompleted();
    }

    private void SubscribeTutorialCompleted()
    {
        if (tutorialController == null || subscribedToTutorialCompleted)
            return;

        tutorialController.TutorialCompleted += HandleTutorialCompleted;
        subscribedToTutorialCompleted = true;
    }

    private void UnsubscribeTutorialCompleted()
    {
        if (tutorialController == null || !subscribedToTutorialCompleted)
            return;

        tutorialController.TutorialCompleted -= HandleTutorialCompleted;
        subscribedToTutorialCompleted = false;
    }
}

public static class ShopSceneGuideBootstrap
{
    private const string ShopSceneName = "ShopScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.name, ShopSceneName, StringComparison.Ordinal))
            return;

        if (UnityEngine.Object.FindObjectOfType<ShopSceneGuideTrigger>(true) != null)
            return;

        ShopUIManager shopUiManager = UnityEngine.Object.FindObjectOfType<ShopUIManager>(true);
        GameObject host = shopUiManager != null ? shopUiManager.gameObject : new GameObject(nameof(ShopSceneGuideTrigger));

        if (host.scene != scene)
            SceneManager.MoveGameObjectToScene(host, scene);

        host.AddComponent<ShopSceneGuideTrigger>();
    }
}
