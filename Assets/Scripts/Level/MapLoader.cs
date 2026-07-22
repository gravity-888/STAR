using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
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

        [Header("스테이지 진행 순서(게이트 통과 시 다음으로)")]
        public string[] stageOrder = { "stage1", "stage2", "stage3", "stage4" };
        ScreenFader _fader;
        bool _transitioning;
        bool _reverseEntry;   // true면 이번 Build는 역주행 진입 → exit_spawn 사용

        [Header("옵션")]
        public bool buildOnStart = true;
        public bool frameCamera = true;      // 팔로우 미사용 시 스테이지 전체를 프레이밍(폴백)

        [Header("게임 플로우 (타이틀·일시정지·엔딩)")]
        public bool useGameFlow = true;      // 켜면 시작 시 GameManager(타이틀→플레이→엔딩) 사용
        public bool showTitleOnBoot = true;  // 부팅 시 타이틀 표시. 끄면 바로 stage1로 시작
        public System.Action OnGameComplete; // 마지막 스테이지 클리어 시 GameManager가 엔딩 처리(플로우 사용 시)
        public bool IsTransitioning => _transitioning;

        [Header("플레이테스트 편의 키")]
        public bool restartKey = true;       // R: 현재 스테이지 리셋(막혔을 때 구제)
        public bool debugStageKeys = true;   // 1~4: 해당 스테이지로 즉시 이동. 데모 빌드 시 끌 것

        // 숫자열 1~9 → stageOrder 인덱스 0~8. Key enum 산술 대신 명시 배열(할당 1회).
        static readonly Key[] DigitKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        [Header("카메라")]
        public bool followPlayer = true;     // 플레이어 추적 카메라 사용
        public float cameraViewCells = 16f;  // 화면 세로에 담을 셀 수(줌 정도)

        [Header("게이트 트리거")]
        // 게이트 통과 판정을 표면(개폐존 셀)에서 레벨 안쪽(통로 방향)으로 이만큼 더 넓힌다(셀 단위).
        public float gateExitInset = 0.5f;

        // 색 팔레트(플레이스홀더)
        static readonly Color C_Terrain  = new(0.35f, 0.26f, 0.18f);
        static readonly Color C_Wall     = new(0.20f, 0.20f, 0.24f);
        static readonly Color C_WallGlass = new(0.45f, 0.55f, 0.70f, 0.45f);   // 빛 통과 예외 벽(반투명)
        static readonly Color C_Platform = new(0.30f, 0.55f, 0.95f, 0.55f);
        static readonly Color C_PlatformSolid = new(0.10f, 0.22f, 0.45f, 1.00f);   // 빛 차단 발판(불투명·진한 남색)
        static readonly Color C_Lens     = new(1.00f, 0.90f, 0.30f);
        static readonly Color C_Gate     = new(0.30f, 0.90f, 0.45f);
        static readonly Color C_Mirror   = new(0.55f, 0.90f, 1.00f);
        static readonly Color C_MirrorFix = new(0.60f, 0.60f, 0.65f);
        static readonly Color C_Prism    = new(0.95f, 0.45f, 0.95f);
        static readonly Color C_Ladder   = new(0.80f, 0.60f, 0.30f);
        static readonly Color C_Decoy    = new(0.95f, 0.35f, 0.35f, 0.6f);
        static readonly Color C_Spawn    = new(1.00f, 1.00f, 1.00f);
        static readonly Color C_Player   = new(1.00f, 0.45f, 0.15f);

        // 프리팹 seam: 오브젝트별 시각 프리팹 슬롯. 비우면 위 색 사각형으로 폴백(동작 동일).
        //   프리팹은 각 오브젝트의 "visual" 자식(=아트)만 대체 — 콜라이더·로직은 루트에 그대로.
        //   임시/최종 아트는 이 슬롯만 채우면 되고 코드 변경이 없어야 한다(로드맵 3·7).
        [Header("프리팹 슬롯 (비우면 색 사각형 폴백)")]
        public GameObject terrainPrefab;
        public GameObject wallPrefab;
        public GameObject wallGlassPrefab;       // 빛 통과 예외 벽(반투명)
        public GameObject platformPrefab;        // 빛 투과 발판
        public GameObject platformSolidPrefab;   // 빛 차단 발판
        public GameObject ladderPrefab;
        public GameObject lensPrefab;            // 랜즈(빛나는 부분)
        public GameObject torchPrefab;           // 랜즈를 장착하는 횃불(고정 배경, 회전 안 함)
        public GameObject mirrorPrefab;          // 회전 가능 거울(돌아가는 반사면)
        public GameObject mirrorFixedPrefab;     // 고정(회색) 거울 — 별도 아트 가능
        public GameObject mirrorMountPrefab;     // 거울 거치대(고정, 회전 안 함) — 거울을 잡아주는 부분
        public GameObject prismPrefab;
        public GameObject gatePrefab;            // 게이트 수광부(별도 오브젝트)
        public GameObject gateDoorPrefab;        // 게이트 개폐부(문) — 개폐존 전체를 덮는 긴 블럭 1개
        public GameObject decoyPrefab;
        public GameObject spawnPrefab;
        public GameObject playerPrefab;

        const int Z_TERRAIN = 0, Z_PLATFORM = 1, Z_OBJECT = 5, Z_SPAWN = 8;

        Transform _root;
        static Sprite _square;

        void Start()
        {
            if (useGameFlow) { GameManager.Bootstrap(this); return; }
            if (buildOnStart) Build();
        }

        // 편의 키 입력. 전환 연출/일시정지/타이틀·엔딩(ControlsLocked) 중에는 무시.
        void Update()
        {
            if (_transitioning || Player.PlayerController.ControlsLocked) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (restartKey && kb.rKey.wasPressedThisFrame) { Restart(); return; }

            if (!debugStageKeys) return;
            int n = Mathf.Min(stageOrder.Length, DigitKeys.Length);
            for (int i = 0; i < n; i++)
                if (kb[DigitKeys[i]].wasPressedThisFrame) { GoToIndex(i); return; }
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
            BuildEntrance(stage);   // 입장 통로 역방향 트리거
            BuildSpawn(stage);
            var player = BuildPlayer(stage);

            // 빛 추적기 생성 후 추적
            var tracer = new GameObject("BeamTracer").AddComponent<BeamTracer>();
            tracer.transform.SetParent(_root, false);
            tracer.Trace();

            SetupCamera(stage, player);

            Debug.Log($"[MapLoader] '{stageKey}' 완료 — 거울 {stage.Mirrors?.Count ?? 0} · " +
                      $"프리즘 {(stage.Prism != null ? 1 : 0)} · 사다리 {stage.Ladders?.Count ?? 0}");

            _reverseEntry = false;   // 1회성 소비 — 다음 Build는 기본(정방향)
        }

        // 이번 진입에 사용할 스폰: 역주행이면 exit_spawn(출구쪽), 아니면 spawn(입장 통로).
        int[] EffectiveSpawn(StageData s)
            => (_reverseEntry && s.ExitSpawn != null && s.ExitSpawn.Length >= 2) ? s.ExitSpawn : s.Spawn;

        // 게이트 통과 시 호출: 다음 스테이지로 페이드 전환.
        // 마지막 스테이지면 → 게임 플로우 사용 시 엔딩(OnGameComplete), 아니면 처음으로 순환(폴백).
        public void GoToNext()
        {
            if (_transitioning) return;
            int idx = System.Array.IndexOf(stageOrder, stageKey);
            bool isLast = idx < 0 || idx + 1 >= stageOrder.Length;
            if (isLast)
            {
                if (OnGameComplete != null) { OnGameComplete.Invoke(); return; }   // 엔딩
                StartCoroutine(Transition(stageOrder[0], false));                  // 폴백: 순환
                return;
            }
            StartCoroutine(Transition(stageOrder[idx + 1], false));
        }

        // 입장 통로 역방향(왼쪽으로 나감) 시 호출: 이전 스테이지로. 첫 스테이지면 이동 없음.
        public void GoToPrev()
        {
            if (_transitioning) return;
            int idx = System.Array.IndexOf(stageOrder, stageKey);
            if (idx <= 0) return;
            StartCoroutine(Transition(stageOrder[idx - 1], true));
        }

        // 타이틀에서 게임 시작: 첫 스테이지(stageOrder[0])부터 즉시 빌드. GameManager가 호출.
        public void StartGame()
        {
            if (_transitioning) return;
            _reverseEntry = false;
            if (stageOrder != null && stageOrder.Length > 0) stageKey = stageOrder[0];
            Build();
        }

        // R키: 현재 스테이지를 처음 상태로 재구축(거울 각도·플레이어 위치 초기화). 퍼즐이 꼬였을 때 구제용.
        public void Restart()
        {
            if (_transitioning) return;
            StartCoroutine(Transition(stageKey, false));
        }

        // 디버그: stageOrder[index]로 즉시 이동. 같은 스테이지를 지정하면 Restart와 동일하게 동작.
        public void GoToIndex(int index)
        {
            if (_transitioning) return;
            if (index < 0 || index >= stageOrder.Length) return;
            StartCoroutine(Transition(stageOrder[index], false));
        }

        IEnumerator Transition(string next, bool reverse)
        {
            _transitioning = true;
            Player.PlayerController.ControlsLocked = true;   // 연출 시작 → 조작 잠금
            if (_fader == null) _fader = ScreenFader.Create();
            yield return _fader.Fade(0f, 1f);   // 페이드 아웃(어둡게)
            _reverseEntry = reverse;             // 역주행이면 exit_spawn(출구쪽)에서 등장
            stageKey = next;
            Build();
            yield return null;                   // 빛 추적/카메라 정착 한 프레임
            yield return _fader.Fade(1f, 0f);   // 페이드 인(밝게)
            Player.PlayerController.ControlsLocked = false;  // 연출 끝 → 조작 복구
            _transitioning = false;
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
                    SolidDecor($"terrain_{x}_{y}", new Vector2(x, y), C_Terrain, Z_TERRAIN, Vector2.one, Vector2.one, terrainPrefab);
            }
        }

        void BuildWalls(StageData s)
        {
            // 빛 통과 예외 셀(wall_transmit): 벽이지만 빔은 관통(플레이어는 계속 막힘).
            var transmit = new HashSet<(int, int)>();
            if (s.WallTransmit != null)
                foreach (var c in s.WallTransmit)
                    if (c != null && c.Length >= 2) transmit.Add((c[0], c[1]));

            // 입장 통로(entrance): 좌측벽에 뚫는 구멍 — 해당 셀은 벽을 세우지 않는다.
            var entrance = new HashSet<(int, int)>();
            if (s.Entrance != null)
                foreach (var c in s.Entrance)
                    if (c != null && c.Length >= 2) entrance.Add((c[0], c[1]));

            foreach (var c in s.AllWalls())
            {
                if (entrance.Contains((c[0], c[1]))) continue;   // 통로 구멍: 벽 생략

                // 벽은 불투명 → 솔리드 콜라이더(빛 차단). IBeamHit 없음 → 빔 정지.
                var go = SolidRoot($"wall_{c[0]}_{c[1]}", new Vector2(c[0], c[1]), 1.0f);
                bool passLight = transmit.Contains((c[0], c[1]));
                Visual(go.transform, passLight ? wallGlassPrefab : wallPrefab,
                       passLight ? C_WallGlass : C_Wall, Z_TERRAIN, Vector2.one);
                if (passLight) go.AddComponent<BeamTransparent>();   // 빛만 통과, 플레이어는 막음
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
                    // 발판은 밟고 서는 표면 → 얇은(0.4) 솔리드 콜라이더.
                    // transmit=true면 빛 투과(마커 부착), false면 벽처럼 빛을 막는다 — 색으로 구분.
                    var go = SolidDecor($"plat_{p.Id}_{c[0]}_{c[1]}", new Vector2(c[0], c[1]),
                          p.Transmit ? C_Platform : C_PlatformSolid, Z_PLATFORM,
                          new Vector2(1f, 0.4f), new Vector2(1f, 0.4f),
                          p.Transmit ? platformPrefab : platformSolidPrefab);
                    if (p.Transmit) go.AddComponent<BeamTransparent>();
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
                // 사다리 높이 h는 데이터 종속 → 프리팹에도 세로 스케일로 전달.
                Visual(go.transform, ladderPrefab, C_Ladder, Z_PLATFORM, new Vector2(0.3f, h), 0f,
                       prefabScale: new Vector2(1f, h));
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
            PrefabChild(go.transform, torchPrefab, "torch", Z_OBJECT - 1);   // 랜즈를 장착한 횃불(배경, 회전 안 함)
            Visual(go.transform, lensPrefab, C_Lens, Z_OBJECT, Vector2.one * 0.8f);
            // 방향 표시 점은 플레이스홀더 전용 — 프리팹 아트는 자체적으로 방향을 표현한다고 보고 생략.
            if (dir != Vector2.zero && lensPrefab == null)
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
                var mp = m.Fixed ? mirrorFixedPrefab : mirrorPrefab;
                // 거치대(고정) — 회전하지 않는 배경 부속. 거울(반사면)만 -angle 회전.
                PrefabChild(go.transform, mirrorMountPrefab, "mount", Z_OBJECT - 1);
                // 거울 각도는 물리적 반사면 → "visual" 자식(=거울)을 -angle 로 회전(런타임 회전은 Mirror가 담당).
                Visual(go.transform, mp, col, Z_OBJECT, new Vector2(1.1f, 0.18f), -m.AngleDeg, prefabRotZ: -m.AngleDeg);
                go.AddComponent<Mirror>().Init(m.AngleDeg, m.Fixed);   // 변수 주입
            }
        }

        void BuildPrism(StageData s)
        {
            if (s.Prism?.Pos == null) return;
            var go = SolidRoot("prism", new Vector2(s.Prism.Pos[0], s.Prism.Pos[1]), 0.9f);
            // 45°는 마름모꼴 플레이스홀더 전용 — 프리팹 아트는 정립(prefabRotZ 기본 0).
            Visual(go.transform, prismPrefab, C_Prism, Z_OBJECT, Vector2.one * 0.9f, 45f);
            var outs = new List<Vector2>();
            if (s.Prism.Out != null)
                foreach (var arrow in s.Prism.Out) outs.Add(GridMap.DirToVector(arrow));
            go.AddComponent<Prism>().Init(outs);   // 출력 방향 주입
        }

        void BuildGate(StageData s)
        {
            if (s.Gate?.Pos == null) return;
            var go = SolidRoot("gate", new Vector2(s.Gate.Pos[0], s.Gate.Pos[1]), 0.9f);
            var sr = Visual(go.transform, gatePrefab, C_Gate, Z_OBJECT, new Vector2(0.9f, 0.9f));
            var det = go.AddComponent<GateDetector>();
            det.visual = sr;
            det.CacheClosedColor();

            // 개폐부(문): gate_open_zone 셀들 — 기본 닫힘(차단), 수광부 Σ≥1.0이면 개방(통과).
            BuildGateDoor(s, det);

            // 통과 감지: 개방 상태에서 플레이어가 개폐부를 지나가면 다음 스테이지로.
            BuildGateExit(s, det);
        }

        void BuildGateExit(StageData s, GateDetector det)
        {
            if (s.GateOpenZone == null || s.GateOpenZone.Count == 0) return;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var c in s.GateOpenZone)
            {
                if (c == null || c.Length < 2) continue;
                minX = Mathf.Min(minX, c[0]); maxX = Mathf.Max(maxX, c[0]);
                minY = Mathf.Min(minY, c[1]); maxY = Mathf.Max(maxY, c[1]);
            }
            // 개폐존 셀들의 바깥 가장자리(월드 경계).
            float left = minX - 0.5f, right = maxX + 0.5f, bottom = minY - 0.5f, top = maxY + 0.5f;

            // 얇은 축(=통로 방향)을 레벨 안쪽(그리드 중심 쪽)으로 inset 만큼 확장.
            //   세로문 → 가로로, 바닥 해치 → 세로로 넓어진다. 표면에 붙기 전부터/붙은 채로도 판정.
            float inset = Mathf.Max(0f, gateExitInset);
            if (inset > 0f && s.Grid != null)
            {
                float cx = (s.Grid.W - 1) * 0.5f, cy = (s.Grid.H - 1) * 0.5f;   // 그리드 중심
                if (maxX - minX <= maxY - minY)   // X가 더 얇음 → 통로는 가로 방향
                {
                    if (cx < (minX + maxX) * 0.5f) left -= inset; else right += inset;
                }
                else                              // Y가 더 얇음 → 통로는 세로 방향(바닥 해치 등)
                {
                    if (cy < (minY + maxY) * 0.5f) bottom -= inset; else top += inset;
                }
            }

            var go = new GameObject("gate_exit");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f, 0f);
            var trig = go.AddComponent<BoxCollider2D>();
            trig.isTrigger = true;                                   // 감지 전용(물리 차단 없음)
            trig.size = new Vector2(right - left, top - bottom);
            go.AddComponent<GateExit>().Init(det, this, +1);   // 게이트 통과 → 다음 스테이지
        }

        // 입장 통로에 역방향 트리거: 플레이어가 왼쪽 통로로 나가면 이전 스테이지로.
        void BuildEntrance(StageData s)
        {
            if (s.Entrance == null || s.Entrance.Count == 0) return;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var c in s.Entrance)
            {
                if (c == null || c.Length < 2) continue;
                minX = Mathf.Min(minX, c[0]); maxX = Mathf.Max(maxX, c[0]);
                minY = Mathf.Min(minY, c[1]); maxY = Mathf.Max(maxY, c[1]);
            }
            var go = new GameObject("stage_entrance");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            var trig = go.AddComponent<BoxCollider2D>();
            trig.isTrigger = true;
            trig.size = new Vector2(maxX - minX + 1f, maxY - minY + 1f);
            go.AddComponent<GateExit>().Init(null, this, -1);   // 역방향 → 이전 스테이지
        }

        void BuildGateDoor(StageData s, GateDetector det)
        {
            if (s.GateOpenZone == null || s.GateOpenZone.Count == 0) return;
            var doorGo = new GameObject("gate_door");
            doorGo.transform.SetParent(_root, false);
            var door = doorGo.AddComponent<GateDoor>();

            // 개폐존(연속된 칸들)을 하나의 긴 블럭으로 묶는다: 콜라이더 1개 + 시각 1개.
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var c in s.GateOpenZone)
            {
                if (c == null || c.Length < 2) continue;
                minX = Mathf.Min(minX, c[0]); maxX = Mathf.Max(maxX, c[0]);
                minY = Mathf.Min(minY, c[1]); maxY = Mathf.Max(maxY, c[1]);
            }
            float w = maxX - minX + 1f, h = maxY - minY + 1f;

            var block = new GameObject("door_block");
            block.transform.SetParent(doorGo.transform, false);
            block.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            var box = block.AddComponent<BoxCollider2D>();
            box.size = new Vector2(w, h);   // 닫히면 통로 전체를 막는 장벽(열리면 비활성)
            // 프리팹은 "긴 하나의 아트"를 존(w×h)에 정확히 맞춤(가로형은 90° 회전). 비면 색 사각형(존 크기, 살짝 인셋).
            SpriteRenderer sr = gateDoorPrefab != null
                ? InstantiateGateDoor(block.transform, gateDoorPrefab, Z_OBJECT, w, h)
                : Visual(block.transform, door.closedColor, Z_OBJECT, new Vector2(w - 0.1f, h - 0.1f));
            door.Register(box, sr);

            door.SetOpen(false);                 // 기본 닫힘(막힘)
            det.OnStateChanged += door.SetOpen;  // 광량 임계 통과 시 즉시 여닫이
        }

        void BuildDecoys(StageData s)
        {
            if (s.Decoys == null) return;
            foreach (var d in s.Decoys)
                if (d.Pos != null)
                    Decor($"decoy_{d.Id}", new Vector2(d.Pos[0], d.Pos[1]), C_Decoy, Z_OBJECT, Vector2.one * 0.8f, 45f, decoyPrefab);
        }

        void BuildSpawn(StageData s)
        {
            var sp = EffectiveSpawn(s);
            if (sp == null || sp.Length < 2) return;
            Decor("spawn", new Vector2(sp[0], sp[1]), C_Spawn, Z_SPAWN, Vector2.one * 0.6f, 0f, spawnPrefab);
        }

        // 스폰 지점에 플레이어 액터 배치(Rigidbody2D + 콜라이더 + PlayerController). 카메라 추적용 Transform 반환.
        Transform BuildPlayer(StageData s)
        {
            var sp = EffectiveSpawn(s);
            if (sp == null || sp.Length < 2) return null;
            var pos = new Vector2(sp[0], sp[1]);
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
            var pc = go.AddComponent<PlayerController>();
            // 레벨 밖 낙하·이탈 시 스폰 복귀. 경계 기준은 카메라 클램프와 동일.
            if (s.Grid != null)
                pc.SetRespawn(pos, new Vector2(-0.5f, -0.5f), new Vector2(s.Grid.W - 0.5f, s.Grid.H - 0.5f));
            go.AddComponent<MirrorInteractor>();   // Phase 4: Q/E로 가까운 거울 회전 + 빛 재추적

            Visual(go.transform, playerPrefab, C_Player, Z_SPAWN + 1, new Vector2(0.6f, 0.9f));
            return go.transform;
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
        GameObject SolidDecor(string name, Vector2 pos, Color col, int order, Vector2 scale, Vector2 colliderSize, GameObject prefab = null)
        {
            var go = Decor(name, pos, col, order, scale, 0f, prefab);
            go.AddComponent<BoxCollider2D>().size = colliderSize;
            return go;
        }

        // 콜라이더 없는 순수 시각 오브젝트.
        GameObject Decor(string name, Vector2 pos, Color col, int order, Vector2 scale, float rotZ = 0f, GameObject prefab = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            Visual(go.transform, prefab, col, order, scale, rotZ);
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

        // 프리팹 seam: prefab이 있으면 "visual" 자식으로 인스턴스화, 없으면 색 사각형으로 폴백(위 오버로드=기존 동작).
        //   prefabRotZ  : 프리팹에도 적용할 회전(거울 각도 등). 기본 0(색 사각형만 rotZ로 회전).
        //   prefabScale : 프리팹에 적용할 스케일(사다리 높이 등). null이면 프리팹 원본 크기 유지.
        //   반환        : 색 폴백이면 그 SpriteRenderer, 프리팹이면 첫 SpriteRenderer(없으면 null) — 게이트 색 피드백 호환.
        SpriteRenderer Visual(Transform parent, GameObject prefab, Color col, int order, Vector2 scale, float rotZ = 0f,
                              float prefabRotZ = 0f, Vector2? prefabScale = null)
        {
            if (prefab == null) return Visual(parent, col, order, scale, rotZ);
            return InstantiatePrefab(parent, prefab, "visual", order, prefabRotZ, prefabScale);
        }

        // 회전·색 폴백이 필요 없는 부속 프리팹(거울 거치대·횃불 등)을 이름 붙인 자식으로 배치. 비면 아무것도 안 함.
        void PrefabChild(Transform parent, GameObject prefab, string childName, int order)
        {
            if (prefab == null) return;
            InstantiatePrefab(parent, prefab, childName, order, 0f, null);
        }

        // 게이트 문 전용: "긴 하나의 아트"를 원본 크기와 무관하게 개폐존(w×h)에 정확히 맞춘다.
        //   세로로 긴 존 → 그대로 맞춤. 가로로 긴 존(바닥 해치) → 세로 아트를 90° 눕혀 맞춤(같은 아트 재사용).
        SpriteRenderer InstantiateGateDoor(Transform parent, GameObject prefab, int order, float w, float h)
        {
            var go = Instantiate(prefab, parent, false);
            go.name = "visual";
            go.transform.localPosition = Vector3.zero;

            SpriteRenderer first = null;
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingOrder = order + sr.sortingOrder;
                if (first == null) first = sr;
            }
            var nat = (first != null && first.sprite != null) ? first.sprite.bounds.size : Vector3.one;
            float nx = Mathf.Max(nat.x, 0.0001f), ny = Mathf.Max(nat.y, 0.0001f);

            if (w > h)   // 가로로 긴 존(바닥 해치) → 90° 눕혀 맞춤
            {
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                go.transform.localScale = new Vector3(h / nx, w / ny, 1f);
            }
            else         // 세로로 긴 존(일반 문) → 그대로 맞춤
            {
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = new Vector3(w / nx, h / ny, 1f);
            }
            return first;
        }

        // 프리팹을 지정 이름의 자식으로 인스턴스화(위치=부모 원점). sortingOrder=기준+내부 상대순서. 첫 SpriteRenderer 반환.
        SpriteRenderer InstantiatePrefab(Transform parent, GameObject prefab, string childName, int order, float rotZ, Vector2? scale)
        {
            var go = Instantiate(prefab, parent, false);
            go.name = childName;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            if (scale.HasValue)
                go.transform.localScale = new Vector3(scale.Value.x, scale.Value.y, 1f);

            SpriteRenderer first = null;
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingOrder = order + sr.sortingOrder;   // 그룹 기준 order + 프리팹 내부 상대순서 보존
                if (first == null) first = sr;
            }
            return first;
        }

        // 플레이어가 있으면 추적 카메라, 아니면(또는 followPlayer=false) 전체 프레이밍.
        void SetupCamera(StageData s, Transform player)
        {
            var cam = Camera.main;
            if (cam == null || s.Grid == null) return;

            if (followPlayer && player != null)
            {
                cam.orthographic = true;

                // 스테이지별 오버라이드(맵 JSON의 camera) 적용: 확대율(view_cells)·경계 여유(pad).
                float viewCells = cameraViewCells;
                var min = new Vector2(-0.5f, -0.5f);
                var max = new Vector2(s.Grid.W - 0.5f, s.Grid.H - 0.5f);
                if (s.Camera != null)
                {
                    if (s.Camera.ViewCells > 0f) viewCells = s.Camera.ViewCells;
                    min.x -= s.Camera.SidePad;   max.x += s.Camera.SidePad;
                    min.y -= s.Camera.BottomPad; max.y += s.Camera.TopPad;
                }

                // 줌 결정: fit_width면 가로 폭에 맞춤(좌우 벽=화면 끝), 아니면 세로 viewCells 칸.
                float orthoSize = viewCells * 0.5f;
                if (s.Camera != null && s.Camera.FitWidth)
                    orthoSize = (max.x - min.x) * 0.5f / Mathf.Max(cam.aspect, 0.01f);
                cam.orthographicSize = Mathf.Min(orthoSize, (max.y - min.y) * 0.5f);   // 세로 경계 초과 방지
                var cp = cam.transform.position;
                cam.transform.position = new Vector3(cp.x, cp.y, -10f);   // 2D 직교 표준 z 보장
                var follow = cam.GetComponent<CameraFollow>();
                if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
                follow.enabled = true;
                follow.Configure(player, min, max);
            }
            else
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow != null) follow.enabled = false;
                if (frameCamera) FrameCamera(s);
            }
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
