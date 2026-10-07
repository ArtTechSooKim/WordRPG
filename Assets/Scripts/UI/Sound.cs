using System.Collections.Generic;
using UnityEngine;
using WordRPG.Game;

namespace WordRPG.UI
{
    public enum Music { None, Title, Meadow, Library, Battle, Boss, Forest }

    public enum Sfx
    {
        Click, Correct, Wrong, Hit, Critical, Heal, Shield, Fail, Faint, Encounter,
        Victory, Defeat, LevelUp, NewWord, Coin, Fountain, EvolveLight, Evolve, DexComplete, Door,
        GateOpen, Combo, Respawn
    }

    // 배경 음악·효과음 (Ninja Adventure 팩 → Resources/Audio/Music·Sfx, 파일 이름 = 열거형 이름 소문자).
    // 음량은 설정(GameSettings)을 따른다. 처음 소리를 낼 때 씬이 바뀌어도 남는 'Sound' 오브젝트를 만든다
    public static class Sound
    {
        private const float MusicBase = 0.6f;    // 음악은 효과음보다 조금 작게
        private const float MusicFadeIn = 0.6f;  // 징글이 끝난 뒤 음악이 다시 커지는 시간(초)

        private static AudioSource music, effects, pitched;
        private static Music current = Music.None;
        private static GameSettings settingsOverride;
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static float resumeAt = -1f; // 징글이 끝나 음악을 다시 틀 시각 (-1 = 멈춘 음악 없음)
        private static bool fadingIn;

        public static Music CurrentMusic => current;
        public static bool IsMusicPausedForJingle => resumeAt >= 0f;

        private static GameSettings Settings =>
            settingsOverride ?? (GameManager.Instance != null ? GameManager.Instance.Settings : null);

        private static float MusicVolume => (Settings?.MusicVolume ?? GameSettings.DefaultMusicVolume) * MusicBase;
        private static float SfxVolume => Settings?.SfxVolume ?? GameSettings.DefaultSfxVolume;

        // 설정 화면에서 음량을 바꾸면 바로 반영
        public static void ApplyVolumes(GameSettings settings)
        {
            if (settings != null) settingsOverride = settings;
            if (music != null && !fadingIn) music.volume = MusicVolume;
        }

        // 같은 곡이면 이어서, 다른 곡이면 처음부터. None = 멈춤
        public static void PlayMusic(Music track)
        {
            if (!Application.isPlaying || track == current || !Ensure()) return;
            current = track;
            resumeAt = -1f;
            fadingIn = false;
            var clip = track == Music.None ? null : Load("Audio/Music/" + track.ToString().ToLowerInvariant());
            music.Stop();
            music.clip = clip;
            music.volume = MusicVolume;
            if (clip != null) music.Play();
        }

        public static void Play(Sfx effect)
        {
            if (!Application.isPlaying || !Ensure()) return;
            var clip = Load("Audio/Sfx/" + effect.ToString().ToLowerInvariant());
            if (clip != null) effects.PlayOneShot(clip, SfxVolume);
        }

        // 음높이를 바꿔 재생 (콤보 단계가 오를수록 높게)
        public static void Play(Sfx effect, float pitch)
        {
            if (!Application.isPlaying || !Ensure()) return;
            var clip = Load("Audio/Sfx/" + effect.ToString().ToLowerInvariant());
            if (clip == null) return;
            pitched.pitch = pitch;
            pitched.PlayOneShot(clip, SfxVolume);
        }

        // 짧은 음악(징글): 나오는 동안 배경 음악을 멈췄다가 끝나면 서서히 다시 튼다 (새 단어 '발견!', 길이 열림)
        public static void PlayJingle(Sfx effect)
        {
            if (!Application.isPlaying || !Ensure()) return;
            var clip = Load("Audio/Sfx/" + effect.ToString().ToLowerInvariant());
            if (clip == null) return;
            effects.PlayOneShot(clip, SfxVolume);
            if (music.clip == null) return;
            music.Pause();
            fadingIn = false;
            resumeAt = Time.unscaledTime + clip.length;
        }

        // 'Sound' 오브젝트가 매 프레임 부른다: 징글이 끝났으면 음악을 다시 틀고 서서히 키운다
        private static void Tick()
        {
            if (music == null) return;
            if (resumeAt >= 0f && Time.unscaledTime >= resumeAt)
            {
                resumeAt = -1f;
                fadingIn = true;
                music.volume = 0f;
                music.UnPause();
            }
            if (fadingIn)
            {
                music.volume = Mathf.MoveTowards(music.volume, MusicVolume, MusicVolume * Time.unscaledDeltaTime / MusicFadeIn);
                if (music.volume >= MusicVolume) fadingIn = false;
            }
        }

        private static bool Ensure()
        {
            if (music != null && effects != null && pitched != null) return true;
            var go = new GameObject("Sound");
            Object.DontDestroyOnLoad(go);
            music = go.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            effects = go.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            pitched = go.AddComponent<AudioSource>();
            pitched.playOnAwake = false;
            go.AddComponent<SoundClock>().OnTick = Tick;
            current = Music.None;
            resumeAt = -1f;
            fadingIn = false;
            return true;
        }

        private static AudioClip Load(string path)
        {
            if (!Clips.TryGetValue(path, out var clip))
            {
                clip = Resources.Load<AudioClip>(path);
                Clips[path] = clip;
            }
            return clip;
        }
    }

    // 'Sound' 오브젝트에 붙어 징글이 끝난 뒤 음악을 다시 트는 시계
    internal class SoundClock : MonoBehaviour
    {
        public System.Action OnTick;

        private void Update() => OnTick?.Invoke();
    }
}
