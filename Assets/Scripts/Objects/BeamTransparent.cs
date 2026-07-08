using UnityEngine;

namespace TowardTheStars.Objects
{
    // 마커: 이 솔리드 콜라이더는 빛이 투과한다(플레이어는 밟고 서지만 빔은 통과).
    // 발판(platform, transmit=true)에 부착. BeamTracer가 이 히트를 건너뛰고 계속 추적한다.
    // 벽(wall)은 이 마커가 없으므로 빛을 차단.
    public class BeamTransparent : MonoBehaviour { }
}
