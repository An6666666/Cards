using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BattleManager
{
    public void ShowBattlePhaseHint(string message, float duration = -1f, bool showCentralHint = true)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        BattlePhaseHintType hintType = ResolveBattlePhaseHintType(message);
        UpdateTopPhaseIndicator(hintType);

        if (phaseHintCoroutine != null)
        {
            StopCoroutine(phaseHintCoroutine);
            phaseHintCoroutine = null;
        }

        if (!showCentralHint)
        {
            HideCentralPhaseHintVisuals();
            return;
        }

        EnsurePhaseHintText();
        if (phaseHintText == null && phaseHintTmpText == null && phaseHintImage == null)
        {
            return;
        }

        float showDuration = duration > 0f ? duration : phaseHintDuration;
        phaseHintCoroutine = StartCoroutine(ShowBattlePhaseHintRoutine(message, showDuration, hintType));
    }

    public IEnumerator ShowBattlePhaseHintAndWait(string message, float duration = -1f, bool showCentralHint = true)
    {
        if (showCentralHint)
        {
            PushPhaseHintInteractionLock();
        }

        try
        {
            ShowBattlePhaseHint(message, duration, showCentralHint);

            if (!showCentralHint)
            {
                yield break;
            }

            float holdDuration = duration > 0f ? duration : phaseHintDuration;
            float waitDuration = Mathf.Max(0f, holdDuration)
                + Mathf.Max(0f, phaseHintFadeInDuration)
                + Mathf.Max(0f, phaseHintFadeOutDuration);

            if (waitDuration > 0f)
            {
                yield return new WaitForSeconds(waitDuration);
            }
        }
        finally
        {
            if (showCentralHint)
            {
                PopPhaseHintInteractionLock();
            }
        }
    }

    private IEnumerator ShowBattlePhaseHintRoutine(string message, float duration, BattlePhaseHintType hintType)
    {
        if (!TryPrepareCentralPhaseHintVisual(hintType, message, out CanvasGroup activeCanvasGroup, out GameObject activeObject))
        {
            yield break;
        }

        float fadeIn = Mathf.Max(0f, phaseHintFadeInDuration);
        float fadeOut = Mathf.Max(0f, phaseHintFadeOutDuration);
        float hold = Mathf.Max(0f, duration);

        SetCentralPhaseHintAlpha(activeCanvasGroup, 0f);
        HideCentralPhaseHintVisuals();
        ShowUiObjectAndParents(activeObject, true);

        if (fadeIn > 0f)
        {
            yield return FadePhaseHintAlpha(activeCanvasGroup, 0f, 1f, fadeIn);
        }
        else
        {
            SetCentralPhaseHintAlpha(activeCanvasGroup, 1f);
        }

        if (hold > 0f)
        {
            yield return new WaitForSeconds(hold);
        }

        if (fadeOut > 0f)
        {
            yield return FadePhaseHintAlpha(activeCanvasGroup, 1f, 0f, fadeOut);
        }
        else
        {
            SetCentralPhaseHintAlpha(activeCanvasGroup, 0f);
        }

        HideCentralPhaseHintVisuals();
        phaseHintCoroutine = null;
    }

    private void EnsurePhaseHintText()
    {
        if (phaseHintText != null || phaseHintTmpText != null)
        {
            EnsurePhaseHintCanvasGroups();
            return;
        }

        Text[] allTexts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < allTexts.Length; i++)
        {
            Text candidate = allTexts[i];
            if (candidate == null)
            {
                continue;
            }

            string nameText = candidate.gameObject.name;
            if (nameText.Contains("BattlePhaseHint") || nameText.Contains("PhaseHint") || nameText.Contains("TurnHint"))
            {
                phaseHintText = candidate;
                EnsurePhaseHintCanvasGroups();
                return;
            }
        }

        TMP_Text[] allTmpTexts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < allTmpTexts.Length; i++)
        {
            TMP_Text candidate = allTmpTexts[i];
            if (candidate == null)
            {
                continue;
            }

            string nameText = candidate.gameObject.name;
            if (nameText.Contains("BattlePhaseHint") || nameText.Contains("PhaseHint") || nameText.Contains("TurnHint"))
            {
                phaseHintTmpText = candidate;
                EnsurePhaseHintCanvasGroups();
                return;
            }
        }

        Canvas targetCanvas = GetComponentInChildren<Canvas>(true);
        if (targetCanvas == null)
        {
            targetCanvas = FindObjectOfType<Canvas>();
        }

        if (targetCanvas == null)
        {
            return;
        }

        GameObject hintObj = new GameObject("BattlePhaseHintText", typeof(RectTransform), typeof(Text));
        RectTransform rect = hintObj.GetComponent<RectTransform>();
        rect.SetParent(targetCanvas.transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.88f);
        rect.anchorMax = new Vector2(0.5f, 0.88f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(800f, 90f);
        rect.anchoredPosition = Vector2.zero;

        Text text = hintObj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 42;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 1f, 1f, 1f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = string.Empty;

        phaseHintText = text;
        EnsurePhaseHintCanvasGroups();
    }

    private void EnsurePhaseHintCanvasGroups()
    {
        if (phaseHintText != null && phaseHintLegacyTextCanvasGroup == null)
        {
            phaseHintLegacyTextCanvasGroup = phaseHintText.GetComponent<CanvasGroup>()
                ?? phaseHintText.gameObject.AddComponent<CanvasGroup>();
        }

        if (phaseHintTmpText != null && phaseHintTmpTextCanvasGroup == null)
        {
            phaseHintTmpTextCanvasGroup = phaseHintTmpText.GetComponent<CanvasGroup>()
                ?? phaseHintTmpText.gameObject.AddComponent<CanvasGroup>();
        }

        if (phaseHintImage != null && phaseHintImageCanvasGroup == null)
        {
            phaseHintImageCanvasGroup = phaseHintImage.GetComponent<CanvasGroup>()
                ?? phaseHintImage.gameObject.AddComponent<CanvasGroup>();
        }

        if (phaseHintLegacyTextCanvasGroup != null)
        {
            phaseHintLegacyTextCanvasGroup.interactable = false;
            phaseHintLegacyTextCanvasGroup.blocksRaycasts = false;
        }

        if (phaseHintTmpTextCanvasGroup != null)
        {
            phaseHintTmpTextCanvasGroup.interactable = false;
            phaseHintTmpTextCanvasGroup.blocksRaycasts = false;
        }

        if (phaseHintImageCanvasGroup != null)
        {
            phaseHintImageCanvasGroup.interactable = false;
            phaseHintImageCanvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator FadePhaseHintAlpha(CanvasGroup canvasGroup, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetCentralPhaseHintAlpha(canvasGroup, to);
            yield break;
        }

        float elapsed = 0f;
        SetCentralPhaseHintAlpha(canvasGroup, from);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetCentralPhaseHintAlpha(canvasGroup, Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetCentralPhaseHintAlpha(canvasGroup, to);
    }

    private void SetCentralPhaseHintAlpha(CanvasGroup canvasGroup, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }

        if (phaseHintImage != null)
        {
            Color imageColor = phaseHintImage.color;
            imageColor.a = alpha;
            phaseHintImage.color = imageColor;
        }

        if (phaseHintTmpText != null)
        {
            Color tmpColor = phaseHintTmpText.color;
            tmpColor.a = alpha;
            phaseHintTmpText.color = tmpColor;
        }

        if (phaseHintText == null)
        {
            return;
        }

        Color color = phaseHintText.color;
        color.a = alpha;
        phaseHintText.color = color;
    }

    private bool TryPrepareCentralPhaseHintVisual(
        BattlePhaseHintType hintType,
        string message,
        out CanvasGroup activeCanvasGroup,
        out GameObject activeObject)
    {
        EnsurePhaseHintText();
        EnsurePhaseHintCanvasGroups();

        if (TryGetCenterHintSprite(hintType, out Sprite sprite) && phaseHintImage != null)
        {
            phaseHintImage.sprite = sprite;
            activeCanvasGroup = phaseHintImageCanvasGroup;
            activeObject = phaseHintImage.gameObject;
            return true;
        }

        if (phaseHintTmpText != null)
        {
            phaseHintTmpText.text = message;
            activeCanvasGroup = phaseHintTmpTextCanvasGroup;
            activeObject = phaseHintTmpText.gameObject;
            return true;
        }

        if (phaseHintText != null)
        {
            phaseHintText.text = message;
            activeCanvasGroup = phaseHintLegacyTextCanvasGroup;
            activeObject = phaseHintText.gameObject;
            return true;
        }

        activeCanvasGroup = null;
        activeObject = null;
        return false;
    }

    private void HideCentralPhaseHintVisuals()
    {
        if (phaseHintText != null)
        {
            Color textColor = phaseHintText.color;
            textColor.a = 0f;
            phaseHintText.color = textColor;
            phaseHintText.gameObject.SetActive(false);
        }

        if (phaseHintTmpText != null)
        {
            Color tmpColor = phaseHintTmpText.color;
            tmpColor.a = 0f;
            phaseHintTmpText.color = tmpColor;
            phaseHintTmpText.gameObject.SetActive(false);
        }

        if (phaseHintImage != null)
        {
            Color imageColor = phaseHintImage.color;
            imageColor.a = 0f;
            phaseHintImage.color = imageColor;
            phaseHintImage.gameObject.SetActive(false);
        }
    }

    private void ShowUiObjectAndParents(GameObject target, bool bringToFront)
    {
        if (target == null)
        {
            return;
        }

        Transform current = target.transform;

        while (current != null)
        {
            current.gameObject.SetActive(true);
            current = current.parent;
        }

        if (bringToFront)
        {
            current = target.transform;
            while (current.parent != null)
            {
                current.SetAsLastSibling();
                current = current.parent;
            }
        }
    }

    private BattlePhaseHintType ResolveBattlePhaseHintType(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return BattlePhaseHintType.Unknown;
        }

        if (message.Contains("玩家回合"))
        {
            return BattlePhaseHintType.PlayerTurn;
        }

        if (message.Contains("妖怪回合") || message.Contains("敵人回合"))
        {
            return BattlePhaseHintType.EnemyTurn;
        }

        if (message.Contains("選擇落點回合"))
        {
            return BattlePhaseHintType.SelectStartTile;
        }

        return BattlePhaseHintType.Unknown;
    }

    private bool TryGetCenterHintSprite(BattlePhaseHintType hintType, out Sprite sprite)
    {
        sprite = null;

        switch (hintType)
        {
            case BattlePhaseHintType.PlayerTurn:
                sprite = playerTurnCenterSprite;
                break;
            case BattlePhaseHintType.EnemyTurn:
                sprite = enemyTurnCenterSprite;
                break;
            case BattlePhaseHintType.SelectStartTile:
                sprite = selectStartTileCenterSprite;
                break;
        }

        return sprite != null;
    }

    private void UpdateTopPhaseIndicator(BattlePhaseHintType hintType)
    {
        if (phaseTopIndicatorImage == null)
        {
            return;
        }

        Sprite targetSprite = null;

        switch (hintType)
        {
            case BattlePhaseHintType.PlayerTurn:
                targetSprite = playerTurnTopSprite;
                break;
            case BattlePhaseHintType.EnemyTurn:
                targetSprite = enemyTurnTopSprite;
                break;
            case BattlePhaseHintType.SelectStartTile:
                targetSprite = selectStartTileTopSprite;
                break;
            default:
                break;
        }

        if (targetSprite == null)
        {
            HideTopPhaseIndicator();
            return;
        }

        phaseTopIndicatorImage.sprite = targetSprite;
        ShowUiObjectAndParents(phaseTopIndicatorImage.gameObject, true);
    }

    private void HideTopPhaseIndicator()
    {
        if (phaseTopIndicatorImage != null)
        {
            phaseTopIndicatorImage.gameObject.SetActive(false);

            Transform parent = phaseTopIndicatorImage.transform.parent;
            if (parent != null)
            {
                parent.gameObject.SetActive(false);
            }
        }
    }
}
