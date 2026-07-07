using System.Collections.Generic;
using UnityEngine;
using TowardTheStars.Data;
using TowardTheStars.Objects;
using TowardTheStars.Light;

namespace TowardTheStars.Level
{
    // Phase 1: 프리팹 없이 JSON 좌표대로 오브젝트를 "색깔 사각형 플레이스홀더"로 배치·시각화.
    // 완료 기준(기획서): 4개 스테이지를 로드하면 오브젝트가 좌표대로 배치된다(빛 판정은 이후 Phase).
    //
    // 사용법:
    //   1) 빈 GameObject에 이 스크립트를 붙인다.
    //   2) StageKey를 "stage1"~"stage4" 중 하나로 설정.
    //   3) Play 하면 자동 배치. 또는 컴포넌트 우클릭 → "Rebuild" / "Clear".
    public class LevelBuilder : MonoBehaviour
    {
        [Header("어느 스테이지를 배치할지")]
        public string stageKey = "stage2";

        [Header("옵션")]
        public bool buildOnStart = true;
        public bool frameCamera = true;   // 메인 카메라를 스테이지에 맞춰 자동 조정

        // 색상 팔레트(플레이스홀더)
        static readonly Color C_Terrain  = new(0.35f, 0.26f, 0.18f);       // 지형(갈색)
        static readonly Color C_Wall     = new(0.20f, 0.20f, 0.24f);       // 벽(짙은 회색)
        static readonly Color C_Platform = new(0.30f, 0.55f, 0.95f, 0.55f);// 빛투과 발판(반투명 파랑)
        static readonly Color C_Source   = new(1.00f, 0.90f, 0.30f);       // 광원(노랑)
        static readonly Color C_Gate     = new(0.30f, 0.90f, 0.45f);       // 게이트(초록)
        static readonly Color C_GateZone = new(0.30f, 0.90f, 0.45f, 0.30f);// 게이트 열림구간(반투명)
        static readonly Color C_Mirror   = new(0.55f, 0.90f, 1.00f);       // 거울(하늘색)
        static readonly Color C_MirrorFix = new(0.60f, 0.60f, 0.65f);      // 고정 거울(회색)
        static readonly Color C_Prism    = new(0.95f, 0.45f, 0.95f);       // 프리즘(자홍)
        static readonly Color C_Decoy    = new(0.95f, 0.35f, 0.35f, 0.6f); // 오답(반투명 빨강)
        static readonly Color C_Spawn    = new(1.00f, 1.00f, 1.00f);       // 스폰(흰색)

        // 렌더 순서(sortingOrder)
        const int Z_TERRAIN = 0, Z_PLATFORM = 1, Z_ZONE = 2, Z_OBJECT = 5, Z_SPAWN = 8;

        Transform _root;
        static Sprite _square;

        void Start()
        {
            if (buildOnStart) Build();
        }

        [ContextMenu("Rebuild")]
        public void Build()
        {
            var stage = StageDataLoader.LoadStage(stageKey);
            if (stage == null) return;

            Clear();
            _root = new GameObject($"Level_{stageKey}").transform;
            _root.SetParent(transform, false);

            BuildTerrain(stage);
            BuildWalls(stage);
            BuildPlatforms(stage);
            BuildGateZone(stage);
            BuildSource(stage);
            BuildPrism(stage);
            BuildGate(stage);
            BuildMirrors(stage);
            BuildDecoys(stage);
            BuildSpawn(stage);
            BuildBeam(stage);   // Phase 2: 거울·게이트 배치 후 빛 추적

            if (frameCamera) FrameCamera(stage);

            Debug.Log($"[LevelBuilder] '{stageKey}' 배치 완료 — " +
                      $"거울 {stage.Mirrors?.Count ?? 0} · 발판 {stage.Platforms?.Count ?? 0} · " +
                      $"프리즘 {(stage.Prism != null ? "있음" : "없음")}");
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            if (_root != null) { DestroySafe(_root.gameObject); _root = null; }
            // 에디터에서 이전 잔여물 정리
            var existing = transform.Find($"Level_{stageKey}");
            if (existing != null) DestroySafe(existing.gameObject);
        }

        // ---------- 요소별 배치 ----------

        void BuildTerrain(StageData s)
        {
            if (s.Terrain == null) return;
            foreach (var kv in s.Terrain)
            {
                if (!int.TryParse(kv.Key, out int x)) continue;
                int height = kv.Value;   // 표면 높이(솔리드). y=0..height 채움. (해석: 하단 솔리드)
                for (int y = 0; y <= height; y++)
                    Piece($"terrain_{x}_{y}", new Vector2(x, y), C_Terrain, Z_TERRAIN, Vector2.one);
            }
        }

        void BuildWalls(StageData s)
        {
            foreach (var c in s.AllWalls())
            {
                var root = SolidRoot($"wall_{c[0]}_{c[1]}", new Vector2(c[0], c[1]), 1.0f);
                VisualChild(root.transform, C_Wall, Z_TERRAIN, Vector2.one);
            }
        }

        void BuildPlatforms(StageData s)
        {
            if (s.Platforms == null) return;
            foreach (var p in s.Platforms)
            {
                if (p.Missing || p.Cells == null) continue;   // stage4 미설계 발판 스킵 [갭]
                foreach (var c in p.Cells)
                    Piece($"plat_{p.Id}_{c[0]}_{c[1]}", new Vector2(c[0], c[1]),
                          C_Platform, Z_PLATFORM, new Vector2(1f, 0.4f));
            }
        }

        void BuildGateZone(StageData s)
        {
            if (s.GateOpenZone == null) return;
            foreach (var c in s.GateOpenZone)
                Piece($"gatezone_{c[0]}_{c[1]}", new Vector2(c[0], c[1]),
                      C_GateZone, Z_ZONE, Vector2.one);
        }

        void BuildSource(StageData s)
        {
            if (s.Source?.Pos == null) return;
            var pos = new Vector2(s.Source.Pos[0], s.Source.Pos[1]);
            Piece("source", pos, C_Source, Z_OBJECT, Vector2.one * 0.8f);
            // 발사 방향 표식(작은 점을 방향으로 오프셋)
            var dir = GridMap.DirToVector(s.Source.Dir);
            if (dir != Vector2.zero)
                Piece("source_dir", pos + dir * 0.6f, C_Source, Z_SPAWN, Vector2.one * 0.25f);
        }

        void BuildPrism(StageData s)
        {
            if (s.Prism?.Pos == null) return;
            Piece("prism", new Vector2(s.Prism.Pos[0], s.Prism.Pos[1]),
                  C_Prism, Z_OBJECT, Vector2.one * 0.9f, 45f);   // 마름모꼴로 표시
        }

        void BuildGate(StageData s)
        {
            if (s.Gate?.Pos == null) return;
            var root = SolidRoot("gate", new Vector2(s.Gate.Pos[0], s.Gate.Pos[1]), 0.9f);
            var det = root.AddComponent<LightDetector>();
            var sr = VisualChild(root.transform, C_Gate, Z_OBJECT, new Vector2(0.9f, 0.9f));
            det.visual = sr;   // 개방 시 색 변경용
        }

        void BuildMirrors(StageData s)
        {
            if (s.Mirrors == null) return;
            foreach (var m in s.Mirrors)
            {
                if (m.Pos == null) continue;
                var root = SolidRoot($"mirror_{m.Id}", new Vector2(m.Pos[0], m.Pos[1]), 0.9f);
                root.AddComponent<Mirror>().Init(m.AngleDeg, m.Fixed);
                // 얇은 막대를 rotation_z(= -angle)로 회전 → 거울 면을 시각화 [GDD §30]
                var col = m.Fixed ? C_MirrorFix : C_Mirror;
                VisualChild(root.transform, col, Z_OBJECT, new Vector2(1.1f, 0.18f), m.RotationZ);
            }
        }

        void BuildBeam(StageData s)
        {
            if (s.Source?.Pos == null) return;
            var dir = GridMap.DirToVector(s.Source.Dir);
            if (dir == Vector2.zero)
            {
                Debug.LogWarning($"[LevelBuilder] 소스 방향 해석 실패: '{s.Source.Dir}'");
                return;
            }
            var go = new GameObject("Beam");
            go.transform.SetParent(_root, false);
            var beam = go.AddComponent<BeamController>();
            beam.origin = new Vector2(s.Source.Pos[0], s.Source.Pos[1]);
            beam.direction = dir;
            beam.intensity = 1f;
            beam.Retrace();
        }

        void BuildDecoys(StageData s)
        {
            if (s.Decoys == null) return;
            foreach (var d in s.Decoys)
            {
                if (d.Pos == null) continue;
                Piece($"decoy_{d.Id}", new Vector2(d.Pos[0], d.Pos[1]),
                      C_Decoy, Z_OBJECT, Vector2.one * 0.8f, 45f);
            }
        }

        void BuildSpawn(StageData s)
        {
            if (s.Spawn == null || s.Spawn.Length < 2) return;
            Piece("spawn", new Vector2(s.Spawn[0], s.Spawn[1]),
                  C_Spawn, Z_SPAWN, Vector2.one * 0.6f);
        }

        // ---------- 유틸 ----------

        GameObject Piece(string name, Vector2 pos, Color col, int order, Vector2 scale, float rotZ = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, rotZ);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Square;
            sr.color = col;
            sr.sortingOrder = order;
            return go;
        }

        // 콜라이더가 있는 광학 오브젝트의 루트(스케일 1, 정사각 콜라이더). 시각은 자식으로 분리.
        GameObject SolidRoot(string name, Vector2 pos, float colliderSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(colliderSize, colliderSize);
            return go;
        }

        // 루트에 붙는 시각 사각형(콜라이더 없음). 회전/스케일은 시각에만 적용돼 반사 판정과 분리.
        SpriteRenderer VisualChild(Transform parent, Color col, int order, Vector2 scale, float rotZ = 0f)
        {
            var go = new GameObject("visual");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Square;
            sr.color = col;
            sr.sortingOrder = order;
            return sr;
        }

        void FrameCamera(StageData s)
        {
            var cam = Camera.main;
            if (cam == null || s.Grid == null) return;
            cam.orthographic = true;
            float cx = (s.Grid.W - 1) * 0.5f;
            float cy = (s.Grid.H - 1) * 0.5f;
            cam.transform.position = new Vector3(cx, cy, -10f);
            // 세로 기준 + 여백. 가로가 더 넓으면 가로에 맞춰 확대.
            float sizeByH = s.Grid.H * 0.5f + 1f;
            float sizeByW = (s.Grid.W * 0.5f + 1f) / Mathf.Max(cam.aspect, 0.01f);
            cam.orthographicSize = Mathf.Max(sizeByH, sizeByW);
        }

        static Sprite Square
        {
            get
            {
                if (_square == null)
                {
                    var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    _square = Sprite.Create(tex, new Rect(0, 0, 1, 1),
                                            new Vector2(0.5f, 0.5f), 1f);  // pixelsPerUnit=1 → 1셀=1유닛
                }
                return _square;
            }
        }

        static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
