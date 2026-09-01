using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오픈 액세스 카드의 [공격]/[스킬]/[코어] 선택 팝업.
/// </summary>
public class OpenAccessTypeSelectUI : MonoBehaviour
{
    private GameObject overlay;
    private string selectedFilter;
    private bool isDone;

    public IEnumerator ShowAndWait(Canvas canvas)
    {
        isDone = false;
        selectedFilter = null;

        BuildUI(canvas);
        yield return new WaitUntil(() => isDone);
        DestroyUI();
    }

    public string GetResult()
    {
        return selectedFilter;
    }

    private void BuildUI(Canvas canvas)
    {
        if (canvas == null)
        {
            selectedFilter = "attack";
            isDone = true;
            return;
        }

        overlay = new GameObject("OpenAccessTypeSelectOverlay",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        var overlayRT = overlay.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        var panel = new GameObject("TypeSelectPanel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(overlay.transform, false);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(520f, 260f);
        panelRT.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.13f, 0.96f);

        var title = CreateText(panel.transform, "Title",
            new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.92f));
        title.text = "카드 종류 선택";
        title.fontSize = 26;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var hint = CreateText(panel.transform, "Hint",
            new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.70f));
        hint.text = "뽑은 카드가 선택한 종류이면 같은 종류의 카드를 1장 더 뽑습니다.";
        hint.fontSize = 15;
        hint.alignment = TextAlignmentOptions.Center;
        hint.color = new Color(0.82f, 0.86f, 0.92f);

        CreateButton(panel.transform, "AttackButton", "공격", "attack", -145f);
        CreateButton(panel.transform, "SkillButton", "스킬", "skill", 0f);
        CreateButton(panel.transform, "CoreButton", "코어", "core", 145f);
    }

    private void CreateButton(Transform parent, string name, string label, string filter, float x)
    {
        var buttonGO = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(parent, false);

        var rt = buttonGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.30f);
        rt.anchorMax = new Vector2(0.5f, 0.30f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 56f);
        rt.anchoredPosition = new Vector2(x, 0f);

        buttonGO.GetComponent<Image>().color = new Color(0.18f, 0.35f, 0.58f, 1f);

        var buttonText = CreateText(buttonGO.transform, "Label", Vector2.zero, Vector2.one);
        buttonText.text = label;
        buttonText.fontSize = 22;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = Color.white;

        var button = buttonGO.GetComponent<Button>();
        button.onClick.AddListener(() => Select(filter));
    }

    private void Select(string filter)
    {
        CardGameSFXManager.PlayBasicClick();
        selectedFilter = filter;
        isDone = true;
    }

    private void DestroyUI()
    {
        if (overlay != null)
            Destroy(overlay);
        overlay = null;
    }

    private static TMP_Text CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return go.GetComponent<TMP_Text>();
    }
}
