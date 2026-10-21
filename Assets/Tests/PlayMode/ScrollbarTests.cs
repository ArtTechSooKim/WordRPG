using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using Object = UnityEngine.Object;

namespace WordRPG.Tests
{
    // 목록 오른쪽 스크롤 막대 (#122 사용자: 성유물 제단을 아래로 밀 수 있다는 걸 알 수 없음) — 성유물 제단 · 단어 도감
    public class ScrollbarTests
    {
        // 배치모드 화면 크기와 상관없이 게임 기준 크기(1080×1920)로
        private static Canvas MakeCanvas()
        {
            var canvas = new GameObject("ScrollbarCanvas", typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, 1920);
            return canvas;
        }

        private static string Sizes(ScrollRect scroll) =>
            $"목록 칸 {scroll.viewport.rect.height:0} · 내용 {scroll.content.rect.height:0}";

        private static RelicData Relic(int i) =>
            TestData.Relic($"relic_{i}", TestData.Skill($"skill_{i}", SkillKind.Damage, SkillTarget.SingleEnemy, 10), new MonsterStats(0, 1, 0));

        // 막대는 목록 칸 밖(같은 부모)에 있어 잘리지 않고, 카드와 겹치지 않게 오른쪽 옆에
        private static void AssertBesideViewport(ScrollRect scroll, Scrollbar bar)
        {
            Assert.AreSame(bar, scroll.verticalScrollbar);
            Assert.IsFalse(bar.transform.IsChildOf(scroll.viewport), "목록 칸(RectMask2D) 안에 두면 잘림");
            var view = new Vector3[4];
            var track = new Vector3[4];
            scroll.viewport.GetWorldCorners(view);
            ((RectTransform)bar.transform).GetWorldCorners(track);
            Assert.GreaterOrEqual(track[0].x, view[2].x, "막대가 목록 오른쪽 옆 (카드와 겹치지 않음)");
            Assert.AreEqual(view[0].y, track[0].y, 0.5f, "막대 길이 = 목록 칸 높이 (아래)");
            Assert.AreEqual(view[1].y, track[1].y, 0.5f, "막대 길이 = 목록 칸 높이 (위)");
        }

        [UnityTest]
        public IEnumerator RelicAltarShowsScrollbarOnlyWhenCardsOverflow()
        {
            var canvas = MakeCanvas();
            var altar = RelicAltarView.Create(canvas.transform, 0.01f);
            var scroll = altar.Root.GetComponentInChildren<ScrollRect>(true);
            var bar = altar.Root.transform.Find("ScrollTrack").GetComponent<Scrollbar>();
            AssertBesideViewport(scroll, bar);

            // 성유물 1개: 카드가 다 들어가므로 막대 숨김
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(60, 14, 10)), 5);
            session.Hero.RestoreRelic(Relic(0), 0, -1);
            altar.Show(session, null);
            yield return null;
            yield return null;
            Assert.IsFalse(bar.gameObject.activeSelf, $"다 보이면 막대 없음 ({Sizes(scroll)})");

            // 8개 다 모음: 넘치므로 막대가 보이고, 손잡이 = 보이는 비율, 맨 위부터
            for (int i = 1; i < 8; i++) session.Hero.RestoreRelic(Relic(i), 0, -1);
            altar.Hide();
            altar.Show(session, null);
            yield return null;
            yield return null;
            Assert.IsTrue(bar.gameObject.activeSelf, $"넘치면 막대가 보임 ({Sizes(scroll)})");
            float visible = scroll.viewport.rect.height / scroll.content.rect.height;
            Assert.Less(visible, 1f);
            Assert.AreEqual(visible, bar.size, 0.02f, "손잡이 길이 = 보이는 비율");
            Assert.AreEqual(1f, bar.value, 0.01f, "열면 맨 위 (손잡이도 위)");

            // 목록을 맨 아래로 밀면 손잡이도 아래로
            scroll.verticalNormalizedPosition = 0f;
            yield return null;
            Assert.AreEqual(0f, bar.value, 0.01f, "목록을 아래로 → 손잡이도 아래");

            // 막대를 끌어도 목록이 따라 움직임
            bar.value = 0.5f;
            yield return null;
            Assert.AreEqual(0.5f, scroll.verticalNormalizedPosition, 0.01f, "막대를 끌면 목록이 움직임");

            Object.Destroy(canvas.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DexListHasScrollbarBesideIt()
        {
            var canvas = MakeCanvas();
            var dex = DexView.Create(canvas.transform);
            var scroll = dex.Root.GetComponentInChildren<ScrollRect>(true);
            var bar = dex.Root.transform.Find("ScrollTrack").GetComponent<Scrollbar>();
            AssertBesideViewport(scroll, bar);

            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(60, 14, 10)), 5);
            dex.Show(words, session, new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc));
            yield return null;
            yield return null;
            bool overflows = scroll.content.rect.height > scroll.viewport.rect.height + 0.5f;
            Assert.AreEqual(overflows, bar.gameObject.activeSelf, $"단어 목록이 넘칠 때만 막대 ({Sizes(scroll)})");

            Object.Destroy(canvas.gameObject);
            Object.Destroy(words);
            yield return null;
        }
    }
}
