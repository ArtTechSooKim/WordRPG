using System;
using System.Text;
using UnityEngine;

namespace WordRPG.Heroes
{
    // 주인공 모습 하나 (#124 사용자: 처음에 8명 중에서 고름 — 모습만 다르고 능력은 같음).
    // 그림 = Resources/Art/NinjaAdventure/Player/{lookId}_sheet.png(걷기 시트) · {lookId}_face.png(얼굴, 고르는 화면).
    // lookId는 세이브에 저장 — 출시 뒤 바꾸지 말 것 (HeroData.looks, 첫 번째가 기본)
    [Serializable]
    public class HeroLook
    {
        public const string Fallback = "boy"; // 1.0부터의 주인공

        [SerializeField] private string lookId;
        [SerializeField] private string displayName;

        public HeroLook(string lookId, string displayName)
        {
            this.lookId = lookId;
            this.displayName = displayName;
        }

        public string LookId => lookId;
        public string DisplayName => displayName;
    }

    // 주인공 이름 (#124 사용자: 이름을 물어 '주인공' 글자를 대신). 한글·영어·숫자만, 앞뒤 빈칸을 빼고 최대 MaxLength글자.
    // 글꼴에 없는 글자(한자·이모지 등)는 빼서 화면에 네모가 생기지 않게 한다
    public static class HeroNames
    {
        public const int MaxLength = 8;

        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new StringBuilder();
            foreach (char c in raw.Trim())
            {
                if (sb.Length >= MaxLength) break;
                if (c == ' ')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(c); // 빈칸은 한 칸만
                }
                else if (IsAllowed(c)) sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        public static bool IsValid(string raw) => Clean(raw).Length > 0;

        // 완성된 한글 · 영어 · 숫자 (입력하는 동안의 자모는 Clean에서 빠짐)
        private static bool IsAllowed(char c) =>
            (c >= '가' && c <= '힣') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    }
}
