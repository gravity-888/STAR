using UnityEngine;

namespace TowardTheStars.Objects
{
    // 거울: 입사 광선을 반사한다. 반사는 각도(AngleDeg) 기반 — 콜라이더 모양과 무관.
    // 반사식[GDD §30]: 법선 n=(cosθ, −sinθ), r = d − 2(d·n)n. 결과는 22.5° 배수여야 유효.
    public class Mirror : MonoBehaviour
    {
        [SerializeField] float angleDeg;
        [SerializeField] bool isFixed;

        public float AngleDeg => angleDeg;
        public bool IsFixed => isFixed;

        public void Init(float angle, bool fixedMirror)
        {
            angleDeg = angle;
            isFixed = fixedMirror;
        }

        // 입사 방향 d를 받아 반사 방향을 돌려준다(정규화).
        public Vector2 Reflect(Vector2 d)
        {
            float th = angleDeg * Mathf.Deg2Rad;
            Vector2 n = new Vector2(Mathf.Cos(th), -Mathf.Sin(th));
            return (d - 2f * Vector2.Dot(d, n) * n).normalized;
        }
    }
}
