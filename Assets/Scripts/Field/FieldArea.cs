using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Field
{
    [Serializable]
    public class ChestContent
    {
        [SerializeField] private ItemData item;
        [SerializeField] private int count = 1;
        [SerializeField] private int gold;
        [Tooltip("상자에서 나오는 성유물 (없으면 비워 둠)")]
        [SerializeField] private RelicData relic;

        public ItemData Item => item;
        public int Count => count;
        public int Gold => gold;
        public RelicData Relic => relic;

        private ChestContent() { } // Unity 직렬화용

        public ChestContent(ItemData item, int count, int gold = 0, RelicData relic = null)
        {
            this.item = item;
            this.count = count;
            this.gold = gold;
            this.relic = relic;
        }
    }

    // 출입구(D) 하나의 연결: 도착 지역 + 도착 지역 맵의 몇 번째 D에 나타나는지.
    // openedByBossOf를 정하면 그 지역의 보스를 물리치기 전까지 잠겨 있다 (예: 서고 보스 → 초원의 숲 입구)
    [Serializable]
    public class AreaExit
    {
        [SerializeField] private FieldArea target;
        [Tooltip("도착 지역 맵에서 D를 위→아래, 왼→오른 순으로 센 번호 (0부터)")]
        [SerializeField] private int targetDoorIndex;
        [Tooltip("이 지역의 보스를 물리쳐야 열림 (비우면 처음부터 열림)")]
        [SerializeField] private FieldArea openedByBossOf;

        public FieldArea Target => target;
        public int TargetDoorIndex => targetDoorIndex;
        public FieldArea OpenedByBossOf => openedByBossOf != null && openedByBossOf.Boss != null ? openedByBossOf : null;

        private AreaExit() { } // Unity 직렬화용

        public AreaExit(FieldArea target, int targetDoorIndex, FieldArea openedByBossOf = null)
        {
            this.target = target;
            this.targetDoorIndex = targetDoorIndex;
            this.openedByBossOf = openedByBossOf;
        }
    }

    // 보스를 물리치면 열리는 출입구 하나 (어느 지역의 어느 칸)
    public readonly struct AreaGate
    {
        public FieldArea Area { get; }
        public Vector2Int Cell { get; }
        public AreaExit Exit { get; }

        public AreaGate(FieldArea area, Vector2Int cell, AreaExit exit)
        {
            Area = area;
            Cell = cell;
            Exit = exit;
        }
    }

    [Serializable]
    public class BossEncounter
    {
        [SerializeField] private MonsterSpecies species;
        [SerializeField] private int level = 7;
        [Tooltip("처음 쓰러뜨리면 주는 성유물 (없으면 비워 둠)")]
        [SerializeField] private RelicData rewardRelic;
        [Tooltip("처음 쓰러뜨리면 주는 아이템 — 기술문서 등 (없으면 비워 둠)")]
        [SerializeField] private ItemData rewardItem;

        public MonsterSpecies Species => species;
        public int Level => level;
        public RelicData RewardRelic => rewardRelic;
        public ItemData RewardItem => rewardItem;

        private BossEncounter() { } // Unity 직렬화용

        public BossEncounter(MonsterSpecies species, int level, RelicData rewardRelic = null, ItemData rewardItem = null)
        {
            this.species = species;
            this.level = level;
            this.rewardRelic = rewardRelic;
            this.rewardItem = rewardItem;
        }
    }

    // 탐험 지역 하나 (초원, 던전 …): 맵 + 출현 몬스터 + 출제 단어장 + 보물상자 내용물(재료·골드·성유물)
    [CreateAssetMenu(fileName = "NewArea", menuName = "WordRPG/Field Area", order = 31)]
    public class FieldArea : ScriptableObject
    {
        [Tooltip("세이브에 기록되는 id. 정한 뒤에는 바꾸지 말 것")]
        [SerializeField] private string areaId;
        [SerializeField] private string displayName; // 예: "초원"
        [SerializeField] private FieldTheme theme = FieldTheme.Meadow;

        [Tooltip(". 길  : 잔디(조우 없음)  , 풀숲(조우)  # 나무  ~ 물  P 시작 위치  D 출입구  /  옆에서 [확인]으로 사용: F 회복의 샘  C 보물상자  E 성유물 제단  S 상점  B 보스")]
        [TextArea(12, 40)]
        [SerializeField] private string map;

        [Header("전투")]
        [SerializeField] private EncounterTable encounters;
        [SerializeField] private WordDatabase words;
        [Tooltip("풀숲 한 걸음마다 조우 확률")]
        [Range(0f, 1f)]
        [SerializeField] private float encounterRate = 0.12f;
        [Tooltip("조우 직후 이 걸음 수만큼은 다시 조우하지 않음")]
        [SerializeField] private int minStepsBetweenEncounters = 4;

        [Header("마을")]
        [Tooltip("맵의 S(상점) 앞에서 [확인]을 누르면 여는 상점")]
        [SerializeField] private ShopData shop;

        [Header("보물상자 — 맵의 C를 위→아래, 왼→오른 순서로 하나씩 대응")]
        [SerializeField] private List<ChestContent> chests = new List<ChestContent>();

        [Header("출입구 — 맵의 D를 위→아래, 왼→오른 순서로 하나씩 대응")]
        [SerializeField] private List<AreaExit> exits = new List<AreaExit>();

        [Header("보스 — 맵의 B (한 번 쓰러뜨리면 다시 나오지 않음)")]
        [SerializeField] private BossEncounter boss;

        [Header("정식판")]
        [Tooltip("정식판(인앱 결제)을 사야 들어갈 수 있는 지역 — 입구에서 정식판 안내가 뜬다 (#40, 숲부터)")]
        [SerializeField] private bool requiresFullVersion;

        [NonSerialized] private FieldMap parsed;
        [NonSerialized] private string parsedFrom;

        public string AreaId => areaId;
        public string DisplayName => displayName;
        public EncounterTable Encounters => encounters;
        public WordDatabase Words => words;
        public float EncounterRate => encounterRate;
        public int MinStepsBetweenEncounters => minStepsBetweenEncounters;
        public IReadOnlyList<ChestContent> ChestContents => chests;
        public ShopData Shop => shop;
        public FieldTheme Theme => theme;
        public IReadOnlyList<AreaExit> Exits => exits;
        public BossEncounter Boss => boss != null && boss.Species != null ? boss : null;
        public string BossId => $"{areaId}:boss";
        public bool RequiresFullVersion => requiresFullVersion;

        // 맵 텍스트가 바뀌면 다시 해석 (인스펙터에서 고치면서 플레이할 수 있게)
        public FieldMap Map
        {
            get
            {
                if (parsed == null || parsedFrom != map)
                {
                    parsed = FieldMap.Parse(map);
                    parsedFrom = map;
                }
                return parsed;
            }
        }

        // 세이브용 상자 id. 맵에서 상자 위치를 옮기면 새 상자로 취급된다
        public string ChestId(Vector2Int position) => $"{areaId}:{position.x},{position.y}";

        public AreaExit GetExit(Vector2Int position)
        {
            int index = Map.DoorIndex(position);
            return index >= 0 && index < exits.Count ? exits[index] : null;
        }

        // 이름표 글자: 성유물 제단 · 상점 이름 · 회복의 샘 · 보스 이름 · 출입구는 도착 지역 이름. 이름표가 없는 칸은 null
        public string LandmarkName(Vector2Int position)
        {
            switch (Map.Get(position))
            {
                case FieldTile.Altar: return "성유물 제단";
                case FieldTile.Fountain: return "회복의 샘";
                case FieldTile.Shop: return shop != null ? shop.DisplayName : "상점";
                case FieldTile.Boss: return Boss?.Species.DisplayName;
                case FieldTile.Door:
                    var exit = GetExit(position);
                    return exit != null && exit.Target != null ? exit.Target.DisplayName : null;
                default: return null;
            }
        }

        // bossArea의 보스를 물리치면 열리는 출입구들 (연출에서 카메라가 찾아가 보여 준다)
        public static List<AreaGate> GatesOpenedBy(FieldArea bossArea, IEnumerable<FieldArea> areas)
        {
            var gates = new List<AreaGate>();
            if (bossArea == null || areas == null) return gates;
            foreach (var area in areas)
            {
                if (area == null) continue;
                var doors = area.Map.Doors;
                for (int i = 0; i < area.exits.Count && i < doors.Count; i++)
                {
                    if (area.exits[i] != null && area.exits[i].OpenedByBossOf == bossArea)
                        gates.Add(new AreaGate(area, doors[i], area.exits[i]));
                }
            }
            return gates;
        }

        public ChestContent GetChestContent(Vector2Int position)
        {
            int index = Map.ChestIndex(position);
            return index >= 0 && index < chests.Count ? chests[index] : null;
        }
    }
}
