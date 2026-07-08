using UnityEngine;

namespace TowardTheStars.Objects
{
    // 사다리(Stage 4 신규): 플레이어가 오르내리는 세로 이동 통로.
    // 광학 상호작용 없음 — 빛은 통과(콜라이더는 Trigger라 빔 레이캐스트에서 제외).
    // Phase 5 플레이어에서 트리거 진입 시 중력 해제 + 수직 이동 허용 예정.
    public class Ladder : MonoBehaviour
    {
        [SerializeField] int height = 1;   // 하단 셀에서 위로 몇 칸인지

        public int Height => height;

        public void Init(int height)
        {
            this.height = Mathf.Max(1, height);
        }
    }
}
