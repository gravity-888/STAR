using System.Collections.Generic;
using UnityEngine;
using TowardTheStars.Light;

namespace TowardTheStars.Objects
{
    // 거울: 입사광을 각도(AngleDeg) 기반으로 1회 반사한다.
    // 반사식[GDD §30]: 법선 n=(cosθ,−sinθ), r=d−2(d·n)n. 결과는 22.5° 배수여야 유효.
    public class Mirror : MonoBehaviour, IBeamHit
    {
        [SerializeField] float angleDeg;
        [SerializeField] float solutionAngle;   // 정답 각도 — 랜덤 초기화·정답 정렬의 기준
        [SerializeField] bool isFixed;
        // 아트 기본각 보정: 프리팹 아트가 0°가 아닌 방향으로 그려졌을 때 그 차이를 메운다(반사 연산에는 영향 없음).
        [SerializeField] float visualAngleOffset;

        public float AngleDeg => angleDeg;
        public bool IsFixed => isFixed;

        public void Init(float solutionAngle, bool isFixed, float visualAngleOffset = 0f)
        {
            this.solutionAngle = solutionAngle;
            this.angleDeg = solutionAngle;   // 기본은 정답
            this.isFixed = isFixed;
            this.visualAngleOffset = visualAngleOffset;
            ApplyVisualRotation();
        }

        // 정답에서 ±(22.5°×maxSteps) 랜덤하게 틀어 놓는다(회전 가능한 거울만).
        //   22.5° 배수로만 어긋나므로 Q/E(22.5°씩)로 정답에 도달할 수 있다.
        public void RandomizeFromSolution(int maxSteps)
        {
            if (isFixed || maxSteps <= 0) return;
            int steps = Random.Range(-maxSteps, maxSteps + 1);
            angleDeg = Mathf.Repeat(solutionAngle + steps * 22.5f, 360f);
            ApplyVisualRotation();
        }

        // 정답 각도로 즉시 정렬(정답 정렬 키).
        public void SnapToSolution()
        {
            angleDeg = Mathf.Repeat(solutionAngle, 360f);
            ApplyVisualRotation();
        }

        public void Interact(Beam incoming, Vector2 hitCenter, List<Beam> outgoing)
        {
            outgoing.Add(new Beam(hitCenter, Reflect(incoming.dir), incoming.intensity));
        }

        // Phase 4: 플레이어가 22.5°씩 회전. 회전 후 BeamTracer 재추적 필요.
        public void Rotate(int steps)
        {
            if (isFixed) return;
            angleDeg = Mathf.Repeat(angleDeg + steps * 22.5f, 360f);
            ApplyVisualRotation();
        }

        Vector2 Reflect(Vector2 d)
        {
            float th = angleDeg * Mathf.Deg2Rad;
            Vector2 n = new Vector2(Mathf.Cos(th), -Mathf.Sin(th));
            return (d - 2f * Vector2.Dot(d, n) * n).normalized;
        }

        // 시각용 자식("visual")을 rotation_z = -angle + 아트 기본각 보정 으로 회전.
        void ApplyVisualRotation()
        {
            var visual = transform.Find("visual");
            if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, -angleDeg + visualAngleOffset);
        }
    }
}
