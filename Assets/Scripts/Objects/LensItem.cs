using UnityEngine;

namespace TowardTheStars.Objects
{
    // 바닥에 떨어진 랜즈 아이템(획득 대상). 트리거 콜라이더로 플레이어와 겹침 감지만 하고,
    //   실제 획득 처리(F키)는 플레이어의 LensInteractor가 담당한다.
    public class LensItem : MonoBehaviour
    {
        public bool Taken { get; private set; }

        // 획득: 시각/트리거를 끈다(오브젝트는 남겨 두되 비활성).
        public void Take()
        {
            Taken = true;
            gameObject.SetActive(false);
        }
    }
}
