using System.Collections.Generic;
using UnityEngine;
using WordRPG.Monsters;

namespace WordRPG.UI
{
    // 기술을 쓸 때 쓰는 쪽 카드의 몸짓 (#137): 달려가 덮침 / 제자리에서 마법진 / 없음
    public enum SkillMotion { None, Lunge, Cast }

    // 효과 그림만으로는 안 되는 연출 (BattleScreen.SkillFx가 그림)
    public enum SkillExtra
    {
        None,
        Letters,   // 방금 맞힌 단어의 글자가 맞은 카드 위로 쏟아짐
        Highlight, // 형광 한 줄이 적 줄을 가로질러 그어짐 (기술 한 번에 한 번)
        Rings      // 소리 고리가 세 겹으로 퍼짐
    }

    // 기술 연출 한 줄: 몸짓 · 맞은 카드 위 효과(색·크기) · 날아가는 그림 · 화면 번쩍 · 흔들림 · 특수 연출 ·
    // 빛 색(시전 마법진·회복 빛·보호막 막) · 회복 때 솟는 알갱이
    public sealed class SkillLook
    {
        public SkillMotion Motion = SkillMotion.None;
        public Fx Hit = Fx.Slash;
        public Color Tint = Color.white;
        public float HitScale = 1f;
        public Fx? Projectile;
        public Color Flash = Color.clear; // 알파 0 = 번쩍이지 않음. Linear 색공간이라 0.05~0.1만 돼도 화면 전체가 확 밝아 보임
        public float Shake;               // 맞은 카드가 흔들리는 폭(px)
        public SkillExtra Extra = SkillExtra.None;
        public Color Glow = Color.white;
        public Fx? Particle;              // 회복 알갱이 (null = 물방울)
    }

    // 기술 연출 표 (#137 사용자: "기술마다 다른 효과 · 회복과 보호막이 더 역동적으로" — Figma '기술 연출 (#137)').
    // SkillData.Fx(그림 종류)마다 한 줄 — 값만 담고 그리는 일은 BattleScreen.SkillFx.
    // 새 종류 = SkillFx 맨 뒤 + 여기 한 줄 (빠지면 SkillLooksTests가 잡음). Auto는 기술 종류·쓰는 쪽으로 정함
    public static class SkillLooks
    {
        private static readonly Color HealGreen = new Color(0.55f, 1f, 0.55f);
        private static readonly Color ShieldBlue = new Color(0.6f, 0.85f, 1f);
        private static readonly Color InkPurple = new Color(0.5f, 0.4f, 0.9f);
        private static readonly Color Neon = new Color(0.86f, 1f, 0.32f);

        private static readonly Dictionary<SkillFx, SkillLook> Table = new Dictionary<SkillFx, SkillLook>
        {
            [SkillFx.Slash] = new SkillLook { Motion = SkillMotion.Lunge, Hit = Fx.Slash, Shake = 6f },
            [SkillFx.Claw] = new SkillLook { Motion = SkillMotion.Lunge, Hit = Fx.Claw, Shake = 6f },
            [SkillFx.Heavy] = new SkillLook { Motion = SkillMotion.Lunge, Hit = Fx.SlashDouble, HitScale = 1.35f, Shake = 14f,
                Flash = new Color(1f, 1f, 1f, 0.05f) },
            [SkillFx.Thunder] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Thunder, HitScale = 1.3f, Shake = 10f,
                Flash = new Color(1f, 0.95f, 0.7f, 0.08f), Glow = new Color(1f, 0.85f, 0.4f) },
            [SkillFx.Ink] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Water, Tint = InkPurple, HitScale = 1.1f, Shake = 4f,
                Glow = new Color(0.6f, 0.5f, 1f) },
            [SkillFx.Water] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Water, HitScale = 1.1f, Shake = 4f, Glow = ShieldBlue },
            [SkillFx.Plant] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Plant, HitScale = 1.2f, Shake = 5f, Glow = HealGreen },
            [SkillFx.Whip] = new SkillLook { Motion = SkillMotion.Lunge, Hit = Fx.SlashCurved, Tint = new Color(1f, 0.6f, 0.72f), Shake = 6f },
            [SkillFx.Rock] = new SkillLook { Motion = SkillMotion.None, Hit = Fx.Rock, HitScale = 1.1f, Projectile = Fx.RockFly, Shake = 8f },
            [SkillFx.Light] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Boost, Tint = new Color(1f, 0.92f, 0.55f), HitScale = 1.2f,
                Flash = new Color(1f, 0.95f, 0.75f, 0.05f), Glow = new Color(1f, 0.9f, 0.5f) },
            [SkillFx.Letters] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Boost, Tint = new Color(1f, 0.85f, 0.4f), Shake = 5f,
                Extra = SkillExtra.Letters, Glow = new Color(1f, 0.85f, 0.4f) },
            [SkillFx.Highlight] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.SlashCurved, Tint = Neon, HitScale = 1.2f, Shake = 4f,
                Extra = SkillExtra.Highlight, Glow = Neon },
            [SkillFx.Sound] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Ring, Tint = new Color(0.55f, 0.95f, 0.8f), Shake = 6f,
                Extra = SkillExtra.Rings, Glow = new Color(0.55f, 0.95f, 0.8f) },
            [SkillFx.Spore] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.SmokeRing, Tint = new Color(0.8f, 0.65f, 1f), HitScale = 1.3f,
                Shake = 3f, Glow = new Color(0.8f, 0.65f, 1f) },
            [SkillFx.Swirl] = new SkillLook { Motion = SkillMotion.Lunge, Hit = Fx.Spirit, Tint = new Color(0.82f, 0.62f, 1f), HitScale = 1.2f,
                Shake = 6f },
            // 회복: 발밑 마법진 + 빛기둥 + 솟는 알갱이 + 카드가 빛남 + HP 막대가 차오름 (Hit = 카드 위에 더하는 효과)
            [SkillFx.HealLight] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Heal, Glow = new Color(0.7f, 1f, 0.55f), Particle = Fx.Spark },
            [SkillFx.HealLeaf] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Heal, Glow = HealGreen, Particle = Fx.Leaf },
            [SkillFx.HealWater] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.WaterPillar, Glow = new Color(0.5f, 0.95f, 1f) },
            [SkillFx.Drain] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Spirit, Tint = new Color(0.8f, 0.5f, 1f),
                Glow = new Color(0.8f, 0.5f, 1f), Particle = Fx.Spark },
            // 보호막: 막이 튕기며 덮음 + 효과 그림 (Glow = 막·남은 동안 테두리 색)
            [SkillFx.GuardBubble] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Shield, Glow = ShieldBlue },
            [SkillFx.GuardEcho] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Ring, Tint = new Color(0.55f, 0.95f, 0.85f),
                Extra = SkillExtra.Rings, Glow = new Color(0.55f, 0.95f, 0.85f) },
            [SkillFx.GuardBook] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.ShieldGold, Glow = new Color(1f, 0.85f, 0.45f) },
            [SkillFx.GuardTime] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Spirit, Tint = new Color(1f, 0.95f, 0.8f),
                Glow = new Color(0.95f, 0.92f, 0.75f) },
            [SkillFx.GuardSeed] = new SkillLook { Motion = SkillMotion.Cast, Hit = Fx.Circle, Tint = HealGreen, Glow = HealGreen },
        };

        public static bool Has(SkillFx fx) => fx == SkillFx.Auto || Table.ContainsKey(fx);

        // Auto면 기술 종류·쓰는 쪽으로 (공격: 아군 = 휘두르기, 적 = 할퀴기 · 회복 = 빛 · 보호막 = 물방울 막)
        public static SkillFx Resolve(SkillData skill, bool playerSide)
        {
            var fx = skill != null ? skill.Fx : SkillFx.Auto;
            if (fx != SkillFx.Auto && Table.ContainsKey(fx)) return fx;
            var kind = skill != null ? skill.Kind : SkillKind.Damage;
            return kind == SkillKind.Heal ? SkillFx.HealLight
                : kind == SkillKind.Guard ? SkillFx.GuardBubble
                : playerSide ? SkillFx.Slash : SkillFx.Claw;
        }

        public static SkillLook Of(SkillData skill, bool playerSide) => Table[Resolve(skill, playerSide)];
        public static SkillLook Of(SkillFx fx) => Table[fx];
    }
}
