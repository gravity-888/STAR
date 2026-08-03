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
        // 반사면 반길이(칸). 입사광이 이 범위 안에서 반사면과 만날 때만 반사, 벗어나면 통과. 색 막대 길이 1.1칸에 맞춤.
        [SerializeField] float surfaceHalfLength = 0.55f;

        public float AngleDeg => angleDeg;
        public bool IsFixed => isFixed;
        public string Id;   // 맵의 거울 id(진행상태 저장/복원 키)

        // 현재 각도를 직접 지정(진행상태 복원용). 22.5° 배수로 저장되므로 Q/E 정합 유지.
        public void SetAngle(float a)
        {
            angleDeg = Mathf.Repeat(a, 360f);
            ApplyVisualRotation();
        }

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

        public Vector2 Interact(Beam incoming, List<Beam> outgoing)
        {
            // 입사광선과 반사면 막대의 교점을 광선 위에서 구한다(클램프 없음 → 입사각 보존, 광원 광선 각도 불변).
            //   교점이 막대 범위(±surfaceHalfLength) 안이면 반사, 벗어나면(거울 밖) 반사 없이 통과. 방향은 각도(법선)로 결정.
            float th = angleDeg * Mathf.Deg2Rad;
            Vector2 tan = new(Mathf.Sin(th), Mathf.Cos(th));   // 반사면 방향(법선 n=(cosθ,−sinθ)에 수직)
            Vector2 o = incoming.origin, d = incoming.dir, c = transform.position;
            Vector2 rhs = c - o;
            float det = tan.x * d.y - d.x * tan.y;
            float u;
            Vector2 p;
            if (Mathf.Abs(det) < 1e-4f) { u = 0f; p = o + d * Vector2.Dot(rhs, d); }   // 거의 평행 → 수선의 발
            else { u = (d.x * rhs.y - d.y * rhs.x) / det; p = c + tan * u; }           // 광선-반사면 교점(광선 위)

            if (Mathf.Abs(u) <= surfaceHalfLength)
            {
                // 반사면 안에서 맞음 → 반사(단, 방향이 안 바뀌면=평행 스침 종료).
                Vector2 r = Reflect(d);
                if (Vector2.Dot(r, d) < 0.999f) outgoing.Add(new Beam(p, r, incoming.intensity));
            }
            else
            {
                // 반사면(막대)을 벗어남 = 거울 밖 → 반사 없이 같은 방향으로 통과.
                outgoing.Add(new Beam(p, d, incoming.intensity));
            }
            return p;
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
