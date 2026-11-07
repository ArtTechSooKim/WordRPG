using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordRPG.Field
{
    // 인물이 움직이는 모습 (에셋에 번호로 저장 — 새 종류는 맨 뒤)
    public enum NpcMotionKind
    {
        Stand,  // 서서 가끔 둘러봄
        Pace,   // 집 둘레를 왔다 갔다
        Stroll, // 아주 천천히, 한 걸음씩
        Wander, // 갈팡질팡
        Sit,    // 쪼그려 앉아 있다가 가끔 일어남
        Busy,   // 손을 바쁘게 움직임
        Hop     // 제자리에서 콩콩
    }

    // 말 한 단계: 조건을 채우면 이 말과 움직임으로 바뀐다 (#209 — 말이 되돌아옴)
    [Serializable]
    public class NpcStage
    {
        [Tooltip("이 지역 보스를 물리쳐야 함 (없으면 비워 둠)")]
        [SerializeField] private FieldArea bossOf;
        [Tooltip("그 지역 단어장에서 찾아야 하는 단어 수")]
        [SerializeField] private int wordsNeeded;
        [Tooltip("{이름} = 플레이어가 지은 이름")]
        [SerializeField] private string line;
        [SerializeField] private NpcMotionKind motion;
        [Tooltip("빠르기 배수 (#211): 1 = 보통, 작을수록 느긋함. 걸음·쉬는 시간·일하는 박자에 모두 적용")]
        [SerializeField] private float pace = 1f;

        public float Pace => pace > 0f ? pace : 1f;
        public FieldArea BossOf => bossOf;
        public int WordsNeeded => wordsNeeded;
        public string Line => line;
        public NpcMotionKind Motion => motion;

        private NpcStage() { } // Unity 직렬화용

        public NpcStage(string line, NpcMotionKind motion, FieldArea bossOf = null, int wordsNeeded = 0, float pace = 1f)
        {
            this.line = line;
            this.motion = motion;
            this.pace = pace;
            this.bossOf = bossOf;
            this.wordsNeeded = wordsNeeded;
        }
    }

    // 말을 잃어 가는 사람들 (#209, GDD 9-10): 맵 글자 N(바닥)·n(잔디) 자리에 FieldArea npcs 순서대로 선다.
    // 그림 = Art/NinjaAdventure/Npcs/{npcId}.png. 세이브에 적히지 않음 — 말은 보스 처치·찾은 단어로 정해짐
    [CreateAssetMenu(fileName = "NewNpc", menuName = "WordRPG/Npc", order = 32)]
    public class NpcData : ScriptableObject
    {
        [SerializeField] private string npcId;
        [SerializeField] private string displayName;
        [Tooltip("0 = 사람(4방향 캐릭터 시트). 동물은 프레임 하나의 가로 픽셀")]
        [SerializeField] private int frameWidth;
        [Tooltip("앞에서부터 차례로. 조건을 채운 것 중 맨 뒤의 것을 씀")]
        [SerializeField] private List<NpcStage> stages = new List<NpcStage>();
        [Tooltip("#223: 이 지역 보스를 물리친 뒤 다른 지역 샘으로 갈 수 있게 되면, 주인공이 가까이 올 때 한 번 샘으로 뛰어들어 사라짐 (컷씬). 비면 없음")]
        [SerializeField] private FieldArea springLeapAfter;
        [Tooltip("뛰어들기 전에 하는 말 (말풍선 차례로, {이름} = 플레이어 이름)")]
        [SerializeField] private List<string> springLeapLines = new List<string>();

        [Header("컷씬 (#234 — 숲 나무꾼)")]
        [Tooltip("이 인물이 서 있는 지역에 처음 들어오면: 주인공이 다섯 칸 걸어 들어가고, 이 인물이 달려와 차례로 말한 뒤 제자리로 (보스를 물리치기 전에만). 비면 없음")]
        [SerializeField] private List<string> entryLines = new List<string>();
        [Tooltip("이 지역 보스를 물리친 뒤: 달려와서 하는 말 (그 자리에서)")]
        [SerializeField] private List<string> bossThanksLines = new List<string>();
        [Tooltip("이어서 보스가 연 길 옆으로 가서 하는 말 (주인공은 아직 제자리)")]
        [SerializeField] private List<string> exitCallLines = new List<string>();
        [Tooltip("주인공이 길 옆으로 온 뒤 하는 말. 끝나면 화면 밖으로 걸어가 제자리로")]
        [SerializeField] private List<string> exitStoryLines = new List<string>();

        public string NpcId => npcId;
        public string DisplayName => displayName;
        public int FrameWidth => frameWidth;
        public bool IsAnimal => frameWidth > 0;
        public IReadOnlyList<NpcStage> Stages => stages;
        public FieldArea SpringLeapAfter => springLeapAfter;
        public bool LeapsIntoSpring => springLeapAfter != null;
        public IReadOnlyList<string> SpringLeapLines => springLeapLines;
        public IReadOnlyList<string> EntryLines => entryLines;
        public IReadOnlyList<string> BossThanksLines => bossThanksLines;
        public IReadOnlyList<string> ExitCallLines => exitCallLines;
        public IReadOnlyList<string> ExitStoryLines => exitStoryLines;
        public bool BriefsOnEntry => entryLines != null && entryLines.Count > 0;
        public bool SeesOffAfterBoss => bossThanksLines != null && bossThanksLines.Count > 0;

        // 샘플 데이터 생성 전용. 런타임에서는 호출하지 않는다
        public void Configure(string id, string name, int animalFrameWidth, IEnumerable<NpcStage> newStages)
        {
            npcId = id;
            displayName = name;
            frameWidth = animalFrameWidth;
            stages = new List<NpcStage>(newStages);
        }

        // 샘플 데이터 생성 전용 (#234)
        public void SetCutscenes(IEnumerable<string> entry, IEnumerable<string> thanks, IEnumerable<string> exitCall, IEnumerable<string> exitStory)
        {
            entryLines = new List<string>(entry);
            bossThanksLines = new List<string>(thanks);
            exitCallLines = new List<string>(exitCall);
            exitStoryLines = new List<string>(exitStory);
        }

        // 샘플 데이터 생성 전용 (#223)
        public void SetSpringLeap(FieldArea after, params string[] lines)
        {
            springLeapAfter = after;
            springLeapLines = new List<string>(lines);
        }
    }
}
