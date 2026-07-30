using UnityEngine;

namespace TowardTheStars.Objects
{
    // 횃불의 랜즈 장착부. 랜즈 아이템을 장착/해제한다.
    //   장착: 광원(LightSource) 발사 ON + 랜즈 시각 표시 / 해제: 발사 OFF + 랜즈 시각 숨김.
    //   플레이어(LensInteractor)가 반경 안에서 F로 Toggle. 이 컴포넌트는 "랜즈 아이템화" 스테이지에만 붙는다.
    public class TorchMount : MonoBehaviour
    {
        LightSource _source;
        Transform _lensVisual;      // 장착 시 표시할 랜즈 시각(없으면 null)

        public bool Mounted { get; private set; }

        public void Init(LightSource source, Transform lensVisual, bool mounted)
        {
            _source = source;
            _lensVisual = lensVisual;
            SetMounted(mounted);
        }

        public void SetMounted(bool mounted)
        {
            Mounted = mounted;
            if (_source != null) _source.Emitting = mounted;              // 빔 발사 여부
            if (_lensVisual != null) _lensVisual.gameObject.SetActive(mounted);   // 랜즈 시각
        }

        public void Toggle() => SetMounted(!Mounted);
    }
}
