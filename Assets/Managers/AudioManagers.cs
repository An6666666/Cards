using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    public AudioSource BGMSource;   // BGM 使用
    public AudioSource SFXSource;   // 效果音使用

    [Header("BGM Clips")]
    public AudioClip cityBGM;       // 背景音樂

    [Header("SFX Clips")]
    public AudioClip attackFire;    // 火屬性攻擊
    public AudioClip attackWood;    // 木屬性攻擊
    public AudioClip attackWater;   // 水屬性攻擊
    public AudioClip attackIce;     // 冰屬性攻擊
    public AudioClip attackThunder; // 雷屬性攻擊

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        DontDestroyOnLoad(gameObject); // 切場景保留
    }

    /// <summary>
    /// 播放背景音樂
    /// </summary>
    public void PlayBGM(AudioClip clip)
    {
        if (BGMSource.clip == clip && BGMSource.isPlaying) return;
        BGMSource.clip = clip;
        BGMSource.Play();
    }

    /// <summary>
    /// 播放單次音效
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null)
            SFXSource.PlayOneShot(clip);
    }

    /// <summary>
    /// 根據元素屬性播放攻擊音效
    /// </summary>
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

    //調整背景音量大小
        public void SetBGMVolume(float value)
    {
        if (BGMSource != null) BGMSource.volume = value;
    }

    public void SetSFXVolume(float value)
    {
        if (SFXSource != null) SFXSource.volume = value;
    }

}
