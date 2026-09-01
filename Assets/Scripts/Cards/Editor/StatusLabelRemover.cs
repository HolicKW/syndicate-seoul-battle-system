using UnityEditor;
using UnityEngine;

/// <summary>
/// 상태이상 패널의 Label(설명 텍스트) 자식 오브젝트를 일괄 제거한다.
/// Tools > Remove Status Labels 실행
/// </summary>
public static class StatusLabelRemover
{
    private static readonly string[] StatusPanelNames =
    {
        "PlayerShieldPanel", "PlayerStrengthPanel", "PlayerWeaknessPanel",
        "PlayerVirusPanel",  "PlayerCorrosionPanel","PlayerOverclockPanel",
        "PlayerLuckPanel",   "PlayerNetworkPanel",
        "EnemyShieldPanel",  "EnemyStrengthPanel",  "EnemyWeaknessPanel",
        "EnemyVirusPanel",   "EnemyCorrosionPanel", "EnemyOverclockPanel",
        "EnemyLuckPanel",    "EnemyNetworkPanel",
    };

    [MenuItem("Tools/Remove Status Labels")]
    public static void RemoveStatusLabels()
    {
        int removed = 0;

        foreach (var panelName in StatusPanelNames)
        {
            var panel = GameObject.Find(panelName);
            if (panel == null)
            {
                Debug.LogWarning($"[StatusLabelRemover] 패널 없음: {panelName}");
                continue;
            }

            var label = panel.transform.Find("Label");
            if (label == null)
            {
                Debug.Log($"[StatusLabelRemover] Label 없음 (이미 제거됨?): {panelName}");
                continue;
            }

            Undo.DestroyObjectImmediate(label.gameObject);
            removed++;
            Debug.Log($"[StatusLabelRemover] Label 제거: {panelName}");
        }

        if (removed > 0)
        {
            EditorUtility.SetDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()[0]);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[StatusLabelRemover] 완료: {removed}개 Label 제거. 씬을 저장해주세요.");
        }
        else
        {
            Debug.Log("[StatusLabelRemover] 제거할 Label이 없습니다.");
        }
    }
}
