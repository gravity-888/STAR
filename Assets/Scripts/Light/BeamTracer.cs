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

        // 재사용 버퍼(매 프레임 재추적 시 GC 억제) + 정적 소스/게이트 캐시.
        readonly List<(Vector2 a, Vector2 b)> _segments = new();
        readonly Stack<(Beam beam, int depth)> _stack = new();
        readonly List<Beam> _outgoing = new();
        LightSource[] _sources;
        GateDetector[] _gates;

        // 매 프레임 재추적: 플레이어 등 움직이는 차폐물의 최신 위치를 반영(물리 후 LateUpdate).
        // 거울 회전은 별도 호출 없이 다음 LateUpdate에서 자동 반영. 에디트 모드 미리보기는 MapLoader가 1회 호출.
        void LateUpdate() => Trace();

        public void Trace()
        {
            Physics2D.queriesStartInColliders = false;   // 시작점이 자기 콜라이더 안이어도 무시
            Physics2D.queriesHitTriggers = false;        // 사다리(Trigger)는 빛 통과
            Physics2D.SyncTransforms();

            _gates ??= Object.FindObjectsByType<GateDetector>(FindObjectsSortMode.None);
            foreach (var gate in _gates) if (gate != null) gate.BeginFrame();

            var segments = _segments; segments.Clear();
            var stack = _stack; stack.Clear();
            var outgoing = _outgoing;

            _sources ??= Object.FindObjectsByType<LightSource>(FindObjectsSortMode.None);
            foreach (var src in _sources) if (src != null) stack.Push((src.Emit(), 0));

            while (stack.Count > 0)
            {
                var (beam, depth) = stack.Pop();
                if (depth > maxDepth) continue;

                // 투과 콜라이더(발판)는 건너뛰며 첫 유효 히트(광학 오브젝트/벽)까지 스캔.
                RaycastHit2D hit = default;
                Vector2 scanFrom = beam.origin;
                float remaining = maxLength;
                for (int skip = 0; skip < 32; skip++)
                {
                    hit = Physics2D.Raycast(scanFrom, beam.dir, remaining);
                    if (hit.collider == null) break;
                    if (hit.collider.GetComponent<IBeamHit>() == null &&
                        hit.collider.GetComponent<BeamTransparent>() != null)
                    {
                        // 발판: 빛 통과 → 표면 조금 너머로 스캔 지점 전진(같은 발판 재검출 방지)
                        float step = hit.distance + 0.02f;
                        remaining -= step;
                        scanFrom = hit.point + beam.dir * 0.02f;
                        if (remaining <= 0f) { hit = default; }
                        else continue;
                    }
                    break;
                }

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

            // 누적 완료 → 게이트 개폐 확정(엣지 트리거)
            foreach (var gate in _gates) if (gate != null) gate.Commit();

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
