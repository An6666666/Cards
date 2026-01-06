using System.Collections.Generic;

/// <summary>
/// Base helper for scene guide triggers: tracks once-only dialogue keys.
/// </summary>
public abstract class SceneGuideTriggerBase : UnityEngine.MonoBehaviour
{
    protected readonly HashSet<string> playedFlags = new HashSet<string>();

    protected bool TryTalk(GuideNPCPresenter presenter, string key, IEnumerable<string> fallbackLines = null)
    {
        if (presenter == null || string.IsNullOrWhiteSpace(key))
            return false;

        if (playedFlags.Contains(key))
            return false;

        bool played = presenter.Talk(key);
        if (!played && fallbackLines != null)
        {
            presenter.TalkLines(fallbackLines);
            played = true;
        }

        if (played)
        {
            playedFlags.Add(key);
        }

        return played;
    }
}