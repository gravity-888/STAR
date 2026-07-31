using UnityEngine;

namespace TowardTheStars.Level
{
    // 배경 시차(parallax) 스크롤: 카메라 이동량의 factor배만큼만 따라 움직여 원근감을 준다.
    //   factor 0 = 월드에 고정(카메라 움직여도 제자리) · 1 = 화면에 고정(카메라와 완전 동행). 원경일수록 1에 가깝게.
    //   배경 프리팹의 레이어(자식)마다 이 컴포넌트를 붙여 factor를 달리하면 다층 시차가 된다.
    //   프리팹에 하나도 없으면 MapLoader가 루트에 기본 factor로 하나 붙인다.
    public class ParallaxBackground : MonoBehaviour
    {
        [Range(0f, 1f)] public float factor = 0.5f;

        Camera _cam;
        Vector3 _base;        // 시작 위치(그리드 중앙 등)
        Vector2 _camStart;    // 시작 시 카메라 위치
        bool _init;

        void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            if (!_init) { _base = transform.position; _camStart = _cam.transform.position; _init = true; }

            Vector2 delta = (Vector2)_cam.transform.position - _camStart;
            transform.position = new Vector3(_base.x + delta.x * factor, _base.y + delta.y * factor, _base.z);
        }
    }
}
