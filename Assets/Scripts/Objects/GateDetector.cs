using System;
using System.Collections.Generic;
using UnityEngine;
using TowardTheStars.Light;

namespace TowardTheStars.Objects
{
    // 게이트 수광부: 도달한 광량을 누적, 임계(Σ≥1.0) 이상이면 개방[GDD §28·§29].
    // 빛을 흡수하므로 이어지는 광선은 없음.
    // 매 프레임 재추적 흐름: BeginFrame()으로 누적만 0으로 → Interact로 누적 → Commit()에서 개폐 확정.
    // 개폐는 엣지 트리거(상태가 바뀔 때만 색/이벤트) — 켜져 있는 동안 OnOpen이 매 프레임 터지지 않음.
    public class GateDetector : MonoBehaviour, IBeamHit
    {
        [SerializeField] float threshold = 1.0f;
        float _acc;

        public bool IsOpen { get; private set; }
        public event Action OnOpen;                 // 열리는 엣지에서 1회(스테이지 진행 등)
        public event Action<bool> OnStateChanged;   // 개폐 상태가 바뀔 때마다(문 여닫이용)

        [Header("개방 시 색 변경(선택)")]
        public SpriteRenderer visual;
        public Color openColor = new(0.4f, 1f, 0.5f);
        Color _closedColor;
        bool _closedCached;

        public void Interact(Beam incoming, Vector2 hitCenter, List<Beam> outgoing)
        {
            _acc += incoming.intensity;   // 흡수: outgoing 없음. 개폐 판정은 Commit에서.
        }

        // 재추적 시작: 누적만 초기화(상태/색/이벤트는 건드리지 않음).
        public void BeginFrame() => _acc = 0f;

        // 재추적 종료: 누적 광량으로 개폐 확정. 상태가 바뀔 때만 색 변경·이벤트 발생.
        public void Commit()
        {
            bool open = _acc >= threshold - 0.001f;
            if (open == IsOpen) return;
            IsOpen = open;
            if (visual != null && (_closedCached || open))
                visual.color = open ? openColor : _closedColor;
            OnStateChanged?.Invoke(open);   // 개폐부(문) 여닫이 — 양방향
            if (open) OnOpen?.Invoke();
        }

        // 닫힘 색을 기억(첫 배치 시 MapLoader가 호출).
        public void CacheClosedColor()
        {
            if (visual != null) { _closedColor = visual.color; _closedCached = true; }
        }
    }
}
