using UnityEngine;

namespace TowardTheStars.Level
{
    // 플레이어 추적 카메라(2D 직교). SmoothDamp로 부드럽게 따라가고, 레벨 경계 밖은 보여주지 않게 클램프.
    // MapLoader가 Configure()로 대상·경계·줌 정책을 주입. 뷰가 레벨보다 크면 해당 축은 중앙 고정.
    // 줌(orthographicSize)은 화면비(aspect)에 의존하므로, 창 리사이즈/해상도 변경으로 aspect가 바뀌면 재계산한다.
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smoothTime = 0.15f;
        public Vector2 levelMin = new(-0.5f, -0.5f);  // 월드 경계(셀 바깥 가장자리)
        public Vector2 levelMax = new(0.5f, 0.5f);
        public bool clampToLevel = true;

        // 줌 정책(MapLoader가 주입) — aspect가 바뀌면 이 값으로 orthographicSize 재계산.
        bool _fitWidth;          // true=가로 폭에 맞춤(좌우 벽=화면 끝), false=세로 viewCells 칸
        float _viewCells = 16f;  // fitWidth=false일 때 화면 세로에 담을 셀 수

        Camera _cam;
        Vector3 _vel;
        float _lastAspect = -1f;

        void Awake() => _cam = GetComponent<Camera>();

        public void Configure(Transform target, Vector2 min, Vector2 max, bool fitWidth, float viewCells)
        {
            this.target = target;
            levelMin = min;
            levelMax = max;
            _fitWidth = fitWidth;
            _viewCells = viewCells;
            if (_cam == null) _cam = GetComponent<Camera>();
            ApplySize();
            // 시작 프레임 튐 방지: 즉시 목표 지점으로 스냅.
            transform.position = Clamp(Focus());
        }

        // aspect(창 크기/해상도)에 맞춰 orthographicSize를 결정. MapLoader.SetupCamera와 동일한 공식.
        void ApplySize()
        {
            if (_cam == null || !_cam.orthographic) return;
            float aspect = Mathf.Max(_cam.aspect, 0.01f);
            float orthoSize = _fitWidth ? (levelMax.x - levelMin.x) * 0.5f / aspect : _viewCells * 0.5f;
            orthoSize = Mathf.Min(orthoSize, (levelMax.y - levelMin.y) * 0.5f);  // 세로 경계 초과 방지
            _cam.orthographicSize = orthoSize;
            _lastAspect = _cam.aspect;
        }

        void LateUpdate()
        {
            if (target == null) return;
            // 창 리사이즈/해상도 변경으로 화면비가 바뀌면 줌 재계산(fit_width가 항상 폭을 채우도록).
            if (_cam != null && !Mathf.Approximately(_cam.aspect, _lastAspect)) ApplySize();
            transform.position = Vector3.SmoothDamp(transform.position, Clamp(Focus()), ref _vel, smoothTime);
        }

        Vector3 Focus() => new(target.position.x, target.position.y, transform.position.z);

        Vector3 Clamp(Vector3 p)
        {
            if (!clampToLevel || _cam == null || !_cam.orthographic) return p;
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;
            float minX = levelMin.x + halfW, maxX = levelMax.x - halfW;
            float minY = levelMin.y + halfH, maxY = levelMax.y - halfH;
            p.x = minX <= maxX ? Mathf.Clamp(p.x, minX, maxX) : (levelMin.x + levelMax.x) * 0.5f;
            p.y = minY <= maxY ? Mathf.Clamp(p.y, minY, maxY) : (levelMin.y + levelMax.y) * 0.5f;
            return p;
        }
    }
}
