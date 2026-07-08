using System;
using System.Collections.Generic;
using UnityEngine;
using TowardTheStars.Light;

namespace TowardTheStars.Objects
{
    // 게이트 수광부: 도달한 광량을 누적, 임계(Σ≥1.0) 이상이면 개방[GDD §28·§29].
    // 빛을 흡수하므로 이어지는 광선은 없음. 재추적 전 ResetAcc()로 초기화.
    public class GateDetector : MonoBehaviour, IBeamHit
    {
        [SerializeField] float threshold = 1.0f;
        float acc;

        public bool IsOpen { get; private set; }
        public event Action OnOpen;

        [Header("개방 시 색 변경(선택)")]
        public SpriteRenderer visual;
        public Color openColor = new(0.4f, 1f, 0.5f);
        Color _closedColor;
        bool _closedCached;

        public void Interact(Beam incoming, Vector2 hitCenter, List<Beam> outgoing)
        {
            Add(incoming.intensity);   // 흡수: outgoing 없음
        }

        public void Add(float intensity)
        {
            acc += intensity;
            if (!IsOpen && acc >= threshold - 0.001f)
            {
                IsOpen = true;
                if (visual != null) visual.color = openColor;
                OnOpen?.Invoke();
            }
        }

        public void ResetAcc()
        {
            acc = 0f;
            if (IsOpen)
            {
                IsOpen = false;
                if (visual != null && _closedCached) visual.color = _closedColor;
            }
        }

        // 닫힘 색을 기억(첫 배치 시 MapLoader가 호출).
        public void CacheClosedColor()
        {
            if (visual != null) { _closedColor = visual.color; _closedCached = true; }
        }
    }
}
