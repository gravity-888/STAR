using UnityEngine;
using TowardTheStars.Objects;
using TowardTheStars.Player;

namespace TowardTheStars.Level
{
    // 스테이지 이동 트리거. 플레이어가 이 영역에 들어오면 스테이지를 전환한다.
    //  - dir +1(게이트 통과): _gate 개방 상태에서만 → 다음 스테이지.
    //  - dir -1(입장 통로 역방향): _gate=null(조건 없음) → 이전 스테이지.
    public class GateExit : MonoBehaviour
    {
        GateDetector _gate;   // null이면 개폐 조건 없이 통과
        MapLoader _loader;
        int _dir = 1;         // +1 다음 / -1 이전

        public void Init(GateDetector gate, MapLoader loader, int dir)
        {
            _gate = gate;
            _loader = loader;
            _dir = dir;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_loader == null) return;
            if (_gate != null && !_gate.IsOpen) return;   // 게이트가 있으면 개방 상태에서만 통과
            if (other.GetComponentInParent<PlayerController>() == null) return;
            if (_dir < 0) _loader.GoToPrev();
            else _loader.GoToNext();
        }
    }
}
