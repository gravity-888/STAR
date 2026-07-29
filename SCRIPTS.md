# 「별을 향해」 스크립트 상세 문서

> 2D 빛 반사 퍼즐 플랫포머 STAR의 전체 C# 스크립트 해설.
> 각 스크립트가 **무슨 역할**을 하고 **어떤 원리**로 작동하는지 파일별·멤버별로 정리한다.
> (개발 진행/로드맵은 `HANDOFF.md`, 아트 규격은 `Assets/Art/PREFAB_SPEC.md` 참고.)

---

## 0. 전체 아키텍처 한눈에

**설계 3원칙**
1. **데이터 주도**: 맵은 JSON(`Assets/Maps/*.json`). 코드는 좌표·각도를 읽어 배치만 한다.
2. **오브젝트별 독립 연산**: 거울·프리즘·수광부가 각자 `IBeamHit`를 구현해 자기 빛 상호작용을 처리. 빔 추적기는 위임만 한다.
3. **시각/로직 분리**: 콜라이더·로직은 오브젝트 루트에, 아트는 `"visual"` 자식에. 그래서 프리팹 아트를 코드 변경 없이 끼울 수 있다(프리팹 seam).

**폴더 구조와 역할**
```
Assets/Scripts/
  Data/     MapData.cs        JSON → C# 모델(Newtonsoft)
  Level/    GridMap.cs        그리드↔월드 좌표 + 화살표 방향 해석
            MapLoader.cs      ★맵을 읽어 모든 오브젝트를 생성·배치(중심 허브)
            GameManager.cs    타이틀→플레이→일시정지→엔딩 UI 상태머신
            ScreenFader.cs    스테이지 전환 페이드(검은 오버레이)
            CameraFollow.cs   플레이어 추적 카메라 + 레벨 경계 클램프
            GateExit.cs       스테이지 이동 트리거(게이트 통과/입장통로 역주행)
  Light/    Beam.cs           Beam 구조체 + IBeamHit 인터페이스
            BeamTracer.cs     빛 추적(스택 기반, 매 프레임) + LineRenderer 렌더
  Objects/  LightSource.cs    광원(랜즈): 빛 발사
            Mirror.cs         거울: 반사(회전 가능)
            Prism.cs          프리즘: 분기(0.5+0.5)
            GateDetector.cs   게이트 수광부: 광량 누적·개방 판정
            GateDoor.cs       게이트 문: 열림/닫힘(위로 슬라이드)
            Ladder.cs         사다리: 등반 감지용 마커
            BeamTransparent.cs 마커: 이 콜라이더는 빛이 통과(발판)
  Player/   PlayerController.cs 이동·점프·사다리 등반
            MirrorInteractor.cs Q/E로 가까운 거울 회전
```

**핵심 규약(어기면 퍼즐이 깨짐)**
- 각도: `0°=동쪽(→)`, **시계방향(CW)**. 시각 회전은 `rotation_z = -angle_deg`.
- 좌표: `1셀 = 1유닛 = 40px`. 그리드 y = Unity y(**위로 증가, 반전 없음**).
- 반사·분기 결과 방향은 **22.5°의 배수**여야 유효.
- Unity 6.3 → Rigidbody2D는 `velocity` 대신 `linearVelocity`, 입력은 **신 Input System**(`Keyboard.current`)만.

**두 개의 큰 흐름**
```
[빛 흐름]  LightSource.Emit() → BeamTracer가 Raycast → 맞은 오브젝트의
           IBeamHit.Interact() 호출 → 이어질 빔을 스택에 push → 반복 →
           GateDetector가 누적 Σ≥1.0이면 개방

[게임 흐름] MapLoader.Start → GameManager(타이틀) → StartGame → Build(stage1)
           → 게이트 통과(GateExit) → GoToNext → Transition(페이드+Build) →
           … → 마지막 스테이지 → OnGameComplete → 엔딩 → 타이틀
```

---

## 1. Data / `MapData.cs`

**역할**: `stages_*.json`을 C# 객체로 역직렬화하는 순수 데이터 모델(Newtonsoft.Json 속성 매핑). 로직 없음.

- **`UnifiedData`**: 최상위. `unit`(단위 정보)과 `stages`(스테이지 딕셔너리, 키 `"stage1"`~`"stage4"`).
- **`StageData`**: 한 스테이지의 전부.
  - `Grid`(W·H), `Camera`(스테이지별 카메라 오버라이드), `Source`(광원), `Prism`(없으면 null), `Gate`(수광부 위치).
  - `Mirrors`·`Platforms`·`Decoys`·`Ladders` 리스트, `Spawn`/`ExitSpawn`(정/역방향 스폰), `GateOpenZone`(문 칸들), `WallTransmit`(빛 통과 벽), `Entrance`(좌측벽 구멍), `Terrain`(열→높이).
  - **`[JsonExtensionData] Extra`**: 매핑 안 된 나머지 JSON 키를 통째로 수집. `wall`, `wall_x25`, `wall_x41` 등 스테이지마다 이름이 다른 벽 키를 유연하게 처리하기 위한 장치.
  - **`AllWalls()`**: `Extra`에서 `"wall"`로 시작하는 모든 키를 훑어 벽 셀 좌표를 열거. → 벽 키 이름이 무엇이든 전부 벽으로 취급.
- **하위 모델**: `GridData`, `CameraSettings`(view_cells·fit_width·pad), `Endpoint`(pos+dir 화살표), `GateData`, `PrismData`(in/out 방향·fixed), `MirrorData`(id·pos·angle_deg·fixed), `PlatformData`(cells·transmit·MISSING), `DecoyData`, `LadderData`(col·y_span).

**원리 노트**: `transmit` 기본값이 `true`(발판은 기본 빛 투과), `MISSING`은 미설계 발판 스킵 플래그. JSON에 없는 필드는 C# 기본값을 쓴다.

---

## 2. Level / `GridMap.cs`

**역할**: 좌표계·방향 변환 유틸(정적 클래스). 상태 없음.

- **`CELL = 1.0f`**: 1셀 = 1유닛 상수.
- **`ToWorld(gx,gy)` / `ToWorld(int[])`**: 그리드 좌표 → 월드 좌표(y 반전 없음).
- **`DirToVector(arrow)`**: 화살표 문자열(`→ ← ↑ ↓ ↗ ↖ ↘ ↙`)을 **정규화된 방향 벡터**로. 대각선은 `.normalized`로 길이 1. 광원 방향·프리즘 출력 방향 주입에 쓰인다.

---

## 3. Light / `Beam.cs`

**역할**: 빛의 최소 단위와 상호작용 계약.

- **`struct Beam`**: `origin`(시작점), `dir`(정규화 방향 — 생성자가 `.normalized`), `intensity`(세기). 값 타입이라 스택에 담아 GC 없이 다룬다.
- **`interface IBeamHit`**: `Interact(incoming, hitCenter, outgoing)`. 빛과 상호작용하는 오브젝트가 구현해 **자기 연산**을 수행하고, 이어질 빔을 `outgoing`에 추가한다.
  - 거울 → 반사광 1개, 프리즘 → 분기광 2개, 수광부 → 추가 없음(흡수) + 누적.
  - **이 인터페이스 덕분에 새 광학 오브젝트를 추가해도 BeamTracer는 수정 불필요.**

---

## 4. Light / `BeamTracer.cs`

**역할**: 빛을 실제로 추적하고 `LineRenderer`로 그린다. 판정은 물리 Raycast, 상호작용은 각 오브젝트에 위임.

- **`LateUpdate() → Trace()`**: **매 프레임 재추적**. 물리 갱신 뒤(LateUpdate)라 플레이어 등 움직이는 차폐물의 최신 위치가 반영된다. 거울을 돌리면 다음 프레임에 자동 반영(별도 호출 불필요).
- **`Trace()`** — 핵심 알고리즘:
  1. `queriesStartInColliders=false`(시작점이 자기 콜라이더 안이어도 무시), `queriesHitTriggers=false`(사다리 Trigger는 빛 통과), `SyncTransforms()`.
  2. 모든 `GateDetector.BeginFrame()`으로 누적만 0 초기화(상태·색은 유지).
  3. 모든 `LightSource.Emit()`을 **스택에 push**(초기 광선). 소스/게이트는 `??=`로 1회만 검색해 캐시.
  4. **스택 루프**(프리즘 분기 때문에 스택 기반):
     - 빔을 pop → 깊이 초과면 스킵.
     - **투과 스캔**: `Raycast`가 맞은 콜라이더가 `IBeamHit`이 없고 `BeamTransparent`가 있으면(=발판) 그 지점 조금 너머로 전진해 다시 Raycast(같은 발판 재검출 방지). 광학 오브젝트/벽을 만날 때까지 반복.
     - 아무것도 없으면 최대 길이까지 직선 세그먼트 추가.
     - `IBeamHit`이면: **오브젝트 중심으로 스냅**(격자 정합) → 세그먼트 추가 → `Interact()` 호출 → 나온 빔들을 스택에 push.
     - 그 외(벽)면 히트 지점까지 세그먼트 추가하고 정지.
  5. 모든 `GateDetector.Commit()`으로 개폐 확정(엣지 트리거).
  6. `Render(segments)`.
- **`Render()`**: `LineRenderer` 풀을 재사용(GC 억제). 세그먼트 수만큼 켜고 나머지는 끈다.

**원리 노트**: 세그먼트를 **오브젝트 중심(collider.transform.position)** 으로 스냅하는 게 핵심 — 실제 히트점이 아니라 격자 중심을 이어 붙여야 반사가 깔끔하게 22.5° 배수로 유지된다. 재사용 버퍼(`_segments`/`_stack`/`_outgoing`)로 매 프레임 할당을 없앴다.

---

## 5. Objects / `LightSource.cs`

**역할**: 랜즈/광원. 고정 방향으로 빛 1개를 발사. 콜라이더 없음(빛 시작점).

- **`Init(dir, intensity)`**: 방향(정규화)·세기 주입.
- **`Emit()`**: 현재 위치·방향·세기로 `Beam`을 만들어 반환. BeamTracer가 매 프레임 호출.

---

## 6. Objects / `Mirror.cs`

**역할**: 거울. 입사광을 각도 기반으로 **1회 반사**. 회전 가능(플레이어가 Q/E) 또는 고정.

- **필드**: `angleDeg`(현재 각), `solutionAngle`(정답 각 — 랜덤/정렬 기준), `isFixed`, `visualAngleOffset`(아트 기본각 보정).
- **`Init(solutionAngle, isFixed, offset)`**: 정답 각을 저장하고 현재 각=정답으로 시작, 시각 회전 적용.
- **`RandomizeFromSolution(maxSteps)`**: 회전 가능한 거울을 정답에서 **±(22.5°×랜덤 steps)** 로 틀어 놓는다. **22.5° 배수로만** 어긋나므로 Q/E로 반드시 정답 도달 가능. 고정 거울은 무시.
- **`SnapToSolution()`**: 현재 각=정답으로 즉시 정렬(P키 정답 정렬용).
- **`Interact()`**: `Reflect(입사방향)` 한 개를 outgoing에 추가(세기 보존).
- **`Rotate(steps)`**: 22.5°씩 회전(고정이면 무시). 플레이어 조작.
- **`Reflect(d)`**: 반사식 `n=(cosθ,−sinθ)`, `r = d − 2(d·n)n`. **`angleDeg`를 반사면의 법선 각으로** 해석. 결과 정규화.
- **`ApplyVisualRotation()`**: `"visual"` 자식을 `rotation_z = -angle + visualAngleOffset`로 회전. **반사 연산과 별개** — 아트를 세로로 그렸을 때(offset=90) 보이는 방향만 보정하고 반사각은 그대로.

**원리 노트**: 반사 연산은 `angleDeg`만 쓰고 `visualAngleOffset`은 시각에만 쓴다 → 아트 방향을 바꿔도 퍼즐이 안 깨진다. 런타임 회전은 `angleDeg`를 바꾸므로, 시각 보정이 `ApplyVisualRotation`에 들어 있어야 회전 후에도 유지된다.

---

## 7. Objects / `Prism.cs`

**역할**: 프리즘(고정). 입사광을 여러 방향으로 **분기**하고 에너지를 균등 분배.

- **`Init(outDirections)`**: 맵 데이터의 출력 화살표들을 방향 벡터로 저장.
- **`Interact()`**: `share = 입사세기 / 출력수`(2방향이면 0.5씩). 각 출력 방향으로 `Beam`을 outgoing에 추가.

**원리 노트**: 게이트는 두 분기의 세기를 **합산**(Σ)해야 1.0에 도달 → "두 갈래를 모두 성립시켜야 개방"이라는 퍼즐 규칙이 여기서 나온다.

---

## 8. Objects / `GateDetector.cs`

**역할**: 게이트 수광부. 도달한 광량을 누적해 임계(Σ≥1.0) 이상이면 개방. 빛을 흡수(이어지는 빔 없음).

- **`Interact()`**: 입사 세기를 `_acc`에 누적만. 개폐 판정은 `Commit`에서.
- **`BeginFrame()`**: 매 프레임 재추적 시작 시 누적만 0으로(상태·색·이벤트는 유지).
- **`Commit()`**: 누적 세기로 개폐 확정. **엣지 트리거** — 상태가 바뀔 때만 색 변경·이벤트 발생. `OnOpen`(열리는 순간 1회), `OnStateChanged(bool)`(양방향, 문 여닫이용).
- **`CacheClosedColor()`**: 최초 배치 시 닫힘 색을 기억(개방 시 초록으로 바꿨다가 되돌리기 위해).

**원리 노트**: `BeginFrame`(누적 0) → 매 프레임 `Interact`로 누적 → `Commit`(판정)의 3단계라, 빛 경로가 매 프레임 바뀌어도 깜빡임 없이 안정적으로 개폐된다. 색 변경은 `visual != null`·엣지에서만 → 프리팹 아트에 대표 SpriteRenderer가 없어도 안전.

---

## 9. Objects / `GateDoor.cs`

**역할**: 게이트 개폐부(문). 수광부가 열리면 **위로 천천히 슬라이드**해 통로를 뚫고, 닫히면 천천히 내려와 막는다.

- **필드**: `slideDuration`(여닫이 시간), `_blocker`(콜라이더), `_visual`(SpriteRenderer), `_door`(움직일 Transform), `_closedPos`(닫힘 위치), `_slide`(열릴 때 위로 이동 거리 = 개폐존 높이).
- **`Register(col, sr, slideUp)`**: MapLoader가 문 블럭·이동거리를 등록. 닫힘 위치·대상 Transform 기억.
- **`SetOpenImmediate(open)`**: 연출 없이 즉시 상태 적용(최초 배치용).
- **`SetOpen(open)`**: 수광부 `OnStateChanged` 구독 대상. 상태 변화 시 슬라이드 코루틴 시작. **열림은 즉시 콜라이더 해제**(올라가는 중에도 통과), **닫힘은 다 내려온 뒤 콜라이더 활성**(플레이어에 관대 + 열린 문이 빔을 가로막지 않음).
- **`Slide(open)`**: from→to로 위치와 색을 Lerp. 남은 거리에 비례해 시간을 잡아 중간에 방향이 뒤집혀도 속도가 일정.

**원리 노트**: 문이 위로 올라가 있어도 콜라이더가 꺼져 있어 빔 추적에 안 걸린다 → 수광부가 문 바로 위(같은 열)에 있어도 사고 없음.

---

## 10. Objects / `Ladder.cs` · `BeamTransparent.cs`

- **`Ladder`**: 등반 통로 마커. `height`만 보관. 실제 등반 로직은 `PlayerController`. 콜라이더는 Trigger라 빛은 통과.
- **`BeamTransparent`**: 빈 마커 컴포넌트. 발판(transmit=true)에 붙어 "이 솔리드 콜라이더는 빛이 통과"를 표시. `BeamTracer`가 이 마커를 보고 히트를 건너뛴다. 벽은 이 마커가 없어 빛을 막는다.

---

## 11. Level / `MapLoader.cs` (중심 허브)

**역할**: 맵 JSON을 읽어 **모든 오브젝트를 생성·배치·변수 주입**한다. "무엇을 어디에 어떤 각도로"만 책임지고, 상호작용은 각 오브젝트에 맡긴다. 씬의 빈 GameObject에 붙어 있다.

### 11.1 인스펙터 필드(요약)
- 맵/스테이지: `mapFile`, `stageKey`, `stageOrder`.
- 게임 플로우: `useGameFlow`(타이틀 사용), `showTitleOnBoot`, `OnGameComplete`(엔딩 콜백), `IsTransitioning`.
- 편의 키: `restartKey`(R), `debugStageKeys`(1~4), `autoSolveKey`(P) — **데모 빌드 시 디버그 키들 끌 것**.
- 카메라: `followPlayer`, `cameraViewCells`.
- 게이트: `gateExitInset`(통과 판정을 안쪽으로 넓히는 정도).
- 아트: `artScale`(표시 배율, 기본 2 — **콜라이더·판정 불변, 보이는 크기만**).
- 거울: `mirrorArtAngleOffset`(아트 기본각 90), `randomizeMirrors`, `mirrorRandomSteps`(2=±45°).
- 프리팹 슬롯 16종(비면 색 사각형 폴백).
- 상수: `Z_*`(정렬순서), `PLATFORM_*`(발판 기하 — 윗면을 칸 위 모서리 y+0.5에 맞춰 지형과 높이 일치).

### 11.2 생명주기
- **`Start()`**: `useGameFlow`면 `GameManager.Bootstrap(this)`(타이틀부터), 아니면 `buildOnStart`로 즉시 Build.
- **`Update()`**: 전환/일시정지/타이틀(ControlsLocked) 중엔 무시. `R`=Restart, `P`=SolveAllMirrors, `1~4`=GoToIndex.
- **`Build()`** — 핵심 절차:
  1. `mapFile` 파싱 → 스테이지 찾기(실패 시 에러 로그).
  2. `Clear()` + `_mirrors.Clear()` + `Level_{stageKey}` 루트 생성.
  3. **비광학**: BuildTerrain → Walls → Platforms → Ladders.
  4. **광학**: BuildLens → Mirrors → Prism → Gate.
  5. 기타: Decoys → Entrance(역주행 트리거) → Spawn → Player.
  6. `BeamTracer` 생성 후 1회 `Trace()`(에디트 모드 미리보기 포함).
  7. `randomizeMirrors`면 **`EnsureUnsolvedStart`**: 랜덤이 우연히 게이트를 열면 닫힌 배치가 나올 때까지 재추첨(최대 20회) → 항상 "안 풀린 상태"로 시작.
  8. `SetupCamera`.
- **`Clear()`**: `Level_`로 시작하는 자식(=레벨 전체)을 파괴. GameUI·ScreenFader는 별도 루트라 안 지워짐.

### 11.3 스테이지 전환
- **`GoToNext()`**: 다음 스테이지로. **마지막이면** `OnGameComplete`(엔딩) 호출, 플로우 미사용 시 stageOrder[0]로 순환(폴백).
- **`GoToPrev()`**: 이전 스테이지로(첫 스테이지면 무시). 입장 통로 역주행 시.
- **`StartGame()`**: 타이틀에서 첫 스테이지부터 빌드(GameManager가 호출).
- **`Restart()`**: 현재 스테이지 재구축(거울 재추첨·플레이어 리셋).
- **`GoToIndex(i)`**: 디버그 즉시 이동.
- **`Transition(next, reverse)`**(코루틴): ControlsLocked 잠금 → 페이드 아웃 → `_reverseEntry` 세팅 → stageKey 교체 → Build → 한 프레임 대기 → 페이드 인 → 잠금 해제. 전환의 유일한 연출 경로.
- **`EffectiveSpawn(s)`**: 역주행이면 `exit_spawn`(출구쪽), 아니면 `spawn`(입장 통로).

### 11.4 거울 퍼즐 보조
- **`SolveAllMirrors()`**: `_mirrors`(회전 가능 거울)를 전부 `SnapToSolution()`. 빛은 다음 프레임 자동 재추적.
- **`EnsureUnsolvedStart(tracer)`**: 게이트가 열려 있으면 거울을 다시 랜덤화하고 재추적, 닫힐 때까지(최대 20회). 로컬 함수 `AnyOpen()`으로 판정.

### 11.5 비광학 배치
- **`BuildTerrain`**: `terrain` 딕셔너리(열→높이)로 0..높이 칸을 솔리드 타일로 채움. `fitToScale`로 1×1칸에 정확히 맞춤(이웃과 연결). 빛 차단은 벽 담당.
- **`BuildWalls`**: `AllWalls()` 순회. `entrance` 칸은 벽 생략(구멍). `wall_transmit` 칸은 반투명 + `BeamTransparent`(빛만 통과). 나머지는 불투명 벽(빔 정지).
- **`BuildPlatforms`**: 발판 셀마다 얇은(0.4) 솔리드. **윗면을 y+0.5로 올려 지형과 높이 일치**(`PLATFORM_CY`). `transmit`면 파랑 + `BeamTransparent`, 아니면 남색(빛 차단). `fitToScale`로 가로 1칸 정합.
- **`BuildLadders`**: `y_span`으로 사다리 세로 구간 계산. **지형이 채운 칸은 건너뛰어 땅 위에만** 생성. Trigger 콜라이더(0.6×h) + `Ladder`. 프리팹은 `BuildLadderSegments`로 1칸 조각 반복.

### 11.6 광학 배치
- **`BuildLens`**: 광원 생성 + `LightSource.Init`. **횃불(`torchPrefab`)을 바닥에 세움**(`PlaceOnSurface`). 색 사각형 모드에선 방향 표시 점을 추가(프리팹 모드에선 생략).
- **`BuildMirrors`**: 거울마다 솔리드 루트 + `"visual"`. 아트 기본각 보정은 프리팹에만. `Mirror.Init(정답각)` 후 `randomizeMirrors`면 랜덤화. 회전 가능한 것만 `_mirrors`에 등록.
- **`BuildPrism`**: 프리즘 루트 + 시각(45°는 플레이스홀더 마름모용) + `Prism.Init(출력방향들)`.
- **`BuildGate`**: 수광부 루트 + 시각(→ `GateDetector.visual`) + `GateDetector`. 이어 `BuildGateDoor`·`BuildGateExit`.
- **`BuildGateExit`**: 개폐존 바운딩 박스로 Trigger 생성. **얇은 축(통로 방향)을 그리드 중심 쪽으로 `gateExitInset`만큼 확장** → 표면에 붙기 전/붙은 채로도 통과 판정. `GateExit.Init(det, this, +1)`.
- **`BuildEntrance`**: 입장 통로에 역방향 Trigger(`GateExit.Init(null, this, -1)`) → 왼쪽으로 나가면 이전 스테이지.
- **`BuildGateDoor`**: 개폐존을 **하나의 긴 블럭**(콜라이더 1 + 시각 1)으로. 프리팹은 `InstantiateGateDoor`로 존에 맞춤(가로형 90° 회전). `door.Register(box, sr, h)` + `SetOpenImmediate(false)` + 수광부 `OnStateChanged` 구독.

### 11.7 기타 배치
- **`BuildDecoys`**: 가짜 광학 표식(45° 마름모).
- **`BuildSpawn`**: 스폰 표식(선택). `EffectiveSpawn` 위치.
- **`BuildPlayer`**: 스폰에 플레이어 액터 생성. BoxCollider(0.6×0.9, 마찰0·모서리라운딩)+Rigidbody2D(중력3)+`PlayerController`(리스폰 경계 주입)+`MirrorInteractor`. 카메라 추적용 Transform 반환.

### 11.8 시각/프리팹 유틸 (프리팹 seam의 핵심)
- **`SolidRoot`**: 솔리드 콜라이더 루트(시각은 자식으로 분리).
- **`Decor` / `SolidDecor`**: 순수 시각 / 시각+솔리드 오브젝트. `prefab`·`fitToScale`를 하위로 전달.
- **`Visual(색)`**: 1×1 흰 텍스처로 색 사각형 `"visual"` 자식 생성(플레이스홀더 기본 경로).
- **`Visual(프리팹, …)`**: 프리팹 있으면 인스턴스화, 없으면 색 사각형 폴백. `fitToScale`면 `InstantiateFitted`, 아니면 `InstantiatePrefab`. **반환 SpriteRenderer는 게이트 색 피드백 호환용.**
- **`InstantiatePrefab`**: 프리팹을 `"visual"` 자식으로. 회전 적용 + **`artScale` 표시 배율** 적용 + sortingOrder=기준+내부 상대순서.
- **`InstantiateFitted`**: 타일용. 원본 크기 무관하게 **지정 칸 크기에 정확히 맞춤**(배율 미적용 — 이웃과 빈틈·겹침 없이 연결).
- **`InstantiateGateDoor`**: 문용. 원본 무관하게 **개폐존에 맞춤**, 가로형 존은 90° 눕혀 재사용.
- **`BuildLadderSegments`**: 1칸 조각을 세로로 h개 쌓음(균일 스케일 → 비율 유지, 늘어나지 않음).
- **`PlaceOnSurface`**: 부속 아트(횃불)를 원본 크기(×artScale)로, **밑면이 지지면에 닿도록** 배치.
- **`SurfaceBelow`**: 지정 칸에서 아래로 가장 가까운 지지면(지형 t+0.5 / 발판 cy+0.5)의 윗면 y. 없으면 NaN.
- **`Square`**: 1×1 흰 스프라이트(정적 캐시). 모든 색 사각형의 원본.

### 11.9 카메라
- **`SetupCamera`**: `followPlayer`면 직교 크기(view_cells 또는 fit_width로 가로 폭)·경계(pad 포함) 계산 후 `CameraFollow.Configure`. 아니면 `FrameCamera`(전체 프레이밍 폴백).
- **`FrameCamera`**: 그리드 전체가 보이도록 중앙·직교크기 세팅.
- **`DestroySafe`**: 플레이 중 `Destroy` / 에디터 `DestroyImmediate` 자동 선택.

**원리 노트**: MapLoader는 "생성만" 한다. 로직은 각 오브젝트가, 빛은 BeamTracer가, UI는 GameManager가 담당 → 관심사가 깔끔히 분리된다.

---

## 12. Level / `GameManager.cs`

**역할**: 게임 흐름(타이틀→플레이→일시정지→엔딩) UI 상태머신. **코드로 Canvas/Text를 자체 생성** → 씬 세팅·프리팹·EventSystem 불필요, 키보드 구동.

- **`Bootstrap(loader)`**(정적): `GameUI` 오브젝트 생성(DontDestroyOnLoad), `OnGameComplete` 구독, UI 생성, 타이틀(또는 바로 시작) 진입. MapLoader.Start가 호출.
- **`Update()`**: "아무 키" 안내문 명멸(unscaled 시간) + 진입 쿨다운 처리 후, 상태별 입력 — 타이틀/엔딩=아무 키, 플레이=ESC 일시정지, 일시정지=ESC 계속/R 재시작/T 타이틀.
- **상태 전이**: `EnterTitle`/`StartGameFromTitle`/`EnterPause`/`ResumeFromPause`/`RestartFromPause`/`QuitToTitle`/`HandleGameComplete`/`EndingToTitle`. 각 전이가 `Time.timeScale`(일시정지·엔딩=0)·`ControlsLocked`·표시 패널을 세팅.
- **UI 생성**: `BuildUI`(Canvas + CanvasScaler + 3패널), `MakePanel`(전체화면 배경 + CanvasGroup), `MakeText`(레거시 Text), `Switch`(한 패널만 활성 + 페이드 인), `FadeIn`(unscaled 시간이라 timeScale=0에서도 동작), `UIFont`(맑은 고딕 등 OS 폰트 동적 로드 → 한글 렌더).

**원리 노트**: 일시정지는 `timeScale=0`(물리 정지) + `ControlsLocked`(입력 차단) 이중. 재시작 시 `timeScale=1` 복구 후에야 전환 페이드가 진행된다(0이면 코루틴 멈춤). 안내문 명멸·페이드가 `unscaledTime`이라 정지 중에도 살아 있다.

---

## 13. Level / `ScreenFader.cs`

**역할**: 스테이지 전환용 전체화면 검은 페이드. 스스로 ScreenSpaceOverlay Canvas + Image 생성, DontDestroyOnLoad라 레벨 Clear에 안 지워짐.

- **`Create()`**(정적): 최상단(sortingOrder=short.MaxValue) 검은 Image 생성.
- **`SetAlpha(a)`** / **`Fade(from,to)`**(코루틴): duration 동안 알파 보간. MapLoader.Transition이 아웃/인에 사용.

---

## 14. Level / `CameraFollow.cs`

**역할**: 2D 직교 플레이어 추적 카메라. 부드럽게 따라가고 레벨 밖은 안 보이게 클램프.

- **`Configure(target, min, max)`**: 대상·경계 주입 + 첫 프레임 튐 방지 스냅.
- **`LateUpdate()`**: `target==null`이면 무동작(레벨 Clear로 플레이어가 사라져도 안전). `SmoothDamp`로 추적.
- **`Clamp(p)`**: 뷰가 레벨보다 크면 그 축은 중앙 고정, 아니면 경계 안으로 클램프.

---

## 15. Level / `GateExit.cs`

**역할**: 스테이지 이동 트리거. `dir +1`(게이트 통과, 개방 상태에서만) 또는 `dir −1`(입장 통로 역주행, 조건 없음).

- **`Init(gate, loader, dir)`**: 게이트(null이면 무조건)·로더·방향 주입.
- **`OnTriggerEnter2D` / `OnTriggerStay2D` → `TryPass`**: 게이트가 있으면 개방 상태에서만, 플레이어면 `GoToNext`/`GoToPrev`. **Stay도 판정**하는 이유: 문에 붙어 있는 동안 게이트가 열려도(재진입 없이) 통과되게. 중복 호출은 `_transitioning` 가드가 막는다.

---

## 16. Player / `PlayerController.cs`

**역할**: 플레이어 액터 — 좌우 이동·가변 점프·사다리 등반. 신 Input System.

- **정적 `ControlsLocked`**: 전환/일시정지/타이틀/엔딩 중 입력·이동 정지(여러 스크립트가 공유하는 게이트).
- **`Awake`**: Rigidbody 세팅(회전 고정·보간·연속 충돌), 접지 필터(트리거=사다리 제외).
- **`Update`**: 점프 눌림/뗌(에지)·좌우 "나중에 누른 방향"(SOCD last-wins)만 기록. 실제 이동은 FixedUpdate.
- **`FixedUpdate`**: ControlsLocked면 완전 정지. 경계 밖이면 리스폰. 사다리 위 상하 입력이면 등반(중력 0·수직 이동). 아니면 지상/공중 이동 + **가변 점프**(상승 중 버튼 떼면 감쇠) + **코요테 타임**(발판 이탈 직후 잠깐 점프 허용) + **하강 중력 가중**(낙하 빠르게, 정점 3.5칸 유지).
- **`SetRespawn`**: 스폰·경계 주입(MapLoader). **`Respawn`**: 스폰 복귀(보간 리셋으로 잔상 방지). **`IsGrounded`**: 발밑 Cast(법선 위쪽만). **`OnTriggerEnter/Exit`**: 사다리 겹침 카운트.

**원리 노트**: 점프 물리는 정점 높이(3.5칸)에서 초기 속도를 역산(`v0=√(2gh)`). 상승엔 손 안 대고 하강에만 중력을 키워 "정점은 유지하되 낙하는 빠른" 손맛.

---

## 17. Player / `MirrorInteractor.cs`

**역할**: 플레이어의 거울 조작기. 반경 안 가장 가까운 **회전 가능(비고정)** 거울을 흰색 하이라이트 → `Q`(반시계)/`E`(시계) 22.5° 회전.

- **`Update`**: ControlsLocked면 무시. `UpdateSelection` 후 Q/E로 `Mirror.Rotate`. 빛은 BeamTracer가 매 프레임 재추적하므로 별도 호출 불필요.
- **`UpdateSelection`**: 모든 Mirror 중 비고정·반경 내 최근접 선택.
- **`Select`**: 이전 선택 색 복원 후 새 선택의 `"visual"` 하위 첫 SpriteRenderer를 하이라이트(프리팹 아트도 호환).
- **`OnDisable`**: 하이라이트 원복.

---

## 부록 A. 데이터 흐름 요약

```mermaid
flowchart TD
  JSON[stages_cord.json] -->|Newtonsoft| MapData
  MapData --> MapLoader
  MapLoader -->|생성/주입| Objects[LightSource·Mirror·Prism·Gate·Door·Ladder·Player]
  MapLoader --> Tracer[BeamTracer]
  Source[LightSource.Emit] --> Tracer
  Tracer -->|Raycast+IBeamHit.Interact| Objects
  Tracer -->|누적 Σ≥1.0| Gate[GateDetector]
  Gate -->|OnStateChanged| Door[GateDoor 슬라이드]
  Player -->|통과| GateExit --> MapLoader
  MapLoader -.OnGameComplete.-> GameManager
  GameManager -->|타이틀/일시정지/엔딩| UI
```

## 부록 B. "누가 무엇을 책임지나" 빠른 표

| 관심사 | 담당 스크립트 |
|---|---|
| 맵 데이터 파싱 | `MapData` |
| 좌표·방향 변환 | `GridMap` |
| 오브젝트 생성·배치·아트 | `MapLoader` |
| 빛 추적·렌더 | `BeamTracer` (+ `Beam`/`IBeamHit`) |
| 광학 연산 | `LightSource`·`Mirror`·`Prism`·`GateDetector` |
| 게이트 문 개폐 | `GateDoor` |
| 스테이지 이동 | `GateExit` → `MapLoader.GoTo*` |
| 플레이어 조작 | `PlayerController`·`MirrorInteractor` |
| 카메라 | `CameraFollow` (+ `MapLoader.SetupCamera`) |
| 게임 흐름·UI | `GameManager` (+ `ScreenFader`) |
