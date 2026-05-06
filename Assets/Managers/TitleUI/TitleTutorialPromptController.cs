using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class TitleTutorialPromptController : MonoBehaviour
{
    [Header("Prompt Panel")]
    [SerializeField, Tooltip("Small panel root shown after TitleScene NewButton is clicked.")]
    private GameObject promptRoot;
    [SerializeField, Tooltip("Button that opens the tutorial pages.")]
    private Button enterTutorialButton;
    [SerializeField, Tooltip("Button that skips the tutorial and starts the new game.")]
    private Button skipTutorialButton;
    [SerializeField, Tooltip("Optional button that closes this prompt without starting a new game.")]
    private Button promptCloseButton;

    [Header("Tutorial Panel")]
    [SerializeField, Tooltip("Root object for the tutorial page viewer.")]
    private GameObject tutorialRoot;
    [SerializeField, Tooltip("RawImage used by Tutorial Video Player.")]
    private RawImage videoSurface;
    [SerializeField, Tooltip("VideoPlayer used to play the four tutorial clips.")]
    private VideoPlayer videoPlayer;
    [SerializeField, Tooltip("Image used on the final tutorial page.")]
    private Image guideImage;
    [SerializeField, Tooltip("Previous page button.")]
    private Button previousButton;
    [SerializeField, Tooltip("Next page button.")]
    private Button nextButton;
    [SerializeField, Tooltip("Close button shown only on the final page. Closing starts the new game.")]
    private Button finishButton;
    [SerializeField, Tooltip("Optional page label, for example 1/5.")]
    private Text pageLabel;
    [SerializeField, Tooltip("Optional TMP page label, for example 1/5.")]
    private TMP_Text pageTmpLabel;

    [Header("Tutorial Assets")]
    [SerializeField, Tooltip("Assign exactly four tutorial videos here. Empty entries are skipped visually but still count as pages.")]
    private VideoClip[] tutorialVideos = new VideoClip[4];
    [SerializeField, Tooltip("Final page image that introduces the tutorial interface.")]
    private Sprite tutorialInterfaceSprite;

    private const int VideoPageCount = 4;
    private const int TotalPageCount = VideoPageCount + 1;

    private Action onTutorialFinished;
    private RenderTexture videoTexture;
    private int pageIndex;

    public bool IsOpen =>
        (promptRoot != null && promptRoot.activeInHierarchy) ||
        (tutorialRoot != null && tutorialRoot.activeInHierarchy);

    private void Awake()
    {
        BindButtons();
        HideAll();
    }

    private void OnDisable()
    {
        StopVideo();
    }

    private void Update()
    {
        if (tutorialRoot == null || !tutorialRoot.activeInHierarchy)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            PreviousPage();
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            NextPage();
        }
    }

    private void OnDestroy()
    {
        StopVideo();

        if (videoTexture != null)
        {
            videoTexture.Release();
            Destroy(videoTexture);
            videoTexture = null;
        }
    }

    public void Open(Action finishAction)
    {
        onTutorialFinished = finishAction;
        StopVideo();

        if (tutorialRoot != null)
        {
            tutorialRoot.SetActive(false);
        }

        if (promptRoot != null)
        {
            promptRoot.SetActive(true);
        }
        else
        {
            OpenTutorial();
        }
    }

    public void Close()
    {
        HideAll();
        onTutorialFinished = null;
    }

    private void BindButtons()
    {
        BindButton(enterTutorialButton, OpenTutorial);
        BindButton(skipTutorialButton, FinishTutorial);
        BindButton(promptCloseButton, Close);
        BindButton(previousButton, PreviousPage);
        BindButton(nextButton, NextPage);
        BindButton(finishButton, FinishTutorial);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void OpenTutorial()
    {
        if (promptRoot != null)
        {
            promptRoot.SetActive(false);
        }

        if (tutorialRoot != null)
        {
            tutorialRoot.SetActive(true);
        }

        ShowPage(0);
    }

    private void PreviousPage()
    {
        ShowPage(Mathf.Max(0, pageIndex - 1));
    }

    private void NextPage()
    {
        ShowPage(Mathf.Min(TotalPageCount - 1, pageIndex + 1));
    }

    private void ShowPage(int targetIndex)
    {
        pageIndex = Mathf.Clamp(targetIndex, 0, TotalPageCount - 1);
        bool isImagePage = pageIndex == TotalPageCount - 1;

        if (videoSurface != null)
        {
            videoSurface.gameObject.SetActive(!isImagePage);
        }

        if (guideImage != null)
        {
            guideImage.gameObject.SetActive(isImagePage);
            guideImage.sprite = tutorialInterfaceSprite;
            guideImage.enabled = tutorialInterfaceSprite != null;
        }

        if (previousButton != null)
        {
            previousButton.interactable = pageIndex > 0;
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(!isImagePage);
            nextButton.interactable = pageIndex < TotalPageCount - 1;
        }

        if (finishButton != null)
        {
            finishButton.gameObject.SetActive(isImagePage);
        }

        SetPageLabel($"{pageIndex + 1}/{TotalPageCount}");

        if (isImagePage)
        {
            StopVideo();
        }
        else
        {
            PlayVideoPage(pageIndex);
        }
    }

    private void PlayVideoPage(int videoIndex)
    {
        if (videoPlayer == null)
        {
            return;
        }

        VideoClip clip = tutorialVideos != null &&
            videoIndex >= 0 &&
            videoIndex < tutorialVideos.Length
                ? tutorialVideos[videoIndex]
                : null;

        StopVideo();

        if (clip == null)
        {
            if (videoSurface != null)
            {
                videoSurface.texture = null;
            }

            return;
        }

        EnsureVideoTexture();
        videoPlayer.clip = clip;
        videoPlayer.isLooping = true;
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;

        if (videoSurface != null)
        {
            videoSurface.texture = videoTexture;
        }

        videoPlayer.Play();
    }

    private void EnsureVideoTexture()
    {
        if (videoTexture != null)
        {
            return;
        }

        videoTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
        {
            name = "TitleTutorialVideoTexture"
        };
        videoTexture.Create();
    }

    private void StopVideo()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.targetTexture = null;
        }
    }

    private void SetPageLabel(string value)
    {
        if (pageLabel != null)
        {
            pageLabel.text = value;
        }

        if (pageTmpLabel != null)
        {
            pageTmpLabel.text = value;
        }
    }

    private void FinishTutorial()
    {
        Action finishAction = onTutorialFinished;
        HideAll();
        onTutorialFinished = null;
        finishAction?.Invoke();
    }

    private void HideAll()
    {
        StopVideo();

        if (promptRoot != null)
        {
            promptRoot.SetActive(false);
        }

        if (tutorialRoot != null)
        {
            tutorialRoot.SetActive(false);
        }
    }
}
