using UnityEngine;

namespace TowardTheStars.Level
{
    // 플레이어 추적 카메라(2D 직교). SmoothDamp로 부드럽게 따라가고, 레벨 경계 밖은 보여주지 않게 클램프.
    // MapLoader가 Configure()로 대상·경계를 주입. 뷰가 레벨보다 크면 해당 축은 중앙 고정.
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smoothTime = 0.15f;
        public Vector2 levelMin = new(-0.5f, -0.5f);  // 월드 경계(셀 바깥 가장자리)
        public Vector2 levelMax = new(0.5f, 0.5f);
        public bool clampToLevel = true;

        Camera _cam;
        Vector3 _vel;

        void Awake() => _cam = GetComponent<Camera>();

        public void Configure(Transform target, Vector2 min, Vector2 max)
        {
            this.target = target;
            levelMin = min;
            levelMax = max;
            if (_cam == null) _cam = GetComponent<Camera>();
            // 시작 프레임 튐 방지: 즉시 목표 지점으로 스냅.
            transform.position = Clamp(Focus());
        }

        void LateUpdate()
        {
            if (target == null) return;
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
