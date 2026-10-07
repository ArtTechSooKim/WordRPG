using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Game
{
    // 필드 진행 상태: 마지막 위치(지역+칸), 열어 본 보물상자, 쓰러뜨린 보스, 탐험한 칸(안개). 세이브 파일에 들어간다
    [Serializable]
    public class WorldState
    {
        // 걸을 때 주변이 밝혀지는 반지름 (칸)
        public const int SightRadius = 3;

        [SerializeField] private string areaId;
        [SerializeField] private int x;
        [SerializeField] private int y;
        [SerializeField] private bool hasPosition;
        [SerializeField] private List<string> openedChests = new List<string>();
        [SerializeField] private List<string> defeatedBosses = new List<string>();
        [SerializeField] private List<ExploredArea> explored = new List<ExploredArea>(); // 예전 세이브에는 없음 → 처음부터 탐험
        [SerializeField] private List<DictionaryReadDay> dictionaryReads = new List<DictionaryReadDay>(); // 사전을 읽은 날 (#45, 예전 세이브엔 없음)

        public string AreaId => hasPosition ? areaId : null;
        public IReadOnlyList<string> OpenedChests => openedChests;

        public bool TryGetPosition(string area, out Vector2Int position)
        {
            position = new Vector2Int(x, y);
            return hasPosition && areaId == area;
        }

        public void SetPosition(string area, Vector2Int position)
        {
            areaId = area;
            x = position.x;
            y = position.y;
            hasPosition = true;
        }

        public void ClearPosition() => hasPosition = false;

        public bool IsBossDefeated(string bossId) => defeatedBosses.Contains(bossId);

        public void MarkBossDefeated(string bossId)
        {
            if (!defeatedBosses.Contains(bossId)) defeatedBosses.Add(bossId);
        }

        // 탐험 안개: center 둘레를 밝히고 새로 밝힌 칸 수를 돌려준다
        public int Reveal(string area, int mapWidth, int mapHeight, Vector2Int center, int radius = SightRadius)
        {
            var record = explored.Find(e => e.AreaId == area);
            if (record == null)
            {
                record = new ExploredArea(area, mapWidth, mapHeight);
                explored.Add(record);
            }
            return record.Reveal(mapWidth, mapHeight, center, radius);
        }

        public bool IsExplored(string area, Vector2Int cell)
        {
            var record = explored.Find(e => e.AreaId == area);
            return record != null && record.IsExplored(cell);
        }

        // 출입구가 지금 열려 있는지: 여는 보스가 정해져 있으면 그 보스를 물리쳤을 때만
        public bool IsExitOpen(AreaExit exit) =>
            exit == null || exit.OpenedByBossOf == null || IsBossDefeated(exit.OpenedByBossOf.BossId);

        public bool IsChestOpened(string chestId) => openedChests.Contains(chestId);

        public void MarkChestOpened(string chestId)
        {
            if (!openedChests.Contains(chestId)) openedChests.Add(chestId);
        }

        // 그 지역 사전을 마지막으로 읽은 날 ("yyyy-MM-dd", 없으면 null)
        public string DictionaryReadDay(string area)
        {
            if (dictionaryReads == null) return null;
            foreach (var read in dictionaryReads)
                if (read.AreaId == area) return read.Day;
            return null;
        }

        public void MarkDictionaryRead(string area, string day)
        {
            if (dictionaryReads == null) dictionaryReads = new List<DictionaryReadDay>();
            dictionaryReads.RemoveAll(r => r.AreaId == area);
            dictionaryReads.Add(new DictionaryReadDay(area, day));
        }
    }

    [Serializable]
    public class DictionaryReadDay
    {
        [SerializeField] private string areaId;
        [SerializeField] private string day;

        public string AreaId => areaId;
        public string Day => day;

        private DictionaryReadDay() { } // Unity 직렬화용

        public DictionaryReadDay(string areaId, string day)
        {
            this.areaId = areaId;
            this.day = day;
        }
    }
}
