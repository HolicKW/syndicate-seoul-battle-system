using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 전투 종료 시 승리/패배 UI를 표시하고,
/// 결과를 BattleSceneData에 저장한 뒤 Management 씬으로 복귀하는 매니저.
/// </summary>
public class BattleResultManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultText;

    [Header("Event References")]
    [Tooltip("전투 종료 이벤트를 받을 BattleEngine. 비워두면 BattleEngine.Instance를 사용한다.")]
    [SerializeField] private BattleEngine engine;

    [Header("돌아가기 버튼 설정")]
    [SerializeField] private Button returnButton;
    [SerializeField] private Vector2 returnBtnSize = new Vector2(200f, 50f);
    [SerializeField] private Vector2 returnBtnOffset = new Vector2(0f, -80f);
    [SerializeField] private Color returnBtnColor = new Color(0.3f, 0.5f, 0.8f, 1f);
    [SerializeField] private float returnBtnFontSize = 22f;
    [SerializeField] private TMP_FontAsset returnBtnFont;

    void Start()
    {
        // 처음에는 패널 숨김
        if (resultPanel != null)
            resultPanel.SetActive(false);

        // 인스펙터에서 지정하지 않았으면 싱글턴을 사용한다.
        if (engine == null)
            engine = BattleEngine.Instance;

        if (engine != null)
            engine.OnBattleEnd += OnBattleEnded;
        else
            Debug.LogWarning("[BattleResultManager] BattleEngine을 찾지 못해 결과 패널이 표시되지 않습니다.");
    }

    void OnDestroy()
    {
        if (engine != null)
            engine.OnBattleEnd -= OnBattleEnded;

        if (returnButton != null)
            returnButton.onClick.RemoveListener(OnReturnClicked);
    }

    /// <summary>
    /// BattleEngine.OnBattleEnd 콜백. won=true 승리, false 패배.
    /// </summary>
    private void OnBattleEnded(bool won)
    {
        if (won)
            ShowVictory();
        else
            ShowDefeat();
    }

    private void ShowVictory()
    {
        if (BattleSceneData.IsTutorial) return;

        BattleSceneData.HasResult = true;
        BattleSceneData.AttackerWon = !BattleSceneData.PlayerControlsDefender;

        string message = BattleSceneData.PlayerControlsDefender ? "방어 성공!" : "승리!";
        ShowResult(message, new Color(0.2f, 0.8f, 0.2f));
    }

    private void ShowDefeat()
    {
        if (BattleSceneData.IsTutorial) return;

        BattleSceneData.HasResult = true;
        BattleSceneData.AttackerWon = BattleSceneData.PlayerControlsDefender;

        ShowResult("패배...", new Color(0.8f, 0.2f, 0.2f));
    }

    private void ShowResult(string message, Color textColor)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            // 맨 앞으로 가져와서 모든 UI를 덮도록 함
            resultPanel.transform.SetAsLastSibling();
        }

        if (resultText != null)
        {
            resultText.text = message;
            resultText.color = textColor;
        }

        ConfigureReturnButton(returnButton);
        if (returnButton != null)
            returnButton.gameObject.SetActive(true);
    }

    private void ConfigureReturnButton(Button button)
    {
        if (button == null) return;

        var btnImg = button.GetComponent<Image>();
        if (btnImg != null)
            btnImg.color = returnBtnColor;

        var btnRT = button.GetComponent<RectTransform>();
        if (btnRT != null)
        {
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = returnBtnSize;
            btnRT.anchoredPosition = returnBtnOffset;
        }

        var tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = "돌아가기";
            tmp.fontSize = returnBtnFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;

            if (returnBtnFont != null)
                tmp.font = returnBtnFont;
        }

        button.onClick.RemoveListener(OnReturnClicked);
        button.onClick.AddListener(OnReturnClicked);
    }

    /// <summary>
    /// "돌아가기" 버튼 클릭 시 Management 씬으로 복귀합니다.
    /// </summary>
    private void OnReturnClicked()
    {
        CardGameSFXManager.PlayBasicClick();
        SceneManager.LoadScene("Management");
    }
}
