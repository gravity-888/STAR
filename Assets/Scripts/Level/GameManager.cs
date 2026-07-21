using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TowardTheStars.Player;

namespace TowardTheStars.Level
{
    // 게임 흐름(타이틀 → 플레이 → 일시정지 → 엔딩)을 관리하는 UI 상태머신.
    // ScreenFader처럼 코드로 ScreenSpaceOverlay Canvas + 패널을 자체 생성한다 —
    //   씬 세팅·프리팹·EventSystem이 전혀 필요 없고(키보드 구동), MapLoader가 Start에서 부트스트랩한다.
    // 조작: 타이틀/엔딩 = 아무 키 · 플레이 중 ESC = 일시정지 · 일시정지에서 ESC(계속)/R(재시작)/T(타이틀).
    public class GameManager : MonoBehaviour
    {
        enum State { Title, Playing, Paused, Ending }

        MapLoader _loader;
        State _state;
        float _cooldown;       // 상태 진입 직후 입력 무시(직전 키 눌림이 새 화면을 즉시 넘기는 것 방지)
        Coroutine _fadeCo;

        readonly List<GameObject> _panels = new();
        GameObject _titlePanel, _pausePanel, _endingPanel;
        Text _pulseText;       // "아무 키나…" 안내 — 부드럽게 명멸

        static Font _uiFont;

        // MapLoader.Start에서 호출: UI를 만들고 타이틀(또는 바로 시작) 상태로 진입.
        public static GameManager Bootstrap(MapLoader loader)
        {
            var go = new GameObject("GameUI");
            DontDestroyOnLoad(go);
            var gm = go.AddComponent<GameManager>();
            gm._loader = loader;
            loader.OnGameComplete = gm.HandleGameComplete;   // 마지막 스테이지 클리어 → 엔딩
            gm.BuildUI();
            if (loader.showTitleOnBoot) gm.EnterTitle();
            else gm.StartGameFromTitle();
            return gm;
        }

        void Update()
        {
            // 안내 문구 명멸(타이틀·엔딩). 일시정지로 timeScale=0이어도 도는 unscaled 시간 사용.
            if (_pulseText != null)
            {
                var c = _pulseText.color;
                c.a = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
                _pulseText.color = c;
            }

            if (_cooldown > 0f) { _cooldown -= Time.unscaledDeltaTime; return; }

            var kb = Keyboard.current;
            if (kb == null) return;

            switch (_state)
            {
                case State.Title:
                    if (kb.anyKey.wasPressedThisFrame) StartGameFromTitle();
                    break;
                case State.Playing:
                    if (kb.escapeKey.wasPressedThisFrame && !_loader.IsTransitioning) EnterPause();
                    break;
                case State.Paused:
                    if (kb.escapeKey.wasPressedThisFrame)      ResumeFromPause();
                    else if (kb.rKey.wasPressedThisFrame)      RestartFromPause();
                    else if (kb.tKey.wasPressedThisFrame)      QuitToTitle();
                    break;
                case State.Ending:
                    if (kb.anyKey.wasPressedThisFrame) EndingToTitle();
                    break;
            }
        }

        // ---------- 상태 전이 ----------

        void EnterTitle()
        {
            Time.timeScale = 1f;
            PlayerController.ControlsLocked = true;   // 레벨 없음(있어도 정지)
            _state = State.Title;
            _cooldown = 0.35f;
            _pulseText = _titleHint;
            Switch(_titlePanel);
        }

        void StartGameFromTitle()
        {
            Time.timeScale = 1f;
            _state = State.Playing;
            _pulseText = null;
            Switch(null);                              // 오버레이 제거 → 게임 화면
            PlayerController.ControlsLocked = false;
            _loader.StartGame();                       // stageOrder[0] 빌드
        }

        void EnterPause()
        {
            Time.timeScale = 0f;                       // 물리·연출 정지
            PlayerController.ControlsLocked = true;    // 게임플레이 입력 차단
            _state = State.Paused;
            _cooldown = 0.15f;
            _pulseText = null;
            Switch(_pausePanel);
        }

        void ResumeFromPause()
        {
            Time.timeScale = 1f;
            PlayerController.ControlsLocked = false;
            _state = State.Playing;
            Switch(null);
        }

        void RestartFromPause()
        {
            Time.timeScale = 1f;                       // 전환 페이드가 진행되려면 필수(0이면 멈춤)
            _state = State.Playing;
            Switch(null);
            _loader.Restart();                         // Transition이 ControlsLocked를 스스로 관리
        }

        void QuitToTitle()
        {
            Time.timeScale = 1f;
            _loader.Clear();                           // 현재 레벨 제거
            EnterTitle();
        }

        // MapLoader가 마지막 스테이지 클리어 시 호출(OnGameComplete).
        void HandleGameComplete()
        {
            PlayerController.ControlsLocked = true;
            Time.timeScale = 0f;                       // 뒤 레벨 정지(불투명 패널이 가림)
            _state = State.Ending;
            _cooldown = 0.4f;
            _pulseText = _endingHint;
            Switch(_endingPanel);
        }

        void EndingToTitle()
        {
            Time.timeScale = 1f;
            _loader.Clear();
            EnterTitle();
        }

        // ---------- UI 생성(코드) ----------

        Text _titleHint, _endingHint;

        void BuildUI()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 10;   // 게임 위, 전환 페이더(short.MaxValue) 아래
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 타이틀
            _titlePanel = MakePanel(canvas.transform, "TitlePanel", 1.0f);
            MakeText(_titlePanel.transform, "별을 향해", 96, new Color(1f, 0.96f, 0.85f), 150f, FontStyle.Bold);
            MakeText(_titlePanel.transform, "Toward the Stars", 40, new Color(0.75f, 0.82f, 1f), 70f, FontStyle.Normal);
            _titleHint = MakeText(_titlePanel.transform, "아무 키나 눌러 시작", 34, new Color(1f, 0.9f, 0.4f), -140f, FontStyle.Normal);

            // 일시정지(반투명 — 게임 화면이 비침)
            _pausePanel = MakePanel(canvas.transform, "PausePanel", 0.72f);
            MakeText(_pausePanel.transform, "일시정지", 72, new Color(1f, 0.97f, 0.9f), 150f, FontStyle.Bold);
            MakeText(_pausePanel.transform, "ESC — 계속하기", 40, Color.white, 20f, FontStyle.Normal);
            MakeText(_pausePanel.transform, "R — 스테이지 재시작", 40, Color.white, -40f, FontStyle.Normal);
            MakeText(_pausePanel.transform, "T — 타이틀로", 40, Color.white, -100f, FontStyle.Normal);

            // 엔딩
            _endingPanel = MakePanel(canvas.transform, "EndingPanel", 1.0f);
            MakeText(_endingPanel.transform, "축하합니다!", 84, new Color(1f, 0.9f, 0.45f), 150f, FontStyle.Bold);
            MakeText(_endingPanel.transform, "모든 스테이지를 클리어했습니다", 42, new Color(0.9f, 0.94f, 1f), 60f, FontStyle.Normal);
            _endingHint = MakeText(_endingPanel.transform, "아무 키나 눌러 타이틀로", 34, new Color(1f, 0.9f, 0.4f), -150f, FontStyle.Normal);

            foreach (var p in _panels) p.SetActive(false);
        }

        // 전체 화면 어두운 배경 패널(+CanvasGroup 페이드). 활성 시 하나만 보이게 Switch가 관리.
        GameObject MakePanel(Transform parent, string name, float bgAlpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.03f, 0.04f, 0.09f, bgAlpha);
            Stretch(img.rectTransform);
            go.AddComponent<CanvasGroup>();
            _panels.Add(go);
            return go;
        }

        Text MakeText(Transform parent, string s, int size, Color col, float y, FontStyle style)
        {
            var go = new GameObject("text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = UIFont;
            t.text = s;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = col;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1700f, size * 1.6f);
            rt.anchoredPosition = new Vector2(0f, y);
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // 지정 패널만 활성화하고 페이드 인. null이면 전부 숨김(게임 화면).
        void Switch(GameObject panel)
        {
            if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
            foreach (var p in _panels)
                if (p != panel) p.SetActive(false);
            if (panel != null)
            {
                panel.SetActive(true);
                _fadeCo = StartCoroutine(FadeIn(panel.GetComponent<CanvasGroup>()));
            }
        }

        static IEnumerator FadeIn(CanvasGroup cg)
        {
            if (cg == null) yield break;
            cg.alpha = 0f;
            const float dur = 0.35f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)   // timeScale=0에서도 동작
            {
                cg.alpha = Mathf.Clamp01(t / dur);
                yield return null;
            }
            cg.alpha = 1f;
        }

        // 한글 렌더링 위해 OS 폰트(맑은 고딕 등) 동적 로드. 없으면 내장 폰트로 폴백.
        static Font UIFont
        {
            get
            {
                if (_uiFont == null)
                {
                    _uiFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Malgun Gothic", "맑은 고딕", "Gulim", "Dotum", "Arial Unicode MS" }, 32);
                    if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _uiFont;
            }
        }
    }
}
