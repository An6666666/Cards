using UnityEngine;
using UnityEngine.SceneManagement;

public class RunSceneRouter
{
    private readonly string runSceneName;
    private readonly string battleSceneName;
    private readonly string shopSceneName;
    private readonly string deathReturnSceneName;

    public RunSceneRouter(string runSceneName, string battleSceneName, string shopSceneName, string deathReturnSceneName)
    {
        this.runSceneName = runSceneName;
        this.battleSceneName = battleSceneName;
        this.shopSceneName = shopSceneName;
        this.deathReturnSceneName = deathReturnSceneName;
    }

    public void LoadRunScene()
    {
        if (!string.IsNullOrEmpty(runSceneName))
        {
            SceneManager.LoadScene(runSceneName);
        }
    }

    public void LoadSceneForNode(MapNodeData node)
    {
        if (node == null)
            return;

        string sceneName = null;
        switch (node.NodeType)
        {
            case MapNodeType.Battle:
            case MapNodeType.EliteBattle:
            case MapNodeType.Boss:
                sceneName = battleSceneName;
                break;
            case MapNodeType.Shop:
                sceneName = shopSceneName;
                break;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"RunSceneRouter: Scene name for {node.NodeType} is not configured.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
    public void LoadDeathReturnScene()
    {
        string targetScene = string.IsNullOrEmpty(deathReturnSceneName) ? runSceneName : deathReturnSceneName;

        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogWarning("RunSceneRouter: Death return scene is not configured.");
            return;
        }

        SceneManager.LoadScene(targetScene);
    }
}
