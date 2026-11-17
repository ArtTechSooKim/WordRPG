using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Game
{
    // 세이브 파일의 id(itemId, relicId, areaId, companionId)를 실제 에셋으로 되돌리는 목록.
    // 메뉴 WordRPG > Data > Refresh Game Database 가 프로젝트의 모든 몬스터·아이템·성유물·지역을 자동으로 채운다
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "WordRPG/Game Database", order = -10)]
    public class GameDatabase : ScriptableObject
    {
        [SerializeField] private List<MonsterSpecies> monsters = new List<MonsterSpecies>();
        [SerializeField] private List<ItemData> items = new List<ItemData>();
        [SerializeField] private List<FieldArea> areas = new List<FieldArea>();
        [SerializeField] private List<RelicData> relics = new List<RelicData>();
        [SerializeField] private List<CompanionData> companions = new List<CompanionData>(); // #77 동료
        [SerializeField] private List<WordCourse> courses = new List<WordCourse>(); // #273 처음에 고르는 단어장 (order 순)
        [Tooltip("정식판 특전 (#233): 정식판을 사면 세이브마다 한 번 받는 아이템 (FullVersionGift)")]
        [SerializeField] private List<ItemGift> fullVersionGift = new List<ItemGift>();

        public IReadOnlyList<ItemGift> FullVersionGift => fullVersionGift;

        public IReadOnlyList<MonsterSpecies> Monsters => monsters;
        public IReadOnlyList<ItemData> Items => items;
        public IReadOnlyList<FieldArea> Areas => areas;
        public IReadOnlyList<RelicData> Relics => relics;
        public IReadOnlyList<CompanionData> Companions => companions ?? (IReadOnlyList<CompanionData>)new List<CompanionData>();

        public IReadOnlyList<WordCourse> Courses => courses ?? (IReadOnlyList<WordCourse>)new List<WordCourse>();

        // 세이브의 단어장 id로 찾기. 없거나 모르는 id면 null (= 기본 단어장)
        public WordCourse FindCourse(string courseId)
        {
            if (string.IsNullOrEmpty(courseId) || courses == null) return null;
            foreach (var course in courses)
                if (course != null && course.CourseId == courseId) return course;
            return null;
        }

        // 지역에 붙은 기본 단어장들 (이야기 순서, 겹치지 않게) — 단어장 고르는 화면의 단어 수·예시 단어
        public List<WordDatabase> BaseWordBooks()
        {
            var books = new List<WordDatabase>();
            var ordered = new List<FieldArea>();
            foreach (var area in areas) if (area != null) ordered.Add(area);
            ordered.Sort((a, b) => a.StoryOrder.CompareTo(b.StoryOrder));
            foreach (var area in ordered)
            {
                var book = area.Words;
                if (book != null && !books.Contains(book)) books.Add(book);
            }
            return books;
        }

        public CompanionData FindCompanion(string companionId)
        {
            if (companions == null) return null;
            foreach (var companion in companions)
                if (companion != null && companion.CompanionId == companionId) return companion;
            return null;
        }

        public MonsterSpecies FindMonster(string speciesId)
        {
            foreach (var species in monsters)
            {
                if (species != null && species.SpeciesId == speciesId) return species;
            }
            return null;
        }

        public ItemData FindItem(string itemId)
        {
            foreach (var item in items)
            {
                if (item != null && item.ItemId == itemId) return item;
            }
            return null;
        }

        public RelicData FindRelic(string relicId)
        {
            foreach (var relic in relics)
            {
                if (relic != null && relic.RelicId == relicId) return relic;
            }
            return null;
        }

        // 세이브의 마지막 위치(지역 id)로 시작 지역을 찾는다
        public FieldArea FindArea(string areaId)
        {
            foreach (var area in areas)
            {
                if (area != null && area.AreaId == areaId) return area;
            }
            return null;
        }

        // 에디터 수집기 전용
        public void ReplaceContents(IEnumerable<MonsterSpecies> newMonsters, IEnumerable<ItemData> newItems,
            IEnumerable<FieldArea> newAreas = null, IEnumerable<RelicData> newRelics = null, IEnumerable<CompanionData> newCompanions = null,
            IEnumerable<WordCourse> newCourses = null)
        {
            courses = newCourses != null ? new List<WordCourse>(newCourses) : new List<WordCourse>();
            monsters = new List<MonsterSpecies>(newMonsters);
            items = new List<ItemData>(newItems);
            areas = newAreas != null ? new List<FieldArea>(newAreas) : new List<FieldArea>();
            relics = newRelics != null ? new List<RelicData>(newRelics) : new List<RelicData>();
            companions = newCompanions != null ? new List<CompanionData>(newCompanions) : new List<CompanionData>();
        }
    }
}
