using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class BattleUIBuilder
{
    [MenuItem("Tools/Build Battle UI")]
    public static void BuildBattleUI()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("Canvas not found!");
            return;
        }
        var canvasRT = canvas.GetComponent<RectTransform>();

        // ============================================================
        // 1. EnemyCharacter (SpriteRenderer - Canvas 외부, 우측 배치)
        // ============================================================
        var existingEnemy = GameObject.Find("EnemyCharacter");
        if (existingEnemy != null) Object.DestroyImmediate(existingEnemy);

        var enemyChar = new GameObject("EnemyCharacter");
        var sr = enemyChar.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.5f, 0.5f, 0.5f, 1f); // placeholder
        enemyChar.transform.position = new Vector3(3f, 0.5f, 0); // 포켓몬식 우측

        // ============================================================
        // 2. PlayerInfoPanel (좌측 중앙 - 플레이어 HP, 이름, 상태)
        // ============================================================
        var playerInfo = CreatePanel(canvasRT, "PlayerInfoPanel", 
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), // left-center
            new Vector2(20, -40), new Vector2(320, 120));

        var playerInfoBG = playerInfo.AddComponent<Image>();
        playerInfoBG.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

        // Player name
        var playerName = CreateTMPText(playerInfo.transform, "PlayerName",
            "PLAYER", 18, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(10, -5), new Vector2(-10, -5),
            30);

        // Player HP Bar Background
        var hpBarBG = CreatePanel(playerInfo.transform, "PlayerHPBar_BG",
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(10, -40), new Vector2(-10, -40), 25);
        hpBarBG.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Player HP Bar Fill
        var hpFill = CreatePanel(hpBarBG.transform, "PlayerHPBar_Fill",
            new Vector2(0, 0), new Vector2(1, 1),
            Vector2.zero, Vector2.zero);
        var hpFillImg = hpFill.AddComponent<Image>();
        hpFillImg.color = new Color(0.2f, 0.8f, 0.2f, 1f); // green
        hpFillImg.type = Image.Type.Filled;
        hpFillImg.fillMethod = Image.FillMethod.Horizontal;
        hpFillImg.fillAmount = 0.75f;

        // Player HP text
        CreateTMPText(hpBarBG.transform, "PlayerHP_Text",
            "75 / 100", 14, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 1),
            Vector2.zero, Vector2.zero);

        // Player Status Icons row
        var playerStatusRow = CreatePanel(playerInfo.transform, "PlayerStatusIcons",
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(10, 10), new Vector2(-10, 10), 30);
        var statusLayout = playerStatusRow.AddComponent<HorizontalLayoutGroup>();
        statusLayout.spacing = 5;
        statusLayout.childAlignment = TextAnchor.MiddleLeft;
        statusLayout.childForceExpandWidth = false;
        statusLayout.childForceExpandHeight = false;

        // ============================================================
        // 3. PlayerStatusBar (좌측 상단 - 버프/디버프 아이콘)
        // ============================================================
        var statusBar = CreatePanel(canvasRT, "PlayerStatusBar",
            new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(20, -20), new Vector2(300, -20), 40);

        var statusBarLayout = statusBar.AddComponent<HorizontalLayoutGroup>();
        statusBarLayout.spacing = 8;
        statusBarLayout.childAlignment = TextAnchor.MiddleLeft;
        statusBarLayout.childForceExpandWidth = false;
        statusBarLayout.childForceExpandHeight = false;
        statusBarLayout.padding = new RectOffset(5, 5, 5, 5);

        var statusBarBG = statusBar.AddComponent<Image>();
        statusBarBG.color = new Color(0.1f, 0.1f, 0.15f, 0.7f);

        // ============================================================
        // 4. EnemyHPPanel (우측 상단 - 적 HP/실드)
        // ============================================================
        var enemyHP = CreatePanel(canvasRT, "EnemyHPPanel",
            new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-320, -20), new Vector2(-20, -20), 130);

        var enemyHPBG = enemyHP.AddComponent<Image>();
        enemyHPBG.color = new Color(0.15f, 0.05f, 0.05f, 0.85f);

        // Enemy name
        CreateTMPText(enemyHP.transform, "EnemyName",
            "ENEMY", 18, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(10, -5), new Vector2(-10, -5), 30);

        // Enemy Shield Bar (orange)
        var shieldBG = CreatePanel(enemyHP.transform, "EnemyShieldBar_BG",
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(10, -40), new Vector2(-10, -40), 20);
        shieldBG.AddComponent<Image>().color = new Color(0.3f, 0.2f, 0.1f, 1f);

        var shieldFill = CreatePanel(shieldBG.transform, "EnemyShieldBar_Fill",
            new Vector2(0, 0), new Vector2(1, 1),
            Vector2.zero, Vector2.zero);
        var shieldFillImg = shieldFill.AddComponent<Image>();
        shieldFillImg.color = new Color(1f, 0.5f, 0f, 1f); // orange
        shieldFillImg.type = Image.Type.Filled;
        shieldFillImg.fillMethod = Image.FillMethod.Horizontal;
        shieldFillImg.fillAmount = 0.5f;

        // Enemy HP Bar
        var enemyHPBarBG = CreatePanel(enemyHP.transform, "EnemyHPBar_BG",
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(10, -65), new Vector2(-10, -65), 25);
        enemyHPBarBG.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        var enemyHPFill = CreatePanel(enemyHPBarBG.transform, "EnemyHPBar_Fill",
            new Vector2(0, 0), new Vector2(1, 1),
            Vector2.zero, Vector2.zero);
        var enemyHPFillImg = enemyHPFill.AddComponent<Image>();
        enemyHPFillImg.color = new Color(0.8f, 0.2f, 0.2f, 1f); // red
        enemyHPFillImg.type = Image.Type.Filled;
        enemyHPFillImg.fillMethod = Image.FillMethod.Horizontal;
        enemyHPFillImg.fillAmount = 1f;

        CreateTMPText(enemyHPBarBG.transform, "EnemyHP_Text",
            "100 / 100", 14, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 1),
            Vector2.zero, Vector2.zero);

        // Enemy status icons
        var enemyStatusRow = CreatePanel(enemyHP.transform, "EnemyStatusIcons",
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(10, 10), new Vector2(-10, 10), 25);
        var enemyStatusLayout = enemyStatusRow.AddComponent<HorizontalLayoutGroup>();
        enemyStatusLayout.spacing = 5;
        enemyStatusLayout.childAlignment = TextAnchor.MiddleLeft;
        enemyStatusLayout.childForceExpandWidth = false;
        enemyStatusLayout.childForceExpandHeight = false;

        // ============================================================
        // 5. CardDetailPanel (우측 - 카드 상세 패널)
        // ============================================================
        var cardDetail = CreatePanel(canvasRT, "CardDetailPanel",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-320, -120), new Vector2(-20, 120));

        var cardDetailBG = cardDetail.AddComponent<Image>();
        cardDetailBG.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        // Card name
        CreateTMPText(cardDetail.transform, "CardDetailName",
            "카드 이름", 22, TextAlignmentOptions.Center,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(10, -10), new Vector2(-10, -10), 35);

        // Card type/tag icons area
        var cardTypeRow = CreatePanel(cardDetail.transform, "CardTypeIcons",
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(10, -50), new Vector2(-10, -50), 30);
        var cardTypeLayout = cardTypeRow.AddComponent<HorizontalLayoutGroup>();
        cardTypeLayout.spacing = 10;
        cardTypeLayout.childAlignment = TextAnchor.MiddleCenter;
        cardTypeLayout.childForceExpandWidth = false;
        cardTypeLayout.childForceExpandHeight = false;

        // Card cost
        CreateTMPText(cardDetail.transform, "CardDetailCost",
            "Cost: 3 RAM", 16, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(10, -85), new Vector2(-10, -85), 25);

        // Card description
        CreateTMPText(cardDetail.transform, "CardDetailDesc",
            "카드 설명이 여기에 표시됩니다.", 14, TextAlignmentOptions.TopLeft,
            new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(10, 10), new Vector2(-10, -115));

        cardDetail.SetActive(false); // 기본 비활성화

        // ============================================================
        // 6. DeckCounter (좌측 하단 - 잔여 덱 수)
        // ============================================================
        var deckCounter = CreatePanel(canvasRT, "DeckCounter",
            new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(20, 20), new Vector2(90, 20), 90);

        var deckBG = deckCounter.AddComponent<Image>();
        deckBG.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

        // Outline
        var deckOutline = deckCounter.AddComponent<Outline>();
        deckOutline.effectColor = new Color(0.4f, 0.4f, 0.5f, 1f);
        deckOutline.effectDistance = new Vector2(2, -2);

        CreateTMPText(deckCounter.transform, "DeckCount_Text",
            "8", 28, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(0, 0), new Vector2(0, -5));

        CreateTMPText(deckCounter.transform, "DeckLabel_Text",
            "DECK", 10, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(0, 5), new Vector2(0, 5), 15);

        // ============================================================
        // 7. RAMCounter (좌측 하단, 덱 위 - 초록 마나)
        // ============================================================
        var ramCounter = CreatePanel(canvasRT, "RAMCounter",
            new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(20, 120), new Vector2(90, 120), 70);

        var ramBG = ramCounter.AddComponent<Image>();
        ramBG.color = new Color(0.05f, 0.2f, 0.1f, 0.9f);

        var ramOutline = ramCounter.AddComponent<Outline>();
        ramOutline.effectColor = new Color(0.1f, 0.7f, 0.3f, 1f);
        ramOutline.effectDistance = new Vector2(2, -2);

        CreateTMPText(ramCounter.transform, "RAM_Text",
            "6/9", 26, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(0, 0), new Vector2(0, -5));

        CreateTMPText(ramCounter.transform, "RAMLabel_Text",
            "RAM", 10, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(0, 5), new Vector2(0, 5), 15);

        // ============================================================
        // 8. HandArea (하단 중앙 - 손패 카드 영역)
        // ============================================================
        var handArea = CreatePanel(canvasRT, "HandArea",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-350, 10), new Vector2(350, 10), 200);

        // ============================================================
        // 9. EndTurnButton (하단 우측)
        // ============================================================
        var endTurnBtn = CreatePanel(canvasRT, "EndTurnButton",
            new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-160, 20), new Vector2(-20, 20), 60);

        var btnImg = endTurnBtn.AddComponent<Image>();
        btnImg.color = new Color(0.6f, 0.15f, 0.15f, 1f);

        var btn = endTurnBtn.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        var btnOutline = endTurnBtn.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.9f, 0.3f, 0.2f, 1f);
        btnOutline.effectDistance = new Vector2(2, -2);

        CreateTMPText(endTurnBtn.transform, "EndTurnBtn_Text",
            "턴 종료", 20, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 1),
            Vector2.zero, Vector2.zero);

        // DeckManager가 이제 UI 갱신도 담당하므로 개별 DeckCounter 부착하지 않음

        Debug.Log("[BattleUIBuilder] Battle UI built successfully!");
    }

    // --- Helper: Create UI Panel ---
    static GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, float height = 0)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;

        if (height > 0)
        {
            // If anchors share same Y, use sizeDelta for height
            if (Mathf.Approximately(anchorMin.y, anchorMax.y))
            {
                rt.sizeDelta = new Vector2(offsetMax.x - offsetMin.x, height);
                rt.anchoredPosition = new Vector2(
                    (offsetMin.x + offsetMax.x) / 2f,
                    offsetMin.y + height / 2f);
            }
            else
            {
                rt.offsetMin = offsetMin;
                rt.offsetMax = new Vector2(offsetMax.x, offsetMin.y + height);
            }
        }
        else
        {
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        return go;
    }

    // --- Helper: Create TMP Text ---
    static GameObject CreateTMPText(Transform parent, string name,
        string text, float fontSize, TextAlignmentOptions alignment,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, float height = 0)
    {
        var go = CreatePanel(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, height);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.enableAutoSizing = false;
        return go;
    }
}
