using System.Collections;
using UnityEngine;
using TowardTheStars.Level;

namespace TowardTheStars.Objects
{
    // 게이트 개폐부(문): 수광부(GateDetector)가 광량 Σ≥1.0을 받으면 **지정 방향으로 천천히 미끄러지며 열리고**,
    // 그렇지 않으면 **원위치로 천천히 돌아와 닫혀** 솔리드 콜라이더로 플레이어를 막는다.
    // 열림 방향은 맵의 gate.open_dir로 정하며(기본 위), 이동거리는 개폐존 크기에서 자동 계산해 MapLoader가 주입한다.
    //
    // 콜라이더 타이밍: 열릴 때는 즉시 해제(움직이는 중에도 통과 가능), 닫힐 때는 다 돌아온 뒤 활성.
    //   → 플레이어에게 관대하고, 열린 동안 문이 빔을 가로막는 사고도 막는다(콜라이더가 꺼져 있으므로).
    // 정렬순서: 열리면 문 시각을 뒤로 보내(offset) 미끄러져 들어가는 쪽 아트에 가려지게 한다. 닫히면 원복.
    public class GateDoor : MonoBehaviour
    {
        [Header("색(닫힘=막힌 장벽 / 열림=반투명 통로)")]
        public Color closedColor = new(0.78f, 0.35f, 0.30f);
        public Color openColor   = new(0.40f, 1.00f, 0.50f, 0.12f);

        [Header("여닫이 연출")]
        public float slideDuration = 0.8f;   // 완전히 열리거나 닫히는 데 걸리는 시간(초)

        [Header("정렬순서")]
        public int openSortingOffset = -20;  // 열렸을 때 문 시각에 더할 정렬순서(음수 = 다른 아트 뒤로 가려짐)

        Collider2D _blocker;
        SpriteRenderer _visual;
        Transform _door;
        Vector3 _closedPos;
        Vector3 _slideVec;   // 열릴 때 이동하는 벡터(방향×거리). 닫힘=원위치.
        Coroutine _anim;

        SpriteRenderer[] _sprites;   // 문 시각의 모든 SpriteRenderer(정렬순서 조정용)
        int[] _baseOrders;           // 각 스프라이트의 기본 정렬순서(닫힘 상태)

        public bool IsOpen { get; private set; }

        // MapLoader가 개폐부 블럭과 "열림 이동 벡터"(방향×거리)를 등록.
        public void Register(Collider2D col, SpriteRenderer sr, Vector3 slideOffset)
        {
            _blocker = col;
            _visual = sr;
            _door = col != null ? col.transform : (sr != null ? sr.transform : null);
            if (_door != null)
            {
                _closedPos = _door.position;
                _sprites = _door.GetComponentsInChildren<SpriteRenderer>(true);
                _baseOrders = new int[_sprites.Length];
                for (int i = 0; i < _sprites.Length; i++)
                    _baseOrders[i] = _sprites[i] != null ? _sprites[i].sortingOrder : 0;
            }
            _slideVec = slideOffset;
        }

        // 열림/닫힘에 따라 문 시각 정렬순서를 뒤로 보내거나 원복.
        void ApplySorting(bool open)
        {
            if (_sprites == null) return;
            for (int i = 0; i < _sprites.Length; i++)
                if (_sprites[i] != null)
                    _sprites[i].sortingOrder = _baseOrders[i] + (open ? openSortingOffset : 0);
        }

        // 연출 없이 즉시 상태 적용(최초 배치용).
        public void SetOpenImmediate(bool open)
        {
            IsOpen = open;
            if (_blocker != null) _blocker.enabled = !open;
            if (_visual != null) _visual.color = open ? openColor : closedColor;
            if (_door != null) _door.position = _closedPos + (open ? _slideVec : Vector3.zero);
            ApplySorting(open);
        }

        // 개폐 적용. 수광부의 OnStateChanged 구독 대상.
        public void SetOpen(bool open)
        {
            if (_door == null) { SetOpenImmediate(open); return; }
            if (IsOpen == open) return;
            IsOpen = open;

            if (open) AudioManager.GateOpen();
            if (open && _blocker != null) _blocker.enabled = false;   // 열림: 즉시 통과 허용
            ApplySorting(open);                                       // 열림=뒤로(가려짐) / 닫힘=앞으로(보임) 즉시 적용
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Slide(open));
        }

        IEnumerator Slide(bool open)
        {
            Vector3 from = _door.position;
            Vector3 to   = _closedPos + (open ? _slideVec : Vector3.zero);
            Color fromC  = _visual != null ? _visual.color : default;
            Color toC    = open ? openColor : closedColor;

            // 중간에 방향이 뒤집혀도 속도가 일정하도록 남은 거리에 비례한 시간을 쓴다.
            float mag = _slideVec.magnitude;
            float dur = mag > 0.0001f ? slideDuration * ((to - from).magnitude / mag) : 0f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = t / dur;
                _door.position = Vector3.Lerp(from, to, k);
                if (_visual != null) _visual.color = Color.Lerp(fromC, toC, k);
                yield return null;
            }

            _door.position = to;
            if (_visual != null) _visual.color = toC;
            if (!open && _blocker != null) _blocker.enabled = true;   // 닫힘: 다 돌아온 뒤 막는다
            _anim = null;
        }
    }
}
