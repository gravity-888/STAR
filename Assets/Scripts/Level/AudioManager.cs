using UnityEngine;

namespace TowardTheStars.Level
{
    // 오디오 seam(로드맵 3의 나머지 절반).
    //   코드 곳곳의 이벤트에서 정적 메서드(AudioManager.GateOpen() 등)를 호출한다.
    //   씬에 이 컴포넌트를 두고 클립 슬롯을 채우면 소리가 나고, 컴포넌트가 없거나 슬롯이 비면 **조용히 무시**(폴백).
    //   → 최종 오디오 교체(로드맵 7)는 이 슬롯만 채우면 되고 호출부(게임 코드) 변경이 없어야 한다.
    //
    // 사용법: 빈 GameObject에 이 컴포넌트를 붙이고 인스펙터에서 클립을 드래그. (프리팹 슬롯을 채우는 것과 같은 방식)
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        [Header("BGM (루프)")]
        public AudioClip bgmTitle;
        public AudioClip bgmPlay;
        public AudioClip bgmEnding;
        [Range(0f, 1f)] public float bgmVolume = 0.6f;

        [Header("SFX (일회성)")]
        public AudioClip sfxMirrorRotate;    // 거울 회전(Q/E)
        public AudioClip sfxGateOpen;        // 게이트 개방
        public AudioClip sfxStageTransition; // 스테이지 전환
        public AudioClip sfxJump;            // 점프
        public AudioClip sfxLand;            // 착지
        public AudioClip sfxLensPickup;      // 랜즈 줍기
        public AudioClip sfxLensMount;       // 랜즈 장착
        public AudioClip sfxLensUnmount;     // 랜즈 해제
        [Range(0f, 1f)] public float sfxVolume = 0.8f;

        AudioSource _bgm, _sfx;
        AudioClip _curBgm;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }   // 중복 방지(전환 간 유지)
            I = this;
            DontDestroyOnLoad(gameObject);
            _bgm = gameObject.AddComponent<AudioSource>(); _bgm.loop = true;  _bgm.playOnAwake = false;
            _sfx = gameObject.AddComponent<AudioSource>(); _sfx.loop = false; _sfx.playOnAwake = false;
        }

        void OnDestroy() { if (I == this) I = null; }

        // ---- 정적 진입점(어디서든 null-safe 호출) ----
        static void Play(AudioClip c) { if (I != null && c != null) I._sfx.PlayOneShot(c, I.sfxVolume); }

        public static void MirrorRotate()    => Play(I?.sfxMirrorRotate);
        public static void GateOpen()        => Play(I?.sfxGateOpen);
        public static void StageTransition() => Play(I?.sfxStageTransition);
        public static void Jump()            => Play(I?.sfxJump);
        public static void Land()            => Play(I?.sfxLand);
        public static void LensPickup()      => Play(I?.sfxLensPickup);
        public static void LensMount()       => Play(I?.sfxLensMount);
        public static void LensUnmount()     => Play(I?.sfxLensUnmount);

        public enum Bgm { None, Title, Play, Ending }

        public static void PlayBgm(Bgm which) { if (I != null) I.SetBgm(which); }

        void SetBgm(Bgm which)
        {
            AudioClip clip = which switch
            {
                Bgm.Title  => bgmTitle,
                Bgm.Play   => bgmPlay,
                Bgm.Ending => bgmEnding,
                _          => null,
            };
            if (clip == _curBgm) return;   // 같은 곡이면 끊지 않음
            _curBgm = clip;
            _bgm.Stop();
            if (clip != null) { _bgm.clip = clip; _bgm.volume = bgmVolume; _bgm.Play(); }
        }
    }
}
