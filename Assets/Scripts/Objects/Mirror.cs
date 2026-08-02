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
        // 반사면 반길이(칸). 입사광이 이 선분 범위에서 반사한다. 색 막대 길이 1.1칸에 맞춤(거울 이동 시 접점이 이 범위를 따라 움직임).
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
            // 입사광이 반사면(선분)과 만나는 실제 지점에서 반사한다. 거울을 이동하면 접점이 반사면을 따라 움직이며
            //   반사되는 빛의 위치가 연속적으로(끊김 없이) 달라진다. 정지 상태에선 접점이 고정 → 빔 안정.
            //   반사 "방향"은 거울 각도(법선)로 결정되므로 위치와 무관(평면거울). 위치만 접점을 따른다.
            float th = angleDeg * Mathf.Deg2Rad;
            Vector2 t = new(Mathf.Sin(th), Mathf.Cos(th));   // 반사면 방향(법선 n=(cosθ,−sinθ)에 수직)
            Vector2 p = SurfaceHitPoint(incoming.origin, incoming.dir, transform.position, t);
            outgoing.Add(new Beam(p, Reflect(incoming.dir), incoming.intensity));
            return p;
        }

        // 광선(O,D)이 반사면 선분(중심 C, 방향 t, 반길이 surfaceHalfLength)과 만나는 점.
        //   선분 밖이면 끝으로 클램프(빔은 콜라이더에 맞았으니 가장 가까운 표면점으로), 평행이면 중심.
        Vector2 SurfaceHitPoint(Vector2 o, Vector2 d, Vector2 c, Vector2 t)
        {
            Vector2 rhs = c - o;
            float det = t.x * d.y - d.x * t.y;
            if (Mathf.Abs(det) < 1e-6f) return c;                       // 평행 → 중심
            float u = (d.x * rhs.y - d.y * rhs.x) / det;                // 반사면 축 위 접점 좌표
            u = Mathf.Clamp(u, -surfaceHalfLength, surfaceHalfLength);
            return c + t * u;
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
