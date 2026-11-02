using System;
using UnityEngine;

namespace WordRPG.Game
{
    // 기기별 설정 (소리·진동·전투 강도). 세이브 파일과 따로 저장해서, 저장 데이터를 지워도 설정은 남는다
    [Serializable]
    public class GameSettings
    {
        public const float DefaultMusicVolume = 0.75f;
        public const float DefaultSfxVolume = 0.5f;

        [SerializeField] private float musicVolume = DefaultMusicVolume;
        [SerializeField] private float sfxVolume = DefaultSfxVolume;
        [SerializeField] private bool vibration = true;
        [SerializeField] private int battleIntensity; // 0 = 공격할 때마다 고름 (예전 설정도)

        // 소리는 아직 없어서 값만 저장해 둔다 (소리를 넣을 때 연결)
        public float MusicVolume
        {
            get => musicVolume;
            set => musicVolume = Volume(value, DefaultMusicVolume);
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set => sfxVolume = Volume(value, DefaultSfxVolume);
        }

        // 오답일 때 짧게 진동 (휴대폰에서만)
        public bool Vibration
        {
            get => vibration;
            set => vibration = value;
        }

        // 전투 강도 (#147 사용자): 0 = 공격할 때마다 고름, 1 이상 = 고르지 않고 늘 그 강도(연속으로 맞힐 단어 수)로 공격
        public int BattleIntensity
        {
            get => battleIntensity;
            set => battleIntensity = Mathf.Max(0, value);
        }

        public string ToJson() => JsonUtility.ToJson(this);

        // 비었거나 깨진 값이면 기본값
        public static GameSettings FromJson(string json)
        {
            var settings = new GameSettings();
            if (string.IsNullOrEmpty(json)) return settings;
            try
            {
                JsonUtility.FromJsonOverwrite(json, settings);
            }
            catch (ArgumentException)
            {
                return new GameSettings();
            }
            settings.musicVolume = Volume(settings.musicVolume, DefaultMusicVolume);
            settings.sfxVolume = Volume(settings.sfxVolume, DefaultSfxVolume);
            settings.battleIntensity = Mathf.Max(0, settings.battleIntensity);
            return settings;
        }

        private static float Volume(float value, float fallback) => float.IsNaN(value) ? fallback : Mathf.Clamp01(value);
    }
}
