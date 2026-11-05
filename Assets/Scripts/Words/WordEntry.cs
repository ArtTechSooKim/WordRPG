using System;
using UnityEngine;

namespace WordRPG.Words
{
    // 단어 1개. CSV 한 줄과 1:1 대응 (id, english, meaning, pos, example, exampleMeaning, story, storyHeading)
    [Serializable]
    public class WordEntry
    {
        [Tooltip("진행도 저장 키. 바뀌면 숙련도가 초기화되므로 한 번 정하면 고정")]
        [SerializeField] private string id;
        [SerializeField] private string english;      // 예: "abandon"
        [SerializeField] private string meaning;      // 예: "버리다, 포기하다" (쉼표로 뜻 구분)
        [SerializeField] private string partOfSpeech; // v, n, adj, adv
        [SerializeField] private string example;
        [SerializeField] private string exampleMeaning;
        [Tooltip("누군가의 기록 안의 순서 (#200). 0 = 기록에 안 들어가고 예문만")]
        [SerializeField] private int story;
        [Tooltip("이 줄부터 시작하는 묶음의 제목. 비어 있으면 앞 묶음이 이어짐")]
        [SerializeField] private string storyHeading;

        public string Id => id;
        public string English => english;
        public string Meaning => meaning;
        public string PartOfSpeech => partOfSpeech;
        public string Example => example;
        public string ExampleMeaning => exampleMeaning;
        public int Story => story;
        public string StoryHeading => storyHeading ?? "";

        private WordEntry() { } // Unity 직렬화용

        public WordEntry(string id, string english, string meaning, string partOfSpeech = "",
            string example = "", string exampleMeaning = "", int story = 0, string storyHeading = "")
        {
            this.english = english.Trim();
            this.id = string.IsNullOrWhiteSpace(id) ? this.english.ToLowerInvariant() : id.Trim();
            this.meaning = meaning.Trim();
            this.partOfSpeech = (partOfSpeech ?? "").Trim();
            this.example = (example ?? "").Trim();
            this.exampleMeaning = (exampleMeaning ?? "").Trim();
            this.story = story;
            this.storyHeading = (storyHeading ?? "").Trim();
        }

        // "버리다, 포기하다" → ["버리다", "포기하다"]. 오답 보기 중복 판정에 사용
        public string[] GetMeaningParts()
        {
            var parts = meaning.Split(',');
            for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
            return parts;
        }
    }
}
