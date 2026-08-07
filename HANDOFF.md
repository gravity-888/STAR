# 별을 향해 (Toward the Stars) — 개발 인수인계

> 다른 컴퓨터에서 이어서 개발하기 위한 정리본. **이 파일부터 읽으면 됩니다.**
> 최종 갱신: 2026-08-03(2차) — 미는 거울 조작감·반사 모델 정교화, 애니 seam, 게이트 게이지/문 정리. **로드맵 3 실작업 진행 중**: 사용자가 오브젝트 아트(`Assets/Art/Sprites/object/`)·플레이어 애니(`.../player/` — Animator Controller + idle/run/jump/fall/move_jump/move_fall)·`PlayerArt.prefab` 제작 중.
>
> ### 👉 다음에 뭘 해야 하는지는 **[§8 데모 완성 로드맵](#8-데모-완성-로드맵-확정--2026-07-20)** 을 볼 것.
> 순서가 확정돼 있고 그렇게 정한 이유도 함께 적혀 있습니다. 임의로 순서를 바꾸지 마세요.

---

## 📍 지금 상태 / 다음 할 일 (2026-08-01 — 새 세션은 여기부터)

**로드맵**: **1·2 완료**, **3 진행 중**(임시 아트 — 사용자가 직접 제작 중, 규격 [`Assets/Art/PREFAB_SPEC.md`](Assets/Art/PREFAB_SPEC.md)).

**그 사이 아트 seam 동작을 대폭 다듬음**(상세는 §6·§7·§10):
- 게이트 문 = 개폐존 전체 **긴 블럭 1개** + 열림 시 **위로 슬라이드**, 닫힘 시 하강(`GateDoor`).
- 거울: 아트 **기본각 보정 `mirrorArtAngleOffset`(90°, 세로로 그림)** + **시작각 ±45° 랜덤**(`randomizeMirrors`) + **정답 정렬 `P`키**(`SolveAllMirrors`) + **안 풀린 상태로 시작 보장**(`EnsureUnsolvedStart`).
- 거울 거치대 = **바닥/천장 2종 재도입**(`mirrorMountPrefab`·`mirrorMountCeilingPrefab`). 코드가 거울마다 위/아래 중 **가까운 지지면**을 골라 자동 배치 — 아래=바닥에 세움, 위(발판=천장)=**뒤집어 매닮**. stage4 천장 거울(발판 `mirror_relation:above`인 M1·M7)이 자동 처리(하드코딩 없음). 천장 전용 아트 10×80.
- 횃불(`torchPrefab`) 바닥에 세움, 발판 윗면을 **지형과 같은 높이(y+0.5)로 올림**, 타일(지형·벽·발판) **칸에 정확 정합**, 사다리 **1칸 조각 세로 반복**, **`artScale` 표시배율**(아트만 확대, 콜라이더 불변).

**맵 에디터 신설·확장**: [`tools/map-editor.html`](tools/map-editor.html) — 브라우저 단일파일. 격자에 전 요소 배치 → **스키마 JSON 입출력**(라운드트립 검증), 카메라 이동범위·화면크기 시각화(게임 계산 재현). **지형 타입칠(잔디/땅/실내)·광원(횃불) has_lens 토글·시작 랜즈(바닥) 배치·게이트 열림방향·스테이지별 배경#·스테이지 추가/삭제(＋/－, `stage_order` 내보냄)** 지원. 전체 스크립트 해설 [`SCRIPTS.md`](SCRIPTS.md), C# 문법사전 [`CSHARP_SYNTAX.md`](CSHARP_SYNTAX.md).

**후속 추가(2026-07-30)**:
- **지형 아트 3종**: `terrainGrassPrefab`·`terrainDirtPrefab`·`terrainIndoorPrefab`(+공통 폴백 `terrainPrefab`). 맵의 `terrain_type`(칸별 `"x,y"→grass|dirt|indoor`)·`terrain_type_default`(기본 `dirt`)로 지정. 색 폴백 초록/갈색/회색.
- **랜즈 시스템(stage4 한정)**: 랜즈가 **획득 아이템**. `LightSource.Emitting`이 false면 빔 없음. 횃불에 `TorchMount`(F키 장착/해제), 바닥 `LensItem`(F키 줍기), 플레이어 `LensInteractor`(F 문맥감응). 맵 스키마: `source.has_lens`(기본 true)·`lens_item:[x,y]`. stage4 = `has_lens:false`+`lens_item:[8,1]`. 1~3은 기존대로(랜즈 고정 장착).
- **게이트 수광부** = 1칸(40×40) 고정(`fitToScale`). **PPU는 100 유지**(과거 40 실험은 롤백).
- **게이트 열림 방향**(`gate.open_dir`) + 열릴 때 아트 뒤로 가려짐(`GateDoor.openSortingOffset`).
- **오디오 seam 구축**([`AudioManager`](Assets/Scripts/Level/AudioManager.cs)): 정적 호출 훅(거울회전·게이트개방·전환·점프/착지·랜즈 줍기/장착/해제·타이틀/플레이/엔딩 BGM) 심음. **씬에 AudioManager 두고 클립만 채우면 재생, 없거나 빈 슬롯이면 무음**(프리팹 seam과 동일 방식) → 로드맵 3의 오디오 절반 **코드 완료, 클립 미할당**.

**후속 추가(2026-08-01)** — 게이트·전환·진행 저장 대폭 개선(플레이 실측 "문제 없음" 확인):
- **충전식 게이트**: 빛(Σ≥1.0)을 `chargeTime`(1.2s) 유지해야 개방. **완전 개방 시 래치**(`latchOnOpen`)—이후 빛 가려져도 열린 채 유지(통과 중 플레이어가 빔 막아도 안 닫힘). 수광부는 **단일 아트(색 변경 없음)**, 진행은 **임시 충전 게이지**(수광부 위 바) → 나중에 수광부 내부 채움 효과로 교체 예정.
- **게이트 열림 방향**(`gate.open_dir` ↑↓←→) + 열릴 때 정렬순서 내려 아트 뒤로 가려짐.
- **스테이지 배경**: `stageBackgrounds` 프리팹 배열(스테이지 수만큼) + 맵 `background` 인덱스 + **시차 스크롤**([`ParallaxBackground`](Assets/Scripts/Level/ParallaxBackground.cs), 프리팹 레이어별 factor 가능).
- **`stage_order` 데이터 주도**: 맵이 스테이지 개수·순서 결정(인스펙터 수정 불필요). 에디터에서 ＋/－로 스테이지 가감.
- **전환 오실레이션 수정**: 스폰이 전환 트리거에 겹치면 무장 해제(나갈 때까지 통과 금지) → 스테이지 이동 렉/멈춤 해결(`GateExit._armed`).
- **진행상태 저장/복원**: 다른 맵 갔다 와도 거울 각도·게이트 개방·랜즈 장착이 이어짐(`_progress`/`CaptureProgress`, **메모리 저장·세션 한정**). `R`=현 스테이지 진행 폐기·새 랜덤, 새 게임=전체 초기화.
- **에디터 지형 기본타입**(`terrain_type_default`) 라운드트립 + 선택기 추가.

**후속 추가(2026-08-03)** — 로드맵 4·5 착수 + 미는 거울 신규:
- **로드맵 5 플레이어 메트릭 확정**: `moveSpeed 6→7`·`climbSpeed 5→6`·`jumpHeightCells 3.5→3.8`(짧게 눌러도 기존 3.5칸 목표 도달, 도달성 불변). 코드 확정([`PlayerController`](Assets/Scripts/Player/PlayerController.cs)). 파생: 최대 수평 점프 ≈6.2칸(기존 5.1)·점프 체공 0.89s. **도달성은 안전**(2026-07-20 통합 플레이테스트가 더 낮은 값으로 전 스테이지 클리어 검증 → 더 쉬워짐). 6번 밸런싱 때 "쉬워진 점프로 의도 안 된 지름길이 없는지"만 확인.
- **로드맵 4 카메라 하드닝(빌드 검증 대기)**: `fit_width` 줌을 [`CameraFollow`](Assets/Scripts/Level/CameraFollow.cs)가 **화면비 변화(창 리사이즈·해상도)마다 재계산**하도록 변경(이전엔 로드 시 1회 고정 → 창 리사이즈 시 좌우 벽 어긋남). 계산상 stage4는 모든 가로형 화면비에서 좌우 벽이 화면 끝에 붙음. **⚠️ 실기 빌드 프레이밍 검증은 미완**(사용자 수동 — Windows 빌드로 여러 해상도 + 창 리사이즈 확인).
- **미는 거울 신규 메커니즘**([`PushableMirror`](Assets/Scripts/Objects/PushableMirror.cs)): 플레이어가 **좌우로 밀어 옮기는 거울**(회전 불가·각도 맵 고정). **Dynamic RB(중력 적용 — 바닥에 놓이고 지지면 없으면 떨어짐)**, 정지 시 **X축 고정(FreezePositionX)**으로 떨림·몸빵밀기 방지. 위치는 진행상태 저장(다른 맵 갔다 와도 유지). 반사는 기존 `Mirror`. 맵 `mirror.pushable:true`·프리팹 `mirrorPushPrefab`+전용 받침대 `mirrorPushMountPrefab`(배치 규칙은 바닥 거치대와 동일, 거울과 함께 미끄러짐)·에디터 토글 지원.
  - **밀기 세션(가감속)**: 밀기 시작 시 0→`pushSpeed`(기본 2.5칸/초)로 **서서히 가속**, 놓으면 관성 방향 다음 칸까지 **arrive식 감속**으로 부드럽게 안착(가속도 `accel` 기본 12칸/초²). 거울이 실제로 움직이는 동안만 [`PlayerController.SetPushDrive`](Assets/Scripts/Player/PlayerController.cs)로 플레이어를 **같은 실시간 속도**로 구동 → **점프·방향전환·등반 불가**(함께 가감속). **놓거나 벽에 막히면 즉시 자유**(스냅 후 멈춤 기능은 삭제). 도착 판정은 "이번 프레임 이동거리 이내"라 진동·무한루프 없음. `pushSpeed`·`accel`은 인스펙터에서 조정.
- **반사 모델 업그레이드 — 표면 접점 반사(2026-08-03)**: `IBeamHit.Interact`가 이제 **실제 상호작용 지점을 반환**([Beam.cs](Assets/Scripts/Light/Beam.cs)). 거울은 **입사광과 반사면(선분, 반길이 `surfaceHalfLength` 0.55칸)의 교점**에서 반사 → **거울의 어느 부분에 맞느냐에 따라 반사 위치가 달라짐**([Mirror.cs](Assets/Scripts/Objects/Mirror.cs)). 반사 *방향*은 각도(법선)로 그대로. 이전의 "중심 스냅/격자 스냅" 제거 → 미는 거울이 미끄러질 때 반사점이 **거울 면을 따라 매끄럽게** 이동, 정지 시 고정. 프리즘·수광부는 중심 반환(기존 동작). **자기충돌 방지**: 반사점이 콜라이더 밖일 때 반사 빔이 자기 거울을 다시 때리지 않도록 BeamTracer가 그 콜라이더를 한 세그먼트 건너뜀. **플레이스홀더 막대**는 0.18×1.1(긴 축=반사면)로 바꿔 반사가 긴 면에서 일어나 보이게(에디터도 동일).
  - **회귀 검증 완료(시뮬레이터)**: 반사 규약을 재현한 시뮬레이터(`scratchpad/beamsim`, C# 콘솔)로 정답 배치를 추적 → **Stage 1~4 전부 게이트 Σ=1.00, 모든 거울에서 빛이 정확히 중심(maxOff=0.00)**. 즉 맵이 완벽히 중심-대-중심 설계라 표면 접점=중심 → **기존 퍼즐 불변**(재조정 불필요). OLD 모델도 같은 시뮬레이터에서 전부 도달을 재현해 시뮬레이터 정확성 자가검증됨.

**후속 추가(2026-08-03, 2차)** — 미는 거울 조작감·반사 마감 + 애니 seam + 게이트 정리:
- **반사 최종 규약**: 반사점은 **항상 입사 광선 위**에서 결정(클램프 제거) → **광원 광선 각도 절대 불변**. 반사면 막대 범위(`surfaceHalfLength` 0.55) 안에서만 반사, **벗어나면 통과**. 진행 방향 안 바뀌면(평행 스침) **거울에서 종료**([Mirror.cs](Assets/Scripts/Objects/Mirror.cs)). ※ 반사점이 면 따라 움직이는 건 허용, 광선 "각도"만 고정이 요구사항이었음.
- **수광부**([GateDetector.cs](Assets/Scripts/Objects/GateDetector.cs)): 빔이 **실제 접점에서 종료**(중앙 스냅 제거) + 광량은 **빛이 중심으로 정확히 들어올 때만**(`centerTolerance` 0.2) 인식.
- **게이트 게이지**: 수광부 **아트 뒤(Z_OBJECT−1)에서 아래→위로 세로로 차오름**(y스케일). 수광부 아트에 **투명 영역** 있어야 보임(불투명 플레이스홀더면 가려서 안 보임).
- **문(GateDoor)**: 열림/닫힘 **색상 변경 제거**(아트 색을 코드가 안 건드림). 슬라이드·정렬·충돌 토글은 유지.
- **미는 거울 조작감**: 가감속(`accel` 12), 관성 안착, **소프트락 방지**(안 움직이면 0.2s 후 플레이어 자유), 벽 막힘 시 자유.
- **애니메이션 seam**([PlayerAnimator.cs](Assets/Scripts/Player/PlayerAnimator.cs)): PlayerController가 `Speed·VSpeed·Grounded·Climbing·Pushing·PushDrive` + `Facing` 노출 → 프리팹 Animator 파라미터로 전달 + 방향 뒤집기. `PushDrive`(0~1)로 밀기 가속·감속(전환) 구분, `Speed`로 이동 점프(RunJump) 구분. **아트 없어도 null-safe**. 계약은 [PREFAB_SPEC](Assets/Art/PREFAB_SPEC.md).
- **플레이어 크기/정렬**: 콜라이더 **1.0×0.9**(0.6→1.0), 프리팹 아트 세로 오프셋 `playerArtOffsetY`(기본 −0.45, 바닥 피벗 아트 발을 콜라이더 바닥에 맞춤 — 인스펙터 조정).
- **사용자 진행(로드맵 3)**: `Assets/Art/Sprites/object/`에 전 오브젝트 아트, `.../player/`에 애니 클립+`PlayerArt.controller`, `PlayerArt.prefab` 제작 중. **아트 스프라이트 Pivot=Bottom**(프레임별 높이 달라도 발 고정), 점프/낙하 클립은 **Loop Time 끔**.

**▶ 다음 순서**: (a) 사용자가 아트 제작·슬롯 채우기 + **오디오 클립 채우기**(씬에 `AudioManager` 추가)로 **3번 마무리**(seam은 코드·구축 완료) → (b) 로드맵 **4(실기 빌드) → 5(플레이어 메트릭) → 6(밸런싱) → 7(최종 아트/오디오 교체)**. 맵 에디터는 필요 시 P4(undo/개별선택 이동)·P6(빛 경로 미리보기) 확장 가능.
> ⚠️ 씬에 새로 추가할 것: 오디오 쓰려면 **빈 GameObject + `AudioManager`**(클립 배정). 프리팹 슬롯(지형3·거치대2·배경 배열 등)도 아트 나오면 MapLoader 인스펙터에 배정 후 **씬 저장·커밋**.

---

## 1. 프로젝트 개요

- **게임**: 「별을 향해」 — 2D 빛 반사 퍼즐 플랫포머
- **엔진**: **Unity 6.3 (정확히 `6000.3.19f1`)** · 2D URP
- **코어 루프**: 광원(랜즈)에서 빛 발사 → 플레이어가 거울/프리즘 조작 → 게이트 수광부에 광량 Σ≥1.0 충족 → 다음 스테이지
- **데모 범위**: Stage 1~4
- **프로젝트 루트**: `D:\STAR\STAR\` (git 저장소 = 이 폴더)
- **기획 원본**: `D:\게임 개발\` (Claude Chat 기획서 7종 + `CLAUDE.md` 규약). **게임 코드는 이 폴더를 읽지 않음** — 참고용.

---

## 2. 다른 컴퓨터에서 여는 법

### 준비물
1. **Unity Hub** + **Unity 6.3 (6000.3.19f1)** 설치 (같은 버전 권장)
2. 이 프로젝트 폴더 (아래 "전송 방법" 참고)

### 여는 순서
1. Unity Hub → **Add → 프로젝트 폴더(`STAR`) 선택**
2. 처음 열면 Unity가 **패키지 자동 복원 + Library 재생성**(몇 분 소요). `Packages/manifest.json`에 Newtonsoft.Json 등 다 기록돼 있어 자동으로 받아짐.
3. 열리면 `Assets/Scenes/SampleScene` 을 연다.

### 전송 방법 — GitHub 연결 완료 ✅
- **원격 저장소**: `https://github.com/gravity-888/STAR.git` (Private, 브랜치 `main`)
- **다른 PC에서 처음 받기**: `git clone https://github.com/gravity-888/STAR.git STAR`
- **일상 동기화**: 작업 시작 전 `git pull`, 마친 뒤 `git add -A && git commit -m "..." && git push`
- (대안) 오프라인 번들: `git bundle create star.bundle --all`, 또는 폴더 복사(`Library/ Temp/ Logs/` 제외).

> ⚠️ Git 소유권 경고가 뜨면: `git config --global --add safe.directory <프로젝트경로>`

---

## 3. 아키텍처 (핵심)

**설계 원칙**: 데이터 주도(맵=JSON) + 오브젝트별 독립 스크립트 + `IBeamHit` 인터페이스로 빛 상호작용 위임.

```
Assets/Scripts/
  Data/MapData.cs        맵 JSON → C# 모델 (Newtonsoft). wall* 키 일반 수집, 사다리=col/y_span. wall_transmit=벽이지만 빛 통과 셀(플레이어는 막음)
  Level/
    GridMap.cs           그리드↔월드 좌표 + 화살표(↙↑←→) 방향 해석
    MapLoader.cs         ★맵 스크립트: Maps의 TextAsset 읽어 오브젝트 배치·변수 주입. stageOrder로 스테이지 진행(GoToNext/GoToPrev)
                         + 편의 키(R=Restart, 1~4=GoToIndex) 입력도 여기서 처리
    ScreenFader.cs       스테이지 전환 페이드 인/아웃(자체 ScreenSpaceOverlay Canvas+Image, DontDestroyOnLoad)
    GateExit.cs          게이트 개폐부 통과 트리거 — 개방 상태에서 플레이어 진입 시 MapLoader.GoToNext() 호출
  Light/
    Beam.cs              Beam 구조체 + IBeamHit 인터페이스
    BeamTracer.cs        빛 추적(스택 기반, 프리즘 분기 지원) + LineRenderer 렌더. 각 오브젝트에 위임만.
                         ★매 프레임(LateUpdate) 재추적 → 플레이어 등 움직이는 차폐물 실시간 반영. 버퍼 재사용.
  Objects/               ★각 오브젝트가 자기 연산 담당
    LightSource.cs       랜즈/광원 — 빛 발사. Emitting=false면 BeamTracer가 건너뜀(랜즈 미장착)
    TorchMount.cs        횃불 랜즈 장착부 — 장착=발사ON+랜즈시각 / 해제=OFF+숨김. F키 대상(랜즈 아이템화 스테이지만)
    LensItem.cs          바닥에 떨어진 랜즈(줍기 대상) — 트리거 마커. 획득 처리는 LensInteractor
    Mirror.cs            거울 — 반사 (IBeamHit). Rotate(steps)로 22.5° 회전(Phase 4용 준비됨)
    Prism.cs             프리즘 — 분기 0.5+0.5 (IBeamHit). 출력방향은 맵 out에서 주입
    GateDetector.cs      게이트 수광부 — 흡수·광량누적·개방 (IBeamHit). BeginFrame/Commit 엣지 트리거(OnOpen 1회, OnStateChanged 양방향)
    GateDoor.cs          게이트 개폐부(문) — gate_open_zone 셀. 닫힘=솔리드 콜라이더로 차단 / 열림=콜라이더 off 통과. 수광부 OnStateChanged 구독
    Ladder.cs            사다리(Stage4) — 등반용 트리거. 광학 상호작용 없음(빛 통과)
  Player/
    PlayerController.cs  이동/점프(가변)/사다리 등반 + 낙사 리스폰. 신 Input System
    MirrorInteractor.cs  Q/E로 가까운 비고정 거울 회전(하이라이트)
    LensInteractor.cs    F키 문맥감응 — 바닥 랜즈 줍기 / 횃불 근처 장착·해제. 소지 시 머리 위 표시
```

**빛 흐름**: `LightSource.Emit()` → `BeamTracer`가 Raycast → 맞은 오브젝트의 `IBeamHit.Interact()` 호출 → 이어질 빔을 스택에 push → 반복. 새 광학 오브젝트를 추가해도 `IBeamHit`만 구현하면 tracer 수정 불필요.

**규약(어기지 말 것)**: 각도 `0°=동쪽, CW`, `rotation_z = -angle_deg`. 1셀=1유닛=40px. 그리드 y=Unity y(위로 증가, 반전 없음).

---

## 4. 맵 파일 시스템

- 맵 데이터 = `Assets/Maps/` 의 **TextAsset(.json)**. `MapLoader`의 **Map File 슬롯**에 드래그해서 읽음.
- **`stages_unified.json`** — 초기 맵 (stage4 발판 미완).
- **`stages_cord.json`** — ★최신 맵 (SVG 재추출, stage4 발판·지형·벽·사다리 완비).
- 맵 수정 = 이 JSON 편집 → 저장 → Unity 포커스(재임포트) → MapLoader 우클릭 **Build**.
- 파일 이름은 자유(스키마만 같으면 됨). Stage Key는 파일 안 stages의 키(`stage1`~`stage4`)와 일치해야 함.
- **신규 스키마 필드(2026-07-30)**:
  - `terrain_type`: `{ "x,y": "grass|dirt|indoor" }` 칸별 지형 타입 오버라이드. `terrain_type_default`(없으면 `"dirt"`). 미지정 칸=기본. (지형 위치 자체는 여전히 `terrain` = col→높이)
  - `source.has_lens`: 시작 시 랜즈 장착 여부(기본 `true`). `false`면 시작 시 빔 없음.
  - `lens_item: [x,y]`: 바닥에 떨어진 시작 랜즈 위치(획득 대상). 있으면 그 스테이지에서 F키 장착/해제 활성.
  - `mirror.pushable`(거울별, 기본 `false`): `true`면 **미는 거울** — 플레이어가 좌우로 밀어 위치를 옮긴다(회전 불가·각도 맵 고정). 미는 동안 부드럽게, 멈추면 칸에 스냅. 회전/랜덤/정답정렬(P) 대상 아님. 위치는 진행상태에 저장돼 다른 맵 갔다 와도 이어짐. 색 폴백 민트, 프리팹 슬롯 `mirrorPushPrefab`. 에디터 거울 속성 "미는 거울" 체크로 배치.
  - `gate.open_dir`(화살표 `↑↓←→`, 기본 `↑`): 게이트 문이 열릴 때 미끄러지는 방향. 이동거리는 개폐존 크기에서 자동(세로=높이·가로=폭). 문은 열릴 때 정렬순서가 내려가 **다른 아트 뒤로 가려짐**(`GateDoor.openSortingOffset`, 기본 −20).
  - `background`(스테이지별, 정수): 배경 아트 인덱스 → `MapLoader.stageBackgrounds[인덱스]`. 없으면 **stageOrder 상 그 스테이지의 순번**을 사용(스테이지가 늘면 배경 배열도 그만큼 늘려 채움). 슬롯이 비거나 범위 밖이면 배경 없음.
  - `stage_order`(최상위, `stages`와 동렬): 진행 순서·개수 배열(예: `["stage1","stage2","stage3"]`). 있으면 **MapLoader.stageOrder를 덮어씀** → 스테이지 개수를 맵이 결정(엔딩·전환·디버그키 자동 연동, 인스펙터 수정 불필요). 없으면 인스펙터 값 폴백. `ApplyStageOrderFromMap()`이 부팅 시(StartGame 전)와 Build에서 반영.
  - 현재 **stage4만** `has_lens:false` + `lens_item:[8,1]`. 1~3은 필드 없음(= 기존 동작).
- **맵 에디터(웹툴)**: [`tools/map-editor.html`](tools/map-editor.html) — 격자에 지형(+타입칠 잔디/땅/실내)·벽·발판·사다리·광원(횃불, `has_lens` 토글·방향)·**시작 랜즈(바닥)**·거울(각도·고정)·프리즘·게이트·개폐존·스폰 등을 배치하고 **이 스키마 그대로 JSON 출력**(내보내기) + 기존 맵 **불러오기**. 위 신규 필드 전부 **라운드트립 지원**. 브라우저에서 파일을 열어 사용(설치 불필요). 좌표 y=위로 증가, 거울 각도 22.5° 스냅. (P1 MVP — 빛 경로 미리보기·undo 등은 미구현)

---

## 5. 씬 세팅 (SampleScene)

씬에 **빈 GameObject + `MapLoader` 컴포넌트**가 있어야 함 (이 세팅은 커밋됨 → 다른 PC에서 그대로 존재):
- **Map File**: `Assets/Maps/stages_cord`(또는 stages_unified) 드래그
- **Stage Key**: `stage2`(반사 데모) / `stage3`(프리즘) / `stage4`(사다리)
- **Build**: 컴포넌트 우클릭 → `Build` (또는 ▶ Play). 우클릭 → `Clear`로 정리.

> 만약 다른 PC에서 MapLoader의 Map File이 비어 보이면 다시 드래그. `[MapLoader] mapFile이 비어있음` 로그가 그 신호.

---

## 6. 현재 상태

| 항목 | 상태 |
|---|---|
| 맵 로딩·배치 | ✅ 4스테이지 좌표대로 배치 |
| 빛 반사(거울) | ✅ Stage2 광원→거울4개→게이트 도달 확인 |
| 거울 회전(Phase 4) | ✅ 플레이어가 가까운 거울 선택(흰색 하이라이트) → Q/E 22.5° 회전 → 빛 즉시 재추적 |
| 거울 시작각 랜덤 | ✅ 맵 생성 시 회전 가능한 거울을 정답에서 **±45°(22.5°×2) 랜덤**하게 틀어 시작(`randomizeMirrors`/`mirrorRandomSteps`). 22.5° 배수라 Q/E로 정답 도달 가능, 고정 거울 제외. 랜덤이 우회 경로로 **게이트를 열어버리면 닫힌 배치가 나올 때까지 재추첨**(최대 20회) → 항상 안 풀린 상태로 시작 |
| 정답 정렬 키 | ✅ `P`=회전 가능한 거울을 전부 정답 각도로 스냅(테스트/구제). `MapLoader.autoSolveKey`로 토글 — **데모 빌드 시 끌 것** |
| 프리즘 분기 | ✅ 구현(0.5+0.5). **Stage3 플레이 검증 완료** — 두 갈래 합산으로만 개방 |
| 게이트 개방 | ✅ **충전식**: Σ≥1.0을 `chargeTime`(기본 1.2s) 유지하면 **개폐부(문) 열림**. 래치 전엔 빛 끊기면 충전 감소→닫힘, **완전 개방 시 래치**(`latchOnOpen`)되어 이후 빛이 가려져도 **열린 채 유지**(통과 중 플레이어가 빔 막아도 안 닫힘). 수광부는 **단일 아트(색 변경 없음)**, 진행은 **임시 충전 게이지**로 표시 — 나중에 수광부 내부 채움 효과로 교체 예정 |
| 스테이지 진행 | ✅ **양방향**: 게이트 통과→다음(GoToNext), 입장 통로 되돌아가면→이전(GoToPrev, exit_spawn 등장). GateExit 트리거(dir±1). **스폰이 트리거에 겹치면 무장 해제**로 전환 오실레이션 방지 |
| 진행상태 저장 | ✅ **다른 맵 갔다 와도 이어짐**: 스테이지 떠날 때 `CaptureProgress`(거울 각도·게이트 개방·랜즈 장착)를 `_progress[stageKey]`에 저장, 재진입 시 `_restore`로 복원(방향 무관). 미방문 스테이지는 정방향=랜덤·역방향=정답(폴백). `R`(리셋)=그 스테이지 진행 폐기 후 새 랜덤, **새 게임(StartGame)=전체 초기화**. 저장은 메모리(플레이 세션 한정, 파일 저장 아님) |
| 광원 화면 밖 | ✅ stage1~3 광원을 광선축(↙) 따라 우상단으로 밀어 화면 밖(x>gridW-0.5). 퍼즐 불변. 벽 그레이즈는 wall_transmit로 예외(stage1 [25,12]; stage3는 우측벽을 x=44로 밀어 그레이즈 소멸→예외 불필요). stage4는 제외 |
| 전환 중 조작잠금 | ✅ 전환 연출(페이드+빌드+페이드) 동안 `PlayerController.ControlsLocked`=true → 이동/점프/등반·거울조작(Q/E) 정지 + 플레이어 완전 정지(속도·중력 0). MapLoader.Transition이 토글 |
| 사다리 | ✅ Stage4 배치(5줄기) + **등반 로직 구현(Phase 5)** |
| 시각 | ⚠️ 아직 **색깔 사각형**(아트 미제작). 단 **프리팹 seam 구축됨**([`MapLoader`](Assets/Scripts/Level/MapLoader.cs) 오브젝트별 프리팹 슬롯 — 비면 색사각형 폴백). 아트는 슬롯만 채우면 됨 |
| 플레이어 | ✅ **Phase 5: 이동/점프/사다리 등반**(스폰 지점 자동 생성). 신 Input System. |
| 지형/발판 콜라이더 | ✅ **솔리드 콜라이더 부여**(플레이어가 밟고 섬). 이전엔 시각 전용이었음. |
| 카메라 | ✅ **플레이어 추적**(`CameraFollow`, SmoothDamp + 레벨 경계 클램프). `MapLoader.followPlayer`/`cameraViewCells`로 제어. |
| 리스타트 / 스테이지 점프 | ✅ `R`=현재 스테이지 리셋(거울 각도 재추첨·플레이어 위치 초기화), `1~4`=해당 스테이지로 즉시 이동, `P`=정답 정렬. `MapLoader.restartKey`/`debugStageKeys`/`autoSolveKey`로 토글 — **데모 빌드 시 `debugStageKeys`·`autoSolveKey` 끌 것** |
| 낙사 리스폰 | ⚠️ 구현됨(레벨 경계 밖 → 스폰 복귀, 거울 각도는 유지). 단 **현재 맵이 사방으로 막혀 있어 도달 불가 = 미검증 보험**. 경계 여유는 `PlayerController.boundsMargin`(기본 4칸) |
| 발판 빛 차단 | ✅ 발판별 `transmit` 플래그를 실제 반영. `false`면 빛 차단(진한 남색). stage3 프리즘 아래 발판이 이 케이스 |
| 게임 플로우 / UI | ✅ **타이틀→플레이→엔딩** 상태머신([`GameManager`](Assets/Scripts/Level/GameManager.cs), 코드 생성 오버레이·씬세팅 불필요). stage4 클리어→엔딩화면→아무 키→타이틀. **일시정지**(ESC): 계속`ESC`/재시작`R`/타이틀`T`. `MapLoader.useGameFlow`(기본 켜짐)·`showTitleOnBoot` 토글. 스테이지 HUD·진행저장은 **사용자 결정으로 제외** |
| 지형 타입 3종 | ✅ 잔디/땅/실내 프리팹 슬롯 + 색 폴백. 맵 `terrain_type`(칸별)·`terrain_type_default`로 지정. 미지정=땅. 타일 규칙 동일(1칸 정합) |
| 랜즈 아이템(stage4) | ✅ 랜즈 획득·장착 시스템. `has_lens`/`lens_item`으로 데이터 구동. **F**=줍기/장착/해제. 미장착 시 빔 없음(`LightSource.Emitting`). 코드 검증(빌드)만 완료 — **플레이 실측은 사용자 확인 대기** |
| 오디오 seam | ✅ [`AudioManager`](Assets/Scripts/Level/AudioManager.cs) 구축 — 정적 훅(SFX 8종 + BGM 3종). **씬에 컴포넌트 두고 클립 채우면 재생, 비면 무음**. 클립은 미할당(사용자가 채움) |
| 스테이지 배경 | ✅ `stageBackgrounds` 프리팹 배열(스테이지 수만큼) + 맵 `background` 인덱스. **시차 스크롤**([`ParallaxBackground`](Assets/Scripts/Level/ParallaxBackground.cs), `backgroundParallax` 기본 0.5, 프리팹 레이어별 factor 가능). 맨 뒤(−100) |

**조작**: 좌우 `A/D`·`←/→` · 점프 `Space`(접지 시, **가변 높이**: 짧게 탭≈0.7칸 / 끝까지 누르면 최대 3.5칸) · 사다리 등반 `W/S`·`↑/↓` · **거울 회전 `Q`(반시계)/`E`(시계)** — 반경 2.5칸 안 가장 가까운 비고정 거울(흰색 하이라이트) 22.5°씩 · **랜즈 줍기/장착·해제 `F`**(stage4 — 바닥 랜즈에 닿아 F로 줍고, 횃불 근처 F로 장착/해제) · **리셋 `R`** · **정답 정렬 `P`**(디버그) · **스테이지 점프 `1~4`**(디버그).

**색상 범례**: 노랑=광원, 초록=게이트, 하늘=거울(회색=고정), 자홍=프리즘, 반투명 파랑=발판(빛 투과), **진한 남색=발판(빛 차단)**, 갈색=지형/사다리, 빨강=오답, 흰=스폰, **주황=플레이어**.

---

## 7. 알려진 이슈 / 갭

- ~~**Stage4 M12→게이트 반사각**~~ **해결(2026-07-20)**: 계산 검증 완료. `Mirror.Reflect`는 `angle_deg`를 **법선**으로 취급(`n=(cosθ,−sinθ)`, `r=d−2(d·n)n`)하며, M12(θ=247.5°, 입사 `→`)의 출력은 `↗`로 맵 데이터와 일치. 거울 12개 전부 같은 규약으로 자기일관적이고, `source(22,2)`부터 M1~M12를 거쳐 `게이트(41,41)`까지 경로가 정확히 맞아떨어짐.
- **stage3 프리즘 우회 해법 차단(2026-07-20)**: 게이트`(38,1)`가 프리즘`(38,10)` 바로 아래 같은 열이라, 프리즘 아래 발판이 빛을 투과하면 **프리즘을 안 거치고 x=38 열로 직행**해 1.0으로 개방되는 우회 해법이 성립했음. 해당 발판`(36~40, 9)`을 `transmit:false`로 변경해 차단. 정답 2갈래는 y=9를 x=11·13·20·21·29·30에서만 지나므로 영향 없음.
  - 근본 원인은 맵 데이터가 아니라 **`MapLoader.BuildPlatforms`가 `transmit` 플래그를 무시하고 무조건 `BeamTransparent`를 붙이던 것**. 이제 플래그를 반영하므로 다른 스테이지도 발판별 빛 차단을 데이터로 지정 가능.
  - ⚠️ **설계 주의**: 게이트와 프리즘/광원이 같은 행·열에 놓이면 비슷한 직행 우회가 생길 수 있음. 새 스테이지 설계 시 확인할 것.
- **점프 = 가변 높이 + 빠른 하강 + 코요테 타임**: `jumpHeightCells=3.5`(최대) + `jumpCutMultiplier=0.45`(상승 감쇠) + `fallGravityMultiplier=1.8`(하강 중 v.y<0일 때만 중력 가중 → 낙하 빠름; 상승·최대높이엔 영향 없어 정점 3.5칸 유지). `coyoteTime=0.07s`: 발판 이탈 직후 잠깐 공중 점프 허용(더블점프 아님). 좌우 동시입력은 나중에 누른 방향 우선(SOCD last-wins). `moveSpeed 6 / climbSpeed 5`는 임시값.
- **벽 끼임 방지 처리됨**: 플레이어 콜라이더에 마찰 0 머티리얼 + `edgeRadius 0.03` → 벽면·타일 이음새에 안 걸림. 그래도 심하면 지형/발판을 CompositeCollider2D로 병합 고려.
- **발판 윗면 = 지형과 같은 높이(2026-07-23)**: 발판(두께 0.4)의 **윗면을 칸 위 모서리(`y+0.5`)에 맞춤** — 중심이 칸에서 0.3칸 위(`PLATFORM_CY`). 이전엔 발판 중심이 칸 중심이라 윗면이 `y+0.2`여서 **같은 행이어도 지형보다 0.3칸 낮게** 섰다. 이제 지형↔발판 서는 높이가 동일.
  - 광학 영향 없음: 빛을 막는 발판은 stage3 프리즘 아래(36~40, 9) **1개뿐**이고, 그 우회 빔은 세로 낙하라 올린 뒤에도 그대로 차단된다. 나머지는 전부 `transmit:true`(BeamTransparent)라 위치와 무관하게 통과.
  - 부수 효과: 발판 아래 통과 높이가 0.3칸 **늘어남**(더 여유), 지형→발판 점프는 0.3칸 **쉬워짐**. 발판끼리 상대 간격은 불변.
- **발판 빛 투과 = 맵 데이터로 제어**: 맵의 발판별 `transmit` 값을 `MapLoader`가 반영한다. `true`(기본)면 `BeamTransparent` 마커 부착 → `BeamTracer`가 히트를 건너뛰고 관통(반투명 파랑). `false`면 마커 없이 빛 차단(진한 남색). 어느 쪽이든 콜라이더는 솔리드라 밟고 설 수 있음. 벽은 마커가 없어 항상 차단.
- **플레이어가 빛을 막음 = 의도(확정)**: 플레이어 몸은 빛을 차단(그림자 역할). `BeamTracer`가 **매 프레임 재추적**하므로 플레이어가 움직이면 빔 경로가 실시간으로 갱신됨(이전엔 거울 변화 때만 갱신돼 스테일 문제 있었음 — 해결).
- **카메라 팔로우 처리됨**: `CameraFollow`가 플레이어를 부드럽게 추적(세로 `cameraViewCells=16`칸 기본) + 레벨 밖은 클램프. 전체 프레이밍이 필요하면 `MapLoader.followPlayer=false`(폴백 `FrameCamera`).
  - **스테이지별 오버라이드**: 맵 JSON 스테이지에 `"camera": { "view_cells", "fit_width", "top_pad", "bottom_pad", "side_pad" }`(모두 선택). `fit_width:true`면 세로 대신 **가로 폭에 맞춰** 줌 → 좌우 벽이 화면 끝(화면비 무관). 현재 stage3=`top_pad:3`(상단 여유), stage4=`fit_width:true`(좌우 벽=화면 끝). 값 조절은 JSON에서.
- **UI·프리팹 seam 완료**. **아트는 사용자 제작 중**(규격 `Assets/Art/PREFAB_SPEC.md`, 슬롯 비면 색 사각형 폴백). **오디오 미착수**(별도 단계).
- IDE(VS/Rider)에 스크립트를 열어둔 채 두면 외부 수정을 덮어쓸 수 있음 — 코드 작업 중엔 닫아두기.

---

## 8. 데모 완성 로드맵 (확정 · 2026-07-20)

> **새 작업 세션은 이 순서대로 진행할 것.** 순서가 임의가 아니라, "같은 작업을 두 번 하지 않기" 위해
> **교체 지점(프리팹)과 기준값(플레이어 메트릭)을 그걸 쓰는 작업보다 앞에** 배치한 것이다.
> 순서를 바꾸려면 아래 "왜 이 순서인가"를 먼저 읽을 것.

| # | 단계 | 완료 조건 |
|---|---|---|
| 1 ✅ | **엔딩 + 최소 UI** | ~~stage4 클리어 시 순환 대신 엔딩 처리·타이틀·일시정지~~ **완료(2026-07-22)**: [`GameManager`](Assets/Scripts/Level/GameManager.cs)가 타이틀→플레이→엔딩→타이틀 관리. [`MapLoader.GoToNext`](Assets/Scripts/Level/MapLoader.cs)는 마지막 스테이지에서 `OnGameComplete`(엔딩) 호출. 스테이지 HUD·진행저장은 사용자 결정으로 제외 |
| 2 ✅ | **프리팹 seam 구축** | **완료(2026-07-22, 이후 확장)**: [`MapLoader`](Assets/Scripts/Level/MapLoader.cs)에 오브젝트별 프리팹 슬롯 16종(지형·벽·투과벽·발판×2·사다리·랜즈·횃불·거울·고정거울·프리즘·게이트·문·디코이·스폰·플레이어). 프리팹은 각 오브젝트의 `"visual"` 자식만 대체(콜라이더·로직은 루트 유지). **비면 색 사각형으로 폴백** — 3·7번은 슬롯만 채우면 코드 변경 0. seam 계약은 아래 §10 참고 |
| 3 🔄 | **임시 아트 + 임시 오디오** | **진행 중**: 규격서 [`Assets/Art/PREFAB_SPEC.md`](Assets/Art/PREFAB_SPEC.md) + 폴더 골격(`Assets/Art/Sprites`·`Prefabs`) 준비 완료. **아트 제작은 사용자 직접**(2026-07-22 결정) — 규격대로 프리팹 만들어 슬롯에 드래그하면 코드 변경 0. **오디오는 이 단계에서 제외**(코드 seam 없음 → 별도 단계로 미룸). 최종 아트의 크기·애니 스펙은 그대로 두고 그림 퀄만 낮춘 버전 |
| 4 🔄 | **실기 빌드 1회 확인** | **코드 준비 완료(2026-08-03)**: `fit_width` 줌을 화면비 변화마다 재계산하도록 하드닝([`CameraFollow`](Assets/Scripts/Level/CameraFollow.cs)). **실기 빌드 프레이밍 검증은 사용자 수동으로 미완**(여러 해상도 + 창 리사이즈) |
| 5 ✅ | **플레이어 메트릭 확정** | **완료(2026-08-03)**: `moveSpeed 7`·`climbSpeed 6`·`jumpHeightCells 3.8` 코드 확정([`PlayerController`](Assets/Scripts/Player/PlayerController.cs)). 도달성 안전(더 쉬워짐) — 지름길 여부만 6번에서 점검 |
| 6 | **밸런싱** | 거울 경로·맵 길이·구간 추가. 맵 검증기 있으면 유리 |
| 7 | **최종 아트/오디오 교체** | 프리팹 내용만 교체 — **코드 변경 0이어야 정상** |

**왜 이 순서인가**
- **2번이 3번 앞인 이유**: 현재 모든 시각 요소는 [`MapLoader.Visual()`](Assets/Scripts/Level/MapLoader.cs)이 1×1 흰 텍스처로 `SpriteRenderer`를 만들어 색만 칠하는 구조라, **스프라이트·애니메이터를 끼울 자리가 없다.** seam 없이 임시 아트를 넣으면 MapLoader 생성 코드를 뜯어야 하고 7번에서 또 뜯게 된다.
- **5번이 6번 앞인 이유**: 6번의 "맵 길이·구간 추가"는 **플레이어가 1초에 몇 칸 가고 점프로 몇 칸 넘는지**가 기준이다. 기준 미확정 상태로 맵을 조정하면 나중에 이동값을 바꾸는 순간 밸런싱이 통째로 무효가 된다.
- **5번이 3번 뒤인 이유**: 캐릭터 크기가 체감 속도를 좌우한다. 3번에서 임시 아트를 **최종 크기로** 만들기 때문에, 5번에서 잡은 조작감이 7번 이후에도 그대로 유효하다.
- **4번을 앞당긴 이유**: 카메라 `fit_width`가 [`cam.aspect`로 나눈다](Assets/Scripts/Level/MapLoader.cs). 에디터 Game 뷰 비율과 빌드 해상도가 다르면 stage4 프레이밍이 달라지는데, 아트가 들어간 뒤 발견하면 원인 분리가 번거롭다.

**결정됨 (2026-07-22 · 1번 진행 시 사용자 확정)**
- **진행 저장/이어하기**: 넣지 않음(4스테이지 데모 규모).
- **UI 범위**: 일시정지 메뉴 + 엔딩→타이틀 복귀. **스테이지 표시(HUD)는 제외.**
- 타이틀: "엔딩→타이틀 복귀"가 성립하도록 최소 타이틀 화면을 구현. 부팅 시 타이틀 표시 여부는 [`MapLoader.showTitleOnBoot`](Assets/Scripts/Level/MapLoader.cs)(기본 켜짐)로 토글 — 끄면 바로 stage1로 부팅.

**선택 사항**
- **맵 검증기**(6번 진입 시): 좌표 배열 손편집으로 정답 경로가 조용히 깨지는 걸 잡는다. 각 스테이지를 훑어 게이트 Σ를 리포트. 단 *의도하지 않은* 별해(§7 stage3 사례)까지는 못 잡고, 오타로 깨진 것만 잡는다.

> ✅ **통합 플레이테스트 완료(2026-07-20)**: Stage1~4 전 구간 — 프리즘 합산 개방, 거울 회전(Q/E), 양방향 스테이지 전환, 사다리 등반, R키 리셋, 스테이지 점프 전부 정상 동작 확인.
> ✅ **Phase 6 완료**: 스테이지 양방향 전환 + 게이트 개폐부(문).
> ✅ **Phase 5 완료**: 플레이어 이동/점프(가변)/사다리 등반 + 지형·발판 솔리드 콜라이더. 신 Input System.
> ✅ **Phase 4 완료**: 거울 회전(Q/E) — 가까운 비고정 거울 선택·하이라이트 후 22.5° 회전 + 빛 재추적(`MirrorInteractor`).

---

## 9. 커밋 히스토리 (브랜치 `main`, 원격: github.com/gravity-888/STAR)

전체 이력은 `git log --oneline` 으로 확인할 것 (이 문서에 손으로 옮겨두면 금방 낡음).

주요 이정표: `855b8f9` Phase 0 초기 셋업 → `00f0e2b` Phase 1 레벨 로더 → `ed4f844` Phase 2 빛 반사 코어 → `cda6821` IBeamHit 아키텍처 리팩터링 → `4dac8c8` Phase 5 플레이어 → `f3b5faa` Phase 4 거울 회전 → `b5e85f4` 카메라 추적 → `9d8cdd2` Phase 6 스테이지 전환.

---

## 10. 프리팹 seam 계약 (로드맵 2번 산출물 · 3·7번에서 사용)

[`MapLoader`](Assets/Scripts/Level/MapLoader.cs) 인스펙터의 **프리팹 슬롯**에 시각 프리팹을 넣으면 해당 오브젝트의 색 사각형 대신 그 프리팹이 배치된다. **슬롯이 비면 지금처럼 색 사각형**(동작 완전 동일).

**프리팹 작성 규칙 (임시·최종 아트 공통)**
- **시각 전용**으로 만들 것 — `SpriteRenderer`(+선택 `Animator`)만. **콜라이더·물리·게임 로직 컴포넌트를 넣지 말 것**(충돌/광학은 코드가 루트에 생성하는 실제 오브젝트가 담당). 프리팹은 각 오브젝트의 `"visual"` 자식으로만 인스턴스화된다.
- **크기 = 최종 크기(1셀 = 1유닛)로 저작**. 코드가 임의 스케일을 강제하지 않는다(플레이스홀더의 얇은 막대/작은 사각형 스케일은 프리팹에 적용 안 됨). 예: 플레이어 ≈ 0.6×0.9, 지형/벽 ≈ 1×1, 발판 ≈ 1×0.4.
- **회전**: 거울만 코드가 `-angle`로 프리팹을 회전시킨다(반사면 정렬). 그 외(프리즘 45° 등)는 플레이스홀더 전용이라 프리팹은 정립 상태로 두면 된다.
- **사다리**: 높이가 데이터 종속 → 프리팹에 세로 스케일 `h`가 곱해진다. 세로 타일/9-slice로 늘어나도 자연스럽게 저작할 것.
- **정렬순서(sortingOrder)**: 코드가 `기준 order + 프리팹 내부 상대순서`로 세팅한다. 프리팹 내부에서 여러 SpriteRenderer의 상대 순서(0,1,2…)만 맞춰두면 그룹 간 레이어는 코드가 처리.
- **게이트/문 색 피드백**: 게이트 수광부·개폐부는 열림/닫힘 시 `visual`의 **첫 SpriteRenderer 색을 코드가 바꾼다**(초록/반투명). 색으로 상태를 보이려면 루트에 대표 SpriteRenderer를 두거나, 그 방식이 싫으면 애니메이터/자식 스크립트로 대체(그 경우 색 틴트는 무시됨 — null-safe).
- **랜즈 방향 점**: `lensPrefab`을 넣으면 플레이스홀더 방향 표시 점은 자동 생략(프리팹이 방향을 표현한다고 가정).

**슬롯 목록(23종)**: `terrainPrefab`(공통 폴백) · `terrainGrassPrefab`(잔디) · `terrainDirtPrefab`(땅) · `terrainIndoorPrefab`(실내) · `wallPrefab` · `wallGlassPrefab`(빛 통과 벽) · `platformPrefab`(투과 발판) · `platformSolidPrefab`(차단 발판) · `ladderPrefab` · `lensPrefab`(랜즈 — 광원 시각 + 아이템/소지 시각 겸용) · `torchPrefab`(랜즈 거치 횃불) · `mirrorPrefab`(회전 반사면) · `mirrorFixedPrefab`(고정 거울) · `mirrorPushPrefab`(미는 거울 — 좌우 슬라이드) · `mirrorPushMountPrefab`(미는 거울 전용 받침대 — 거울과 함께 미끄러짐) · `mirrorMountPrefab`(바닥 거치대) · `mirrorMountCeilingPrefab`(천장 거치대 10×80) · `prismPrefab` · `gatePrefab`(수광부, 1칸 fitToScale) · `gateDoorPrefab`(문) · `decoyPrefab` · `spawnPrefab` · `playerPrefab`. (랜즈 아이템·소지 시각은 별도 슬롯 없이 `lensPrefab` 재사용) + **`stageBackgrounds`(배경 프리팹 배열 — 스테이지 수만큼 채움, 맨 뒤 정렬 `Z_BACKGROUND=-100`)**.

**구조 메모**: 랜즈=`lensPrefab`+`torchPrefab`(랜즈는 위치 중심, 횃불은 바닥에 세움·회전X). 거울=`mirrorPrefab` 하나(코드가 회전; 아트 기본각 보정 `mirrorArtAngleOffset`=90, 시작각 ±45° 랜덤). **게이트 문**은 개폐존 전체를 덮는 **긴 블럭 1개**(열리면 위로 슬라이드). 수광부(`gatePrefab`)는 문과 **별개 위치의 오브젝트**.

> ⚠️ 슬롯은 씬의 MapLoader 컴포넌트에 직렬화된다 → 프리팹을 드래그해 넣은 뒤 **씬을 저장·커밋**해야 다른 PC에도 반영된다.
