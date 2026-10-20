using System;
using System.Collections.Generic;

namespace WordRPG.Field
{
    // 칸 종류의 성질 (#116 맵 글자 표) — 한 칸 종류에 여러 개가 붙는다
    [Flags]
    public enum TileTraits
    {
        None = 0,
        Walk = 1 << 0,          // 걸을 수 있음 (FieldMap.IsWalkable)
        Roam = 1 << 1,          // 보이는 몬스터도 걷는 땅 (RoamingMonsters)
        Use = 1 << 2,           // 걸을 수는 없고 옆에서 [확인] (FieldMap.IsInteractive)
        NameTag = 1 << 3,       // 2칸 안이면 이름표 (FieldInteraction.HasNameTag)
        DoneLook = 1 << 4,      // 끝난 모습이 따로 있음 — 연 상자·당긴 레버… (CellState.IsDone, 그림 _done, 미니맵)
        OpensWhenDone = 1 << 5, // 끝나면 지나갈 수 있음 — 쓰러뜨린 보스 자리·연 문·놓인 다리 (CellState.IsPassable)
        Camp = 1 << 6           // 쉼터 물건 — 동굴에서 빛을 내고(CaveLight) 꾸밈이 피해 감(FieldDecor)
    }

    // 칸 종류 하나의 정의: 맵 글자 · 성질 · 이름표 기본 글자 · 쓰는 테마 (게임 규칙 쪽 데이터만).
    // 화면에 어떻게 보이는지(미니맵 색·먼지 색·그림)는 UI의 표(MinimapArt·FieldArt·PlaceholderArt)가 칸 종류로 찾는다
    public sealed class TileDefinition
    {
        private readonly FieldTheme[] themes; // null = 모든 테마

        public FieldTile Tile { get; }
        public string Symbols { get; }  // 맵 글자 (여럿이면 모두 이 칸 — 길 '.' + 시작 'P', 출입구 'D' 'O', 얕은 강 '>' '<' '^' 'v')
        public TileTraits Traits { get; }
        public string Label { get; }    // 이름표 기본 글자 (상점·보스·사전·열쇠·출입구처럼 지역 데이터로 정해지면 FieldArea.LandmarkName이 먼저)

        public bool Walkable => Has(TileTraits.Walk);
        public bool RoamerWalkable => Has(TileTraits.Roam);
        public bool Interactive => Has(TileTraits.Use);
        public bool HasNameTag => Has(TileTraits.NameTag);
        public bool HasDoneLook => Has(TileTraits.DoneLook);
        public bool OpensWhenDone => Has(TileTraits.OpensWhenDone);
        public bool IsCamp => Has(TileTraits.Camp);

        public TileDefinition(FieldTile tile, string symbols, TileTraits traits, string label = null, FieldTheme[] themes = null)
        {
            Tile = tile;
            Symbols = symbols ?? "";
            Traits = traits;
            Label = label;
            this.themes = themes;
        }

        public bool Has(TileTraits trait) => (Traits & trait) == trait;

        // 그 테마의 맵에 쓸 수 있는지 — 그림(Tiles/{테마}_{종류}.png)도 이대로 있어야 함 (AudioArtTests)
        public bool SupportsTheme(FieldTheme theme) => themes == null || Array.IndexOf(themes, theme) >= 0;
    }

    // 맵 글자 표 (#116): 칸 종류마다 한 줄. 예전엔 같은 칸을 맵 읽기·걷기·[확인]·이름표·끝난 모습·테마가 저마다 따로 적어서
    // 새 칸을 만들 때 빠뜨리기 쉬웠음 → 규칙은 여기 한 곳, 모습은 UI 쪽 표.
    // 새 칸 종류: FieldTile 맨 뒤에 더하고 → 여기 한 줄 → 그림 Tiles/{테마}_{종류}.png → 미니맵 색(MinimapArt) · 임시 그림(PlaceholderArt)
    //   → [확인]이면 FieldScreen.Interact, 끝난 모습이면 CellState.IsDone에 상태 (빠진 것은 FieldTilesTests·AudioArtTests가 잡음)
    public static class FieldTiles
    {
        public const char StartSymbol = 'P';     // 시작 위치 (칸은 길)
        public const char CaveMouthSymbol = 'O'; // 그림만 큰 동굴 입구인 출입구 (#115, FieldMap.IsCaveMouth)

        private const TileTraits None = TileTraits.None, Walk = TileTraits.Walk, Roam = TileTraits.Roam, Use = TileTraits.Use,
            Tag = TileTraits.NameTag, Done = TileTraits.DoneLook, Opens = TileTraits.OpensWhenDone, Camp = TileTraits.Camp;

        private static readonly FieldTheme[] Jungle = { FieldTheme.Jungle };
        private static readonly FieldTheme[] JungleAndCave = { FieldTheme.Jungle, FieldTheme.Cave };
        private static readonly FieldTheme[] Cave = { FieldTheme.Cave };

        private static readonly TileDefinition[] Table =
        {
            //                  칸 종류              글자     성질                          이름표 기본 글자   쓰는 테마 (없으면 전부)
            new TileDefinition(FieldTile.Floor,     ".P",   Walk | Roam),
            new TileDefinition(FieldTile.Grass,     ",",    Walk | Roam),                  // 풀숲 — 조우
            new TileDefinition(FieldTile.Wall,      "#",    None),
            new TileDefinition(FieldTile.Water,     "~",    None),
            new TileDefinition(FieldTile.Fountain,  "F",    Use | Tag | Camp,              "회복의 샘"),
            new TileDefinition(FieldTile.Chest,     "C",    Use | Done),                   // 상자는 모양으로 알 수 있어 이름표 없음
            new TileDefinition(FieldTile.Altar,     "E",    Use | Tag | Camp,              "성유물 제단"),
            new TileDefinition(FieldTile.Shop,      "S",    Use | Tag | Camp,              "상점"),
            new TileDefinition(FieldTile.Door,      "DO",   Walk | Tag),                   // 이름표 = 도착 지역 이름
            new TileDefinition(FieldTile.Boss,      "B",    Use | Tag | Done | Opens),     // 이름표 = 보스 이름
            new TileDefinition(FieldTile.Lawn,      ":",    Walk | Roam),                  // 잔디 — 조우 없음
            new TileDefinition(FieldTile.Lectern,   "L",    Use | Tag | Done | Camp,       "빈 받침대"),
            new TileDefinition(FieldTile.Gate,      "G",    Use | Tag | Done | Opens,      "보스 방 문"),
            new TileDefinition(FieldTile.Key,       "K",    Use | Tag | Done,              "열쇠"),
            new TileDefinition(FieldTile.Stream,    "><^v", Walk | Roam,                   null,          Jungle),
            new TileDefinition(FieldTile.Dock,      "J",    Walk | Tag,                    "나루터",      Jungle),
            new TileDefinition(FieldTile.Lever,     "Y",    Use | Tag | Done,              "레버",        JungleAndCave),
            new TileDefinition(FieldTile.Bridge,    "H",    Done | Opens,                  null,          JungleAndCave),
            new TileDefinition(FieldTile.RuinWall,  "U",    None,                          null,          Jungle),
            new TileDefinition(FieldTile.RuinFloor, ";",    Walk,                          null,          Jungle), // 보이는 몬스터는 안 들어옴
            new TileDefinition(FieldTile.Tablet,    "T",    Use | Tag | Done,              "단어 석판",   Jungle),
            new TileDefinition(FieldTile.Shrine,    "A",    Use | Tag | Done,              "유적 제단",   Jungle),
            new TileDefinition(FieldTile.Decor,     "Q",    None,                          null,          JungleAndCave),
            new TileDefinition(FieldTile.GlowStone, "I",    Use | Tag | Done,              "글자 반딧돌", Cave),
        };

        private static readonly TileDefinition[] ByTile = BuildByTile();
        private static readonly Dictionary<char, FieldTile> BySymbol = BuildBySymbol();

        public static IReadOnlyList<TileDefinition> All => Table;

        public static TileDefinition Of(FieldTile tile)
        {
            int index = (int)tile;
            var definition = index >= 0 && index < ByTile.Length ? ByTile[index] : null;
            return definition ?? throw new InvalidOperationException($"FieldTiles 표에 {tile}이(가) 없습니다");
        }

        public static bool TryParse(char symbol, out FieldTile tile) => BySymbol.TryGetValue(symbol, out tile);

        // 그 성질이 있는 칸 종류 전부 (예: 쉼터 물건)
        public static FieldTile[] With(TileTraits trait)
        {
            var tiles = new List<FieldTile>();
            foreach (var definition in Table)
                if (definition.Has(trait)) tiles.Add(definition.Tile);
            return tiles.ToArray();
        }

        // 얕은 강 글자의 물살 방향
        public static Direction StreamDirection(char symbol) =>
            symbol == '>' ? Direction.Right : symbol == '<' ? Direction.Left : symbol == '^' ? Direction.Up : Direction.Down;

        private static TileDefinition[] BuildByTile()
        {
            var byTile = new TileDefinition[Enum.GetValues(typeof(FieldTile)).Length];
            foreach (var definition in Table)
                if ((int)definition.Tile < byTile.Length) byTile[(int)definition.Tile] = definition;
            return byTile;
        }

        // 같은 글자가 두 줄에 있으면 앞줄이 이김 — 표가 맞는지는 FieldTilesTests가 따로 검사
        private static Dictionary<char, FieldTile> BuildBySymbol()
        {
            var bySymbol = new Dictionary<char, FieldTile>();
            foreach (var definition in Table)
            foreach (char symbol in definition.Symbols)
                if (!bySymbol.ContainsKey(symbol)) bySymbol[symbol] = definition.Tile;
            return bySymbol;
        }
    }
}
