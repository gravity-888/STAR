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
        [SerializeField] bool isFixed;

        public float AngleDeg => angleDeg;
        public bool IsFixed => isFixed;

        public void Init(float angleDeg, bool isFixed)
        {
            this.angleDeg = angleDeg;
            this.isFixed = isFixed;
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

        // 시각용 막대(자식)를 rotation_z = -angle 로 회전.
        void ApplyVisualRotation()
        {
            var visual = transform.Find("visual");
            if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);
        }
    }
}
