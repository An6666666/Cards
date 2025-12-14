using UnityEngine;
using UnityEngine.SceneManagement;

public class RunSceneRouter
{
    private readonly string runSceneName;
    private readonly string battleSceneName;
    private readonly string shopSceneName;

    public RunSceneRouter(string runSceneName, string battleSceneName, string shopSceneName)
    {
        this.runSceneName = runSceneName;
        this.battleSceneName = battleSceneName;
        this.shopSceneName = shopSceneName;
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
}
