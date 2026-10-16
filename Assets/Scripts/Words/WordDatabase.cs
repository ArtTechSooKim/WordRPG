using System.Collections.Generic;
using UnityEngine;
using WordRPG.Items;

namespace WordRPG.Words
{
    // 지역(난이도) 하나의 단어장. Assets/Data/Words/*.csv 를 임포트해서 만든다 (메뉴: WordRPG > Data > Import Word CSVs)
    [CreateAssetMenu(fileName = "NewWordDatabase", menuName = "WordRPG/Word Database", order = 0)]
    public class WordDatabase : ScriptableObject
    {
        [Tooltip("난이도 단계. 1 = 초원")]
        [SerializeField] private int tier = 1;

        [Header("지역 도감")]
        [Tooltip("세이브에 기록되는 id (예: meadow). 정한 뒤에는 바꾸지 말 것")]
        [SerializeField] private string regionId;
        [SerializeField] private string regionName; // 예: "초원"
        [Tooltip("도감을 모두 채우면 받는 징표(기념물). 지역 특색에 맞게")]
        [SerializeField] private ItemData completionKeepsake;
        [SerializeField] private int completionGold;
        [Tooltip("이야기 순서 (1 = 초원, 2 = 서고, 3 = 숲, 4 = 정글…) — 징표 진열장이 이 순서로 놓는다 (#97). 새 지역은 다음 번호")]
        [SerializeField] private int storyOrder;

        [SerializeField] private List<WordEntry> words = new List<WordEntry>();

        public int Tier => tier;
        public string RegionId => regionId;
        public string RegionName => regionName;
        public ItemData CompletionKeepsake => completionKeepsake;
        public int CompletionGold => completionGold;
        public int StoryOrder => storyOrder;
        public IReadOnlyList<WordEntry> Words => words;

        // CSV 임포터 전용. 런타임에서는 호출하지 않는다
        public void ReplaceWords(IEnumerable<WordEntry> newWords)
        {
            words = new List<WordEntry>(newWords);
        }
    }
}
