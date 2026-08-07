using UnityEngine;
using TowardTheStars.Player;

namespace TowardTheStars.Objects
{
    // 미는 거울: 플레이어가 좌우로 밀어 옮기는 거울(회전 불가·각도 맵 고정). 반사는 같은 오브젝트의 Mirror가 담당.
    //   · 중력(Dynamic RB): 바닥에 놓이고 지지면이 없으면 떨어진다. 회전은 항상 고정.
    //   · 밀지 않을 때는 X축을 물리적으로 고정(FreezePositionX) → 미동·떨림 없이 칸에 정지, 플레이어가 몸으로 못 민다.
    //   · 밀 때만 X를 풀어 pushSpeed로 이동. 앞이 막히면(벽·다른 솔리드) X를 다시 고정해 더 못 민다.
    //   · 밀기 시작 시 0→pushSpeed로 서서히 가속, 놓으면 목표 칸까지 서서히 감속해 멈춘다(가속도 모델).
    //     플레이어도 거울의 실시간 속도로 구동(점프·방향전환 금지) → 함께 가감속.
    //   · 반사는 Mirror의 표면 접점 방식(거울 이동 시 반사점이 거울 면을 따라 이동).
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PushableMirror : MonoBehaviour
    {
        public float pushSpeed = 2.5f;       // 최고 이동 속도(칸/초). 플레이어(7)보다 느려 "무겁게 미는" 느낌.
        public float accel = 12f;            // 가속·감속률(칸/초²). 0→최고속 ≈0.2s, 정지까지 감속거리 ≈0.26칸.
        public float reachY = 0.7f;          // 플레이어가 옆에 있다고 볼 세로 오차(위/아래에선 안 밀림)
        public float touchPad = 0.15f;       // 접촉 판정 여유(칸)
        const float PlayerHalfW = 0.5f;      // 플레이어 콜라이더 반폭(1.0/2)
        const float SnapEps = 0.02f;         // 이 이내면 칸에 정렬된 것으로 간주(빔은 어차피 칸 스냅이라 무해)

        BoxCollider2D _col;
        Rigidbody2D _rb;
        Transform _player;
        PlayerController _pc;
        ContactFilter2D _solidFilter;
        readonly RaycastHit2D[] _buf = new RaycastHit2D[8];
        float _minX, _maxX;
        bool _hasBounds;
        bool _sliding;         // true=밀기/스냅으로 이동 중(X 자유), false=칸에 정지(X 고정)
        float _vx;             // 현재 X 속도(가감속 상태)
        bool _snapping;        // 놓은 뒤 안착 진행 중
        float _snapTargetX;    // 안착 목표 칸(놓는 순간 1회 결정 → 목표 튐/떨림 방지)
        float _prevX;          // 직전 프레임 위치(실제 이동 여부 판정)
        float _stuckTimer;     // 밀고 있는데 안 움직인 시간 — 넘으면 플레이어 구동 해제(끼임/소프트락 탈출)

        void Awake()
        {
            _col = GetComponent<BoxCollider2D>();
            _rb = GetComponent<Rigidbody2D>();
            _solidFilter = new ContactFilter2D { useTriggers = false };   // 사다리(Trigger)는 장애물 아님
            _solidFilter.SetLayerMask(Physics2D.AllLayers);
            _solidFilter.useLayerMask = true;
            _prevX = _rb.position.x;
            Freeze(true);   // 시작은 칸에 정지(X 고정)
        }

        // 이동 가능 x 범위 주입(레벨 경계). 실제 벽은 콜라이더/캐스트가 막으므로 이건 안전망.
        public void Init(float minX, float maxX) { _minX = minX; _maxX = maxX; _hasBounds = true; }

        // 논리 칸 x(진행상태 저장/복원용) — 정수 칸으로 반올림.
        public float CellX => Mathf.Round(_rb.position.x);
        public void SetCellX(float x) { _rb.position = new Vector2(x, _rb.position.y); }

        // X축 고정/해제(중력=Y, 회전은 항상 고정). 고정 시 X속도 0으로.
        void Freeze(bool freezeX)
        {
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation |
                              (freezeX ? RigidbodyConstraints2D.FreezePositionX : RigidbodyConstraints2D.None);
            if (freezeX) SetVX(0f);
            _sliding = !freezeX;
        }

        void SetVX(float vx) { var v = _rb.linearVelocity; v.x = vx; _rb.linearVelocity = v; }

        void FixedUpdate()
        {
            if (PlayerController.ControlsLocked) { _vx = 0f; _snapping = false; if (_sliding) Freeze(true); return; }
            EnsurePlayer();
            if (_pc == null) return;
            float dt = Time.fixedDeltaTime;
            bool moved = Mathf.Abs(_rb.position.x - _prevX) > 1e-4f;   // 직전 스텝에 실제로 움직였는지
            _prevX = _rb.position.x;

            int dir = PushDir();
            if (dir != 0)
            {
                // 능동 밀기: 0→pushSpeed로 서서히 가속. 앞이 막혔으면 X 고정해 못 민다.
                _snapping = false;
                if (Blocked(dir)) { _vx = 0f; _stuckTimer = 0f; Freeze(true); return; }   // 막힘 → 거울 정지, 플레이어 자유
                Freeze(false);
                _vx = Mathf.MoveTowards(_vx, dir * pushSpeed, accel * dt);
                SetVX(_vx);
                // 안전장치: 밀고 있는데 실제로 안 움직인 채 0.2s 넘으면(끼임·미검출 장애물) 플레이어 구동 해제 → 자유.
                _stuckTimer = moved ? 0f : _stuckTimer + dt;
                if (_stuckTimer < 0.2f) _pc.SetPushDrive(dir, Mathf.Abs(_vx), pushSpeed);   // 플레이어도 같은 실시간 속도로 구동(함께 가속·잠김)
                return;
            }
            _stuckTimer = 0f;

            // 밀지 않음 → 놓는 순간 관성 방향으로 목표 칸을 1회 정하고, arrive식 감속으로 부드럽게 안착.
            if (!_snapping)
            {
                bool atCell = Mathf.Abs(_rb.position.x - Mathf.Round(_rb.position.x)) <= SnapEps;
                if (atCell && Mathf.Abs(_vx) < 0.05f) { _vx = 0f; if (_sliding) Freeze(true); return; }   // 유휴 유지
                _snapping = true;
                _snapTargetX = ChooseSnapCell();
            }

            float d = _snapTargetX - _rb.position.x;
            float step = Mathf.Abs(_vx) * dt;   // 이번 프레임 이동거리 — 이 이내면 도착(진동/오버슛 방지, 무한루프 차단)
            bool stuck = Mathf.Abs(_vx) < 0.05f && Blocked(d > 0f ? 1 : -1);
            if (Mathf.Abs(d) <= Mathf.Max(SnapEps, step) || stuck)
            {
                float fc = stuck ? Mathf.Round(_rb.position.x) : _snapTargetX;
                if (_hasBounds) fc = Mathf.Clamp(fc, _minX, _maxX);
                _rb.position = new Vector2(fc, _rb.position.y);   // 정확히 안착
                _vx = 0f; _snapping = false; Freeze(true);
                return;
            }
            Freeze(false);
            int sdir = d > 0f ? 1 : -1;
            float arriveV = sdir * Mathf.Min(pushSpeed, Mathf.Sqrt(2f * accel * Mathf.Abs(d)));   // 목표에서 0이 되도록 감속
            _vx = Mathf.MoveTowards(_vx, arriveV, accel * dt);
            SetVX(_vx);
            if (moved && PlayerBeside()) _pc.SetPushDrive(sdir, Mathf.Abs(_vx), pushSpeed);   // 스냅 중 플레이어 동행(움직일 때만 = 자유 보장)
        }

        void EnsurePlayer()
        {
            if (_pc != null) return;
            _pc = Object.FindFirstObjectByType<PlayerController>();
            if (_pc != null) _player = _pc.transform;
        }

        // 플레이어가 옆에 붙어 거울 쪽으로 밀고 있으면 미는 방향(+1 오른쪽 / -1 왼쪽), 아니면 0.
        int PushDir()
        {
            if (_pc == null || _player == null) return 0;
            Vector2 pp = _player.position;
            if (Mathf.Abs(pp.y - _rb.position.y) > reachY) return 0;             // 옆이 아니라 위/아래 → 안 밀림
            float dx = _rb.position.x - pp.x;                                    // 플레이어→거울 방향의 부호·거리
            float touch = _col.size.x * 0.5f + PlayerHalfW + touchPad;
            if (Mathf.Abs(dx) > touch) return 0;                                // 안 닿음
            int input = _pc.InputX > 0.5f ? 1 : (_pc.InputX < -0.5f ? -1 : 0);  // 플레이어 좌우 입력 의도(실제 키)
            if (input == 0) return 0;
            int toward = dx > 0f ? 1 : -1;                                      // 플레이어가 거울을 미는 방향
            return input == toward ? toward : 0;                               // 거울 쪽으로 밀 때만
        }

        // 놓는 순간 안착할 칸을 1회 결정: 관성이 있으면 진행 방향 다음 칸, 아니면 가장 가까운 칸.
        //   그쪽이 막혀 있으면 가장 가까운 칸. 경계 클램프.
        float ChooseSnapCell()
        {
            float x = _rb.position.x;
            float cell = Mathf.Abs(_vx) > 0.2f
                ? (_vx > 0f ? Mathf.Ceil(x - 0.02f) : Mathf.Floor(x + 0.02f))   // 진행 방향 다음 칸(관성)
                : Mathf.Round(x);                                               // 거의 멈춤 → 가장 가까운 칸
            if (_hasBounds) cell = Mathf.Clamp(cell, _minX, _maxX);
            if (Mathf.Abs(cell - x) > SnapEps && Blocked(cell > x ? 1 : -1))
            {
                cell = Mathf.Round(x);
                if (_hasBounds) cell = Mathf.Clamp(cell, _minX, _maxX);
            }
            return cell;
        }

        // 스냅 동행 판정 — 플레이어가 거울 옆(조금 넉넉하게)에 있으면 true.
        bool PlayerBeside()
        {
            if (_player == null) return false;
            if (Mathf.Abs(_player.position.y - _rb.position.y) > reachY) return false;
            float touch = _col.size.x * 0.5f + PlayerHalfW + touchPad + 1f;
            return Mathf.Abs(_rb.position.x - _player.position.x) <= touch;
        }

        // dir 방향 한 스텝 거리 안에 솔리드(플레이어·자기 자신 제외)가 있으면 true → 그쪽으로 못 감.
        bool Blocked(int dir)
        {
            float dist = pushSpeed * Time.fixedDeltaTime + 0.02f;
            int n = _col.Cast(new Vector2(dir, 0f), _solidFilter, _buf, dist);
            for (int i = 0; i < n; i++)
            {
                var c = _buf[i].collider;
                if (c == null) continue;
                if (c.transform == transform || c.transform.IsChildOf(transform)) continue;  // 자기 자신/자식(거치대 등)
                if (c.GetComponent<PlayerController>() != null) continue;                     // 미는 플레이어는 장애물 아님
                return true;
            }
            return false;
        }
    }
}
