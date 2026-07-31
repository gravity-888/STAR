using UnityEngine;
using TowardTheStars.Objects;
using TowardTheStars.Player;

namespace TowardTheStars.Level
{
    // 스테이지 이동 트리거. 플레이어가 이 영역에 들어오면 스테이지를 전환한다.
    //  - dir +1(게이트 통과): _gate 개방 상태에서만 → 다음 스테이지.
    //  - dir -1(입장 통로 역방향): _gate=null(조건 없음) → 이전 스테이지.
    // 스폰 겹침 방지: 새 스테이지에서 플레이어가 트리거 위에 스폰되면 즉시 되돌아가는 오실레이션이 생긴다.
    //   → 스폰 시 겹쳐 있으면 "한 번 밖으로 나갈 때까지" 통과를 막는다(_armed=false).
    public class GateExit : MonoBehaviour
    {
        GateDetector _gate;   // null이면 개폐 조건 없이 통과
        MapLoader _loader;
        int _dir = 1;         // +1 다음 / -1 이전
        Collider2D _col;
        bool _armed = true;   // false면 통과 금지(플레이어가 밖으로 나가면 다시 무장)

        void Awake() => _col = GetComponent<Collider2D>();

        public void Init(GateDetector gate, MapLoader loader, int dir)
        {
            _gate = gate;
            _loader = loader;
            _dir = dir;
        }

        // MapLoader가 빌드 직후 호출: 플레이어가 이 트리거에 겹쳐 스폰됐으면 무장 해제(나갈 때까지 통과 금지).
        public void DisarmIfOverlaps(Collider2D playerCol)
        {
            if (_col != null && playerCol != null && _col.bounds.Intersects(playerCol.bounds))
                _armed = false;
        }

        void OnTriggerEnter2D(Collider2D other) => TryPass(other);
        void OnTriggerStay2D(Collider2D other) => TryPass(other);

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null) _armed = true;   // 한 번 나감 → 이후 진입/체류는 정상 통과
        }

        void TryPass(Collider2D other)
        {
            if (_loader == null || !_armed) return;
            if (_gate != null && !_gate.IsOpen) return;   // 게이트가 있으면 개방 상태에서만 통과
            if (other.GetComponentInParent<PlayerController>() == null) return;
            if (_dir < 0) _loader.GoToPrev();
            else _loader.GoToNext();
        }
    }
}
