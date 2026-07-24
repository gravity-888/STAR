using System.Collections;
using UnityEngine;

namespace TowardTheStars.Objects
{
    // 게이트 개폐부(문): 수광부(GateDetector)가 광량 Σ≥1.0을 받으면 **위로 천천히 올라가며 열리고**,
    // 그렇지 않으면 **천천히 내려와 닫혀** 솔리드 콜라이더로 플레이어를 막는다.
    // MapLoader가 개폐존 전체를 덮는 블럭(콜라이더+시각) 하나를 등록한다.
    //
    // 콜라이더 타이밍: 열릴 때는 즉시 해제(올라가는 중에도 통과 가능), 닫힐 때는 다 내려온 뒤 활성.
    //   → 플레이어에게 관대하고, 열린 동안 문이 빔을 가로막는 사고도 막는다(콜라이더가 꺼져 있으므로).
    public class GateDoor : MonoBehaviour
    {
        [Header("색(닫힘=막힌 장벽 / 열림=반투명 통로)")]
        public Color closedColor = new(0.78f, 0.35f, 0.30f);
        public Color openColor   = new(0.40f, 1.00f, 0.50f, 0.12f);

        [Header("여닫이 연출")]
        public float slideDuration = 0.8f;   // 완전히 열리거나 닫히는 데 걸리는 시간(초)

        Collider2D _blocker;
        SpriteRenderer _visual;
        Transform _door;
        Vector3 _closedPos;
        float _slide;        // 열릴 때 위로 이동하는 거리(개폐존 높이)
        Coroutine _anim;

        public bool IsOpen { get; private set; }

        // MapLoader가 개폐부 블럭과 열림 이동거리를 등록.
        public void Register(Collider2D col, SpriteRenderer sr, float slideUp)
        {
            _blocker = col;
            _visual = sr;
            _door = col != null ? col.transform : (sr != null ? sr.transform : null);
            if (_door != null) _closedPos = _door.position;
            _slide = Mathf.Max(0f, slideUp);
        }

        // 연출 없이 즉시 상태 적용(최초 배치용).
        public void SetOpenImmediate(bool open)
        {
            IsOpen = open;
            if (_blocker != null) _blocker.enabled = !open;
            if (_visual != null) _visual.color = open ? openColor : closedColor;
            if (_door != null) _door.position = _closedPos + Vector3.up * (open ? _slide : 0f);
        }

        // 개폐 적용. 수광부의 OnStateChanged 구독 대상.
        public void SetOpen(bool open)
        {
            if (_door == null) { SetOpenImmediate(open); return; }
            if (IsOpen == open) return;
            IsOpen = open;

            if (open && _blocker != null) _blocker.enabled = false;   // 열림: 즉시 통과 허용
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Slide(open));
        }

        IEnumerator Slide(bool open)
        {
            Vector3 from = _door.position;
            Vector3 to   = _closedPos + Vector3.up * (open ? _slide : 0f);
            Color fromC  = _visual != null ? _visual.color : default;
            Color toC    = open ? openColor : closedColor;

            // 중간에 방향이 뒤집혀도 속도가 일정하도록 남은 거리에 비례한 시간을 쓴다.
            float dur = _slide > 0.0001f ? slideDuration * (Mathf.Abs(to.y - from.y) / _slide) : 0f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = t / dur;
                _door.position = Vector3.Lerp(from, to, k);
                if (_visual != null) _visual.color = Color.Lerp(fromC, toC, k);
                yield return null;
            }

            _door.position = to;
            if (_visual != null) _visual.color = toC;
            if (!open && _blocker != null) _blocker.enabled = true;   // 닫힘: 다 내려온 뒤 막는다
            _anim = null;
        }
    }
}
