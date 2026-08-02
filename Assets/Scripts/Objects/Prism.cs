using System.Collections.Generic;
using UnityEngine;
using TowardTheStars.Light;

namespace TowardTheStars.Objects
{
    // 프리즘(고정): 입사광을 여러 방향으로 분기[GDD §40].
    // 규칙: 한 면 입력 → 반대면 직선 + 상단 대각(2방향), 에너지 균등 분배(0.5+0.5).
    // 출력 방향은 맵 데이터(out)에서 주입받는다.
    public class Prism : MonoBehaviour, IBeamHit
    {
        [SerializeField] List<Vector2> outDirs = new();

        public void Init(IEnumerable<Vector2> outDirections)
        {
            outDirs = new List<Vector2>();
            foreach (var d in outDirections)
                if (d != Vector2.zero) outDirs.Add(d.normalized);
        }

        public Vector2 Interact(Beam incoming, List<Beam> outgoing)
        {
            Vector2 c = transform.position;   // 프리즘은 중심에서 분기
            if (outDirs.Count == 0) return c;
            float share = incoming.intensity / outDirs.Count;   // 2방향 → 0.5+0.5
            foreach (var d in outDirs)
                outgoing.Add(new Beam(c, d, share));
            return c;
        }
    }
}
