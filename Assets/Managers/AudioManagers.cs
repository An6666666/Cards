using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    [Serializable]
    public class SceneBgmOverrideEntry
    {
        public string sceneName;
        public AudioClip clip;
    }

    [Serializable]
    public class NodeTypeBgmOverrideEntry
    {
        public MapNodeType nodeType;
        public AudioClip clip;
    }

    public static AudioManager Instance;
    private const string AutoCreatedSfxSourceName = "AutoCreated SFX Source";

    [Header("Audio Sources")]
    public AudioSource BGMSource;
    public AudioSource SFXSource;

    [Header("Legacy / Title BGM")]
    public AudioClip cityBGM;

    [Header("Auto BGM Switching")]
    [SerializeField] private bool autoSwitchBgmOnSceneLoad = true;
    [SerializeField] private List<SceneBgmOverrideEntry> sceneBgmOverrides = new List<SceneBgmOverrideEntry>();
    [SerializeField] private List<NodeTypeBgmOverrideEntry> nodeTypeBgmOverrides = new List<NodeTypeBgmOverrideEntry>();

    [Header("BGM Fade")]
    [SerializeField] private bool useBgmFade = true;
    [SerializeField] private bool fadeOnInitialPlay = true;
    [SerializeField] private float bgmFadeOutDuration = 0.35f;
    [SerializeField] private float bgmFadeInDuration = 0.5f;

    [Header("SFX Clips")]
    public AudioClip attackFire;
    public AudioClip attackWood;
    public AudioClip attackWater;
    public AudioClip attackIce;
    public AudioClip attackThunder;

    [Header("UI SFX Clips")]
    [SerializeField] private AudioClip uiButtonClick;
    [SerializeField] private AudioClip uiButtonHover;
    [SerializeField] private AudioClip uiButtonDisabled;

    [Header("Player Combat SFX")]
    [SerializeField] private AudioClip playerHurt;
    [SerializeField] private AudioClip playerBlockNoDamage;

    public AudioClip UIButtonClickClip => uiButtonClick;
    public AudioClip UIButtonHoverClip => uiButtonHover;
    public AudioClip UIButtonDisabledClip => uiButtonDisabled;
    public float BGMVolume => bgmVolume;
    public float SFXVolume => sfxVolume;

    private Coroutine bgmTransitionRoutine;
    private float bgmVolume = 1f;
    private float sfxVolume = 1f;
    private float bgmFadeMultiplier = 1f;
    private bool volumeStateInitialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureAudioSourcesConfigured();
        InitializeVolumeState();

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        DontDestroyOnLoad(gameObject);

        if (autoSwitchBgmOnSceneLoad)
        {
            RefreshSceneBGM();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }

    public void PlayBGM(AudioClip clip)
    {
        EnsureAudioSourcesConfigured();
        InitializeVolumeState();
        if (BGMSource == null || clip == null) return;

        bool alreadyPlayingTargetClip =
            BGMSource.clip == clip &&
            BGMSource.isPlaying &&
            bgmTransitionRoutine == null &&
            Mathf.Approximately(bgmFadeMultiplier, 1f);

        if (alreadyPlayingTargetClip)
        {
            return;
        }

        if (bgmTransitionRoutine != null)
        {
            StopCoroutine(bgmTransitionRoutine);
        }

        bgmTransitionRoutine = StartCoroutine(TransitionBGM(clip));
    }

    public void PlaySFX(AudioClip clip)
    {
        PlaySFX(clip, 1f);
    }

    public void PlaySFX(AudioClip clip, float volumeScale)
    {
        EnsureAudioSourcesConfigured();
        InitializeVolumeState();
        if (clip == null || SFXSource == null) return;
        SFXSource.PlayOneShot(clip, Mathf.Max(0f, volumeScale));
    }

    public void PlayAttackSFX(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire:
                PlaySFX(attackFire);
                break;
            case ElementType.Wood:
                PlaySFX(attackWood);
                break;
            case ElementType.Water:
                PlaySFX(attackWater);
                break;
            case ElementType.Ice:
                PlaySFX(attackIce);
                break;
            case ElementType.Thunder:
                PlaySFX(attackThunder);
                break;
        }
    }

    public void PlayPlayerHurtSFX()
    {
        PlaySFX(playerHurt);
    }

    public void PlayPlayerBlockNoDamageSFX()
    {
        PlaySFX(playerBlockNoDamage);
    }

    public void SetBGMVolume(float value)
    {
        EnsureAudioSourcesConfigured();
        InitializeVolumeState();
        bgmVolume = Mathf.Clamp01(value);
        ApplyBgmVolume();
    }

    public void SetSFXVolume(float value)
    {
        EnsureAudioSourcesConfigured();
        InitializeVolumeState();
        sfxVolume = Mathf.Clamp01(value);
        ApplySfxVolume();
    }

    public void RefreshSceneBGM()
    {
        RefreshSceneBGM(SceneManager.GetActiveScene());
    }

    public void RefreshSceneBGM(Scene scene)
    {
        AudioClip clip = ResolveBgmForScene(scene);
        if (clip != null)
        {
            PlayBGM(clip);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!autoSwitchBgmOnSceneLoad)
        {
            return;
        }

        RefreshSceneBGM(scene);
    }

    private AudioClip ResolveBgmForScene(Scene scene)
    {
        RunManager runManager = RunManager.Instance;
        MapNodeData activeNode = runManager != null ? runManager.ActiveNode : null;

        if (activeNode != null)
        {
            RunEncounterDefinition encounter = activeNode.Encounter;
            if (encounter != null && encounter.BgmOverride != null)
            {
                return encounter.BgmOverride;
            }

            if (TryGetNodeTypeOverride(activeNode.NodeType, out AudioClip nodeTypeClip))
            {
                return nodeTypeClip;
            }
        }

        if (TryGetSceneOverride(scene.name, out AudioClip sceneClip))
        {
            return sceneClip;
        }

        if (!string.IsNullOrWhiteSpace(scene.name) &&
            scene.name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0 &&
            cityBGM != null)
        {
            return cityBGM;
        }

        return null;
    }

    private bool TryGetSceneOverride(string sceneName, out AudioClip clip)
    {
        clip = null;
        if (string.IsNullOrWhiteSpace(sceneName) || sceneBgmOverrides == null)
        {
            return false;
        }

        for (int i = 0; i < sceneBgmOverrides.Count; i++)
        {
            SceneBgmOverrideEntry entry = sceneBgmOverrides[i];
            if (entry == null || entry.clip == null || string.IsNullOrWhiteSpace(entry.sceneName))
            {
                continue;
            }

            if (string.Equals(entry.sceneName.Trim(), sceneName, StringComparison.OrdinalIgnoreCase))
            {
                clip = entry.clip;
                return true;
            }
        }

        return false;
    }

    private bool TryGetNodeTypeOverride(MapNodeType nodeType, out AudioClip clip)
    {
        clip = null;
        if (nodeTypeBgmOverrides == null)
        {
            return false;
        }

        for (int i = 0; i < nodeTypeBgmOverrides.Count; i++)
        {
            NodeTypeBgmOverrideEntry entry = nodeTypeBgmOverrides[i];
            if (entry == null || entry.clip == null)
            {
                continue;
            }

            if (entry.nodeType == nodeType)
            {
                clip = entry.clip;
                return true;
            }
        }

        return false;
    }

    private void EnsureAudioSourcesConfigured()
    {
        if (BGMSource == null)
        {
            BGMSource = GetComponent<AudioSource>();
        }

        if (BGMSource != null)
        {
            BGMSource.spatialBlend = 0f;
        }

        if (SFXSource == null || SFXSource == BGMSource)
        {
            SFXSource = FindDistinctAudioSource();

            if (SFXSource == null)
            {
                SFXSource = CreateSfxSource();
                Debug.LogWarning("[AudioManager] SFXSource was missing or shared with BGMSource, so a dedicated SFX AudioSource was created automatically.", this);
            }
        }

        if (SFXSource != null)
        {
            SFXSource.playOnAwake = false;
            SFXSource.loop = false;
            SFXSource.spatialBlend = 0f;
        }

        if (volumeStateInitialized)
        {
            ApplyBgmVolume();
            ApplySfxVolume();
        }
    }

    private AudioSource FindDistinctAudioSource()
    {
        AudioSource[] sourcesOnSelf = GetComponents<AudioSource>();
        for (int i = 0; i < sourcesOnSelf.Length; i++)
        {
            AudioSource source = sourcesOnSelf[i];
            if (source != null && source != BGMSource)
            {
                return source;
            }
        }

        Transform child = transform.Find(AutoCreatedSfxSourceName);
        if (child != null)
        {
            AudioSource childSource = child.GetComponent<AudioSource>();
            if (childSource != null && childSource != BGMSource)
            {
                return childSource;
            }
        }

        return null;
    }

    private AudioSource CreateSfxSource()
    {
        GameObject sfxHost = new GameObject(AutoCreatedSfxSourceName);
        sfxHost.transform.SetParent(transform, false);

        AudioSource source = sfxHost.AddComponent<AudioSource>();
        source.volume = volumeStateInitialized ? sfxVolume : (BGMSource != null ? BGMSource.volume : 1f);
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        return source;
    }

    private void InitializeVolumeState()
    {
        if (!volumeStateInitialized)
        {
            if (BGMSource != null)
            {
                bgmVolume = Mathf.Clamp01(BGMSource.volume);
            }

            if (SFXSource != null)
            {
                sfxVolume = Mathf.Clamp01(SFXSource.volume);
            }

            bgmFadeMultiplier = 1f;
            volumeStateInitialized = true;
        }

        ApplyBgmVolume();
        ApplySfxVolume();
    }

    private void ApplyBgmVolume()
    {
        if (BGMSource == null)
        {
            return;
        }

        BGMSource.volume = Mathf.Clamp01(bgmVolume * bgmFadeMultiplier);
    }

    private void ApplySfxVolume()
    {
        if (SFXSource == null)
        {
            return;
        }

        SFXSource.volume = Mathf.Clamp01(sfxVolume);
    }

    private IEnumerator TransitionBGM(AudioClip nextClip)
    {
        bool hasCurrentTrack = BGMSource != null && BGMSource.clip != null && BGMSource.isPlaying;
        bool shouldFade = useBgmFade && (hasCurrentTrack || fadeOnInitialPlay);

        if (shouldFade)
        {
            yield return FadeBgmMultiplier(0f, Mathf.Max(0f, bgmFadeOutDuration));
        }
        else
        {
            bgmFadeMultiplier = 1f;
            ApplyBgmVolume();
        }

        if (BGMSource == null)
        {
            bgmTransitionRoutine = null;
            yield break;
        }

        BGMSource.Stop();
        BGMSource.clip = nextClip;
        BGMSource.Play();

        if (shouldFade)
        {
            bgmFadeMultiplier = 0f;
            ApplyBgmVolume();
            yield return FadeBgmMultiplier(1f, Mathf.Max(0f, bgmFadeInDuration));
        }
        else
        {
            bgmFadeMultiplier = 1f;
            ApplyBgmVolume();
        }

        bgmTransitionRoutine = null;
    }

    private IEnumerator FadeBgmMultiplier(float targetMultiplier, float duration)
    {
        float startingMultiplier = bgmFadeMultiplier;

        if (duration <= 0f)
        {
            bgmFadeMultiplier = Mathf.Clamp01(targetMultiplier);
            ApplyBgmVolume();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            bgmFadeMultiplier = Mathf.Lerp(startingMultiplier, targetMultiplier, progress);
            ApplyBgmVolume();
            yield return null;
        }

        bgmFadeMultiplier = Mathf.Clamp01(targetMultiplier);
        ApplyBgmVolume();
    }
}
