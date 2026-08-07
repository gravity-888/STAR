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
        [SerializeField] float centerTolerance = 0.2f;   // 빛이 "정확히 중앙"으로 들어왔다고 볼 수직 오차(칸). 이 안일 때만 광량 인식.
        const float HalfSize = 0.45f;                    // 수광부 콜라이더 반폭(SolidRoot 0.9)
        float _acc;
        float _charge;   // 0..chargeTime
        bool _latched;   // 래치되면 이후 빛과 무관하게 개방 유지

        public bool IsOpen { get; private set; }
        // 이번 프레임 광량이 임계 이상인지(충전 시간과 무관한 즉시 판정). EnsureUnsolvedStart가 "정답 배치인지"를 이걸로 본다.
        public bool IsLit => _acc >= threshold - 0.001f;
        public float ChargeFraction => chargeTime > 0.0001f ? Mathf.Clamp01(_charge / chargeTime) : (IsOpen ? 1f : 0f);
        public event Action OnOpen;                 // 열리는 엣지에서 1회(스테이지 진행 등)
        public event Action<bool> OnStateChanged;   // 개폐 상태가 바뀔 때마다(문 여닫이용)

        Transform _gaugeFill;   // 게이지 채움 피벗(y 스케일 = 충전 비율, 아래에서 차오름)

        public void SetGauge(Transform fillPivot) { _gaugeFill = fillPivot; UpdateGauge(); }

        // 이미 푼 스테이지로 되돌아올 때: 충전 없이 즉시 개방·래치. 문은 MapLoader가 SetOpenImmediate로 맞춘다(이벤트 미발생).
        public void PresetOpen()
        {
            _charge = chargeTime;
            _latched = true;
            IsOpen = true;
            UpdateGauge();
        }

        public Vector2 Interact(Beam incoming, List<Beam> outgoing)
        {
            Vector2 c = transform.position;
            // 인식(충전)은 빛이 "정확히 중앙"으로 들어올 때만 — 광선과 중심의 수직거리가 centerTolerance 이내일 때.
            Vector2 rel = c - incoming.origin;
            float perp = Mathf.Abs(rel.x * incoming.dir.y - rel.y * incoming.dir.x);   // 광선과 중심의 수직거리(dir 정규화됨)
            if (perp <= centerTolerance) _acc += incoming.intensity;   // 흡수: outgoing 없음. 충전·개폐는 Commit에서.
            // 표면 접점: 빔은 수광부에 실제로 들어오는 지점에서 끝(중앙 스냅 없음).
            return EntryPoint(incoming.origin, incoming.dir, c);
        }

        // 광선(O,D)이 수광부 AABB(중심 C, 반폭 HalfSize)에 진입하는 지점. 안 만나면 중심.
        static Vector2 EntryPoint(Vector2 o, Vector2 d, Vector2 c)
        {
            float tmin = -1e18f, tmax = 1e18f;
            for (int i = 0; i < 2; i++)
            {
                float oi = i == 0 ? o.x : o.y, di = i == 0 ? d.x : d.y, ci = i == 0 ? c.x : c.y;
                if (Mathf.Abs(di) < 1e-6f) { if (oi < ci - HalfSize || oi > ci + HalfSize) return c; }
                else
                {
                    float t1 = (ci - HalfSize - oi) / di, t2 = (ci + HalfSize - oi) / di;
                    if (t1 > t2) (t1, t2) = (t2, t1);
                    tmin = Mathf.Max(tmin, t1); tmax = Mathf.Min(tmax, t2);
                }
            }
            if (tmax < Mathf.Max(tmin, 0f)) return c;
            return o + d * Mathf.Max(tmin, 0f);
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
            _gaugeFill.localScale = new Vector3(s.x, ChargeFraction, s.z);   // y = 충전 비율(아래에서 위로 차오름)
        }
    }
}
