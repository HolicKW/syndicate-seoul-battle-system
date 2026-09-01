using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 슬레이더스파이어/하스스톤 스타일 부채꼴 손패 레이아웃.
/// 카드가 겹치며 원호를 그리고, 가운데가 높고 양끝이 낮아지며 기울어진다.
/// </summary>
public class HandFanLayout : MonoBehaviour
{
    [Header("Arc Settings")]
    [Tooltip("원호의 반지름. 클수록 완만한 곡선")]
    [SerializeField] private float arcRadius = 1600f;

    [Tooltip("카드 간 각도 (degree). 작을수록 카드가 밀집")]
    [SerializeField] private float anglePerCard = 5f;

    [Tooltip("최대 전체 각도 (degree). 카드가 많을 때 제한")]
    [SerializeField] private float maxTotalAngle = 40f;

    [Header("Spacing")]
    [Tooltip("카드 간 수평 간격 (px). 겹치게 하려면 카드 폭보다 작게")]
    [SerializeField] private float horizontalSpacing = 80f;

    [Tooltip("손패 전체의 Y 오프셋")]
    [SerializeField] private float baseYOffset = 0f;

    public void ArrangeCards()
    {
        var cards = new List<RectTransform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.gameObject.activeSelf && child.GetComponent<HandCardUI>() != null)
                cards.Add(child.GetComponent<RectTransform>());
        }

        if (cards.Count == 0) return;

        int count = cards.Count;

        // 전체 각도 계산
        float totalAngle = Mathf.Min(maxTotalAngle, (count - 1) * anglePerCard);

        // 전체 수평 폭
        float totalWidth = (count - 1) * horizontalSpacing;

        for (int i = 0; i < count; i++)
        {
            // 정규화 값: -1 (좌) ~ 0 (중앙) ~ +1 (우)
            float t = (count == 1) ? 0f : ((float)i / (count - 1)) * 2f - 1f;

            // X: 균등 수평 배치
            float x = (count == 1) ? 0f : -totalWidth / 2f + i * horizontalSpacing;

            // Y: 원호 - 가운데가 가장 높고 양끝이 낮음
            float y = baseYOffset + (1f - t * t) * (arcRadius * (1f - Mathf.Cos(totalAngle * 0.5f * Mathf.Deg2Rad)));

            // Rotation: 가운데=0°, 좌=+각도, 우=-각도
            float angle = (count == 1) ? 0f : Mathf.Lerp(totalAngle / 2f, -totalAngle / 2f, (float)i / (count - 1));

            var rt = cards[i];
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.localRotation = Quaternion.Euler(0, 0, angle);

            // 렌더 순서: 왼쪽→오른쪽 (뒤→앞)
            rt.SetSiblingIndex(i);
        }
    }
}
