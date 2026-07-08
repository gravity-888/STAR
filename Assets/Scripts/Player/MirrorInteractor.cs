using UnityEngine;
using UnityEngine.InputSystem;
using TowardTheStars.Objects;
using TowardTheStars.Light;

namespace TowardTheStars.Player
{
    // Phase 4: 플레이어의 거울 조작기. 플레이어 오브젝트에 부착.
    // 가장 가까운 "회전 가능(비고정) 거울"을 자동 선택해 흰색으로 하이라이트,
    //   Q = 반시계 22.5° · E = 시계 22.5° 회전 후 BeamTracer.Trace()로 즉시 빛 재추적.
    // 고정 거울(fixed=회색)은 선택 대상에서 제외. 입력은 신 Input System.
    public class MirrorInteractor : MonoBehaviour
    {
        [Header("거울 조작")]
        public float reach = 2.5f;                 // 이 반경 안 가장 가까운 회전 가능 거울 선택
        public Color highlightColor = Color.white; // 선택 표시 색

        Mirror _selected;
        SpriteRenderer _selSprite;
        Color _selBaseColor;
        BeamTracer _tracer;

        void Update()
        {
            UpdateSelection();

            var kb = Keyboard.current;
            if (_selected == null || kb == null) return;

            int steps = 0;
            if (kb.qKey.wasPressedThisFrame) steps -= 1;   // 반시계
            if (kb.eKey.wasPressedThisFrame) steps += 1;   // 시계
            if (steps != 0)
            {
                _selected.Rotate(steps);
                Retrace();
            }
        }

        // 반경 내 가장 가까운 비고정 거울을 고른다.
        void UpdateSelection()
        {
            Vector2 me = transform.position;
            Mirror nearest = null;
            float best = reach * reach;
            foreach (var m in FindObjectsByType<Mirror>(FindObjectsSortMode.None))
            {
                if (m.IsFixed) continue;
                float d = ((Vector2)m.transform.position - me).sqrMagnitude;
                if (d <= best) { best = d; nearest = m; }
            }
            if (nearest != _selected) Select(nearest);
        }

        void Select(Mirror m)
        {
            if (_selSprite != null) _selSprite.color = _selBaseColor;   // 이전 선택 원복
            _selSprite = null;
            _selected = m;
            if (m == null) return;

            var vis = m.transform.Find("visual");
            if (vis != null && vis.TryGetComponent(out SpriteRenderer sr))
            {
                _selSprite = sr;
                _selBaseColor = sr.color;
                sr.color = highlightColor;
            }
        }

        void Retrace()
        {
            if (_tracer == null) _tracer = FindFirstObjectByType<BeamTracer>();
            if (_tracer != null) _tracer.Trace();
        }

        void OnDisable()
        {
            if (_selSprite != null) _selSprite.color = _selBaseColor;   // 하이라이트 원복
        }
    }
}
