using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Items;

namespace WordRPG.Words
{
    // 도감 보상 하나 (#147): 이 단어장에서 단어를 정해진 수만큼 발견하면 받는 징표 + 골드. 징표 진열장 한 칸이 된다
    [Serializable]
    public class DexReward
    {
        [Tooltip("세이브에 기록되는 id (예: meadow). 정한 뒤에는 바꾸지 말 것")]
        [SerializeField] private string regionId;
        [SerializeField] private string regionName; // 진열장에 보이는 이름. 예: "초원"
        [SerializeField] private ItemData keepsake;
        [SerializeField] private int gold;
        [Tooltip("발견해야 하는 단어 수. 0이면 단어장을 모두 채웠을 때")]
        [SerializeField] private int wordsNeeded;
        [Tooltip("이야기 순서 (1 = 초원, 2 = 서고, 3 = 숲, 4 = 정글…) — 징표 진열장이 이 순서로 놓는다 (#97). 새 지역은 다음 번호")]
        [SerializeField] private int storyOrder;

        public string RegionId => regionId;
        public string RegionName => string.IsNullOrEmpty(regionName) ? regionId : regionName;
        public ItemData Keepsake => keepsake;
        public int Gold => gold;
        public int StoryOrder => storyOrder;

        public DexReward(string regionId, string regionName, ItemData keepsake, int gold, int wordsNeeded = 0, int storyOrder = 0)
        {
            this.regionId = regionId;
            this.regionName = regionName;
            this.keepsake = keepsake;
            this.gold = gold;
            this.wordsNeeded = wordsNeeded;
            this.storyOrder = storyOrder;
        }

        // 단어장 크기가 total일 때 발견해야 하는 수 (단어장이 줄어도 받을 수 있게 total을 넘지 않음)
        public int WordsNeeded(int total) => wordsNeeded > 0 ? Mathf.Min(wordsNeeded, total) : total;
    }

    // 지역(난이도) 하나의 단어장. Assets/Data/Words/*.csv 를 임포트해서 만든다 (메뉴: WordRPG > Data > Import Word CSVs)
    [CreateAssetMenu(fileName = "NewWordDatabase", menuName = "WordRPG/Word Database", order = 0)]
    public class WordDatabase : ScriptableObject
    {
        [Tooltip("난이도 단계. 1 = 초원·서고, 2 = 숲·정글·동굴 (#147 — 여러 지역이 단어장 하나를 같이 씀)")]
        [SerializeField] private int tier = 1;

        [Header("지역 도감")]
        [SerializeField] private string regionId;
        [SerializeField] private string regionName; // 도감에 보이는 이름. 예: "초원·서고"
        [Tooltip("발견한 단어 수에 닿을 때마다 받는 보상 (적은 수부터). 지역 특색에 맞는 징표 + 골드")]
        [SerializeField] private List<DexReward> rewards = new List<DexReward>();

        [SerializeField] private List<WordEntry> words = new List<WordEntry>();

        public int Tier => tier;
        public string RegionId => regionId;
        public string RegionName => regionName;
        public IReadOnlyList<DexReward> Rewards => rewards;
        public IReadOnlyList<WordEntry> Words => words;

        // 샘플 데이터 생성 전용. 런타임에서는 호출하지 않는다
        public void ConfigureRegion(string id, string displayName, IEnumerable<DexReward> newRewards)
        {
            regionId = id;
            regionName = displayName;
            rewards = new List<DexReward>(newRewards);
        }

        // CSV 임포터 전용. 런타임에서는 호출하지 않는다
        public void ReplaceWords(IEnumerable<WordEntry> newWords)
        {
            words = new List<WordEntry>(newWords);
        }
    }
}
