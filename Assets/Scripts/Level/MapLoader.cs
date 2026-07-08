using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using TowardTheStars.Data;
using TowardTheStars.Objects;
using TowardTheStars.Light;
using TowardTheStars.Player;

namespace TowardTheStars.Level
{
    // 맵 스크립트: Assets/Maps 의 맵 파일(TextAsset)을 읽어, 각 오브젝트를 배치하고 변수를 주입한다.
    // 오브젝트의 상호작용/연산은 각 오브젝트 스크립트(Mirror/Prism/GateDetector/LightSource)가 담당.
    // 여기서는 "무엇을 어디에 어떤 각도로" 만 책임진다.
    //
    // 사용법: 빈 오브젝트에 이 스크립트를 붙이고, mapFile 칸에 Assets/Maps/stages_unified 를 드래그.
    //         StageKey 설정 후 Play 또는 컴포넌트 우클릭 → Build.
    public class MapLoader : MonoBehaviour
    {
        [Header("맵 파일 (Assets/Maps/stages_unified 드래그)")]
        public TextAsset mapFile;

        [Header("배치할 스테이지")]
        public string stageKey = "stage2";

        [Header("옵션")]
        public bool buildOnStart = true;
        public bool frameCamera = true;

        // 색 팔레트(플레이스홀더)
        static readonly Color C_Terrain  = new(0.35f, 0.26f, 0.18f);
        static readonly Color C_Wall     = new(0.20f, 0.20f, 0.24f);
        static readonly Color C_Platform = new(0.30f, 0.55f, 0.95f, 0.55f);
        static readonly Color C_Lens     = new(1.00f, 0.90f, 0.30f);
        static readonly Color C_Gate     = new(0.30f, 0.90f, 0.45f);
        static readonly Color C_Mirror   = new(0.55f, 0.90f, 1.00f);
        static readonly Color C_MirrorFix = new(0.60f, 0.60f, 0.65f);
        static readonly Color C_Prism    = new(0.95f, 0.45f, 0.95f);
        static readonly Color C_Ladder   = new(0.80f, 0.60f, 0.30f);
        static readonly Color C_Decoy    = new(0.95f, 0.35f, 0.35f, 0.6f);
        static readonly Color C_Spawn    = new(1.00f, 1.00f, 1.00f);
        static readonly Color C_Player   = new(1.00f, 0.45f, 0.15f);

        const int Z_TERRAIN = 0, Z_PLATFORM = 1, Z_OBJECT = 5, Z_SPAWN = 8;

        Transform _root;
        static Sprite _square;

        void Start()
        {
            if (buildOnStart) Build();
        }

        [ContextMenu("Build")]
        public void Build()
        {
            if (mapFile == null)
            {
                Debug.LogError("[MapLoader] mapFile이 비어있음 — Assets/Maps의 맵 파일을 Inspector에 드래그하세요.");
                return;
            }

            UnifiedData data;
            try { data = JsonConvert.DeserializeObject<UnifiedData>(mapFile.text); }
            catch (System.Exception e) { Debug.LogError($"[MapLoader] 파싱 실패: {e.Message}"); return; }

            if (data == null || !data.Stages.TryGetValue(stageKey, out var stage))
            {
                string keys = data != null ? string.Join(", ", data.Stages.Keys) : "-";
                Debug.LogError($"[MapLoader] 스테이지 '{stageKey}' 없음 (가능: {keys})");
                return;
            }

            Clear();
            _root = new GameObject($"Level_{stageKey}").transform;
            _root.SetParent(transform, false);

            // 지형/발판/벽 (비광학)
            BuildTerrain(stage);
            BuildWalls(stage);
            BuildPlatforms(stage);
            BuildLadders(stage);

            // 광학 오브젝트 (각자 IBeamHit 연산)
            BuildLens(stage);
            BuildMirrors(stage);
            BuildPrism(stage);
            BuildGate(stage);

            BuildDecoys(stage);
            BuildSpawn(stage);
            BuildPlayer(stage);

            // 빛 추적기 생성 후 추적
            var tracer = new GameObject("BeamTracer").AddComponent<BeamTracer>();
            tracer.transform.SetParent(_root, false);
            tracer.Trace();

            if (frameCamera) FrameCamera(stage);

            Debug.Log($"[MapLoader] '{stageKey}' 완료 — 거울 {stage.Mirrors?.Count ?? 0} · " +
                      $"프리즘 {(stage.Prism != null ? 1 : 0)} · 사다리 {stage.Ladders?.Count ?? 0}");
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            _root = null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("Level_")) DestroySafe(child.gameObject);
            }
        }

        // ---------- 비광학 배치 ----------

        void BuildTerrain(StageData s)
        {
            if (s.Terrain == null) return;
            foreach (var kv in s.Terrain)
            {
                if (!int.TryParse(kv.Key, out int x)) continue;
                for (int y = 0; y <= kv.Value; y++)
                    // 지형은 밟는 바닥 → 솔리드 콜라이더(플레이어 지지). 빛 차단은 벽이 담당.
                    SolidDecor($"terrain_{x}_{y}", new Vector2(x, y), C_Terrain, Z_TERRAIN, Vector2.one, Vector2.one);
            }
        }

        void BuildWalls(StageData s)
        {
            foreach (var c in s.AllWalls())
            {
                // 벽은 불투명 → 솔리드 콜라이더(빛 차단). IBeamHit 없음 → 빔 정지.
                var go = SolidRoot($"wall_{c[0]}_{c[1]}", new Vector2(c[0], c[1]), 1.0f);
                Visual(go.transform, C_Wall, Z_TERRAIN, Vector2.one);
            }
        }

        void BuildPlatforms(StageData s)
        {
            if (s.Platforms == null) return;
            foreach (var p in s.Platforms)
            {
                if (p.Missing || p.Cells == null) continue;   // stage4 미설계 발판 스킵 [갭]
                foreach (var c in p.Cells)
                {
                    // 발판은 밟고 서는 표면 → 얇은(0.4) 솔리드 콜라이더. 단, 빛은 투과(마커 부착).
                    var go = SolidDecor($"plat_{p.Id}_{c[0]}_{c[1]}", new Vector2(c[0], c[1]),
                          C_Platform, Z_PLATFORM, new Vector2(1f, 0.4f), new Vector2(1f, 0.4f));
                    go.AddComponent<BeamTransparent>();
                }
            }
        }

        void BuildLadders(StageData s)
        {
            if (s.Ladders == null) return;
            foreach (var l in s.Ladders)
            {
                if (l.YSpan == null || l.YSpan.Length < 2) continue;
                int y0 = Mathf.Min(l.YSpan[0], l.YSpan[1]);
                int y1 = Mathf.Max(l.YSpan[0], l.YSpan[1]);
                int h = y1 - y0 + 1;
                var pos = new Vector2(l.Col, (y0 + y1) * 0.5f);   // 열 col, 세로 중앙
                var go = new GameObject($"ladder_{l.Col}");
                go.transform.SetParent(_root, false);
                go.transform.position = pos;
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(0.6f, h);
                box.isTrigger = true;   // 빛 통과 + 플레이어 등반 감지용
                go.AddComponent<Ladder>().Init(h);
                Visual(go.transform, C_Ladder, Z_PLATFORM, new Vector2(0.3f, h));
            }
        }

        // ---------- 광학 배치 (변수 주입) ----------

        void BuildLens(StageData s)
        {
            if (s.Source?.Pos == null) return;
            var pos = new Vector2(s.Source.Pos[0], s.Source.Pos[1]);
            var go = new GameObject("lens");
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            var dir = GridMap.DirToVector(s.Source.Dir);
            go.AddComponent<LightSource>().Init(dir, 1f);
            Visual(go.transform, C_Lens, Z_OBJECT, Vector2.one * 0.8f);
            if (dir != Vector2.zero)
                Decor("lens_dir", pos + dir * 0.6f, C_Lens, Z_SPAWN, Vector2.one * 0.25f);
        }

        void BuildMirrors(StageData s)
        {
            if (s.Mirrors == null) return;
            foreach (var m in s.Mirrors)
            {
                if (m.Pos == null) continue;
                var go = SolidRoot($"mirror_{m.Id}", new Vector2(m.Pos[0], m.Pos[1]), 0.9f);
                var col = m.Fixed ? C_MirrorFix : C_Mirror;
                Visual(go.transform, col, Z_OBJECT, new Vector2(1.1f, 0.18f), -m.AngleDeg);
                go.AddComponent<Mirror>().Init(m.AngleDeg, m.Fixed);   // 변수 주입
            }
        }

        void BuildPrism(StageData s)
        {
            if (s.Prism?.Pos == null) return;
            var go = SolidRoot("prism", new Vector2(s.Prism.Pos[0], s.Prism.Pos[1]), 0.9f);
            Visual(go.transform, C_Prism, Z_OBJECT, Vector2.one * 0.9f, 45f);
            var outs = new List<Vector2>();
            if (s.Prism.Out != null)
                foreach (var arrow in s.Prism.Out) outs.Add(GridMap.DirToVector(arrow));
            go.AddComponent<Prism>().Init(outs);   // 출력 방향 주입
        }

        void BuildGate(StageData s)
        {
            if (s.Gate?.Pos == null) return;
            var go = SolidRoot("gate", new Vector2(s.Gate.Pos[0], s.Gate.Pos[1]), 0.9f);
            var sr = Visual(go.transform, C_Gate, Z_OBJECT, new Vector2(0.9f, 0.9f));
            var det = go.AddComponent<GateDetector>();
            det.visual = sr;
            det.CacheClosedColor();
        }

        void BuildDecoys(StageData s)
        {
            if (s.Decoys == null) return;
            foreach (var d in s.Decoys)
                if (d.Pos != null)
                    Decor($"decoy_{d.Id}", new Vector2(d.Pos[0], d.Pos[1]), C_Decoy, Z_OBJECT, Vector2.one * 0.8f, 45f);
        }

        void BuildSpawn(StageData s)
        {
            if (s.Spawn == null || s.Spawn.Length < 2) return;
            Decor("spawn", new Vector2(s.Spawn[0], s.Spawn[1]), C_Spawn, Z_SPAWN, Vector2.one * 0.6f);
        }

        // 스폰 지점에 플레이어 액터 배치(Rigidbody2D + 콜라이더 + PlayerController).
        void BuildPlayer(StageData s)
        {
            if (s.Spawn == null || s.Spawn.Length < 2) return;
            var pos = new Vector2(s.Spawn[0], s.Spawn[1]);
            var go = new GameObject("Player");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var body = go.AddComponent<BoxCollider2D>();
            body.size = new Vector2(0.6f, 0.9f);          // 몸통(트리거 아님) — 지형/발판과 충돌
            body.edgeRadius = 0.03f;                       // 모서리 라운딩 → 타일 콜라이더 이음새에 안 걸림
            // 벽 끼임 방지: 마찰 0. 정지 시 x속도는 PlayerController가 0으로 세팅하므로 미끄러짐 없음.
            body.sharedMaterial = new PhysicsMaterial2D("PlayerSlip") { friction = 0f, bounciness = 0f };
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            go.AddComponent<PlayerController>();

            Visual(go.transform, C_Player, Z_SPAWN + 1, new Vector2(0.6f, 0.9f));
        }

        // ---------- 유틸 ----------

        // 솔리드 콜라이더가 있는 오브젝트 루트(스케일 1). 시각은 자식으로 분리.
        GameObject SolidRoot(string name, Vector2 pos, float colliderSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(colliderSize, colliderSize);
            return go;
        }

        // 시각 + 솔리드(트리거 아님) BoxCollider2D. 플레이어가 밟고 설 수 있는 지형/발판용.
        GameObject SolidDecor(string name, Vector2 pos, Color col, int order, Vector2 scale, Vector2 colliderSize)
        {
            var go = Decor(name, pos, col, order, scale);
            go.AddComponent<BoxCollider2D>().size = colliderSize;
            return go;
        }

        // 콜라이더 없는 순수 시각 오브젝트.
        GameObject Decor(string name, Vector2 pos, Color col, int order, Vector2 scale, float rotZ = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            Visual(go.transform, col, order, scale, rotZ);
            return go;
        }

        // 부모에 시각용 사각형 자식("visual") 부착. 회전/스케일은 시각에만 적용.
        SpriteRenderer Visual(Transform parent, Color col, int order, Vector2 scale, float rotZ = 0f)
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
            cam.transform.position = new Vector3((s.Grid.W - 1) * 0.5f, (s.Grid.H - 1) * 0.5f, -10f);
            float byH = s.Grid.H * 0.5f + 1f;
            float byW = (s.Grid.W * 0.5f + 1f) / Mathf.Max(cam.aspect, 0.01f);
            cam.orthographicSize = Mathf.Max(byH, byW);
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
                    _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
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
