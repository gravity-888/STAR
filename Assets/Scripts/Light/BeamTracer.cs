using System.Collections.Generic;
using UnityEngine;
using TowardTheStars.Objects;

namespace TowardTheStars.Light
{
    // 빛 추적 + 연출. 판정은 Raycast, 각 오브젝트가 자기 상호작용(IBeamHit)을 수행한다.
    // 프리즘 분기를 위해 스택 기반으로 여러 광선을 추적. 세그먼트는 LineRenderer 풀로 렌더.
    public class BeamTracer : MonoBehaviour
    {
        public int maxDepth = 64;
        public float maxLength = 200f;
        public float width = 0.08f;
        public Color color = new(1f, 0.95f, 0.5f);

        readonly List<LineRenderer> _pool = new();
        Material _mat;

        // 거울/프리즘 상태 변경 시 호출(Phase 4). 광원부터 다시 추적.
        public void Trace()
        {
            Physics2D.queriesStartInColliders = false;   // 시작점이 자기 콜라이더 안이어도 무시
            Physics2D.queriesHitTriggers = false;        // 사다리(Trigger)는 빛 통과
            Physics2D.SyncTransforms();

            foreach (var gate in Object.FindObjectsByType<GateDetector>(FindObjectsSortMode.None))
                gate.ResetAcc();

            var segments = new List<(Vector2 a, Vector2 b)>();
            var stack = new Stack<(Beam beam, int depth)>();

            foreach (var src in Object.FindObjectsByType<LightSource>(FindObjectsSortMode.None))
                stack.Push((src.Emit(), 0));

            var outgoing = new List<Beam>();
            while (stack.Count > 0)
            {
                var (beam, depth) = stack.Pop();
                if (depth > maxDepth) continue;

                var hit = Physics2D.Raycast(beam.origin, beam.dir, maxLength);
                if (hit.collider == null)
                {
                    segments.Add((beam.origin, beam.origin + beam.dir * maxLength));
                    continue;
                }

                var interactable = hit.collider.GetComponent<IBeamHit>();
                if (interactable != null)
                {
                    // 격자 정합을 위해 오브젝트 중심으로 스냅
                    Vector2 center = hit.collider.transform.position;
                    segments.Add((beam.origin, center));

                    outgoing.Clear();
                    interactable.Interact(beam, center, outgoing);
                    foreach (var o in outgoing) stack.Push((o, depth + 1));
                }
                else
                {
                    // 벽 등 불투명 → 정지
                    segments.Add((beam.origin, hit.point));
                }
            }

            Render(segments);
        }

        void Render(List<(Vector2 a, Vector2 b)> segments)
        {
            if (_mat == null) _mat = new Material(Shader.Find("Sprites/Default"));

            while (_pool.Count < segments.Count)
            {
                var go = new GameObject($"beam_seg_{_pool.Count}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.material = _mat;
                lr.numCapVertices = 2;
                lr.textureMode = LineTextureMode.Stretch;
                lr.sortingOrder = 10;
                _pool.Add(lr);
            }

            for (int i = 0; i < _pool.Count; i++)
            {
                var lr = _pool[i];
                if (i < segments.Count)
                {
                    lr.enabled = true;
                    lr.startWidth = lr.endWidth = width;
                    lr.startColor = lr.endColor = color;
                    lr.positionCount = 2;
                    lr.SetPosition(0, segments[i].a);
                    lr.SetPosition(1, segments[i].b);
                }
                else lr.enabled = false;
            }
        }
    }
}
