using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("UI/Button SFX Player")]
[RequireComponent(typeof(Button))]
public class UIButtonSfxPlayer : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerClickHandler, ISubmitHandler
{
    [Header("Target")]
    [SerializeField] private Button targetButton;

    [Header("Playback")]
    [SerializeField] private bool useAudioManager = true;
    [SerializeField] private AudioSource localFallbackSource;
    [SerializeField] [Min(0f)] private float volumeScale = 1f;

    [Header("Triggers")]
    [SerializeField] private bool playOnHover = true;
    [SerializeField] private bool playOnSelect = false;
    [SerializeField] private bool playOnClick = true;
    [SerializeField] private bool playOnDisabledClick = false;
    [SerializeField] [Min(0f)] private float hoverCooldown = 0.05f;

    [Header("Custom Clips")]
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip disabledClickClip;

    private static AudioSource runtimeFallbackSource;
    private float lastHoverPlayTime = float.NegativeInfinity;

    private void Reset()
    {
        targetButton = GetComponent<Button>();
        localFallbackSource = GetComponent<AudioSource>();
        volumeScale = 1f;
        hoverCooldown = 0.05f;
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

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!playOnHover || !CanPlayInteractableSound()) return;
        TryPlayHover();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!playOnSelect || !CanPlayInteractableSound()) return;
        TryPlayHover();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
        if (!TryGetTargetButton(out var button)) return;

        if (button.IsInteractable())
        {
            if (playOnClick)
            {
                PlayClip(clickClip, AudioManager.Instance != null ? AudioManager.Instance.UIButtonClickClip : null);
            }
            return;
        }

        if (playOnDisabledClick)
        {
            PlayClip(disabledClickClip, AudioManager.Instance != null ? AudioManager.Instance.UIButtonDisabledClip : null);
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!playOnClick || !CanPlayInteractableSound()) return;
        PlayClip(clickClip, AudioManager.Instance != null ? AudioManager.Instance.UIButtonClickClip : null);
    }

    private void CacheReferences()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }

        if (localFallbackSource == null)
        {
            localFallbackSource = GetComponent<AudioSource>();
        }
    }

    private bool CanPlayInteractableSound()
    {
        return TryGetTargetButton(out var button) && button.IsActive() && button.IsInteractable();
    }

    private bool TryGetTargetButton(out Button button)
    {
        CacheReferences();
        button = targetButton;
        return button != null;
    }

    private void TryPlayHover()
    {
        if (Time.unscaledTime - lastHoverPlayTime < hoverCooldown) return;

        lastHoverPlayTime = Time.unscaledTime;
        PlayClip(hoverClip, AudioManager.Instance != null ? AudioManager.Instance.UIButtonHoverClip : null);
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

        GameObject host = GameObject.Find("UIButtonSfxRuntime");
        if (host == null)
        {
            host = new GameObject("UIButtonSfxRuntime");
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
