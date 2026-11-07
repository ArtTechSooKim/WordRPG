using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.UI
{
    public enum Music
    {
        None, Title, Meadow, Library, Battle, Boss, Forest, Jungle, Cave, Intro, HeroSelect,
        // 지역마다 다른 전투곡·보스곡 (#166 — 어느 지역이 어느 곡인지는 테마 표 ThemeLooks). 파일은 Licensed 폴더에만 있음(깃 제외) →
        // 없으면 Battle·Boss로 대신 남 (Sound.FallbackOf). 이름은 Battle…·Boss…로 시작할 것. 곡 다듬기 = Tools/prepare_music.py
        BattleMeadow, BattleLibrary, BattleForest, BattleJungle, BattleCave, BossLibrary, BossForest, BossJungle, BossCave, BattleCave2
    }

    public enum Sfx
    {
        Click, Correct, Wrong, Hit, Critical, Heal, Shield, Fail, Faint, Encounter,
        Victory, Defeat, LevelUp, NewWord, Coin, Fountain, EvolveLight, Evolve, DexComplete, Door,
        GateOpen, Combo, Respawn, Flee, Preempt,
        // 기술마다 다른 소리 (#164 — 표 SkillLooks, 파일은 Tools/make_skill_sfx.py가 만듦)
        SkillSlash, SkillClaw, SkillHeavy, SkillThunder, SkillInk, SkillWater, SkillPlant, SkillWhip, SkillRock, SkillLight,
        SkillLetters, SkillHighlight, SkillSound, SkillSpore, SkillSwirl, SkillHealLeaf, SkillHealWater, SkillDrain,
        SkillGuardEcho, SkillGuardBook, SkillGuardTime, SkillGuardSeed, SkillHealLight,
        VineGrow // 덩굴 다리가 놓임 (#232 — Tools/make_skill_sfx.py)
    }

    // 지역 환경음 (#193): BGM 밑에 깔리는 소리. 어느 지역이 어느 소리인지는 테마 표 ThemeLooks.
    // 파일 = Resources/Audio/Ambience/{소문자}, 다듬기 = Tools/prepare_ambience.py (팩 + CC0 녹음). 초원은 없음 (사용자)
    public enum Ambience
    {
        None, Library, Forest, Jungle, Cave, Raft
    }

    // 배경 음악·효과음 (Ninja Adventure 팩 → Resources/Audio/Music·Sfx, 파일 이름 = 열거형 이름 소문자).
    // 유료 라이선스 소리(Envato Elements 등)는 Resources/Audio/Licensed/{같은 이름} — 있으면 그걸 먼저 쓴다 (음악도 #133).
    // 그 폴더는 공개 저장소에 올리지 않으므로(재배포 금지) 새로 받은 저장소에서는 팩 소리로 대신 난다 (#66)
    // 음량은 설정(GameSettings)을 따른다. 처음 소리를 낼 때 씬이 바뀌어도 남는 'Sound' 오브젝트를 만든다
    public static class Sound
    {
        private const float MusicBase = 0.6f;    // 음악은 효과음보다 조금 작게
        private const float MusicFadeIn = 0.6f;  // 징글이 끝난 뒤 음악이 다시 커지는 시간(초)
        private const float AmbienceBase = 0.6f; // 효과음 설정 × 0.6 — 파일 음량과 합쳐 BGM보다 약 11dB 작게 들림 (#193)
        private const float AmbienceFade = 0.8f; // 환경음이 커지고 줄어드는 시간(초)

        private static AudioSource music, effects, pitched, ambience;
        private static Music current = Music.None;
        private static Ambience wantedAmbience = Ambience.None, playingAmbience = Ambience.None;
        private static GameSettings settingsOverride;
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static float resumeAt = -1f; // 징글이 끝나 음악을 다시 틀 시각 (-1 = 멈춘 음악 없음)
        private static bool fadingIn;
        private static int battlesStarted; // 지역에 전투곡이 여러 개면 전투마다 번갈아 (#166)

        public static Music CurrentMusic => current;
        public static bool IsMusicPausedForJingle => resumeAt >= 0f;
        public static Ambience WantedAmbience => wantedAmbience;
        public static Ambience PlayingAmbience => playingAmbience;

        private static GameSettings Settings =>
            settingsOverride ?? (GameManager.Instance != null ? GameManager.Instance.Settings : null);

        private static float MusicVolume => (Settings?.MusicVolume ?? GameSettings.DefaultMusicVolume) * MusicBase;
        private static float SfxVolume => Settings?.SfxVolume ?? GameSettings.DefaultSfxVolume;
        private static float AmbienceVolume => SfxVolume * AmbienceBase;

        // 설정 화면에서 음량을 바꾸면 바로 반영
        public static void ApplyVolumes(GameSettings settings)
        {
            if (settings != null) settingsOverride = settings;
            if (music != null && !fadingIn) music.volume = MusicVolume;
        }

        // 전투에 들어갈 때 (#166): 그 지역의 전투곡 또는 보스곡
        public static void PlayBattleMusic(FieldTheme theme, bool boss = false) =>
            PlayMusic(ThemeLooks.Of(theme).BattleTrack(boss, boss ? 0 : battlesStarted++));

        // 파일이 Licensed 폴더에만 있는 곡을 못 찾으면 대신 틀 곡 (새로 받은 저장소 등). None = 대신 틀 곡 없음(팩에 있는 곡)
        public static Music FallbackOf(Music track)
        {
            string name = track.ToString();
            if (track != Music.Battle && name.StartsWith("Battle")) return Music.Battle;
            if (track != Music.Boss && name.StartsWith("Boss")) return Music.Boss;
            return Music.None;
        }

        // 같은 곡이면 이어서, 다른 곡이면 처음부터. None = 멈춤
        public static void PlayMusic(Music track)
        {
            if (!Application.isPlaying || track == current || !Ensure()) return;
            current = track;
            resumeAt = -1f;
            fadingIn = false;
            var clip = track == Music.None ? null : LoadMusic(track);
            music.Stop();
            music.clip = clip;
            music.volume = MusicVolume;
            if (clip != null) music.Play();
        }

        // 환경음 (#193): 바꾸면 줄어든 뒤 새 소리가 커진다. 같은 소리면 아무 일도 없어서 매 프레임 불러도 됨
        public static void SetAmbience(Ambience wanted)
        {
            if (!Application.isPlaying) return;
            if (wanted == Ambience.None && ambience == null) // 끌 것이 없음 — 앱을 끄는 중에 소리 오브젝트를 새로 만들지 않게
            {
                wantedAmbience = wanted;
                return;
            }
            if (Ensure()) wantedAmbience = wanted;
        }

        // 소리 기록 (#119, 테스트용): 효과음을 낼 때마다 (효과음, 음높이 — 1 = 기본). 평소엔 null
        public static System.Action<Sfx, float> Recorder;

        public static void Play(Sfx effect)
        {
            Recorder?.Invoke(effect, 1f);
            if (!Application.isPlaying || !Ensure()) return;
            var clip = LoadSfx(effect);
            if (clip != null) effects.PlayOneShot(clip, SfxVolume);
        }

        // 음높이를 바꿔 재생 (콤보 단계가 오를수록 높게)
        public static void Play(Sfx effect, float pitch)
        {
            Recorder?.Invoke(effect, pitch);
            if (!Application.isPlaying || !Ensure()) return;
            var clip = LoadSfx(effect);
            if (clip == null) return;
            pitched.pitch = pitch;
            pitched.PlayOneShot(clip, SfxVolume);
        }

        // 짧은 음악(징글): 나오는 동안 배경 음악을 멈췄다가 끝나면 서서히 다시 튼다 (보스를 이겨 길이 열림)
        public static void PlayJingle(Sfx effect)
        {
            if (!Application.isPlaying || !Ensure()) return;
            var clip = LoadSfx(effect);
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
            TickAmbience();
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

        private static void TickAmbience()
        {
            float step = Mathf.Max(AmbienceVolume, 0.01f) * Time.unscaledDeltaTime / AmbienceFade;
            if (playingAmbience == wantedAmbience)
            {
                if (ambience.clip != null) ambience.volume = Mathf.MoveTowards(ambience.volume, AmbienceVolume, step);
                return;
            }
            ambience.volume = Mathf.MoveTowards(ambience.volume, 0f, step);
            if (ambience.volume > 0f && ambience.clip != null) return;
            playingAmbience = wantedAmbience;
            ambience.Stop();
            ambience.clip = wantedAmbience == Ambience.None ? null : Load("Audio/Ambience/" + wantedAmbience.ToString().ToLowerInvariant());
            ambience.volume = 0f;
            if (ambience.clip != null) ambience.Play();
        }

        private static bool Ensure()
        {
            if (music != null && effects != null && pitched != null && ambience != null) return true;
            var go = new GameObject("Sound");
            Object.DontDestroyOnLoad(go);
            music = go.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            effects = go.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            pitched = go.AddComponent<AudioSource>();
            pitched.playOnAwake = false;
            ambience = go.AddComponent<AudioSource>();
            ambience.loop = true;
            ambience.playOnAwake = false;
            go.AddComponent<SoundClock>().OnTick = Tick;
            current = Music.None;
            wantedAmbience = playingAmbience = Ambience.None;
            resumeAt = -1f;
            fadingIn = false;
            return true;
        }

        // 찾는 순서: 라이선스 폴더 → 팩 폴더
        public static string[] CandidatePaths(string folder, string name) =>
            new[] { "Audio/Licensed/" + name, $"Audio/{folder}/" + name };

        private static AudioClip LoadMusic(Music track)
        {
            var clip = LoadFirst("Music", track.ToString().ToLowerInvariant());
            var fallback = FallbackOf(track);
            return clip == null && fallback != Music.None ? LoadFirst("Music", fallback.ToString().ToLowerInvariant()) : clip;
        }

        private static AudioClip LoadSfx(Sfx effect) => LoadFirst("Sfx", effect.ToString().ToLowerInvariant());

        private static AudioClip LoadFirst(string folder, string name)
        {
            foreach (var path in CandidatePaths(folder, name))
            {
                var clip = Load(path);
                if (clip != null) return clip;
            }
            return null;
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
