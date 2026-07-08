using UnityEngine;
using TowardTheStars.Light;

namespace TowardTheStars.Objects
{
    // 랜즈/광원: 고정 방향으로 빛을 발사한다. 콜라이더 없음(빛 시작점).
    public class LightSource : MonoBehaviour
    {
        [SerializeField] Vector2 direction = Vector2.down;
        [SerializeField] float intensity = 1f;

        public Vector2 Direction => direction;

        public void Init(Vector2 dir, float intensity = 1f)
        {
            direction = dir.normalized;
            this.intensity = intensity;
        }

        // 초기 광선을 생성해 반환.
        public Beam Emit() => new Beam(transform.position, direction, intensity);
    }
}
