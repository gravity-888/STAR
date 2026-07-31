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
        public bool autoSolveKey = true;     // P: 회전 가능한 거울을 전부 정답 각도로 정렬. 데모 빌드 시 끌 것

        readonly List<Mirror> _mirrors = new();   // 이번 스테이지의 회전 가능 거울(정답 정렬·재랜덤 대상)

        // 스테이지별 진행상태(다른 맵 갔다 돌아와도 이어지게). 새 게임 시작 시 초기화.
        class StageProgress
        {
            public readonly Dictionary<string, float> mirrors = new();   // 거울 id → 현재 각도
            public bool gateOpen;      // 게이트를 열어 놨는지(래치)
            public bool hasLens;       // 이 스테이지가 랜즈 장착 메커닉을 쓰는지
            public bool lensMounted;   // 랜즈를 횃불에 장착했는지
        }
        readonly Dictionary<string, StageProgress> _progress = new();
        string _currentStageKey;      // 지금 씬에 빌드돼 있는 스테이지(떠나기 전 상태 저장 대상)
        bool _skipCapture;            // Restart 등: 재빌드 전 현재 상태를 저장하지 않음
        StageProgress _restore;       // 이번 빌드에 복원할 진행상태(없으면 null)

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
        static readonly Color C_Terrain  = new(0.35f, 0.26f, 0.18f);   // = 땅(dirt) 기본색
        static readonly Color C_Grass    = new(0.35f, 0.60f, 0.28f);   // 잔디
        static readonly Color C_Indoor   = new(0.42f, 0.42f, 0.48f);   // 실내
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
        // 아트 파일 크기·콜라이더는 그대로 두고, 프리팹 아트가 화면에 보이는 크기만 이 배율로 키운다.
        //   1 = 원본 크기. 콜라이더·빛 판정은 영향 없음(퍼즐 동작 불변).
        [Header("아트 표시 배율")]
        public float artScale = 2f;

        [Header("거울")]
        // 거울 아트 기본각 보정(도). 아트를 세로로 그렸으면 90. 반사 연산에는 영향 없고 프리팹 아트에만 적용된다.
        public float mirrorArtAngleOffset = 90f;
        // 맵 생성 시 회전 가능한 거울을 정답 각도에서 랜덤하게 틀어 놓는다(퍼즐 초기화).
        public bool randomizeMirrors = true;
        // 틀어놓을 최대 단계. 22.5°씩이라 2 = ±45°. Q/E로 맞출 수 있도록 반드시 22.5° 배수로만 어긋난다.
        public int mirrorRandomSteps = 2;

        [Header("프리팹 슬롯 (비우면 색 사각형 폴백)")]
        public GameObject terrainPrefab;         // 지형 공통 폴백(타입별 슬롯이 비면 이걸 사용)
        public GameObject terrainGrassPrefab;    // 잔디
        public GameObject terrainDirtPrefab;     // 땅
        public GameObject terrainIndoorPrefab;   // 실내
        public GameObject wallPrefab;
        public GameObject wallGlassPrefab;       // 빛 통과 예외 벽(반투명)
        public GameObject platformPrefab;        // 빛 투과 발판
        public GameObject platformSolidPrefab;   // 빛 차단 발판
        public GameObject ladderPrefab;
        public GameObject lensPrefab;            // 랜즈(빛나는 부분)
        public GameObject torchPrefab;           // 랜즈를 장착하는 횃불(고정 배경, 회전 안 함)
        public GameObject mirrorPrefab;          // 회전 가능 거울(돌아가는 반사면)
        public GameObject mirrorFixedPrefab;     // 고정(회색) 거울 — 별도 아트 가능
        public GameObject mirrorMountPrefab;     // 바닥 거치대 — 거울과 같은 x, 밑면이 바닥에 닿게 세움(회전 안 함)
        public GameObject mirrorMountCeilingPrefab;  // 천장 거치대(전용 아트 10×80) — 천장에 매다는 거울용. 비면 바닥 거치대를 뒤집어 폴백
        public GameObject prismPrefab;
        public GameObject gatePrefab;            // 게이트 수광부(별도 오브젝트)
        public GameObject gateDoorPrefab;        // 게이트 개폐부(문) — 개폐존 전체를 덮는 긴 블럭 1개
        public GameObject decoyPrefab;
        public GameObject spawnPrefab;
        public GameObject playerPrefab;

        [Header("스테이지 배경 (스테이지 수만큼 채움 — 인덱스 = stageOrder 순번, 맵의 background로 개별 지정 가능)")]
        public GameObject[] stageBackgrounds;
        [Range(0f, 1f)] public float backgroundParallax = 0.5f;   // 시차 강도(0=고정, 1=화면 고정). 프리팹에 ParallaxBackground가 없을 때 기본값

        const int Z_TERRAIN = 0, Z_PLATFORM = 1, Z_OBJECT = 5, Z_SPAWN = 8;
        const int Z_BACKGROUND = -100;   // 모든 아트 뒤(지형 0보다 훨씬 아래)

        // 발판 기하: 두께 0.4, 윗면을 칸 위 모서리(y+0.5)에 맞춘다 → 지형(1×1 블록)과 서는 높이가 같아진다.
        //   중심은 칸에서 (0.5 - 0.4/2) = 0.3칸 위. 빔은 칸 중심선을 지나지만 빛 차단 발판은
        //   stage3의 세로 낙하 빔 1건뿐이고 그 경로는 여전히 관통하므로 광학 판정에 영향 없음.
        const float PLATFORM_THICK = 0.4f;
        const float PLATFORM_TOP   = 0.5f;                                   // 칸 중심 기준 윗면 높이
        const float PLATFORM_CY    = PLATFORM_TOP - PLATFORM_THICK * 0.5f;   // 칸 중심 기준 발판 중심(=0.3)

        Transform _root;
        static Sprite _square;

        void Start()
        {
            ApplyStageOrderFromMap();   // 맵의 stage_order로 진행 순서 확정(StartGame이 stageOrder[0]을 쓰기 전에)
            if (useGameFlow) { GameManager.Bootstrap(this); return; }
            if (buildOnStart) Build();
        }

        // 맵 파일에 stage_order가 있으면 진행 순서·개수를 그걸로 덮어쓴다(인스펙터 값은 폴백). 파싱 실패는 Build에서 로그.
        void ApplyStageOrderFromMap()
        {
            if (mapFile == null) return;
            try
            {
                var d = JsonConvert.DeserializeObject<UnifiedData>(mapFile.text);
                if (d?.StageOrder != null && d.StageOrder.Length > 0) stageOrder = d.StageOrder;
            }
            catch { /* 파싱 오류는 Build()에서 상세 로그 */ }
        }

        // 편의 키 입력. 전환 연출/일시정지/타이틀·엔딩(ControlsLocked) 중에는 무시.
        void Update()
        {
            if (_transitioning || Player.PlayerController.ControlsLocked) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (restartKey && kb.rKey.wasPressedThisFrame) { Restart(); return; }
            if (autoSolveKey && kb.pKey.wasPressedThisFrame) { SolveAllMirrors(); return; }

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

            // 맵이 진행 순서를 지정했으면 반영(인스펙터 값 폴백).
            if (data != null && data.StageOrder != null && data.StageOrder.Length > 0)
                stageOrder = data.StageOrder;

            if (data == null || !data.Stages.TryGetValue(stageKey, out var stage))
            {
                string keys = data != null ? string.Join(", ", data.Stages.Keys) : "-";
                Debug.LogError($"[MapLoader] 스테이지 '{stageKey}' 없음 (가능: {keys})");
                return;
            }

            // 떠나기 전 현재 스테이지 진행상태 저장 → 새 스테이지에 복원할 상태 로드(다른 맵 갔다 와도 이어짐).
            if (!_skipCapture) CaptureProgress();
            _skipCapture = false;
            _progress.TryGetValue(stageKey, out _restore);

            Clear();
            _mirrors.Clear();
            _root = new GameObject($"Level_{stageKey}").transform;
            _root.SetParent(transform, false);

            // 배경(가장 뒤) → 지형/발판/벽 (비광학)
            BuildBackground(stage);
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
            BuildLensItem(stage);   // 바닥에 떨어진 랜즈 아이템(있으면)
            BuildEntrance(stage);   // 입장 통로 역방향 트리거
            BuildSpawn(stage);
            var player = BuildPlayer(stage);

            // 빛 추적기 생성 후 추적
            var tracer = new GameObject("BeamTracer").AddComponent<BeamTracer>();
            tracer.transform.SetParent(_root, false);
            tracer.Trace();   // 콜라이더 위치 동기화 포함

            // 스폰 겹침 방지: 플레이어가 전환 트리거 위에 스폰됐으면 그 트리거를 무장 해제(나갈 때까지 통과 금지) → 전환 오실레이션 차단.
            if (player != null)
            {
                var playerCol = player.GetComponent<Collider2D>();
                foreach (var ge in _root.GetComponentsInChildren<GateExit>(true))
                    ge.DisarmIfOverlaps(playerCol);
            }

            // 랜덤 초기화가 우연히 게이트를 열어버리면(우회 경로) 닫힌 배치가 나올 때까지 다시 섞는다.
            //   → 항상 "안 풀린 상태"로 시작. 22.5° 배수 랜덤이라 정답 도달성은 유지.
            if (randomizeMirrors && _mirrors.Count > 0 && _restore == null && !_reverseEntry) EnsureUnsolvedStart(tracer);

            SetupCamera(stage, player);

            Debug.Log($"[MapLoader] '{stageKey}' 완료 — 거울 {stage.Mirrors?.Count ?? 0} · " +
                      $"프리즘 {(stage.Prism != null ? 1 : 0)} · 사다리 {stage.Ladders?.Count ?? 0}");

            _reverseEntry = false;   // 1회성 소비 — 다음 Build는 기본(정방향)
            _currentStageKey = stageKey;   // 이제 이 스테이지가 씬에 빌드됨
        }

        // 지금 빌드돼 있는 스테이지(_currentStageKey)의 상태를 저장(거울 각도·게이트 개방·랜즈).
        void CaptureProgress()
        {
            if (_currentStageKey == null || _root == null) return;
            var p = new StageProgress();
            foreach (var m in _mirrors)
                if (m != null && !string.IsNullOrEmpty(m.Id)) p.mirrors[m.Id] = m.AngleDeg;
            var det = _root.GetComponentInChildren<GateDetector>(true);
            if (det != null) p.gateOpen = det.IsOpen;
            var mount = _root.GetComponentInChildren<TorchMount>(true);
            if (mount != null) { p.hasLens = true; p.lensMounted = mount.Mounted; }
            _progress[_currentStageKey] = p;
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

        // 정답 정렬 키(P): 회전 가능한 거울을 전부 정답 각도로 스냅. 빛은 BeamTracer가 다음 LateUpdate에 자동 재추적.
        public void SolveAllMirrors()
        {
            foreach (var mir in _mirrors) if (mir != null) mir.SnapToSolution();
        }

        // 랜덤 초기화가 게이트를 열어버리면 닫힌 배치가 나올 때까지 다시 섞는다(최대 시도 제한).
        void EnsureUnsolvedStart(BeamTracer tracer)
        {
            var gates = Object.FindObjectsByType<GateDetector>(FindObjectsSortMode.None);
            // 충전식 게이트는 빌드 시점엔 아직 안 열림(IsOpen=false)이라, "정답 배치"는 즉시 광량(IsLit)으로 판정.
            bool AnyOpen()
            {
                foreach (var g in gates) if (g != null && g.IsLit) return true;
                return false;
            }
            for (int attempt = 0; attempt < 20 && AnyOpen(); attempt++)
            {
                foreach (var mir in _mirrors) if (mir != null) mir.RandomizeFromSolution(mirrorRandomSteps);
                tracer.Trace();
            }
        }

        // 타이틀에서 게임 시작: 첫 스테이지(stageOrder[0])부터 즉시 빌드. GameManager가 호출.
        public void StartGame()
        {
            if (_transitioning) return;
            _reverseEntry = false;
            _progress.Clear();            // 새 게임 → 모든 스테이지 진행상태 초기화
            _currentStageKey = null;      // 첫 빌드는 저장 대상 없음
            if (stageOrder != null && stageOrder.Length > 0) stageKey = stageOrder[0];
            Build();
        }

        // R키: 현재 스테이지를 처음 상태로 재구축(거울 재랜덤·플레이어 위치 초기화). 퍼즐이 꼬였을 때 구제용.
        public void Restart()
        {
            if (_transitioning) return;
            _progress.Remove(stageKey);   // 이 스테이지 진행상태 폐기 → 새로 랜덤
            _skipCapture = true;          // 재빌드 전 현재(꼬인) 상태를 저장하지 않음
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
            AudioManager.StageTransition();
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

        // 스테이지 배경: stageBackgrounds[인덱스]를 레벨 중앙에 깔고 모든 아트 뒤로 보낸다.
        //   인덱스 = 맵의 background(>=0) 우선, 없으면 stageOrder 상 이 스테이지의 순번. 슬롯이 비거나 범위를 벗어나면 배경 없음.
        void BuildBackground(StageData s)
        {
            if (stageBackgrounds == null || stageBackgrounds.Length == 0) return;
            int idx = s.Background >= 0 ? s.Background : System.Array.IndexOf(stageOrder, stageKey);
            if (idx < 0 || idx >= stageBackgrounds.Length || stageBackgrounds[idx] == null) return;

            int w = s.Grid != null ? s.Grid.W : 30, h = s.Grid != null ? s.Grid.H : 15;
            var go = Instantiate(stageBackgrounds[idx], _root, false);
            go.name = "background";
            go.transform.position = new Vector3((w - 1) * 0.5f, (h - 1) * 0.5f, 0f);   // 그리드 중앙(시차 기준점)
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                sr.sortingOrder = Z_BACKGROUND + sr.sortingOrder;   // 내부 상대순서 유지, 전체는 맨 뒤로

            // 시차 스크롤: 프리팹이 레이어별 ParallaxBackground를 안 가졌으면 루트에 기본 하나 부착.
            if (go.GetComponentInChildren<ParallaxBackground>() == null)
                go.AddComponent<ParallaxBackground>().factor = backgroundParallax;
        }

        void BuildTerrain(StageData s)
        {
            if (s.Terrain == null) return;
            foreach (var kv in s.Terrain)
            {
                if (!int.TryParse(kv.Key, out int x)) continue;
                for (int y = 0; y <= kv.Value; y++)
                {
                    // 칸별 타입(잔디/땅/실내)에 맞는 프리팹·색을 고른다. 타입별 슬롯이 비면 terrainPrefab, 그것도 비면 색 사각형.
                    var (prefab, col) = TerrainStyle(TerrainTypeAt(s, x, y));
                    // 지형은 밟는 바닥 → 솔리드 콜라이더(플레이어 지지). 빛 차단은 벽이 담당.
                    SolidDecor($"terrain_{x}_{y}", new Vector2(x, y), col, Z_TERRAIN, Vector2.one, Vector2.one,
                               prefab, fitToScale: true);   // 1칸에 딱 맞춤 → 옆 칸과 연결
                }
            }
        }

        // 칸 (x,y)의 지형 타입: terrain_type 오버라이드 → 없으면 terrain_type_default("dirt").
        string TerrainTypeAt(StageData s, int x, int y)
        {
            if (s.TerrainType != null && s.TerrainType.TryGetValue($"{x},{y}", out var t) && !string.IsNullOrEmpty(t))
                return t;
            return string.IsNullOrEmpty(s.TerrainTypeDefault) ? "dirt" : s.TerrainTypeDefault;
        }

        (GameObject prefab, Color color) TerrainStyle(string type)
        {
            switch (type)
            {
                case "grass":  return (terrainGrassPrefab  != null ? terrainGrassPrefab  : terrainPrefab, C_Grass);
                case "indoor": return (terrainIndoorPrefab != null ? terrainIndoorPrefab : terrainPrefab, C_Indoor);
                default:       return (terrainDirtPrefab   != null ? terrainDirtPrefab   : terrainPrefab, C_Terrain);
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
                       passLight ? C_WallGlass : C_Wall, Z_TERRAIN, Vector2.one, fitToScale: true);   // 1칸 딱 맞춤
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
                    // 발판은 밟고 서는 표면 → 얇은(0.4) 솔리드 콜라이더. 윗면은 지형과 같은 높이(칸 위 모서리).
                    // transmit=true면 빛 투과(마커 부착), false면 벽처럼 빛을 막는다 — 색으로 구분.
                    var go = SolidDecor($"plat_{p.Id}_{c[0]}_{c[1]}", new Vector2(c[0], c[1] + PLATFORM_CY),
                          p.Transmit ? C_Platform : C_PlatformSolid, Z_PLATFORM,
                          new Vector2(1f, PLATFORM_THICK), new Vector2(1f, PLATFORM_THICK),
                          p.Transmit ? platformPrefab : platformSolidPrefab, fitToScale: true);   // 가로 1칸 딱 맞춤 → 옆 칸과 연결
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

                // 땅 위에만 존재하게: 지형이 채운 칸(0..t)은 건너뛰고 그 위 칸부터 시작.
                if (s.Terrain != null && s.Terrain.TryGetValue(l.Col.ToString(), out int t) && t >= 0)
                    y0 = Mathf.Max(y0, t + 1);
                if (y1 < y0) continue;   // 전부 땅속이면 사다리 없음

                int h = y1 - y0 + 1;
                var pos = new Vector2(l.Col, (y0 + y1) * 0.5f);   // 열 col, 세로 중앙
                var go = new GameObject($"ladder_{l.Col}");
                go.transform.SetParent(_root, false);
                go.transform.position = pos;
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(0.6f, h);
                box.isTrigger = true;   // 빛 통과 + 플레이어 등반 감지용
                go.AddComponent<Ladder>().Init(h);
                // 사다리는 1칸짜리 조각을 세로로 h개 쌓아 만든다(늘이지 않고 아트 비율 유지).
                if (ladderPrefab != null) BuildLadderSegments(go.transform, ladderPrefab, Z_PLATFORM, h);
                else Visual(go.transform, C_Ladder, Z_PLATFORM, new Vector2(0.3f, h));
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
            var src = go.AddComponent<LightSource>();
            src.Init(dir, 1f);
            bool mounted = s.Source.HasLens;   // 랜즈 장착 여부
            if (_restore != null && _restore.hasLens) mounted = _restore.lensMounted;   // 저장된 랜즈 상태가 있으면 우선
            src.Emitting = mounted;            // 미장착이면 시작 시 빔 없음

            // 횃불(랜즈 거치대) — 랜즈 유무와 무관하게 항상 바닥에 세운다.
            PlaceOnSurface(go.transform, torchPrefab, "torch", Z_OBJECT - 1,
                           pos.y, SurfaceBelow(s, s.Source.Pos[0], s.Source.Pos[1]));

            // 랜즈 시각("visual") — 장착 시에만 표시. 방향 점은 플레이스홀더 전용.
            Visual(go.transform, lensPrefab, C_Lens, Z_OBJECT, Vector2.one * 0.8f);
            if (mounted && dir != Vector2.zero && lensPrefab == null)
                Decor("lens_dir", pos + dir * 0.6f, C_Lens, Z_SPAWN, Vector2.one * 0.25f);
            var lensVis = go.transform.Find("visual");

            // "랜즈 아이템화" 스테이지(lens_item 존재)만 장착/해제 가능 → TorchMount 부착.
            //   그 외 스테이지는 랜즈 고정(현행 동작): 마운트 없음, F 무반응.
            if (s.LensItem != null && s.LensItem.Length >= 2)
                go.AddComponent<TorchMount>().Init(src, lensVis, mounted);
            else if (lensVis != null)
                lensVis.gameObject.SetActive(mounted);
        }

        // 바닥에 떨어진 랜즈 아이템(줍기 대상). 트리거 콜라이더 + LensItem 마커 + 랜즈 시각.
        void BuildLensItem(StageData s)
        {
            if (s.LensItem == null || s.LensItem.Length < 2) return;
            if (_restore != null && _restore.hasLens && _restore.lensMounted) return;   // 이미 장착된 스테이지로 복귀 → 바닥 아이템 없음
            var pos = new Vector2(s.LensItem[0], s.LensItem[1]);
            var go = new GameObject("lens_item");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;                 // 플레이어와 겹침 감지(빛·이동 방해 안 함)
            box.size = new Vector2(0.7f, 0.7f);
            go.AddComponent<TowardTheStars.Objects.LensItem>();
            Visual(go.transform, lensPrefab, C_Lens, Z_OBJECT, Vector2.one * 0.6f);
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

                // 아트 기본각 보정은 프리팹에만 적용(색 사각형 막대는 보정 없이 -angle 그대로). 초기 회전은 Mirror가 곧 덮어씀.
                float artOffset = mp != null ? mirrorArtAngleOffset : 0f;
                Visual(go.transform, mp, col, Z_OBJECT, new Vector2(1.1f, 0.18f), -m.AngleDeg,
                       prefabRotZ: -m.AngleDeg + artOffset);

                var mirror = go.AddComponent<Mirror>();
                mirror.Id = m.Id;
                mirror.Init(m.AngleDeg, m.Fixed, artOffset);   // 기준 = 정답
                // 복원 우선: 저장된 진행상태가 있으면 그 각도로 되돌린다(다른 맵 갔다 와도 이어짐).
                //   없으면 — 정방향은 랜덤하게 흐트러뜨리고(새 도전), 역주행은 정답 그대로 둔다.
                if (_restore != null && !string.IsNullOrEmpty(m.Id) && _restore.mirrors.TryGetValue(m.Id, out float savedAngle))
                    mirror.SetAngle(savedAngle);
                else if (randomizeMirrors && !_reverseEntry)
                    mirror.RandomizeFromSolution(mirrorRandomSteps);
                if (!m.Fixed) _mirrors.Add(mirror);   // 정답 정렬/재랜덤 대상

                // 거치대: 거울과 같은 x에, 위/아래 중 더 가까운 지지면 쪽에 붙인다.
                //   아래(지형/발판)가 가까우면 밑면을 바닥에 세우고, 위(발판=천장)가 가까우면 뒤집어 천장에 매단다.
                //   → stage4 천장 거울(발판 mirror_relation=above인 M1·M7)은 자동으로 천장 부착. 거울 id 하드코딩 없음.
                //   거울 루트는 회전하지 않고 "visual"만 회전(Mirror.ApplyVisualRotation)하므로 거치대는 안 돌아간다.
                float mY = m.Pos[1];
                float below = SurfaceBelow(s, m.Pos[0], mY);
                float above = SurfaceAbove(s, m.Pos[0], mY);
                bool ceiling = !float.IsNaN(above) && (float.IsNaN(below) || (above - mY) <= (mY - below));
                if (ceiling)
                    // 천장 거울: 전용 천장 거치대 아트(있으면 정립), 없으면 바닥 거치대를 뒤집어 폴백.
                    PlaceOnSurface(go.transform, mirrorMountCeilingPrefab != null ? mirrorMountCeilingPrefab : mirrorMountPrefab,
                                   "mount", Z_OBJECT - 1, mY, above, ceiling: true, flip: mirrorMountCeilingPrefab == null);
                else
                    PlaceOnSurface(go.transform, mirrorMountPrefab, "mount", Z_OBJECT - 1, mY, below);
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
            // 수광부 아트는 1칸(40×40px)에 fitToScale. 단일 아트 — 상태에 따른 색 변경 없음(#8).
            Visual(go.transform, gatePrefab, C_Gate, Z_OBJECT, Vector2.one, fitToScale: true);
            var det = go.AddComponent<GateDetector>();

            // 임시 충전 게이지(수광부 위) — 빛을 받는 동안 채워지고 가득 차면 개방. 나중에 수광부 아트 내부 채움 효과로 교체 예정.
            det.SetGauge(BuildGateGauge(go.transform));

            // 시작 개방 여부: 저장된 진행상태가 있으면 그것, 없으면 역주행(복귀)이면 열림.
            bool gateStartOpen = _restore != null ? _restore.gateOpen : _reverseEntry;
            // 개폐부(문): gate_open_zone 셀들 — 기본 닫힘(차단), 수광부 충전 완료 시 개방(통과).
            BuildGateDoor(s, det, gateStartOpen);
            if (gateStartOpen) det.PresetOpen();   // 이미 열어 놨던/복귀 스테이지면 즉시 개방·래치

            // 통과 감지: 개방 상태에서 플레이어가 개폐부를 지나가면 다음 스테이지로.
            BuildGateExit(s, det);
        }

        // 임시 충전 게이지: 수광부 위에 배경 바 + 채움 바. 채움은 왼쪽 정렬로 자라난다(x 스케일 = 충전 비율).
        //   반환 = 채움 피벗(GateDetector가 x 스케일을 갱신). 나중에 수광부 아트 내부 채움 효과로 교체 예정.
        Transform BuildGateGauge(Transform parent)
        {
            const float W = 1.0f, H = 0.16f, Y = 0.75f;   // 폭·높이·수광부 위 오프셋
            var root = new GameObject("gauge");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, Y, 0f);

            MakeSprite(root.transform, "bg", new Color(0.08f, 0.09f, 0.12f, 0.9f), Z_SPAWN, new Vector2(W, H), Vector3.zero);

            // 채움 피벗을 왼쪽 끝(-W/2)에 두고 자식 채움을 +W/2로 → 피벗 x스케일을 키우면 왼쪽 고정으로 자라남.
            var pivot = new GameObject("fillPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(-W * 0.5f, 0f, 0f);
            MakeSprite(pivot.transform, "fill", new Color(0.45f, 1f, 0.7f), Z_SPAWN + 1, new Vector2(W, H), new Vector3(W * 0.5f, 0f, 0f));
            pivot.transform.localScale = new Vector3(0f, 1f, 1f);   // 시작 0(빈 게이지)
            return pivot.transform;
        }

        // 색 사각형 스프라이트 자식 생성(지정 로컬위치·크기). 게이지 등 코드 UI용.
        SpriteRenderer MakeSprite(Transform parent, string name, Color col, int order, Vector2 size, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Square;
            sr.color = col;
            sr.sortingOrder = order;
            return sr;
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

        void BuildGateDoor(StageData s, GateDetector det, bool startOpen)
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
            // 열림 방향(맵 gate.open_dir, 기본 위 ↑). 세로로 열리면 존 높이(h), 가로로 열리면 폭(w)만큼 이동해 통로를 완전히 비운다.
            Vector2 openDir = GridMap.DirToVector(s.Gate?.OpenDir);
            if (openDir == Vector2.zero) openDir = Vector2.up;
            openDir = openDir.normalized;
            float dist = Mathf.Abs(openDir.y) >= Mathf.Abs(openDir.x) ? h : w;
            door.Register(box, sr, (Vector3)(openDir * dist));   // 열릴 때 이 벡터만큼 미끄러진다

            door.SetOpenImmediate(startOpen);    // 연출 없이 초기 배치. 복귀/저장 상태면 이미 열린 채로
            det.OnStateChanged += door.SetOpen;  // 광량 충전 완료/해제 시 천천히 여닫이
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
            go.AddComponent<LensInteractor>().carryVisualPrefab = lensPrefab;   // F: 랜즈 줍기/장착/해제

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
        GameObject SolidDecor(string name, Vector2 pos, Color col, int order, Vector2 scale, Vector2 colliderSize,
                              GameObject prefab = null, bool fitToScale = false)
        {
            var go = Decor(name, pos, col, order, scale, 0f, prefab, fitToScale);
            go.AddComponent<BoxCollider2D>().size = colliderSize;
            return go;
        }

        // 콜라이더 없는 순수 시각 오브젝트.
        GameObject Decor(string name, Vector2 pos, Color col, int order, Vector2 scale, float rotZ = 0f,
                         GameObject prefab = null, bool fitToScale = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            Visual(go.transform, prefab, col, order, scale, rotZ, fitToScale: fitToScale);
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
                              float prefabRotZ = 0f, Vector2? prefabScale = null, bool fitToScale = false)
        {
            if (prefab == null) return Visual(parent, col, order, scale, rotZ);
            // fitToScale: 타일처럼 칸에 딱 맞아야 하는 것 → scale 크기에 정확히 맞춤(이웃과 연결).
            if (fitToScale) return InstantiateFitted(parent, prefab, "visual", order, prefabRotZ, scale);
            return InstantiatePrefab(parent, prefab, "visual", order, prefabRotZ, prefabScale);
        }

        // 지정 칸(col)에서 y보다 아래에 있는 가장 높은 지지면의 윗면 y를 구한다. 없으면 NaN.
        //   지형은 0..t칸을 채우므로 윗면 = t+0.5, 발판도 윗면을 칸 위 모서리에 맞추므로 = cy+0.5.
        float SurfaceBelow(StageData s, int col, float y)
        {
            float best = float.NegativeInfinity;

            if (s.Terrain != null && s.Terrain.TryGetValue(col.ToString(), out int t) && t >= 0)
            {
                float top = t + 0.5f;
                if (top < y) best = Mathf.Max(best, top);
            }

            if (s.Platforms != null)
                foreach (var p in s.Platforms)
                {
                    if (p.Missing || p.Cells == null) continue;
                    foreach (var c in p.Cells)
                    {
                        if (c == null || c.Length < 2 || c[0] != col) continue;
                        float top = c[1] + PLATFORM_TOP;
                        if (top < y) best = Mathf.Max(best, top);
                    }
                }

            return float.IsNegativeInfinity(best) ? float.NaN : best;
        }

        // 지정 칸(col)에서 y보다 위에 있는 가장 낮은 지지면의 아랫면 y를 구한다(천장). 없으면 NaN.
        //   발판 아랫면 = 발판 중심(cy+PLATFORM_CY) − 두께/2. 지형은 바닥(0..t)이라 천장이 되지 않으므로 제외.
        float SurfaceAbove(StageData s, int col, float y)
        {
            float best = float.PositiveInfinity;

            if (s.Platforms != null)
                foreach (var p in s.Platforms)
                {
                    if (p.Missing || p.Cells == null) continue;
                    foreach (var c in p.Cells)
                    {
                        if (c == null || c.Length < 2 || c[0] != col) continue;
                        float bottom = c[1] + PLATFORM_CY - PLATFORM_THICK * 0.5f;
                        if (bottom > y) best = Mathf.Min(best, bottom);
                    }
                }

            return float.IsPositiveInfinity(best) ? float.NaN : best;
        }

        // 부속 아트(횃불·거치대 등)를 원본 크기(×artScale) 그대로 두고, 지지면에 닿도록 놓는다(늘이지 않음).
        //   ceiling=false: 밑면을 아래 지지면에 맞춰 세운다. ceiling=true: 윗면을 위 지지면(천장)에 맞춰 매단다(위치).
        //   flip=true: 세로를 상하 반전(바닥 아트를 천장에 재사용할 때). 전용 천장 아트면 flip=false로 정립 배치.
        //   지지면을 못 찾으면 기준 오브젝트 중심에 배치.
        void PlaceOnSurface(Transform parent, GameObject prefab, string childName, int order, float anchorY, float surfaceY, bool ceiling = false, bool flip = false)
        {
            if (prefab == null) return;

            var go = Instantiate(prefab, parent, false);
            go.name = childName;
            go.transform.localRotation = Quaternion.identity;

            SpriteRenderer first = null;
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingOrder = order + sr.sortingOrder;
                if (first == null) first = sr;
            }

            var baseScale = go.transform.localScale;
            float sx = baseScale.x * artScale, sy = baseScale.y * artScale;
            // flip이면 세로를 음수로 → 상하 반전. 가로·크기는 동일.
            go.transform.localScale = new Vector3(sx, flip ? -sy : sy, baseScale.z);

            if (float.IsNaN(surfaceY)) { go.transform.localPosition = Vector3.zero; return; }

            // 바닥: 밑면=지지면 → 중심 = 지지면 + 높이/2. 천장: 윗면=천장면 → 중심 = 천장면 − 높이/2.
            float nh = (first != null && first.sprite != null) ? first.sprite.bounds.size.y : 1f;
            float height = nh * Mathf.Abs(sy);
            float centerY = ceiling ? surfaceY - height * 0.5f : surfaceY + height * 0.5f;
            go.transform.localPosition = new Vector3(0f, centerY - anchorY, 0f);
        }

        // 타일 전용: 아트 원본 크기와 무관하게 지정 크기(칸)에 정확히 맞춘다.
        //   → 옆 칸 오브젝트와 빈틈·겹침 없이 딱 맞물린다. 배율(artScale)은 적용하지 않는다(적용하면 타일링이 깨짐).
        SpriteRenderer InstantiateFitted(Transform parent, GameObject prefab, string childName, int order, float rotZ, Vector2 size)
        {
            var go = Instantiate(prefab, parent, false);
            go.name = childName;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);

            SpriteRenderer first = null;
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingOrder = order + sr.sortingOrder;
                if (first == null) first = sr;
            }
            var nat = (first != null && first.sprite != null) ? first.sprite.bounds.size : Vector3.one;
            go.transform.localScale = new Vector3(size.x / Mathf.Max(nat.x, 0.0001f),
                                                  size.y / Mathf.Max(nat.y, 0.0001f), 1f);
            return first;
        }

        // 사다리 전용: 1칸짜리 조각 프리팹을 세로로 h개 쌓는다.
        //   각 조각은 세로 1칸에 맞추되 **균일 스케일**이라 아트 비율이 그대로 유지된다(늘어나지 않음).
        //   가로 굵기는 아트의 가로:세로 비율이 결정한다(예: 24×40 → 0.6칸). 배율(artScale)은 미적용 — 칸 격자에 맞춰야 하므로.
        void BuildLadderSegments(Transform parent, GameObject prefab, int order, int h)
        {
            for (int i = 0; i < h; i++)
            {
                var seg = Instantiate(prefab, parent, false);
                seg.name = $"seg_{i}";
                // 부모는 스팬 중앙에 있으므로, i번째 칸은 중앙 기준 (i - (h-1)/2)칸 위치.
                seg.transform.localPosition = new Vector3(0f, i - (h - 1) * 0.5f, 0f);
                seg.transform.localRotation = Quaternion.identity;

                SpriteRenderer first = null;
                foreach (var sr in seg.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.sortingOrder = order + sr.sortingOrder;
                    if (first == null) first = sr;
                }
                float nh = (first != null && first.sprite != null) ? Mathf.Max(first.sprite.bounds.size.y, 0.0001f) : 1f;
                float s = 1f / nh;                                   // 세로 1칸에 맞추는 균일 배율 → 비율 유지
                seg.transform.localScale = new Vector3(s, s, 1f);
            }
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
            // 표시 배율 적용(아트 파일·콜라이더는 그대로, 보이는 크기만 확대).
            var baseScale = scale.HasValue ? new Vector3(scale.Value.x, scale.Value.y, 1f) : go.transform.localScale;
            go.transform.localScale = new Vector3(baseScale.x * artScale, baseScale.y * artScale, baseScale.z);

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
