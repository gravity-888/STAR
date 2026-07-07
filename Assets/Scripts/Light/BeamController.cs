using System.Collections.Generic;
using UnityEngine;
using TowardTheStars.Objects;

namespace TowardTheStars.Light
{
    // 빛 판정(Raycast)과 연출(LineRenderer)을 함께 담당하는 단일 빔 추적기(Phase 2, 반사 전용).
    // 프리즘 분기(0.5+0.5)는 Phase 3에서 추가. Stage 2(순수 반사)를 첫 목표로 검증.
    //
    // 판정: 광원에서 방향으로 Raycast → 거울이면 반사(중심으로 스냅) → 게이트면 광량 등록 → 벽이면 정지.
    // 연출: 경로 꼭짓점을 LineRenderer 폴리라인으로 그림.
    [RequireComponent(typeof(LineRenderer))]
    public class BeamController : MonoBehaviour
    {
        [Header("광원 설정 (LevelBuilder가 채움)")]
        public Vector2 origin;
        public Vector2 direction = Vector2.down;
        public float intensity = 1.0f;

        [Header("추적 한계")]
        public int maxBounce = 32;
        public float maxLength = 100f;

        [Header("연출")]
        public float beamWidth = 0.08f;
        public Color beamColor = new(1f, 0.95f, 0.5f);

        LineRenderer _line;

        void Awake() => EnsureLine();

        void Start() => Retrace();

        void EnsureLine()
        {
            if (_line != null) return;
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.numCapVertices = 2;
            _line.numCornerVertices = 2;
            _line.textureMode = LineTextureMode.Stretch;
            _line.alignment = LineAlignment.View;
            _line.startWidth = _line.endWidth = beamWidth;
            // URP 2D에서 안전한 셰이더. 정점 색상 반영됨.
            if (_line.sharedMaterial == null)
                _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.startColor = _line.endColor = beamColor;
            _line.sortingOrder = 10;   // 오브젝트 위에 그려지도록
        }

        // 빔 경로를 다시 계산하고 그린다. 거울 회전 등 변경 시 호출(Phase 4 연동 예정).
        public void Retrace()
        {
            EnsureLine();

            // 새 콜라이더 위치 반영 + 시작점이 콜라이더 내부여도 자기 자신은 무시.
            Physics2D.queriesStartInColliders = false;
            Physics2D.SyncTransforms();

            // 재추적 전 모든 센서 초기화
            foreach (var det in Object.FindObjectsByType<LightDetector>(FindObjectsSortMode.None))
                det.ResetAcc();

            var points = new List<Vector3> { origin };
            Vector2 pos = origin;
            Vector2 dir = direction.normalized;
            bool reachedGate = false;

            for (int i = 0; i < maxBounce; i++)
            {
                var hit = Physics2D.Raycast(pos, dir, maxLength);
                if (hit.collider == null)
                {
                    points.Add(pos + dir * maxLength);   // 허공으로 소실
                    break;
                }

                if (hit.collider.TryGetComponent(out Mirror mirror))
                {
                    // 거울 중심으로 스냅 → 격자 정합한 깔끔한 center-to-center 경로
                    Vector2 center = mirror.transform.position;
                    points.Add(center);
                    dir = mirror.Reflect(dir);
                    pos = center;
                    continue;
                }

                if (hit.collider.TryGetComponent(out LightDetector detector))
                {
                    Vector2 center = detector.transform.position;
                    points.Add(center);
                    detector.Add(intensity);
                    reachedGate = true;
                    break;
                }

                // 벽 등 불투명 → 정지(데드엔드)
                points.Add(hit.point);
                break;
            }

            _line.positionCount = points.Count;
            _line.SetPositions(points.ToArray());

            Debug.Log(reachedGate
                ? $"[Beam] 게이트 도달 ✓ (꼭짓점 {points.Count}개)"
                : $"[Beam] 게이트 미도달 (꼭짓점 {points.Count}개)");
        }
    }
}
