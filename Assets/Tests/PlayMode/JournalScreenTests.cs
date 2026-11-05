using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;
using Object = UnityEngine.Object;

namespace WordRPG.Tests
{
    // 누군가의 기록 화면 (#200): 상자에서 얻으면 도감에 버튼이 생기고, 찾은 단어의 줄만 읽힌다
    public class JournalScreenTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);

        private Canvas canvas;
        private DexView dex;
        private WordDatabase book;
        private ItemData journal;
        private GameSession session;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("JournalCanvas", typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, 1920);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            dex = DexView.Create(canvas.transform);

            journal = TestData.Item("journal_diary").Set("displayName", "누군가의 일기").Set("kind", ItemKind.Journal);
            book = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "common").Set("regionName", "초원·서고").Set("journal", journal);
            book.ReplaceWords(new[]
            {
                new WordEntry("lose", "lose", "잃다", "v", "If we lose a word, we lose a memory too.", "말을 하나 잃으면, 기억도 하나 잃는다.", 1, "첫 빈칸"),
                new WordEntry("sign", "sign", "징후", "n", "The first sign was a small blank on my page.", "첫 징조는 작은 빈칸이었다.", 2),
                new WordEntry("gene", "gene", "유전자", "n", "This gene affects the color of your eyes.", "이 유전자는 눈 색깔에 영향을 준다."),
            });
            session = GameSession.NewGame(TestData.Hero(new MonsterStats(60, 14, 10)), 5);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(canvas.gameObject);
            Object.Destroy(book);
        }

        private Button JournalButton => FindButton(canvas.transform, "DexJournalButton");
        private RectTransform CloseRect => (RectTransform)FindButton(canvas.transform, "DexCloseButton").transform;

        [UnityTest]
        public IEnumerator TheButtonAppearsOnlyAfterFindingTheJournal()
        {
            dex.Show(book, session, Now);
            yield return null;
            Assert.IsFalse(JournalButton.gameObject.activeSelf, "상자에서 얻기 전에는 버튼이 없음");
            Assert.AreEqual(0.05f, CloseRect.anchorMin.x, 0.001f, "지금처럼 [닫기]만");

            dex.Hide();
            session.Inventory.Add(journal);
            dex.Show(book, session, Now);
            yield return null;
            Assert.IsTrue(JournalButton.gameObject.activeSelf);
            Assert.AreEqual("누군가의 일기", UiKit.LabelOf(JournalButton).text);
            Assert.AreEqual(0.51f, CloseRect.anchorMin.x, 0.001f, "[일기] [닫기] 나란히");

            // 다른 단어장을 열면 (그 단어장의 기록을 안 가졌으므로) 다시 없음
            dex.Hide();
            var other = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "advanced").Set("regionName", "숲");
            other.ReplaceWords(TestData.SampleWords());
            dex.Show(other, session, Now);
            yield return null;
            Assert.IsFalse(JournalButton.gameObject.activeSelf);
            Assert.AreEqual(0.05f, CloseRect.anchorMin.x, 0.001f);
            Object.Destroy(other);
        }

        [UnityTest]
        public IEnumerator FoundWordsMakeTheirLinesReadable()
        {
            session.Inventory.Add(journal);
            session.Vocabulary.Discover("lose", Now);
            dex.Show(book, session, Now);
            JournalButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(dex.IsJournalOpen);
            var view = dex.Journal;
            Assert.AreEqual(1, view.ReadableLines);
            Assert.AreEqual(1, view.LostLines, "예문만 있는 단어(gene)는 기록에 없음");
            string text = AllText(view.Root.GetComponent<Image>());
            StringAssert.Contains("누군가의 일기", text);
            StringAssert.Contains("첫 빈칸", text);
            StringAssert.Contains("lose</color></b> a word", text, "찾은 줄: 단어를 굵게");
            StringAssert.Contains("말을 하나 잃으면", text);
            StringAssert.DoesNotContain("first", text, "못 찾은 줄의 글자는 화면에 아예 없음");
            StringAssert.DoesNotContain("빈칸이었다", text);

            var smudges = view.Root.GetComponentsInChildren<RawImage>();
            Assert.AreEqual(2, smudges.Length, "못 찾은 줄 = 영어·해석 자리의 필기 그림");
            foreach (var smudge in smudges)
            {
                Assert.IsNotNull(smudge.texture);
                Assert.GreaterOrEqual(smudge.uvRect.xMin, 0f);
                Assert.LessOrEqual(smudge.uvRect.xMax, 1f);
                Assert.GreaterOrEqual(smudge.uvRect.yMin, 0f);
                Assert.LessOrEqual(smudge.uvRect.yMax, 1f);
            }

            // 단어를 찾고 다시 펼치면 선명해진다
            session.Vocabulary.Discover("sign", Now);
            view.Hide();
            JournalButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(2, view.ReadableLines);
            Assert.AreEqual(0, view.LostLines);
            StringAssert.Contains("sign</color></b> was", AllText(view.Root.GetComponent<Image>()));
            Assert.AreEqual(0, view.Root.GetComponentsInChildren<RawImage>().Length, "지난번 줄이 남지 않음");
        }

        [UnityTest]
        public IEnumerator BackClosesTheJournalBeforeTheDex()
        {
            session.Inventory.Add(journal);
            dex.Show(book, session, Now);
            JournalButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(dex.IsJournalOpen);

            dex.Back();
            Assert.IsFalse(dex.IsJournalOpen);
            Assert.IsTrue(dex.IsOpen, "뒤로가기 한 번 = 기록만 닫힘");
            dex.Back();
            Assert.IsFalse(dex.IsOpen);

            // 도감을 통째로 닫아도 기록이 남아 있지 않음 (전투가 끝날 때 등)
            dex.Show(book, session, Now);
            JournalButton.onClick.Invoke();
            yield return null;
            dex.Hide();
            Assert.IsFalse(dex.IsJournalOpen);
            Assert.IsFalse(dex.IsOpen);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator TheRealBooksOpenWithBothKindsOfLines()
        {
            foreach (var path in new[] { "Assets/Data/Words/tier1_common.asset", "Assets/Data/Words/tier2_advanced.asset" })
            {
                var realBook = UnityEditor.AssetDatabase.LoadAssetAtPath<WordDatabase>(path);
                session.Inventory.Add(realBook.Journal);
                foreach (var word in realBook.Words.Where((_, i) => i % 2 == 0)) session.Vocabulary.Discover(word.Id, Now);
                dex.Show(realBook, session, Now);
                JournalButton.onClick.Invoke();
                yield return null;
                var view = dex.Journal;
                Assert.Greater(view.ReadableLines, 0, path);
                Assert.Greater(view.LostLines, 0, path);
                var scroll = view.Root.GetComponentInChildren<ScrollRect>();
                Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height, $"{path}: 내용이 넘쳐서 밀 수 있음");
                Assert.AreEqual(1f, scroll.verticalNormalizedPosition, 0.01f, $"{path}: 맨 위부터");
                dex.Hide();
            }
        }
#endif

        // 실제 흐름: 상자에서 얻음 → 가방에 안내 → 도감 버튼 → 뒤로가기 순서
        [UnityTest]
        public IEnumerator TheChestGivesItAndTheFieldDexOpensIt()
        {
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "library").Set("displayName", "잊혀진 서고").Set("map", "#####\n#PC##\n#####")
                .Set("encounters", ScriptableObject.CreateInstance<EncounterTable>()).Set("words", book)
                .Set("chests", new List<ChestContent> { new ChestContent(journal, 1) });
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new[] { journal }, new[] { area });
            var field = new GameObject("JournalFieldUnderTest").AddComponent<FieldScreen>();
            field.Configure(area, session, () => { }, step: 0.05f, animScale: 0.01f, gameDatabase: database);
            yield return null;
            yield return null;

            var hud = field.transform.Find("FieldHud/SafeArea");
            FindButton(hud, "DexButton").onClick.Invoke();
            Assert.IsFalse(FindButton(hud, "DexJournalButton").gameObject.activeSelf, "상자를 열기 전");
            FindButton(hud, "DexCloseButton").onClick.Invoke();

            yield return FaceStick(field, Direction.Right);
            yield return PressConfirm(field);
            yield return WaitFor(() => session.Inventory.GetCount(journal) == 1);
            yield return new WaitForSecondsRealtime(0.1f);

            FindButton(hud, "BagButton").onClick.Invoke();
            var bag = hud.Find("InventoryView");
            FindButton(bag, "TabItems").onClick.Invoke();
            StringAssert.Contains("도감에서 펼쳐 볼 수 있어요", AllText(bag), "가방에는 여는 곳만 안내");
            FindButton(bag, "InventoryCloseButton").onClick.Invoke();

            FindButton(hud, "DexButton").onClick.Invoke();
            FindButton(hud, "DexJournalButton").onClick.Invoke();
            yield return null;
            Assert.IsTrue(hud.Find("JournalView").gameObject.activeSelf);
            field.HandleBack();
            Assert.IsFalse(hud.Find("JournalView").gameObject.activeSelf, "뒤로가기 = 기록만 닫힘");
            Assert.IsTrue(field.IsPanelOpen, "도감은 그대로");
            field.HandleBack();
            Assert.IsFalse(field.IsPanelOpen);

            Object.Destroy(field.gameObject);
            Object.Destroy(area);
            Object.Destroy(database);
        }
    }
}
