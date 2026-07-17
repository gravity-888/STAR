using System.Collections.Generic;
using UnityEngine;

namespace TowardTheStars.Objects
{
    // 게이트 개폐부(문): 수광부(GateDetector)가 광량 Σ≥1.0을 받으면 열려 통로가 뚫리고,
    // 그렇지 않으면 닫혀서 솔리드 콜라이더로 플레이어를 막는다.
    // MapLoader가 맵의 gate_open_zone 셀마다 콜라이더+시각을 붙여 이 컴포넌트에 등록한다.
    // 닫힘 = 콜라이더 활성(플레이어·빛 차단, 불투명) / 열림 = 콜라이더 비활성(통과, 반투명).
    public class GateDoor : MonoBehaviour
    {
        [Header("색(닫힘=막힌 장벽 / 열림=반투명 통로)")]
        public Color closedColor = new(0.78f, 0.35f, 0.30f);
        public Color openColor   = new(0.40f, 1.00f, 0.50f, 0.12f);

        readonly List<Collider2D> _blockers = new();
        readonly List<SpriteRenderer> _visuals = new();

        public bool IsOpen { get; private set; }

        // MapLoader가 개폐부 셀(콜라이더+시각)을 하나씩 등록.
        public void Register(Collider2D col, SpriteRenderer sr)
        {
            if (col != null) _blockers.Add(col);
            if (sr != null) _visuals.Add(sr);
        }

        // 개폐 적용. 수광부의 OnStateChanged 구독 대상 — 광량 임계 통과 시 여닫힌다.
        public void SetOpen(bool open)
        {
            IsOpen = open;
            foreach (var c in _blockers) if (c != null) c.enabled = !open;   // 열리면 통과, 닫히면 차단
            foreach (var v in _visuals) if (v != null) v.color = open ? openColor : closedColor;
        }
    }
}
