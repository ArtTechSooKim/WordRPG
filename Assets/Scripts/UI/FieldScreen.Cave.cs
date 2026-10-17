using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 필드 > 1.2 어두운 밀림 동굴 (#112, GDD 9-7): 어둠(주인공 빛 · 밝힌 반딧돌 · 쉼터 · 지나간 곳) · 어둠 속 몬스터는 빨간 눈만 · 글자 반딧돌
    public partial class FieldScreen
    {
        private FieldDarkness darkness;         // 동굴 어둠 한 장 (어두운 지역에서만)
        // 꺼진 반딧돌이 어둠 속에서도 아주 희미하게 깜빡이는 청록 빛 — 찾아갈 수 있게 (Figma #111). 빛 안에 들어오면 돌 그림이 보이므로 끔
        private readonly List<(Vector2Int cell, SpriteRenderer glow)> stoneGlimmers = new List<(Vector2Int, SpriteRenderer)>();
        private static readonly Color GlimmerColor = new Color(0.55f, 0.9f, 0.95f, 0f);

        public bool HasDarkness => darkness != null && darkness.IsShowing;     // 테스트용
        public int DarknessRepaints => darkness != null ? darkness.Repaints : 0;
        public float BrightnessAt(Vector2Int cell) => darkness != null ? darkness.Brightness(CellCenter(cell)) : 1f;
        public int RoamerEyesShown => roamerViews != null ? roamerViews.EyesShown : 0;
        public int StoneGlimmers => stoneGlimmers.Count;

        // 지역에 들어올 때 (EnterArea)
        private void BuildDarkness()
        {
            darkness.Build(area.Map, area.Theme, cell => GlowStones.IsLit(area, session.World, cell),
                cell => session.World.IsExplored(area.AreaId, cell));
            BuildGlimmers();
            UpdateDarkness();
        }

        private void BuildGlimmers()
        {
            foreach (var (_, glow) in stoneGlimmers)
                if (glow != null) Destroy(glow.gameObject);
            stoneGlimmers.Clear();
            if (!darkness.IsShowing) return;
            foreach (var stone in GlowStones.Stones(area))
            {
                if (GlowStones.IsLit(area, session.World, stone)) continue;
                var go = new GameObject("StoneGlimmer", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                go.transform.position = CellCenter(stone) + new Vector3(0f, 0.05f, 0f);
                go.transform.localScale = Vector3.one * 0.7f;
                var glow = go.GetComponent<SpriteRenderer>();
                glow.sprite = UiKit.GlowSprite();
                glow.color = GlimmerColor;
                glow.sortingOrder = 15; // 어둠(14) 위
                stoneGlimmers.Add((stone, glow));
            }
        }

        // 매 프레임: 주인공 빛을 따라 어둠을 칠하고, 빛 밖 몬스터는 눈만 (#111 사용자: 적의 눈은 모두 빨간색)
        private void UpdateDarkness()
        {
            if (darkness == null || !darkness.IsShowing)
            {
                roamerViews?.UpdateEyes(null, Time.unscaledDeltaTime);
                return;
            }
            darkness.Tick(player.position, session.PartyCompanion != null);
            roamerViews?.UpdateEyes(world => darkness.Brightness(world) < CaveLight.EyesBelow, Time.unscaledDeltaTime);
            float t = Time.unscaledTime;
            foreach (var (cell, glow) in stoneGlimmers)
            {
                float pulse = 0.2f + 0.16f * Mathf.Sin(t * 2.2f + cell.x * 1.7f); // 은은하게 깜빡 (#95 사용자: 효과는 은은하게)
                float hidden = Mathf.Clamp01((darkness.Brightness(CellCenter(cell)) - 0.25f) / 0.4f); // 빛 안이면 사라짐
                var c = GlimmerColor;
                c.a = pulse * (1f - hidden);
                glow.color = c;
            }
        }

        // 새 칸을 지나가 탐험 기록이 늘었을 때 — 지나간 곳이 희미하게 남게
        private void RefreshDarkness() => darkness?.Refresh();

        // 글자 반딧돌 [확인] — 단어 문제로 밝히는 창은 다음 단계(#113)에서. 지금은 혼잣말만
        private void TouchGlowStone(Vector2Int cell)
        {
            if (GlowStones.IsLit(area, session.World, cell))
            {
                bubble.Say("반딧돌이 환하게 빛나고 있다", 2.5f);
                return;
            }
            bubble.Say("희미하게 깜빡이는 돌이다…\n글자가 떠오를 것 같다", 3f);
        }
    }
}
