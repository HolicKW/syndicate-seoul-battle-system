using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public static class BattleResultUIBuilder
{
    [MenuItem("Tools/Build Battle Result UI")]
    public static void BuildBattleResultUI()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("Canvas not found");
            return;
        }

        // Check if BattleResultPanel already exists
        var existingPanel = canvas.transform.Find("BattleResultPanel");
        if (existingPanel != null)
        {
            GameObject.DestroyImmediate(existingPanel.gameObject);
        }

        // Create Panel
        GameObject panelObj = new GameObject("BattleResultPanel");
        panelObj.transform.SetParent(canvas.transform, false);
        var panelImg = panelObj.AddComponent<Image>();
        panelImg.color = new Color(0, 0, 0, 0.7f); // semi-transparent black
        
        var panelRT = panelObj.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.sizeDelta = Vector2.zero;
        panelRT.anchoredPosition = Vector2.zero;

        // Result Text
        GameObject textObj = new GameObject("ResultText");
        textObj.transform.SetParent(panelObj.transform, false);
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        
        var textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.5f, 0.5f);
        textRT.anchorMax = new Vector2(0.5f, 0.5f);
        textRT.sizeDelta = new Vector2(800, 300);
        textRT.anchoredPosition = Vector2.zero;

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Maplestory Bold SDF.asset");

        tmp.text = "승리!";
        tmp.fontSize = 120;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        if (font != null) tmp.font = font;

        // Add BattleResultManager to Canvas (so it's always active)
        var manager = canvas.GetComponent<BattleResultManager>();
        if (manager == null)
            manager = canvas.AddComponent<BattleResultManager>();
        
        // Setup references
        var so = new SerializedObject(manager);
        
        so.FindProperty("resultPanel").objectReferenceValue = panelObj;
        so.FindProperty("resultText").objectReferenceValue = tmp;
        // engine 필드는 비워두면 런타임에 BattleEngine.Instance로 자동 연결된다.

        so.ApplyModifiedProperties();

        // Hide panel initially so it doesn't block the editor view
        panelObj.SetActive(false);

        EditorUtility.SetDirty(canvas);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log("[BattleResultUIBuilder] Battle Result UI built successfully!");
    }
}
