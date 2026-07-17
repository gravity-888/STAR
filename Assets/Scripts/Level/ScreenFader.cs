using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TowardTheStars.Level
{
    // 전체 화면 검은 오버레이로 페이드 인/아웃(스테이지 전환용).
    // 스스로 ScreenSpaceOverlay Canvas + 화면을 꽉 채우는 Image를 생성한다.
    // MapLoader 바깥의 독립 오브젝트라 레벨 Clear()에 지워지지 않는다.
    public class ScreenFader : MonoBehaviour
    {
        public float duration = 0.4f;
        Image _img;

        public static ScreenFader Create()
        {
            var go = new GameObject("ScreenFader");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;   // 항상 최상단

            var imgGo = new GameObject("fade");
            imgGo.transform.SetParent(go.transform, false);
            var img = imgGo.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(0f, 0f, 0f, 0f);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;   // 화면 네 귀퉁이에 스트레치
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var f = go.AddComponent<ScreenFader>();
            f._img = img;
            return f;
        }

        public void SetAlpha(float a)
        {
            if (_img != null) _img.color = new Color(0f, 0f, 0f, a);
        }

        // from→to 알파로 duration 동안 보간.
        public IEnumerator Fade(float from, float to)
        {
            SetAlpha(from);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetAlpha(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetAlpha(to);
        }
    }
}
