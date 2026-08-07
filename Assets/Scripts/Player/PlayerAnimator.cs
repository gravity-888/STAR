using UnityEngine;

namespace TowardTheStars.Player
{
    // 애니메이션 seam: PlayerController의 상태를 읽어 프리팹 아트의 Animator 파라미터로 전달 + 바라보는 방향 뒤집기.
    //   · 프리팹에 Animator가 있으면 파라미터 구동, 없으면(색 사각형 플레이스홀더) 방향 뒤집기만 → 코드는 아트에 의존하지 않는다.
    //   · Animator 파라미터 계약(프리팹 아트가 이 이름으로 만들 것):
    //       Speed(Float)    수평 속도 크기(칸/초)      — idle↔run 블렌드
    //       VSpeed(Float)   수직 속도(+상승/−하강)     — jump/fall 구분
    //       Grounded(Bool)  접지 여부
    //       Climbing(Bool)  사다리 등반 중
    //       Pushing(Bool)   미는 거울 밀기 중
    //       PushDrive(Float) 밀기 속도 비율 0~1 — 가속(0→1)·감속(1→0) 전환 구간 구분(정속≈1)
    //   · 아트는 오른쪽을 바라보는 기준으로 그릴 것(Facing<0이면 코드가 x를 뒤집음).
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimator : MonoBehaviour
    {
        PlayerController _pc;
        Animator _anim;
        Transform _visual;

        static readonly int PSpeed    = Animator.StringToHash("Speed");
        static readonly int PVSpeed   = Animator.StringToHash("VSpeed");
        static readonly int PGrounded = Animator.StringToHash("Grounded");
        static readonly int PClimbing = Animator.StringToHash("Climbing");
        static readonly int PPushing  = Animator.StringToHash("Pushing");
        static readonly int PPushDrive = Animator.StringToHash("PushDrive");

        void Awake()
        {
            _pc = GetComponent<PlayerController>();
            _visual = transform.Find("visual");                 // 아트/색사각형이 들어가는 자식
            _anim = GetComponentInChildren<Animator>(true);     // 프리팹 아트에 Animator가 있으면
        }

        void Update()
        {
            if (_pc == null) return;

            // 바라보는 방향 뒤집기(아트 유무와 무관). 대칭 색사각형은 시각 변화 없음.
            if (_visual != null)
            {
                var s = _visual.localScale;
                float mag = Mathf.Abs(s.x);
                s.x = _pc.Facing >= 0 ? mag : -mag;
                _visual.localScale = s;
            }

            if (_anim == null) return;   // 아트 없음 → 파라미터 구동 생략(무음)
            Vector2 vel = _pc.Velocity;
            _anim.SetFloat(PSpeed, Mathf.Abs(vel.x));
            _anim.SetFloat(PVSpeed, vel.y);
            _anim.SetBool(PGrounded, _pc.Grounded);
            _anim.SetBool(PClimbing, _pc.Climbing);
            _anim.SetBool(PPushing, _pc.IsPushing);
            _anim.SetFloat(PPushDrive, _pc.PushDrive);
        }
    }
}
