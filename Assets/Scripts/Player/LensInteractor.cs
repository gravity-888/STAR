using UnityEngine;
using UnityEngine.InputSystem;
using TowardTheStars.Objects;

namespace TowardTheStars.Player
{
    // 플레이어의 랜즈 조작기. 플레이어 오브젝트에 부착(MirrorInteractor와 같은 패턴).
    //   F키(문맥 감응):
    //     1) 반경 안에 횃불 마운트가 있으면 → 장착/해제 토글(장착 시 랜즈 소모, 해제 시 랜즈 회수).
    //     2) 아니면 바닥 랜즈에 겹쳐 있고 미소지면 → 줍기.
    //   입력은 신 Input System. "랜즈 아이템화" 스테이지에만 TorchMount/LensItem이 존재하므로,
    //   다른 스테이지에서는 F를 눌러도 아무 일도 없다(마운트/아이템이 없음).
    public class LensInteractor : MonoBehaviour
    {
        [Header("장착 반경")]
        public float reach = 2.5f;                 // 이 반경 안 가장 가까운 횃불에 장착/해제

        public GameObject carryVisualPrefab;       // 들고 있을 때 머리 위에 표시(선택). 비면 표시 없음.

        bool _hasLens;
        LensItem _touching;                        // 현재 겹친 바닥 랜즈
        GameObject _carryVisual;

        void Update()
        {
            if (PlayerController.ControlsLocked) return;

            var kb = Keyboard.current;
            if (kb == null || !kb.fKey.wasPressedThisFrame) return;

            // 1) 근처 횃불 마운트 우선
            var mount = NearestMount();
            if (mount != null)
            {
                if (mount.Mounted)           { mount.SetMounted(false); _hasLens = true; }   // 해제 → 회수
                else if (_hasLens)           { mount.SetMounted(true);  _hasLens = false; }   // 장착 → 소모
                UpdateCarryVisual();
                return;
            }

            // 2) 바닥 랜즈 줍기
            if (!_hasLens && _touching != null && !_touching.Taken)
            {
                _touching.Take();
                _touching = null;
                _hasLens = true;
                UpdateCarryVisual();
            }
        }

        TorchMount NearestMount()
        {
            Vector2 me = transform.position;
            TorchMount best = null;
            float bestSqr = reach * reach;
            foreach (var t in FindObjectsByType<TorchMount>(FindObjectsSortMode.None))
            {
                if (t == null) continue;
                float d = ((Vector2)t.transform.position - me).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = t; }
            }
            return best;
        }

        void UpdateCarryVisual()
        {
            if (carryVisualPrefab == null) return;
            if (_carryVisual == null)
            {
                _carryVisual = Instantiate(carryVisualPrefab, transform, false);
                _carryVisual.name = "carried_lens";
                _carryVisual.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                _carryVisual.transform.localScale *= 0.6f;
                foreach (var sr in _carryVisual.GetComponentsInChildren<SpriteRenderer>(true))
                    sr.sortingOrder += 20;   // 플레이어 위로
            }
            _carryVisual.SetActive(_hasLens);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var li = other.GetComponent<LensItem>();
            if (li != null && !li.Taken) _touching = li;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            var li = other.GetComponent<LensItem>();
            if (li != null && _touching == li) _touching = null;
        }
    }
}
