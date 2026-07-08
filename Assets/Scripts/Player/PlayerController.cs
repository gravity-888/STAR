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
        public float climbSpeed = 5f;

        [Header("점프 (가변 높이)")]
        public float jumpHeightCells = 3.5f;   // 끝까지 누른 최대 정점 높이(칸). 맵파일 jump_units=3.5 기준.
        [Range(0.05f, 1f)]
        public float jumpCutMultiplier = 0.45f;  // 상승 중 버튼 떼면 상승속도 ×이 값 → 낮은 점프.
        // 최대 속도는 중력에서 역산(정점=3.5칸). 짧게 누르면 상승 감쇠로 정점≈0.45²·3.5≈0.7칸.

        [Header("접지 판정")]
        public float groundCheckDist = 0.08f;   // 발밑으로 이만큼 캐스트해 바닥 감지
        public float coyoteTime = 0.07f;         // 발판에서 떨어진 직후 이 시간 동안은 점프 허용(코요테 타임)

        Rigidbody2D _rb;
        BoxCollider2D _col;
        ContactFilter2D _groundFilter;   // 트리거(=사다리) 제외한 솔리드만
        readonly RaycastHit2D[] _hitBuf = new RaycastHit2D[4];

        float _baseGravity;
        bool _jumpQueued;      // 이번에 점프 눌림(에지)
        bool _jumpReleased;    // 점프 버튼 뗌(에지) → 상승 감쇠 신호
        float _coyoteTimer;    // 접지 후 남은 코요테 유예(>0이면 공중이어도 점프 가능)
        int _lastHorizDir;     // 마지막으로 누른 수평 방향(-1/+1). 좌우 동시입력 시 이쪽 우선

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
            // 점프 눌림/뗌은 에지 트리거 → Update에서 잡아 FixedUpdate에서 소비(가변 점프).
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame)  _jumpQueued = true;
            if (kb.spaceKey.wasReleasedThisFrame) _jumpReleased = true;

            // 좌우 동시입력 시 "나중에 누른 방향" 우선 → 눌린 순간을 기록.
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)  _lastHorizDir = -1;
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) _lastHorizDir = +1;
        }

        void FixedUpdate()
        {
            float ix = 0f, iy = 0f;
            var kb = Keyboard.current;
            if (kb != null)
            {
                bool left  = kb.aKey.isPressed || kb.leftArrowKey.isPressed;
                bool right = kb.dKey.isPressed || kb.rightArrowKey.isPressed;
                if (left && right)  ix = _lastHorizDir;              // 반대 방향 동시 → 나중에 누른 쪽
                else if (left)      { ix = -1f; _lastHorizDir = -1; }
                else if (right)     { ix =  1f; _lastHorizDir =  1; }

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

            // 코요테 타임: 접지 중이면 유예 리필, 공중이면 감소. >0인 동안은 점프 허용.
            if (IsGrounded()) _coyoteTimer = coyoteTime;
            else _coyoteTimer -= Time.fixedDeltaTime;

            if (_jumpQueued && _coyoteTimer > 0f)
            {
                v.y = JumpVelocity();     // 최대 속도로 발사(끝까지 누르면 3.5칸)
                _coyoteTimer = 0f;        // 소비 → 공중 재점프(더블점프) 방지
                _jumpReleased = false;    // 새 점프 → 이전 릴리즈 신호 무시
            }
            // 가변 점프: 상승 중 버튼을 떼면 상승속도 감쇠 → 낮은 점프(누른 시간에 비례).
            if (_jumpReleased && v.y > 0f) v.y *= jumpCutMultiplier;

            _rb.linearVelocity = v;
            _jumpQueued = false;
            _jumpReleased = false;
        }

        // 정점 높이 h를 내려면 v0 = sqrt(2·g·h). g = |중력.y|·gravityScale.
        float JumpVelocity()
        {
            float g = Mathf.Abs(Physics2D.gravity.y) * _baseGravity;
            return Mathf.Sqrt(2f * g * jumpHeightCells);
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
