using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Audio/Collider SFX Player")]
public class ColliderSfxPlayer : MonoBehaviour
{
    [Header("Playback")]
    [SerializeField] private bool useAudioManager = true;
    [SerializeField] private AudioSource localFallbackSource;
    [SerializeField] [Min(0f)] private float volumeScale = 1f;

    [Header("Triggers")]
    [SerializeField] private bool playOnHover = true;
    [SerializeField] private bool playOnClick = true;
    [SerializeField] private bool blockWhilePointerOverUi = true;
    [SerializeField] [Min(0f)] private float hoverCooldown = 0.05f;

    [Header("Conditions")]
    [SerializeField] private bool requireBoardTileSelectableIfBoardTile = true;

    [Header("Custom Clips")]
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;

    private static AudioSource runtimeFallbackSource;
    private BoardTile cachedBoardTile;
    private float lastHoverPlayTime = float.NegativeInfinity;

    private void Reset()
    {
        localFallbackSource = GetComponent<AudioSource>();
        volumeScale = 1f;
        hoverCooldown = 0.05f;
        requireBoardTileSelectableIfBoardTile = true;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
        volumeScale = Mathf.Max(0f, volumeScale);
        hoverCooldown = Mathf.Max(0f, hoverCooldown);
    }

    private void OnMouseEnter()
    {
        if (!playOnHover || !CanPlayNow()) return;
        if (Time.unscaledTime - lastHoverPlayTime < hoverCooldown) return;

        lastHoverPlayTime = Time.unscaledTime;
        PlayClip(hoverClip, AudioManager.Instance != null ? AudioManager.Instance.UIButtonHoverClip : null);
    }

    private void OnMouseDown()
    {
        if (!playOnClick || !CanPlayNow()) return;
        PlayClip(clickClip, AudioManager.Instance != null ? AudioManager.Instance.UIButtonClickClip : null);
    }

    private void CacheReferences()
    {
        if (cachedBoardTile == null)
        {
            cachedBoardTile = GetComponent<BoardTile>();
        }

        if (localFallbackSource == null)
        {
            localFallbackSource = GetComponent<AudioSource>();
        }
    }

    private bool CanPlayNow()
    {
        if (!isActiveAndEnabled || IsBlockedByUi())
        {
            return false;
        }

        CacheReferences();

        if (requireBoardTileSelectableIfBoardTile &&
            cachedBoardTile != null &&
            GetComponent<BoardTileSelectable>() == null)
        {
            return false;
        }

        return true;
    }

    private bool IsBlockedByUi()
    {
        return blockWhilePointerOverUi && PointerUiBlocker.IsPointerBlockedByUi();
    }

    private void PlayClip(AudioClip customClip, AudioClip defaultClip)
    {
        AudioClip clipToPlay = customClip != null ? customClip : defaultClip;
        if (clipToPlay == null) return;

        if (useAudioManager && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clipToPlay, volumeScale);
            return;
        }

        AudioSource source = localFallbackSource != null ? localFallbackSource : GetRuntimeFallbackSource();
        source.PlayOneShot(clipToPlay, volumeScale);
    }

    private static AudioSource GetRuntimeFallbackSource()
    {
        if (runtimeFallbackSource != null) return runtimeFallbackSource;

        GameObject host = GameObject.Find("ColliderSfxRuntime");
        if (host == null)
        {
            host = new GameObject("ColliderSfxRuntime");
            DontDestroyOnLoad(host);
        }

        runtimeFallbackSource = host.GetComponent<AudioSource>();
        if (runtimeFallbackSource == null)
        {
            runtimeFallbackSource = host.AddComponent<AudioSource>();
        }

        runtimeFallbackSource.playOnAwake = false;
        runtimeFallbackSource.spatialBlend = 0f;
        return runtimeFallbackSource;
    }
}
