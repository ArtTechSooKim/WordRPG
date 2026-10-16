using System;
using System.Collections.Generic;
using WordRPG.Items;
using WordRPG.Monsters;

namespace WordRPG.Heroes
{
    // 모은 성유물 하나 + 강화 단계
    public class OwnedRelic
    {
        public RelicData Data { get; }
        public int Level { get; internal set; }

        public SkillData Skill => Data.SkillAt(Level);
        public MonsterStats Bonus => Data.BonusAt(Level);
        public bool IsMaxLevel => Level >= RelicData.MaxLevel;
        public bool IsAwakened => Data.HasAwakening && Level >= Data.AwakenLevel;

        public OwnedRelic(RelicData data, int level = 0)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Level = Math.Max(0, Math.Min(level, RelicData.MaxLevel));
        }
    }

    // 기술 칸 하나: 끼운 성유물의 기술(강화·각성하면 따라 바뀜) 또는 기술문서로 배운 기술
    public class SkillSlot
    {
        public OwnedRelic Relic { get; }
        public ItemData Document { get; }

        public SkillData Skill => Relic != null ? Relic.Skill : Document?.TaughtSkill;

        // 어디서 온 기술인지: 성유물 이름 / "기술문서"
        public string SourceName => Relic != null ? Relic.Data.DisplayName : "기술문서";

        // 세이브용: "relic:{relicId}" / "doc:{itemId}"
        public string Key => Relic != null ? "relic:" + Relic.Data.RelicId : "doc:" + Document?.ItemId;

        public SkillSlot(OwnedRelic relic) => Relic = relic ?? throw new ArgumentNullException(nameof(relic));

        public SkillSlot(ItemData document)
        {
            if (document == null || !document.IsSkillDocument) throw new ArgumentException("기술문서가 아닙니다", nameof(document));
            Document = document;
        }
    }

    // 혼자 싸우는 주인공. 능력치 = 레벨 능력치 + 끼운 성유물 보너스.
    // 기술 = 기본 기술(항상) + 기술 칸 3개 (끼운 성유물의 기술 또는 기술문서로 배운 기술) — 전투 기술은 최대 4개.
    //   성유물을 끼울 때 기술 칸이 비어 있으면 그 기술이 자동으로 들어가고, 가득이면 새 기술은 기존 것과 바꿔야 한다.
    // 성유물은 여러 개 모으고(Relics) 그중 3개까지 칸에 끼운다(Slots)
    public class Hero : ICombatant
    {
        public const int SkillSlotCount = 3; // 기본 기술 말고 고를 수 있는 기술 칸

        private readonly List<OwnedRelic> relics = new List<OwnedRelic>();
        private readonly OwnedRelic[] slots = new OwnedRelic[HeroData.SlotCount];
        private readonly List<SkillSlot> skillSlots = new List<SkillSlot>();

        public HeroData Data { get; }
        public int Level { get; private set; }
        public int Exp { get; private set; } // 현재 레벨에서 쌓은 경험치
        public int CurrentHp { get; private set; }

        public string DisplayName => Data.DisplayName;
        public bool IsFainted => CurrentHp <= 0;
        public bool IsMaxLevel => Level >= LevelCurve.MaxLevel;
        public int ExpToNextLevel => IsMaxLevel ? 0 : LevelCurve.ExpToNextLevel(Level) - Exp;

        public IReadOnlyList<OwnedRelic> Relics => relics;
        public int SlotCount => slots.Length;

        public Hero(HeroData data, int level, int exp = 0)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Level = Math.Max(1, Math.Min(level, LevelCurve.MaxLevel));
            Exp = IsMaxLevel ? 0 : Math.Max(0, exp);
            CurrentHp = Stats.MaxHp;
        }

        // ------------------------------------------------------------------ 능력치·기술

        public MonsterStats LevelStats => Data.GetStats(Level);

        public MonsterStats RelicBonus
        {
            get
            {
                var sum = new MonsterStats(0, 0, 0);
                foreach (var relic in slots) if (relic != null) sum = sum + relic.Bonus;
                return sum;
            }
        }

        public MonsterStats Stats => LevelStats + RelicBonus;

        // 기본 기술 + 기술 칸 순서대로 (같은 기술은 한 번만)
        public IReadOnlyList<SkillData> Skills
        {
            get
            {
                var list = new List<SkillData>();
                if (Data.BasicSkill != null) list.Add(Data.BasicSkill);
                foreach (var slot in skillSlots)
                {
                    var skill = slot.Skill;
                    if (skill != null && !list.Contains(skill)) list.Add(skill);
                }
                return list;
            }
        }

        public IReadOnlyList<SkillSlot> SkillSlots => skillSlots;

        public bool HasSkillRoom => skillSlots.Count < SkillSlotCount;

        // 이 기술이 든 기술 칸 (기본 기술이면 null)
        public SkillSlot SourceOf(SkillData skill) => skillSlots.Find(s => s.Skill == skill);

        public bool Knows(ItemData document) => document != null && skillSlots.Exists(s => s.Document == document);

        public bool HasSkillOf(OwnedRelic relic) => relic != null && skillSlots.Exists(s => s.Relic == relic);

        // 기술문서로 기술을 배운다. 칸이 가득이면 replace(0~2) 칸의 기술과 바꾼다. 배웠으면 true
        public bool LearnSkill(ItemData document, int replace = -1)
        {
            if (document == null || !document.IsSkillDocument || Knows(document)) return false;
            foreach (var skill in Skills)
                if (skill == document.TaughtSkill) return false; // 같은 기술이 이미 있음
            return Place(new SkillSlot(document), replace);
        }

        // 끼운 성유물의 기술을 칸에 넣는다 (예전에 다른 기술과 바꿔서 빠졌을 때). 칸이 가득이면 replace 칸과 바꾼다
        public bool PlaceRelicSkill(OwnedRelic relic, int replace = -1)
        {
            if (relic == null || !IsEquipped(relic) || HasSkillOf(relic) || relic.Skill == null) return false;
            return Place(new SkillSlot(relic), replace);
        }

        private bool Place(SkillSlot slot, int replace)
        {
            if (HasSkillRoom)
            {
                skillSlots.Add(slot);
                return true;
            }
            if (replace < 0 || replace >= skillSlots.Count) return false;
            skillSlots[replace] = slot;
            return true;
        }

        // ------------------------------------------------------------------ 성유물 모으기·끼우기

        public OwnedRelic Find(RelicData data) => relics.Find(r => r.Data == data);

        public bool Owns(RelicData data) => Find(data) != null;

        // 새 성유물을 얻는다. 빈 칸이 있으면 바로 끼운다. 이미 가진 것이면 null
        public OwnedRelic AddRelic(RelicData data, int level = 0)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (Owns(data)) return null;
            var relic = new OwnedRelic(data, level);
            relics.Add(relic);
            Equip(relic);
            return relic;
        }

        // 저장된 성유물을 되살릴 때: 칸 번호를 지정 (-1 = 안 끼움)
        public OwnedRelic RestoreRelic(RelicData data, int level, int slot)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var relic = Find(data);
            if (relic == null)
            {
                relic = new OwnedRelic(data, level);
                relics.Add(relic);
            }
            if (slot >= 0 && slot < slots.Length && slots[slot] == null && !IsEquipped(relic)) slots[slot] = relic;
            // 끼우면 (기술 칸이 비어 있을 때) 성유물 기술도 — 저장된 기술 칸이 있으면 GameSession이 그 순서로 다시 맞춘다
            if (IsEquipped(relic) && HasSkillRoom) PlaceRelicSkill(relic);
            ClampHp();
            return relic;
        }

        // 저장된 기술 칸을 되살릴 때 (성유물·기술문서를 먼저 되살린 뒤). 끼우지 않은 성유물·없는 문서는 건너뜀
        public void ClearSkillSlots() => skillSlots.Clear();

        public OwnedRelic SlotAt(int slot) => slot >= 0 && slot < slots.Length ? slots[slot] : null;

        public int SlotOf(OwnedRelic relic) => Array.IndexOf(slots, relic);

        public bool IsEquipped(OwnedRelic relic) => relic != null && SlotOf(relic) >= 0;

        public bool HasEmptySlot => Array.IndexOf(slots, null) >= 0;

        public int EquippedCount
        {
            get
            {
                int count = 0;
                foreach (var relic in slots) if (relic != null) count++;
                return count;
            }
        }

        // 첫 빈 칸에 끼운다. 기술 칸이 비어 있으면 성유물 기술도 넣는다. 이미 끼웠거나 칸이 가득 찼으면 false
        public bool Equip(OwnedRelic relic)
        {
            if (relic == null || !relics.Contains(relic) || IsEquipped(relic)) return false;
            int empty = Array.IndexOf(slots, null);
            if (empty < 0) return false;
            slots[empty] = relic;
            if (HasSkillRoom) PlaceRelicSkill(relic);
            return true;
        }

        // 바로 바꿔 끼우기 (#91 사용자 요청 — 전에는 빼기 → 장착하기 두 번): 그 칸의 성유물을 빼고 incoming을 끼운다.
        // 빠진 성유물의 기술이 기술 칸에 있었으면 같은 자리에 새 성유물 기술이 들어가 기술 순서가 그대로, 없었으면 빈 기술 칸이 있을 때만 넣는다.
        // 빈 칸을 고르면 그 칸에 끼운다. 이미 끼운 성유물이거나 칸 번호가 틀리면 false
        public bool SwapInto(OwnedRelic incoming, int slot)
        {
            if (incoming == null || !relics.Contains(incoming) || IsEquipped(incoming)) return false;
            if (slot < 0 || slot >= slots.Length) return false;
            var outgoing = slots[slot];
            slots[slot] = incoming;
            int skillIndex = outgoing != null ? skillSlots.FindIndex(s => s.Relic == outgoing) : -1;
            if (skillIndex >= 0) skillSlots[skillIndex] = new SkillSlot(incoming);
            else if (HasSkillRoom) PlaceRelicSkill(incoming);
            ClampHp();
            return true;
        }

        // 칸에서 뺀다 (그 성유물 기술도 기술 칸에서 빠진다). 최대 HP가 줄면 현재 HP도 맞춘다
        public bool Unequip(OwnedRelic relic)
        {
            int slot = SlotOf(relic);
            if (slot < 0) return false;
            slots[slot] = null;
            skillSlots.RemoveAll(s => s.Relic == relic);
            ClampHp();
            return true;
        }

        // ------------------------------------------------------------------ 성장·HP

        // 반환값: 오른 레벨 수. 레벨업으로 늘어난 최대 HP만큼 현재 HP도 늘어난다
        public int GainExp(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (IsMaxLevel) return 0;

            int levelsGained = 0;
            Exp += amount;
            while (!IsMaxLevel && Exp >= LevelCurve.ExpToNextLevel(Level))
            {
                int oldMaxHp = Stats.MaxHp;
                Exp -= LevelCurve.ExpToNextLevel(Level);
                Level++;
                levelsGained++;
                if (!IsFainted) CurrentHp += Stats.MaxHp - oldMaxHp;
            }
            if (IsMaxLevel) Exp = 0;
            return levelsGained;
        }

        public int TakeDamage(int amount)
        {
            int dealt = Math.Min(Math.Max(0, amount), CurrentHp);
            CurrentHp -= dealt;
            return dealt;
        }

        public int Heal(int amount)
        {
            if (IsFainted) return 0;
            int healed = Math.Min(Math.Max(0, amount), Stats.MaxHp - CurrentHp);
            CurrentHp += healed;
            return healed;
        }

        public void RestoreFully() => CurrentHp = Stats.MaxHp;

        // 저장된 HP로 맞출 때 (0 ~ 최대 HP)
        public void SetHp(int hp) => CurrentHp = Math.Max(0, Math.Min(hp, Stats.MaxHp));

        private void ClampHp()
        {
            if (CurrentHp > Stats.MaxHp) CurrentHp = Stats.MaxHp;
        }
    }
}
