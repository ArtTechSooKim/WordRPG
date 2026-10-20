using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 바닥 안개 색 (#103): 옅은 곳 · 그늘 · 가장 짙은 곳의 불투명도
    public readonly struct FogLook
    {
        public readonly Color32 Light, Shade;
        public readonly float Alpha;

        public FogLook(Color32 light, Color32 shade, float alpha)
        {
            Light = light;
            Shade = shade;
            Alpha = alpha;
        }
    }

    // 테마 하나의 화면 모습 (#118 테마 표): 값만 담는다 — 음악을 트는 일·안개를 그리는 일은 Sound·FieldFog·FieldDarkness·BattleScreen이 여기서 값을 찾아서 한다
    public sealed class ThemeLook
    {
        public FieldTheme Theme { get; }
        public Music Music { get; }           // 필드 배경 음악
        public Color Outside { get; }         // 맵 밖(카메라 배경) 색
        public FieldTile BattleFloor { get; } // 전투 배경 바닥 칸 (벽은 늘 그 테마의 Wall 그림)
        public FogLook? Fog { get; }          // 몬스터 칸을 덮는 바닥 안개 (없으면 null — FieldFog)
        public bool Dark { get; }             // 어두운 지역 (FieldDarkness — 빛 규칙 CaveLight)

        public ThemeLook(FieldTheme theme, Music music, Color outside, FieldTile battleFloor, FogLook? fog = null, bool dark = false)
        {
            Theme = theme;
            Music = music;
            Outside = outside;
            BattleFloor = battleFloor;
            Fog = fog;
            Dark = dark;
        }
    }

    // 테마 표 (#118): 테마마다 한 줄. 예전엔 음악·맵 밖 색·전투 배경·안개·어둠을 다섯 곳이 저마다 테마를 갈라 적었음.
    // 칸마다 다른 색은 칸 종류 표라 따로 둔다: 미니맵 색 = MinimapArt, 먼지 색 = FieldArt.DustColor.
    // 새 테마 (예: 2.0 사막): FieldTheme 맨 뒤 → 여기 한 줄 → MinimapArt·DustColor 표 한 줄씩 → 그림 Tiles/{테마}_{종류}.png · 음악 파일
    //   (빠지면 ThemeTableTests·AudioArtTests가 잡음 — 표에 없으면 초원 모습으로 조용히 보이던 것)
    public static class ThemeLooks
    {
        private static readonly ThemeLook[] Table =
        {
            new ThemeLook(FieldTheme.Meadow, Music.Meadow, new Color(0.10f, 0.22f, 0.12f), FieldTile.Grass),
            new ThemeLook(FieldTheme.Library, Music.Library, new Color(0.12f, 0.08f, 0.06f), FieldTile.Floor, // 서고 전투 바닥 = 돌바닥
                fog: new FogLook(new Color32(206, 218, 232, 255), new Color32(150, 168, 192, 255), 0.5f)),   // 옅은 푸른 회색 바닥 안개 (#103)
            new ThemeLook(FieldTheme.Forest, Music.Forest, new Color(0.05f, 0.14f, 0.08f), FieldTile.Grass),   // 숲 전투 바닥 = 고사리
            new ThemeLook(FieldTheme.Jungle, Music.Jungle, new Color(0.03f, 0.12f, 0.07f), FieldTile.Grass),
            new ThemeLook(FieldTheme.Cave, Music.Cave, new Color(0.02f, 0.02f, 0.04f), FieldTile.Grass, dark: true), // 맵 밖은 캄캄 (#112)
        };

        private static readonly Dictionary<FieldTheme, ThemeLook> ByTheme = Build();

        public static IReadOnlyList<ThemeLook> All => Table;

        // 표에 없는 테마는 초원 모습 (게임이 멈추지 않게) — 테스트는 모든 테마가 표에 있는지 따로 본다
        public static ThemeLook Of(FieldTheme theme) => ByTheme.TryGetValue(theme, out var look) ? look : ByTheme[FieldTheme.Meadow];

        public static bool Has(FieldTheme theme) => ByTheme.ContainsKey(theme);

        private static Dictionary<FieldTheme, ThemeLook> Build()
        {
            var byTheme = new Dictionary<FieldTheme, ThemeLook>();
            foreach (var look in Table)
                if (!byTheme.ContainsKey(look.Theme)) byTheme[look.Theme] = look;
            return byTheme;
        }
    }
}
