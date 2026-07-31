using System;
using System.Collections.Generic;
using UnityEngine;
using TowardTheStars.Light;

namespace TowardTheStars.Objects
{
    // 게이트 수광부: 도달 광량 Σ≥threshold를 "일정 시간(chargeTime) 유지"하면 개방(충전식).
    //   빛을 받는 동안 충전이 차오르고, 빛이 끊기면 줄어든다. 충전이 가득 차면 열림, 아래로 떨어지면 닫힘.
    //   시각화: 임시 게이지(fillPivot의 x 스케일 = 충전 비율). 상태에 따른 색 변경 없음(단일 아트).
    //   매 프레임 재추적 흐름: BeginFrame()으로 누적만 0 → Interact로 누적 → Commit()에서 충전·개폐 확정.
    public class GateDetector : MonoBehaviour, IBeamHit
    {
        [SerializeField] float threshold = 1.0f;
        [SerializeField] float chargeTime = 1.2f;   // 빛을 이만큼(초) 유지하면 개방
        [SerializeField] bool latchOnOpen = true;   // 완전히 열리면 빛이 끊겨도 열린 상태 유지(래치)
        float _acc;
        float _charge;   // 0..chargeTime
        bool _latched;   // 래치되면 이후 빛과 무관하게 개방 유지

        public bool IsOpen { get; private set; }
        // 이번 프레임 광량이 임계 이상인지(충전 시간과 무관한 즉시 판정). EnsureUnsolvedStart가 "정답 배치인지"를 이걸로 본다.
        public bool IsLit => _acc >= threshold - 0.001f;
        public float ChargeFraction => chargeTime > 0.0001f ? Mathf.Clamp01(_charge / chargeTime) : (IsOpen ? 1f : 0f);
        public event Action OnOpen;                 // 열리는 엣지에서 1회(스테이지 진행 등)
        public event Action<bool> OnStateChanged;   // 개폐 상태가 바뀔 때마다(문 여닫이용)

        Transform _gaugeFill;   // 게이지 채움 피벗(x 스케일 = 충전 비율, 왼쪽 정렬로 자라남)

        public void SetGauge(Transform fillPivot) { _gaugeFill = fillPivot; UpdateGauge(); }

        public void Interact(Beam incoming, Vector2 hitCenter, List<Beam> outgoing)
        {
            _acc += incoming.intensity;   // 흡수: outgoing 없음. 충전·개폐는 Commit에서.
        }

        // 재추적 시작: 누적만 0으로(충전/상태는 유지).
        public void BeginFrame() => _acc = 0f;

        // 재추적 종료: 이번 프레임 광량으로 충전을 올리거나 내리고, 가득 차면 개방.
        public void Commit()
        {
            if (_latched) { UpdateGauge(); return; }   // 이미 완전 개방 → 빛과 무관하게 유지(충전 감소 없음)

            bool lit = _acc >= threshold - 0.001f;
            float dt = Time.deltaTime;
            _charge = Mathf.Clamp(_charge + (lit ? dt : -dt), 0f, chargeTime);
            UpdateGauge();

            bool open = _charge >= chargeTime - 0.0001f;
            if (open == IsOpen) return;
            IsOpen = open;
            if (open && latchOnOpen) _latched = true;   // 완전 개방 순간 래치 → 이후 빛이 가려져도 열린 채 유지
            OnStateChanged?.Invoke(open);   // 개폐부(문) 여닫이
            if (open) OnOpen?.Invoke();
        }

        void UpdateGauge()
        {
            if (_gaugeFill == null) return;
            var s = _gaugeFill.localScale;
            _gaugeFill.localScale = new Vector3(ChargeFraction, s.y, s.z);
        }
    }
}
