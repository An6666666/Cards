using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RunEventUIManager : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;                           // 事件視窗本體
    [SerializeField] private Text titleText;                             // 標題文字元件
    [SerializeField] private Text descriptionText;                       // 事件描述文字元件
    [SerializeField] private List<EventOptionView> optionViews = new();      // 預先放在場景中的選項按鈕們

    private Action<RunEventOption> onOptionSelected;

    private void Awake()
    {
        Hide();
    }

    // 顯示事件內容，並依序綁定選項按鈕
    public void ShowEvent(RunEventDefinition definition, Action<RunEventOption> optionCallback)
    {
        onOptionSelected = optionCallback;

        if (definition == null)
        {
            optionCallback?.Invoke(null);
            return;
        }

        if (panelRoot != null && !panelRoot.activeSelf)
        {
            panelRoot.SetActive(true);
        }

        if (titleText != null)
            titleText.text = definition.Title;

        if (descriptionText != null)
            descriptionText.text = definition.Description;

        IReadOnlyList<RunEventOption> options = definition.Options;
        for (int i = 0; i < optionViews.Count; i++)
        {
            EventOptionView view = optionViews[i];
            bool hasOption = options != null && i < options.Count;
            if (hasOption)
            {
                view.Bind(options[i], HandleOptionClicked);
            }
            else
            {
                view.SetActive(false);
            }
        }
    }

    // 關閉事件視窗
    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void HandleOptionClicked(RunEventOption option)
    {
        Hide();
        onOptionSelected?.Invoke(option);
    }

    [Serializable]
    private class EventOptionView
    {
        [SerializeField] private GameObject root;   // 選項本身的根物件
        [SerializeField] private Button button;     // 按鈕
        [SerializeField] private Text label;    // 顯示選項文字

        public void Bind(RunEventOption option, Action<RunEventOption> onClick)
        {
            SetActive(true);

            if (label != null)
            {
                label.text = option != null ? option.optionLabel : string.Empty;
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke(option));
            }
        }

        public void SetActive(bool active)
        {
            if (root != null)
            {
                root.SetActive(active);
            }
        }
    }
}