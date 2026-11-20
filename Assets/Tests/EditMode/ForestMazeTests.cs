using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 1.4 숲 넓히기 — 뒤섞인 이정표 미로 (#279, GDD 9-15): 이정표를 바로잡으면 가장 가까운 헷갈 안개가 걷힘, 맴도는 안개는 늘 막힘
    public class SignpostTests
    {
        //  y=5  #########
        //  y=4  #..B....#   보스 (3,4)
        //  y=3  ##GGG####   보스 방 문
        //  y=2  #K.MM...#   열쇠 (1,2) · 헷갈 안개 (3,2)(4,2)
        //  y=1  ###W#X.P#   맴도는 안개 (3,1) · 이정표 (5,1) · 시작 (7,1)
        //  y=0  #########
        private const string Map = "#########\n#..B....#\n##GGG####\n#K.MM...#\n###W#X.P#\n#########";
        private static readonly Vector2Int Post = new Vector2Int(5, 1);

        private static FieldArea Area()
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var raccoon = TestData.Species("raccoon", new MonsterStats(40, 1, 5), new MonsterStats(0, 0, 0), bite);
            var key = TestData.Item("maze_key").Set("displayName", "숲 열쇠").Set("kind", ItemKind.Key);
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "maze_test").Set("displayName", "숲").Set("map", Map).Set("words", words).Set("theme", FieldTheme.Forest)
                .Set("boss", new BossEncounter(raccoon, 3)).Set("bossKey", key).Set("bossGateWords", 1);
        }

        private static GameSession Session() => GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 3);

        [Test]
        public void EachSignpostClearsItsNearestHaze()
        {
            var map = Area().Map;
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(3, 2), new Vector2Int(4, 2) }, map.HazeOf(Post).ToArray());
            Assert.AreEqual(Post, map.SignpostOf(new Vector2Int(4, 2)));
            Assert.IsNull(map.SignpostOf(new Vector2Int(3, 1)), "맴도는 안개는 맡은 이정표가 없음");
            Assert.IsNull(map.SignpostOf(new Vector2Int(6, 1)), "안개가 아닌 칸");
        }

        [Test]
        public void FixingASignpostOpensItsHazeButNotTheDecoy()
        {
            var area = Area();
            var session = Session();
            var haze = new Vector2Int(4, 2);
            var decoy = new Vector2Int(3, 1);
            var key = new Vector2Int(1, 2);
            Assert.IsTrue(Signposts.Loops(area, session.World, haze), "바로잡기 전 안개 = 맴돎");
            Assert.IsTrue(Signposts.Loops(area, session.World, decoy));
            Assert.IsFalse(CellState.IsPassable(area, session, haze));
            Assert.IsFalse(FieldReach.Touches(Signposts.Reach(area, session), key), "열쇠는 안개 너머");
            Assert.AreEqual(0, Signposts.FixedCount(area, session.World));

            var cleared = Signposts.Fix(area, session.World, Post);
            Assert.AreEqual(2, cleared.Count);
            Assert.IsTrue(Signposts.IsFixed(area, session.World, Post));
            Assert.IsTrue(CellState.IsDone(area, session, Post), "바로잡은 이정표 그림");
            Assert.IsTrue(CellState.IsDone(area, session, haze), "걷힌 안개 그림");
            Assert.IsTrue(CellState.IsPassable(area, session, haze));
            Assert.IsFalse(Signposts.Loops(area, session.World, haze));
            Assert.IsTrue(Signposts.Loops(area, session.World, decoy), "가짜 길은 바로잡아도 그대로");
            Assert.IsFalse(CellState.IsPassable(area, session, decoy));
            Assert.IsTrue(FieldReach.Touches(Signposts.Reach(area, session), key));
            Assert.AreEqual(0, Signposts.Fix(area, session.World, Post).Count, "두 번은 없음");
            Assert.AreEqual(0, Signposts.Fix(area, session.World, new Vector2Int(6, 1)).Count, "이정표가 아닌 칸");
            Assert.AreEqual(1, Signposts.FixedCount(area, session.World));
        }

        [Test]
        public void TheWalkerPassesHazeOnlyAfterItClears()
        {
            var area = Area();
            var session = Session();
            var walker = new FieldWalker(area.Map, new Vector2Int(5, 2), cleared: cell => CellState.IsPassable(area, session, cell));
            var bump = walker.TryStep(Direction.Left);
            Assert.AreEqual(StepKind.Blocked, bump.Kind);
            Assert.AreEqual(FieldTile.Haze, bump.TargetTile);
            Signposts.Fix(area, session.World, Post);
            Assert.AreEqual(StepKind.Moved, walker.TryStep(Direction.Left).Kind);
            Assert.AreEqual(StepKind.Blocked, walker.TryStep(Direction.Down).Kind, "아래 = 맴도는 안개");
        }

        [Test]
        public void TheObjectivePointsAtTheSignpostUntilTheKeyIsReachable()
        {
            var area = Area();
            var session = Session();
            session.Record.RecordBattle(true, 1, 0);
            var goal = Objectives.Current(session, area);
            Assert.AreEqual(ObjectiveKind.FixSignposts, goal.Kind);
            Assert.AreEqual(Post, goal.Cell, "닿는 이정표에 별 (미로 입구를 찾게)");
            Signposts.Fix(area, session.World, Post);
            Assert.AreEqual(ObjectiveKind.FindKey, Objectives.Current(session, area).Kind, "열쇠에 닿으면 열쇠 찾기 (별 없음)");
            BossGate.TakeKey(area, session);
            Assert.AreEqual(ObjectiveKind.GatherWords, Objectives.Current(session, area).Kind);

            var other = Session();
            other.Record.RecordBattle(true, 1, 0);
            BossGate.TakeKey(area, other); // 예전 세이브: 넓히기 전에 이미 열쇠를 주움
            Assert.AreEqual(ObjectiveKind.GatherWords, Objectives.Current(other, area).Kind, "열쇠가 있으면 이정표는 목표가 아님");
        }

        [Test]
        public void FixedSignpostsSurviveSaveAndOldSavesHaveNone()
        {
            var area = Area();
            var world = new WorldState();
            Signposts.Fix(area, world, Post);
            var loaded = JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(world));
            Assert.IsTrue(Signposts.IsFixed(area, loaded, Post));
            var old = JsonUtility.FromJson<WorldState>("{\"areaId\":\"forest\"}");
            Assert.IsFalse(Signposts.IsFixed(area, old, Post), "1.3 세이브(칸 없음)도 안전");
        }
    }

    // 뒤섞인 철자 문제 (#279 사용자: '뒤섞인 철자 맞히기')
    public class SpellingQuizTests
    {
        private static string Sorted(string s) => new string(s.ToLowerInvariant().OrderBy(c => c).ToArray());

        [Test]
        public void MisspellingsUseTheSameLettersAndKeepTheFirstOne()
        {
            var wrong = SpellingQuiz.Misspellings("forest");
            Assert.GreaterOrEqual(wrong.Count, 3);
            CollectionAssert.AllItemsAreUnique(wrong);
            foreach (var s in wrong)
            {
                Assert.AreNotEqual("forest", s);
                Assert.AreEqual(Sorted("forest"), Sorted(s), s);
                Assert.AreEqual('f', s[0], "첫 글자는 그대로 — 그럴듯하게");
            }
        }

        [Test]
        public void RealWordsAreNeverWrongChoices()
        {
            var wrong = SpellingQuiz.Misspellings("east", new[] { "eats" });
            CollectionAssert.DoesNotContain(wrong, "eats", "다른 진짜 단어는 틀린 보기로 안 씀");
            Assert.IsFalse(SpellingQuiz.Fits(new WordEntry("", "east", "동쪽"), new[] { "eats" }), "틀린 보기를 3개 못 만들면 안 냄");
        }

        [Test]
        public void OnlySingleWordsOfTheRightLengthFit()
        {
            Assert.IsTrue(SpellingQuiz.Fits(new WordEntry("", "forest", "숲")));
            Assert.IsFalse(SpellingQuiz.Fits(new WordEntry("", "take part", "참가하다")), "띄어쓰기");
            Assert.IsFalse(SpellingQuiz.Fits(new WordEntry("", "well-known", "유명한")), "하이픈");
            Assert.IsFalse(SpellingQuiz.Fits(new WordEntry("", "cat", "고양이")), "너무 짧음");
            Assert.IsFalse(SpellingQuiz.Fits(new WordEntry("", "aaaa", "?")), "같은 글자뿐");
        }

        [Test]
        public void AQuestionHasOneRightSpellingAmongFourAnagrams()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var word = new WordEntry("", seed % 2 == 0 ? "mutual" : "suspicious", "상호의");
                var q = SpellingQuiz.Make(word, new System.Random(seed));
                Assert.AreEqual(4, q.Choices.Count);
                CollectionAssert.AllItemsAreUnique(q.Choices);
                Assert.AreEqual(word.English, q.CorrectAnswer);
                Assert.AreEqual(1, q.Choices.Count(c => c == word.English));
                foreach (var c in q.Choices) Assert.AreEqual(Sorted(word.English), Sorted(c), "보기는 모두 같은 글자 (글자 수로는 못 고름)");
                Assert.AreNotEqual(word.English, q.Scrambled, "뒤섞인 글자는 답이 아님");
                CollectionAssert.DoesNotContain(q.Choices, q.Scrambled, "뒤섞인 글자는 보기와도 다름");
                Assert.AreEqual(Sorted(word.English), Sorted(q.Scrambled));
            }
            var a = SpellingQuiz.Make(new WordEntry("", "forest", "숲"), new System.Random(7));
            var b = SpellingQuiz.Make(new WordEntry("", "forest", "숲"), new System.Random(7));
            CollectionAssert.AreEqual(a.Choices.ToArray(), b.Choices.ToArray(), "같은 rng면 같은 문제 (결정성)");
            Assert.AreEqual(a.Scrambled, b.Scrambled);
            Assert.AreEqual("T S E R O F", SpellingQuiz.Spaced("tserof"));
        }

        // 이정표 문제: 철자로 못 내는 단어는 건너뛰고, 틀리면 벌점 없이 다시 — 숙련도는 처음 답만
        [Test]
        public void ASignpostQuizSkipsPhrasesAndRecordsOnlyTheFirstAnswer()
        {
            var provider = new ListQuizProvider(new WordEntry("p", "take part", "참가하다"), new WordEntry("f", "forest", "숲"));
            var quiz = new SignpostQuiz(provider, new System.Random(3));
            Assert.AreEqual("forest", quiz.Current.Word.English, "띄어쓰기 있는 말은 건너뜀");
            int wrong = (quiz.Current.CorrectIndex + 1) % 4;
            Assert.AreEqual(TabletAnswer.Wrong, quiz.Answer(wrong));
            Assert.IsFalse(quiz.IsSolved);
            Assert.AreEqual(TabletAnswer.Correct, quiz.Answer(quiz.Current.CorrectIndex));
            Assert.IsTrue(quiz.IsSolved);
            Assert.AreEqual(TabletAnswer.Ignored, quiz.Answer(quiz.Current.CorrectIndex));
            CollectionAssert.AreEqual(new[] { false }, provider.Submitted, "처음 고른 답(틀림)만 기록");
            Assert.AreEqual("f", provider.SubmittedIds.Single(), "기록하는 단어 = 낸 단어");
        }

        private class ListQuizProvider : IQuizProvider
        {
            private readonly Queue<WordEntry> words;
            public readonly List<bool> Submitted = new List<bool>();
            public readonly List<string> SubmittedIds = new List<string>();

            public ListQuizProvider(params WordEntry[] words) => this.words = new Queue<WordEntry>(words);

            public QuizQuestion NextQuestion(QuizDirection preferredDirection)
            {
                var word = words.Count > 1 ? words.Dequeue() : words.Peek();
                return new QuizQuestion(word, preferredDirection, false, word.English, new[] { word.Meaning, "a", "b", "c" }, 0);
            }

            public MasteryChange SubmitAnswer(QuizQuestion question, bool correct)
            {
                Submitted.Add(correct);
                SubmittedIds.Add(question.Word.Id);
                return new MasteryChange(question.Word.Id, MasteryLevel.Learning, MasteryLevel.Learning, correct);
            }
        }
    }

    // 실제 숲 (#279): 72×44 · 이정표를 차례로 바로잡으면 모두 닿음 · 바로잡기 전엔 열쇠에 못 감 · 예전 상자는 같은 자리에 같은 내용물
    public class ForestMazeDataTests
    {
        private static FieldArea Forest() => AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/forest.asset");

        [Test]
        public void TheForestIsTwiceAsWideAndKeepsItsOldCells()
        {
            var forest = Forest();
            Assert.AreEqual(72, forest.Map.Width);
            Assert.AreEqual(44, forest.Map.Height);
            var source = FieldMap.Parse(WordRPG.EditorTools.SampleDataBuilder.ForestMap);
            for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
                Assert.AreEqual(source.Get(new Vector2Int(x, y)), forest.Map.Get(new Vector2Int(x, y)), $"에셋 맵 = 원본 상수 ({x},{y})");
            Assert.AreEqual(5, Signposts.All(forest).Count);
        }

        // 예전 숲의 상자 7개 (위치는 1.3과 같음 — 상자 id "forest:x,y"와 연 기록이 그대로 맞음)
        [Test]
        public void OldChestsKeepTheirPlaceAndContents()
        {
            var forest = Forest();
            void Has(int x, int y, string what, Func<ChestContent, bool> check)
            {
                var content = forest.GetChestContent(new Vector2Int(x, y));
                Assert.IsNotNull(content, $"({x},{y}) {what}");
                Assert.IsTrue(check(content), $"({x},{y}) {what}");
            }
            Has(2, 40, "덩굴 채찍", c => c.Relic != null && c.Relic.name == "relic_whip");
            Has(14, 38, "단단한 표지 3", c => c.Item != null && c.Item.ItemId == "hard_cover" && c.Count == 3);
            Has(34, 31, "빛나는 잉크 3", c => c.Item != null && c.Item.ItemId == "shiny_ink" && c.Count == 3);
            Has(17, 22, "반짝 가루 3", c => c.Item != null && c.Item.ItemId == "sparkle_dust" && c.Count == 3);
            Has(33, 12, "시간의 모래시계", c => c.Relic != null && c.Relic.name == "relic_hourglass");
            Has(8, 2, "생존일지", c => c.Item != null && c.Item.ItemId == "journal_survival");
            Has(33, 1, "150G", c => c.Item == null && c.Gold == 150);
            Assert.AreEqual(forest.Map.Chests.Count, forest.ChestContents.Count, "상자 수 = 내용물 수");
            Assert.AreEqual(12, forest.ChestContents.Count);
        }

        [Test]
        public void FixingSignpostsInOrderReachesEverything()
        {
            var forest = Forest();
            var map = forest.Map;
            var posts = Signposts.All(forest);
            var fixedPosts = new HashSet<Vector2Int>();
            bool Cleared(Vector2Int cell) => map.SignpostOf(cell) is Vector2Int p && fixedPosts.Contains(p);
            var key = map.Find(FieldTile.Key).Value;
            Assert.IsFalse(FieldReach.Touches(FieldReach.Flood(map, false, true, null, Cleared), key), "이정표를 안 바로잡으면 열쇠에 못 감");
            while (true)
            {
                var reach = FieldReach.Flood(map, false, true, null, Cleared);
                var next = posts.Where(p => !fixedPosts.Contains(p) && FieldReach.Touches(reach, p)).ToList();
                if (next.Count == 0) break;
                fixedPosts.UnionWith(next);
            }
            CollectionAssert.AreEquivalent(posts, fixedPosts, "모든 이정표에 차례로 닿음");
            var all = FieldReach.Flood(map, false, true, null, Cleared);
            Assert.IsTrue(FieldReach.Touches(all, key));
            foreach (var chest in map.Chests) Assert.IsTrue(FieldReach.Touches(all, chest), $"상자 {chest}");
            Assert.IsFalse(FieldReach.Touches(all, map.BossPosition.Value), "보스는 문 안에서만");
        }

        // 헷갈 안개 덩어리마다 맡은 이정표가 하나, 이정표마다 덩어리 하나 — 바로잡으면 그 자리 옆의 안개가 걷힘
        [Test]
        public void EveryHazeHasExactlyOneSignpostNextToIt()
        {
            var map = Forest().Map;
            var owners = new Dictionary<Vector2Int, Vector2Int>();
            foreach (var post in map.FindAll(FieldTile.Signpost))
            {
                var haze = map.HazeOf(post);
                Assert.IsNotEmpty(haze, $"이정표 {post}");
                Assert.LessOrEqual(haze.Min(c => Mathf.Abs(c.x - post.x) + Mathf.Abs(c.y - post.y)), 2, $"이정표 {post} 바로 옆의 안개");
                foreach (var cell in haze)
                {
                    Assert.IsFalse(owners.ContainsKey(cell), $"안개 {cell}를 이정표 둘이 맡음");
                    owners[cell] = post;
                }
            }
            foreach (var cell in map.FindAll(FieldTile.Haze))
                Assert.IsTrue(owners.ContainsKey(cell), $"안개 {cell}를 맡은 이정표가 없음");
        }

    }
}
