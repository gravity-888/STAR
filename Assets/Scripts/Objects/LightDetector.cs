using System;
using UnityEngine;

namespace TowardTheStars.Objects
{
    // 게이트 광센서: 도달한 광량을 누적, 임계(Σ≥1.0) 이상이면 개방 [GDD §28·§29].
    // 부동소수 오차 대비 여유(-0.001). 재추적 전 ResetAcc()로 초기화.
    public class LightDetector : MonoBehaviour
    {
        [SerializeField] float threshold = 1.0f;
        float acc;

        public bool IsOpen { get; private set; }
        public event Action OnOpen;

        [Header("개방 시 색 변경(선택)")]
        public SpriteRenderer visual;
        public Color openColor = new(0.4f, 1f, 0.5f);
        Color _closedColor;

        void Awake()
        {
            if (visual != null) _closedColor = visual.color;
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
                if (visual != null) visual.color = _closedColor;
            }
        }
    }
}
