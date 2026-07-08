using UnityEngine;
using UnityEngine.InputSystem;
using TowardTheStars.Objects;

namespace TowardTheStars.Player
{
    // Phase 5: 플레이어 액터 — 좌우 이동 / 점프 / 사다리 등반.
    // 입력: 신 Input System(프로젝트 activeInputHandler=1). 구 Input.GetKey는 런타임 예외이므로 사용 금지.
    //   좌우: A/D 또는 ←/→   ·   점프: Space(접지 시)   ·   등반: W/S 또는 ↑/↓ (사다리와 겹칠 때)
    // 규약[GDD]: 1셀 = 1유닛. 지형/발판 = 솔리드 콜라이더, 사다리 = Trigger + Ladder 컴포넌트(빛은 통과).
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("이동 (유닛/초)")]
        public float moveSpeed = 6f;
        public float jumpSpeed = 11f;
        public float climbSpeed = 5f;

        [Header("접지 판정")]
        public float groundCheckDist = 0.08f;   // 발밑으로 이만큼 캐스트해 바닥 감지

        Rigidbody2D _rb;
        BoxCollider2D _col;
        ContactFilter2D _groundFilter;   // 트리거(=사다리) 제외한 솔리드만
        readonly RaycastHit2D[] _hitBuf = new RaycastHit2D[4];

        float _baseGravity;
        bool _jumpQueued;

        int _ladderCount;        // 현재 겹친 사다리 수 (>0 이면 사다리 위)
        bool _climbing;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<BoxCollider2D>();

            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (_rb.gravityScale <= 0f) _rb.gravityScale = 3f;
            _baseGravity = _rb.gravityScale;

            _groundFilter = new ContactFilter2D();
            _groundFilter.useTriggers = false;                 // 사다리 트리거는 바닥으로 치지 않음
            _groundFilter.SetLayerMask(Physics2D.AllLayers);
            _groundFilter.useLayerMask = true;
        }

        void Update()
        {
            // 점프는 에지 트리거 → Update에서 잡아 FixedUpdate에서 소비.
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) _jumpQueued = true;
        }

        void FixedUpdate()
        {
            float ix = 0f, iy = 0f;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  ix -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) ix += 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    iy += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  iy -= 1f;
            }

            bool onLadder = _ladderCount > 0;
            if (!onLadder) _climbing = false;
            else if (Mathf.Abs(iy) > 0.01f) _climbing = true;   // 사다리 위에서 상/하 입력 → 등반 시작

            if (_climbing)
            {
                // 점프로 사다리 이탈
                if (_jumpQueued) { _climbing = false; }
                else
                {
                    _rb.gravityScale = 0f;
                    _rb.linearVelocity = new Vector2(ix * moveSpeed * 0.6f, iy * climbSpeed);
                    _jumpQueued = false;
                    return;
                }
            }

            // 일반 지상/공중 이동
            _rb.gravityScale = _baseGravity;
            var v = _rb.linearVelocity;
            v.x = ix * moveSpeed;
            if (_jumpQueued && IsGrounded()) v.y = jumpSpeed;
            _rb.linearVelocity = v;
            _jumpQueued = false;
        }

        bool IsGrounded()
        {
            int n = _col.Cast(Vector2.down, _groundFilter, _hitBuf, groundCheckDist);
            for (int i = 0; i < n; i++)
                if (_hitBuf[i].collider != null && _hitBuf[i].normal.y > 0.5f)
                    return true;
            return false;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<Ladder>() != null) _ladderCount++;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<Ladder>() != null)
            {
                _ladderCount = Mathf.Max(0, _ladderCount - 1);
                if (_ladderCount == 0) _climbing = false;
            }
        }
    }
}
