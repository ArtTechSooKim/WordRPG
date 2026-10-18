using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Game;
using WordRPG.Items;
using WordRPG.Words;

namespace WordRPG.Save
{
    // v1(몬스터 파티 시절) 세이브의 몬스터 한 마리. v2부터는 쓰지 않고, 예전 세이브를 주인공 레벨로 바꿀 때만 읽는다
    [Serializable]
    public class MonsterSaveData
    {
        [SerializeField] private string speciesId;
        [SerializeField] private int level;
        [SerializeField] private int exp;
        [SerializeField] private int currentHp;

        public string SpeciesId => speciesId;
        public int Level => level;
        public int Exp => exp;
        public int CurrentHp => currentHp;

        private MonsterSaveData() { } // Unity 직렬화용

        public MonsterSaveData(string speciesId, int level, int exp, int currentHp)
        {
            this.speciesId = speciesId;
            this.level = level;
            this.exp = exp;
            this.currentHp = currentHp;
        }
    }

    [Serializable]
    public class HeroSaveData
    {
        [SerializeField] private int level;
        [SerializeField] private int exp;
        [SerializeField] private int currentHp;

        public int Level => level;
        public int Exp => exp;
        public int CurrentHp => currentHp;

        private HeroSaveData() { } // Unity 직렬화용

        public HeroSaveData(int level, int exp, int currentHp)
        {
            this.level = level;
            this.exp = exp;
            this.currentHp = currentHp;
        }
    }

    // 모은 성유물 하나: id · 강화 단계 · 끼운 칸(-1 = 안 끼움)
    [Serializable]
    public class RelicSaveData
    {
        [SerializeField] private string relicId;
        [SerializeField] private int level;
        [SerializeField] private int slot = -1;

        public string RelicId => relicId;
        public int Level => level;
        public int Slot => slot;

        private RelicSaveData() { } // Unity 직렬화용

        public RelicSaveData(string relicId, int level, int slot)
        {
            this.relicId = relicId;
            this.level = level;
            this.slot = slot;
        }
    }

    // 얻은 동료 한 명 (#77): id · 레벨 · 경험치 · HP
    [Serializable]
    public class CompanionSaveData
    {
        [SerializeField] private string companionId;
        [SerializeField] private int level;
        [SerializeField] private int exp;
        [SerializeField] private int currentHp;

        public string CompanionId => companionId;
        public int Level => level;
        public int Exp => exp;
        public int CurrentHp => currentHp;

        private CompanionSaveData() { } // Unity 직렬화용

        public CompanionSaveData(string companionId, int level, int exp, int currentHp)
        {
            this.companionId = companionId;
            this.level = level;
            this.exp = exp;
            this.currentHp = currentHp;
        }
    }

    // 세이브 파일(save.json) 한 개의 내용. 에셋은 전부 id 문자열로 저장해서 에셋 이름이 바뀌어도 깨지지 않는다.
    // 필드를 추가해도 예전 세이브는 그 필드가 기본값으로 읽힌다. 기존 필드의 의미를 바꿀 때만 version을 올리고 변환 코드를 넣을 것
    //   v1: 몬스터 파티(party)  →  v2: 주인공(hero) + 성유물(relics). v1은 GameSession.FromSaveData가 주인공으로 바꿔 읽는다
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 2;

        [SerializeField] private int version = CurrentVersion;
        [SerializeField] private long savedAtTicks;
        [SerializeField] private HeroSaveData hero = new HeroSaveData(1, 0, 0);
        [SerializeField] private List<RelicSaveData> relics = new List<RelicSaveData>();
        [SerializeField] private List<MonsterSaveData> party = new List<MonsterSaveData>(); // v1 전용 (읽기만)
        [SerializeField] private Inventory inventory = new Inventory();
        [SerializeField] private VocabularyProgress vocabulary = new VocabularyProgress();
        [SerializeField] private PlayerRecord record = new PlayerRecord();
        [SerializeField] private WorldState world = new WorldState();
        // 주인공 기술 칸 ("relic:{relicId}" / "doc:{itemId}"). 기술문서가 생기기 전 세이브에는 없음 → 끼운 성유물 기술로 채움
        [SerializeField] private List<string> skillSlots = new List<string>();
        [SerializeField] private bool hasSkillSlots;
        // 이미 본 튜토리얼 id (TutorialProgress). 예전 세이브에는 없음 → 처음부터 한 번씩 보여 준다
        [SerializeField] private List<string> tutorials = new List<string>();
        // 얻은 동료와 파티에 넣은 동료 id (#77, 1.1 — 예전 세이브엔 없음 → 동료 없이 시작). 빈 문자열 = 동료가 쉬는 중
        [SerializeField] private List<CompanionSaveData> companions = new List<CompanionSaveData>();
        [SerializeField] private string partyCompanionId = "";
        // 이미 본 업데이트 안내 id (#115, UpdateNotices). 예전 세이브엔 없음 → 업데이트한 사람이라 안내를 한 번 보여 준다
        [SerializeField] private List<string> notices = new List<string>();

        public int Version => version;
        public DateTime SavedAtUtc => new DateTime(savedAtTicks, DateTimeKind.Utc);
        public HeroSaveData Hero => hero;
        public IReadOnlyList<RelicSaveData> Relics => relics;
        public IReadOnlyList<MonsterSaveData> LegacyParty => party;
        public Inventory Inventory => inventory;
        public VocabularyProgress Vocabulary => vocabulary;
        public PlayerRecord Record => record;
        public WorldState World => world;
        public IReadOnlyList<string> SkillSlots => skillSlots;
        public bool HasSkillSlots => hasSkillSlots;
        public IReadOnlyList<string> Tutorials => tutorials ?? (IReadOnlyList<string>)new List<string>();
        public IReadOnlyList<CompanionSaveData> Companions => companions ?? (IReadOnlyList<CompanionSaveData>)new List<CompanionSaveData>();
        public string PartyCompanionId => partyCompanionId ?? "";
        public IReadOnlyList<string> Notices => notices ?? (IReadOnlyList<string>)new List<string>();

        public SaveData() { }

        public SaveData WithTutorials(IEnumerable<string> ids)
        {
            tutorials = new List<string>(ids);
            return this;
        }

        public SaveData WithNotices(IEnumerable<string> ids)
        {
            notices = new List<string>(ids);
            return this;
        }

        public SaveData WithCompanions(IEnumerable<CompanionSaveData> saved, string partyId)
        {
            companions = new List<CompanionSaveData>(saved);
            partyCompanionId = partyId ?? "";
            return this;
        }

        public SaveData WithSkillSlots(IEnumerable<string> keys)
        {
            skillSlots = new List<string>(keys);
            hasSkillSlots = true;
            return this;
        }

        public SaveData(DateTime savedAtUtc, HeroSaveData hero, List<RelicSaveData> relics, Inventory inventory,
            VocabularyProgress vocabulary, PlayerRecord record, WorldState world = null)
        {
            savedAtTicks = savedAtUtc.Ticks;
            this.hero = hero;
            this.relics = relics ?? new List<RelicSaveData>();
            this.inventory = inventory;
            this.vocabulary = vocabulary;
            this.record = record;
            this.world = world ?? new WorldState();
        }

        // 테스트·변환용: v1 형식(몬스터 파티) 세이브를 만든다
        public static SaveData LegacyV1(DateTime savedAtUtc, List<MonsterSaveData> party, Inventory inventory,
            VocabularyProgress vocabulary, PlayerRecord record, WorldState world = null)
        {
            var data = new SaveData(savedAtUtc, new HeroSaveData(1, 0, 0), new List<RelicSaveData>(), inventory, vocabulary, record, world)
            {
                version = 1,
                party = party ?? new List<MonsterSaveData>()
            };
            return data;
        }

        public string ToJson() => JsonUtility.ToJson(this, true);

        // 형식이 깨졌으면 예외
        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("세이브 파일이 비어 있습니다");
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.version <= 0) throw new FormatException("세이브 형식이 아닙니다");
            if (data.version > CurrentVersion) throw new SaveTooNewException(data.version);
            data.hero = data.hero ?? new HeroSaveData(1, 0, 0);
            data.relics = data.relics ?? new List<RelicSaveData>();
            data.party = data.party ?? new List<MonsterSaveData>();
            data.inventory = data.inventory ?? new Inventory();
            data.vocabulary = data.vocabulary ?? new VocabularyProgress();
            data.record = data.record ?? new PlayerRecord();
            data.world = data.world ?? new WorldState();
            data.skillSlots = data.skillSlots ?? new List<string>();
            return data;
        }
    }

    // 더 새 버전의 앱이 쓴 세이브 (예: TestFlight에서 예전 빌드를 다시 설치). 깨진 것이 아니므로 지우거나 치우지 않는다 (#48)
    public class SaveTooNewException : FormatException
    {
        public int Version { get; }

        public SaveTooNewException(int version) : base($"더 새로운 버전의 세이브입니다 (v{version}). 앱을 업데이트하세요")
        {
            Version = version;
        }
    }
}
