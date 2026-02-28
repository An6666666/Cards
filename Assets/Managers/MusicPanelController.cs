using UnityEngine;
using UnityEngine.UI;

public class MusicPanelController : MonoBehaviour
{
    [Header("Sliders")]
    public Slider bgmSlider;
    public Slider sfxSlider;
    private AudioManager cachedAudioManager;

    private void Start()
    {
        cachedAudioManager = ResolveAudioManager();

        // 初始化 Slider 值
        if (bgmSlider != null)
        {
            if (cachedAudioManager != null && cachedAudioManager.BGMSource != null)
            {
                bgmSlider.value = cachedAudioManager.BGMSource.volume;
            }
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        if (sfxSlider != null)
        {
            if (cachedAudioManager != null && cachedAudioManager.SFXSource != null)
            {
                sfxSlider.value = cachedAudioManager.SFXSource.volume;
            }
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }
    }

    private void OnBGMVolumeChanged(float value)
    {
        cachedAudioManager ??= ResolveAudioManager();
        if (cachedAudioManager != null)
        {
            cachedAudioManager.SetBGMVolume(value);
        }
    }

    private void OnSFXVolumeChanged(float value)
    {
        cachedAudioManager ??= ResolveAudioManager();
        if (cachedAudioManager != null)
        {
            cachedAudioManager.SetSFXVolume(value);
        }
    }

    private static AudioManager ResolveAudioManager()
    {
        if (AudioManager.Instance != null)
        {
            return AudioManager.Instance;
        }

        return Object.FindObjectOfType<AudioManager>();
    }

    private void OnDestroy()
    {
        if (bgmSlider != null)
            bgmSlider.onValueChanged.RemoveListener(OnBGMVolumeChanged);
        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
    }
}
