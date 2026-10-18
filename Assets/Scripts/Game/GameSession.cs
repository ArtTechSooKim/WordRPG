using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Save;
using WordRPG.Words;

namespace WordRPG.Game
{
    public class ChestResult
    {
        public bool WasEmpty { get; }  // 이미 연 상자 (또는 내용물 미지정)
        public ItemData Item { get; }
        public int Count { get; }
        public int Gold { get; }
        public OwnedRelic Relic { get; } // 새로 얻은 성유물 (없으면 null)

        public ChestResult(bool wasEmpty, ItemData item, int count, int gold, OwnedRelic relic = null)
        {
            WasEmpty = wasEmpty;
            Item = item;
            Count = count;
            Gold = gold;
            Relic = relic;
        }
    }

    // 플레이어 한 명의 게임 진행 상태 전체: 주인공(레벨·성유물), 동료, 소지품, 단어 학습 기록, 전적, 필드 위치·연 상자.
    // 세이브 파일(SaveData)과 서로 변환된다. MonoBehaviour가 아니라 테스트에서 바로 만들 수 있다
    public class GameSession
    {
        private readonly List<string> loadWarnings = new List<string>();
        private readonly List<Companion> companions = new List<Companion>();

        public Hero Hero { get; }
        public IReadOnlyList<Companion> Companions => companions;   // 얻은 동료 (#77)
        public Companion PartyCompanion { get; private set; }       // 함께 다니는 동료 (없으면 null) — 파티 = 주인공 + 동료 1명

        // 전투에 나서는 순서 (차례도 이 순서): 주인공 → 동료
        public IReadOnlyList<ICombatant> PartyMembers =>
            PartyCompanion != null ? new ICombatant[] { Hero, PartyCompanion } : new ICombatant[] { Hero };
        public Inventory Inventory { get; }
        public VocabularyProgress Vocabulary { get; }
        public PlayerRecord Record { get; }
        public WorldState World { get; }
        public TutorialProgress Tutorials { get; private set; } = new TutorialProgress(); // 이미 본 안내 (#36)
        public UpdateNotices Notices { get; private set; } = new UpdateNotices();       // 이미 본 업데이트 안내 (#115)
        public IReadOnlyList<string> LoadWarnings => loadWarnings;

        // 연속 정답 수 (전투 콤보). 전투가 끝나도 이어지지만 세이브에는 넣지 않는다 — 게임을 다시 켜면 0부터
        public int ComboStreak { get; set; }

        private DateTime? comboIdleSinceUtc; // 마지막 전투가 끝난 시각 (전투 중이면 null)

        // 전투를 시작할 때: 지난 전투가 끝나고 Combo.Expiry(3분)가 넘었으면 콤보를 0부터. 이어갈 연속 정답 수를 돌려준다
        public int StartBattleCombo(DateTime nowUtc)
        {
            if (comboIdleSinceUtc.HasValue && nowUtc - comboIdleSinceUtc.Value > Battle.Combo.Expiry) ComboStreak = 0;
            comboIdleSinceUtc = null;
            return ComboStreak;
        }

        // 전투가 끝날 때: 이때부터 3분을 잰다 (전투가 길어져도 그동안은 끊기지 않음)
        public void EndBattleCombo(DateTime nowUtc) => comboIdleSinceUtc = nowUtc;

        // 콤보를 0으로 (#83 사용자 결정: 수련에 들어갈 때와 나올 때 — 수련 콤보는 실제 전투로 이어지지 않음, 실제 전투 콤보도 수련으로 안 이어짐)
        public void ResetCombo()
        {
            ComboStreak = 0;
            comboIdleSinceUtc = null;
        }

        public bool CanFight => !Hero.IsFainted;

        private GameSession(Hero hero, Inventory inventory, VocabularyProgress vocabulary, PlayerRecord record, WorldState world)
        {
            Hero = hero;
            Inventory = inventory;
            Vocabulary = vocabulary;
            Record = record;
            World = world;
        }

        // level을 안 주면 HeroData의 시작 레벨. 시작 성유물을 가지고(앞에서부터 끼운 채로) 시작한다.
        // 업데이트 안내는 모두 본 것으로 (#115 — 새로 시작한 사람은 업데이트한 사람이 아님)
        public static GameSession NewGame(HeroData heroData, int? level = null)
        {
            var session = new GameSession(CreateHero(heroData, level ?? heroData.StartLevel), new Inventory(), new VocabularyProgress(),
                new PlayerRecord(), new WorldState());
            session.Notices.MarkAll();
            return session;
        }

        // 샘·패배 뒤 회복: 주인공과 동료 모두
        public void RestoreHero()
        {
            Hero.RestoreFully();
            foreach (var companion in companions) companion.RestoreFully();
        }

        // 전투가 끝난 뒤(이김·도망): 쓰러졌던 동료는 HP 1로 일어난다 (#77 기본값)
        public void AfterBattle()
        {
            foreach (var companion in companions) companion.ReviveIfFainted();
        }

        public Companion FindCompanion(CompanionData data) => companions.Find(c => c.Data == data);

        public bool HasCompanion(CompanionData data) => FindCompanion(data) != null;

        // 동료를 얻는다 (#77): 주인공 레벨로 합류하고, 함께 다니는 동료가 없으면 바로 파티에. 이미 있으면 그 동료
        public Companion AddCompanion(CompanionData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var existing = FindCompanion(data);
            if (existing != null) return existing;
            var companion = new Companion(data, Hero.Level);
            companions.Add(companion);
            if (PartyCompanion == null) PartyCompanion = companion;
            return companion;
        }

        // 함께 다닐 동료 (null = 아무도 안 데려감). 얻은 동료만 고를 수 있다
        public bool SetPartyCompanion(Companion companion)
        {
            if (companion != null && !companions.Contains(companion)) return false;
            PartyCompanion = companion;
            return true;
        }

        // 회복 아이템(상처약)을 필드에서 쓴다. HP가 가득이거나 아이템이 없으면 쓰지 않고 0
        public int UseHealingItem(ItemData item)
        {
            if (item == null || !item.IsHealingItem || Hero.IsFainted) return 0;
            if (Hero.CurrentHp >= Hero.Stats.MaxHp || Inventory.GetCount(item) <= 0) return 0;
            Inventory.TryRemove(item);
            return Hero.Heal(item.HealAmount);
        }

        // 성유물을 얻는다 (보물상자·보스). 이미 가진 것이면 null
        public OwnedRelic GrantRelic(RelicData relic) => relic != null ? Hero.AddRelic(relic) : null;

        // 가방에 있는 기술문서로 기술을 배운다 (문서는 남는다). 칸이 가득이면 replace 칸과 바꾼다
        public bool LearnSkill(ItemData document, int replace = -1) =>
            document != null && document.IsSkillDocument && Inventory.GetCount(document) > 0 && Hero.LearnSkill(document, replace);

        // 도감을 모두 채운 지역의 보상(징표 + 골드)을 지급한다. 지역당 한 번만 — 이미 받은 지역은 건너뜀.
        // 나중에 단어가 추가돼 완성률이 내려가도 받은 보상은 그대로 유지
        public List<DexCompletion> ClaimDexRewards(IEnumerable<WordDatabase> regions)
        {
            var completed = new List<DexCompletion>();
            foreach (var region in regions)
            {
                if (region == null || string.IsNullOrEmpty(region.RegionId)) continue;
                if (Record.HasClaimedRegion(region.RegionId)) continue;
                if (!Dex.GetProgress(region, Vocabulary).IsComplete) continue;

                Record.MarkRegionClaimed(region.RegionId);
                if (region.CompletionKeepsake != null) Inventory.Add(region.CompletionKeepsake);
                if (region.CompletionGold > 0) Inventory.AddGold(region.CompletionGold);
                completed.Add(new DexCompletion(region));
            }
            return completed;
        }

        // 보물상자는 상자마다 한 번만 열린다 (세이브에 기록). 강화 재료·골드·성유물을 얻는 곳
        public ChestResult OpenChest(FieldArea area, Vector2Int position)
        {
            string chestId = area.ChestId(position);
            if (World.IsChestOpened(chestId)) return new ChestResult(true, null, 0, 0);

            var content = area.GetChestContent(position);
            World.MarkChestOpened(chestId);
            if (content == null) return new ChestResult(true, null, 0, 0);

            if (content.Item != null && content.Count > 0) Inventory.Add(content.Item, content.Count);
            if (content.Gold > 0) Inventory.AddGold(content.Gold);
            var relic = GrantRelic(content.Relic);
            return new ChestResult(false, content.Item, content.Count, content.Gold, relic);
        }

        // 지도 화면의 '남은 보물상자' — 맵의 상자 칸 중 아직 안 연 것
        public int RemainingChests(FieldArea area)
        {
            var map = area.Map;
            int remaining = 0;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (map.Get(cell) == FieldTile.Chest && !World.IsChestOpened(area.ChestId(cell))) remaining++;
            }
            return remaining;
        }

        // 지도 화면의 '탐험 N%' — 벽이 아닌 칸 중 가 본 칸
        public (int explored, int total) ExplorationProgress(FieldArea area)
        {
            var map = area.Map;
            int explored = 0, total = 0;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (map.Get(cell) == FieldTile.Wall) continue;
                total++;
                if (World.IsExplored(area.AreaId, cell)) explored++;
            }
            return (explored, total);
        }

        public SaveData ToSaveData(DateTime nowUtc)
        {
            var relics = new List<RelicSaveData>();
            foreach (var relic in Hero.Relics)
                relics.Add(new RelicSaveData(relic.Data.RelicId, relic.Level, Hero.SlotOf(relic)));
            var skills = new List<string>();
            foreach (var slot in Hero.SkillSlots) skills.Add(slot.Key);
            var savedCompanions = new List<CompanionSaveData>();
            foreach (var companion in companions)
                savedCompanions.Add(new CompanionSaveData(companion.Data.CompanionId, companion.Level, companion.Exp, companion.CurrentHp));
            return new SaveData(nowUtc, new HeroSaveData(Hero.Level, Hero.Exp, Hero.CurrentHp), relics,
                    Inventory, Vocabulary, Record, World).WithSkillSlots(skills).WithTutorials(Tutorials.Seen)
                .WithNotices(Notices.Seen).WithCompanions(savedCompanions, PartyCompanion?.Data.CompanionId);
        }

        // 세이브에 있는 성유물을 찾을 수 없으면(삭제·id 변경) 빼고 경고를 남긴다. 성유물이 하나도 없으면 시작 성유물로 채운다.
        // v1(몬스터 파티) 세이브는 파티에서 가장 높은 레벨을 주인공 레벨로 — 가장 소중한 단어 학습 기록은 그대로 유지
        public static GameSession FromSaveData(SaveData data, GameDatabase database, HeroData heroData)
        {
            var warnings = new List<string>();
            Hero hero;
            if (data.Version < 2)
            {
                int level = heroData.StartLevel;
                foreach (var monster in data.LegacyParty) level = Math.Max(level, monster.Level);
                hero = new Hero(heroData, level);
                warnings.Add($"예전 세이브(몬스터 파티)를 주인공 Lv{hero.Level}로 바꿨습니다");
            }
            else
            {
                hero = new Hero(heroData, data.Hero.Level, data.Hero.Exp);
                foreach (var saved in data.Relics)
                {
                    var relic = database != null ? database.FindRelic(saved.RelicId) : null;
                    if (relic == null)
                    {
                        warnings.Add($"세이브의 성유물 '{saved.RelicId}'를 찾을 수 없어 제외했습니다");
                        continue;
                    }
                    hero.RestoreRelic(relic, saved.Level, saved.Slot);
                }
            }

            if (hero.Relics.Count == 0)
            {
                foreach (var relic in heroData.StartingRelics) if (relic != null) hero.AddRelic(relic);
            }

            // 전에 끼운 칸이 사라져 하나도 안 끼운 상태면 가진 것부터 끼운다
            if (hero.EquippedCount == 0)
                foreach (var relic in hero.Relics) hero.Equip(relic);

            if (data.HasSkillSlots) RestoreSkillSlots(hero, data, database, warnings);
            else
            {
                // 기술 칸 정보가 없는 예전 세이브: 끼운 칸 순서대로 성유물 기술 (예전과 같은 기술 구성)
                for (int i = 0; i < hero.SlotCount; i++)
                {
                    var relic = hero.SlotAt(i);
                    if (relic != null && hero.HasSkillRoom) hero.PlaceRelicSkill(relic);
                }
            }

            if (data.Version >= 2 && data.Hero.CurrentHp > 0) hero.SetHp(data.Hero.CurrentHp);
            else hero.RestoreFully(); // v1이거나 쓰러진 채로 저장됐다면(패배 직후 앱 종료 등) 회복한 것으로

            var session = new GameSession(hero, data.Inventory, data.Vocabulary, data.Record, data.World);
            session.Tutorials = new TutorialProgress(data.Tutorials);
            session.Notices = new UpdateNotices(data.Notices);
            RestoreCompanions(session, data, database, warnings);
            session.loadWarnings.AddRange(warnings);
            return session;
        }

        // 동료 (#77): 찾을 수 없는 동료는 빼고 경고. 쓰러진 채 저장됐으면 회복한 것으로 (주인공과 같은 규칙)
        private static void RestoreCompanions(GameSession session, SaveData data, GameDatabase database, List<string> warnings)
        {
            foreach (var saved in data.Companions)
            {
                var companionData = database != null ? database.FindCompanion(saved.CompanionId) : null;
                if (companionData == null)
                {
                    warnings.Add($"세이브의 동료 '{saved.CompanionId}'를 찾을 수 없어 제외했습니다");
                    continue;
                }
                if (session.HasCompanion(companionData)) continue;
                var companion = new Companion(companionData, saved.Level, saved.Exp);
                if (saved.CurrentHp > 0) companion.SetHp(saved.CurrentHp);
                session.companions.Add(companion);
                if (saved.CompanionId == data.PartyCompanionId) session.PartyCompanion = companion;
            }
        }

        // 저장된 기술 칸 순서대로 되살린다. 끼우지 않은 성유물·찾을 수 없는 기술문서는 건너뜀
        // (저장된 칸 정보가 없는 예전 세이브는 FromSaveData가 끼운 성유물 기술로 채운다)
        private static void RestoreSkillSlots(Hero hero, SaveData data, GameDatabase database, List<string> warnings)
        {
            hero.ClearSkillSlots();
            foreach (var key in data.SkillSlots)
            {
                if (key == null) continue;
                if (key.StartsWith("relic:"))
                {
                    var relic = database != null ? database.FindRelic(key.Substring(6)) : null;
                    var owned = relic != null ? hero.Find(relic) : null;
                    if (owned != null) hero.PlaceRelicSkill(owned);
                }
                else if (key.StartsWith("doc:"))
                {
                    var document = database != null ? database.FindItem(key.Substring(4)) : null;
                    if (document == null || !document.IsSkillDocument || !hero.LearnSkill(document))
                        warnings.Add($"세이브의 기술 '{key}'를 찾을 수 없어 기술 칸에서 뺐습니다");
                }
            }
        }

        private static Hero CreateHero(HeroData heroData, int level)
        {
            if (heroData == null) throw new ArgumentNullException(nameof(heroData), "주인공 데이터가 없습니다");
            var hero = new Hero(heroData, level);
            foreach (var relic in heroData.StartingRelics) if (relic != null) hero.AddRelic(relic);
            hero.RestoreFully();
            return hero;
        }
    }
}
