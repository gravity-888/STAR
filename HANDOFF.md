# 별을 향해 (Toward the Stars) — 개발 인수인계

> 다른 컴퓨터에서 이어서 개발하기 위한 정리본. **이 파일부터 읽으면 됩니다.**
> 최종 갱신: 2026-07-09 (Phase 5: 플레이어)

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
  Data/MapData.cs        맵 JSON → C# 모델 (Newtonsoft). wall* 키 일반 수집, 사다리=col/y_span
  Level/
    GridMap.cs           그리드↔월드 좌표 + 화살표(↙↑←→) 방향 해석
    MapLoader.cs         ★맵 스크립트: Maps의 TextAsset 읽어 오브젝트 배치·변수 주입 (로직 없음)
  Light/
    Beam.cs              Beam 구조체 + IBeamHit 인터페이스
    BeamTracer.cs        빛 추적(스택 기반, 프리즘 분기 지원) + LineRenderer 렌더. 각 오브젝트에 위임만.
                         ★매 프레임(LateUpdate) 재추적 → 플레이어 등 움직이는 차폐물 실시간 반영. 버퍼 재사용.
  Objects/               ★각 오브젝트가 자기 연산 담당
    LightSource.cs       랜즈/광원 — 빛 발사
    Mirror.cs            거울 — 반사 (IBeamHit). Rotate(steps)로 22.5° 회전(Phase 4용 준비됨)
    Prism.cs             프리즘 — 분기 0.5+0.5 (IBeamHit). 출력방향은 맵 out에서 주입
    GateDetector.cs      게이트 수광부 — 흡수·광량누적·개방 (IBeamHit). BeginFrame/Commit 엣지 트리거(OnOpen 1회)
    Ladder.cs            사다리(Stage4) — 등반용 트리거. 광학 상호작용 없음(빛 통과)
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
| 프리즘 분기 | ✅ 구현(0.5+0.5), Stage3 검증은 플레이로 확인 필요 |
| 게이트 개방 | ✅ Σ≥1.0 시 초록으로 변함 |
| 사다리 | ✅ Stage4 배치(5줄기) + **등반 로직 구현(Phase 5)** |
| 시각 | ⚠️ **전부 색깔 사각형 플레이스홀더**(프리팹·스프라이트 미제작) |
| 플레이어 | ✅ **Phase 5: 이동/점프/사다리 등반**(스폰 지점 자동 생성). 신 Input System. |
| 지형/발판 콜라이더 | ✅ **솔리드 콜라이더 부여**(플레이어가 밟고 섬). 이전엔 시각 전용이었음. |

**조작**: 좌우 `A/D`·`←/→` · 점프 `Space`(접지 시, **가변 높이**: 짧게 탭≈0.7칸 / 끝까지 누르면 최대 3.5칸) · 사다리 등반 `W/S`·`↑/↓` · **거울 회전 `Q`(반시계)/`E`(시계)** — 반경 2.5칸 안 가장 가까운 비고정 거울(흰색 하이라이트) 22.5°씩.

**색상 범례**: 노랑=광원, 초록=게이트, 하늘=거울(회색=고정), 자홍=프리즘, 파랑=발판, 갈색=지형/사다리, 빨강=오답, 흰=스폰, **주황=플레이어**.

---

## 7. 알려진 이슈 / 갭

- **Stage4 M12→게이트 반사각**: 맵 `build_note`에 "물리 정합 재검증 필요" 표기. 빛이 게이트에 정확히 안 닿을 수 있음(맵 데이터 이슈).
- **점프 = 가변 높이 + 빠른 하강 + 코요테 타임**: `jumpHeightCells=3.5`(최대) + `jumpCutMultiplier=0.45`(상승 감쇠) + `fallGravityMultiplier=1.8`(하강 중 v.y<0일 때만 중력 가중 → 낙하 빠름; 상승·최대높이엔 영향 없어 정점 3.5칸 유지). `coyoteTime=0.07s`: 발판 이탈 직후 잠깐 공중 점프 허용(더블점프 아님). 좌우 동시입력은 나중에 누른 방향 우선(SOCD last-wins). `moveSpeed 6 / climbSpeed 5`는 임시값.
- **벽 끼임 방지 처리됨**: 플레이어 콜라이더에 마찰 0 머티리얼 + `edgeRadius 0.03` → 벽면·타일 이음새에 안 걸림. 그래도 심하면 지형/발판을 CompositeCollider2D로 병합 고려.
- **발판 빛 투과 처리됨**: 발판 콜라이더에 `BeamTransparent` 마커 → `BeamTracer`가 히트를 건너뛰고 관통. 발판은 밟히되 빛은 통과(맵 `transmit:true`와 일치). 벽은 마커 없어 차단.
- **플레이어가 빛을 막음 = 의도(확정)**: 플레이어 몸은 빛을 차단(그림자 역할). `BeamTracer`가 **매 프레임 재추적**하므로 플레이어가 움직이면 빔 경로가 실시간으로 갱신됨(이전엔 거울 변화 때만 갱신돼 스테일 문제 있었음 — 해결).
- **카메라**: `FrameCamera`가 스테이지 전체를 담아 플레이어가 작게 보임. 추후 카메라 팔로우 추가 후보.
- **프리팹/아트/오디오/UI 미착수**: 플레이스홀더로 진행 중.
- IDE(VS/Rider)에 스크립트를 열어둔 채 두면 외부 수정을 덮어쓸 수 있음 — 코드 작업 중엔 닫아두기.

---

## 8. 다음 단계 (후보)

1. **Phase 4 검증(플레이)**: Stage2/4에서 Q/E로 거울 돌려 게이트가 열리는지 실제 플레이로 확인(코드는 완료).
2. **Phase 3 — 프리즘 광량 합산 검증**: Stage3에서 두 갈래 0.5+0.5=1.0으로 게이트 개방되는지 플레이로 확인.
3. **플레이어 튜닝/카메라 팔로우**: 이동·점프 파라미터 플레이테스트, 큰 스테이지용 카메라 추적.
4. **프리팹화**: 플레이스홀더 사각형 → 실제 스프라이트 프리팹.

> ✅ **Phase 5 완료**: 플레이어 이동/점프(가변)/사다리 등반 + 지형·발판 솔리드 콜라이더. 신 Input System.
> ✅ **Phase 4 완료**: 거울 회전(Q/E) — 가까운 비고정 거울 선택·하이라이트 후 22.5° 회전 + 빛 재추적(`MirrorInteractor`).

---

## 9. 커밋 히스토리 (브랜치 `main`, 원격: github.com/gravity-888/STAR)

```
31924ba 맵 스키마 확장: 사다리(col/y_span) + 일반화된 wall_* 처리
cda6821 아키텍처 리팩터링: 오브젝트 단위 설계 + IBeamHit 인터페이스
e5309dc Clear() 수정: 스테이지 전환 시 이전 레벨 잔존물 완전 제거
ed4f844 Phase 2: 빛 반사 코어 (Raycast 판정 + LineRenderer 연출)
00f0e2b Phase 1: 레벨 데이터 로더 + 좌표 배치 (플레이스홀더 시각화)
31a1bb6 Newtonsoft.Json 3.2.1 패키지 추가
855b8f9 Phase 0: 프로젝트 초기 셋업
```
