using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 동료 (#77): 조우 연출 → [함께 가기] → 파티·HUD 배지, 전투에서 동료 차례(기술 + 단어 문제), 가방 > 동료 탭에서 쉬게 하기
    public class CompanionScreenTests
    {
        private const string Map = "#######\n#.....#\n#..P..#\n#######";

        private static (FieldScreen field, GameSession session, CompanionData ara) Create()
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var slime = TestData.Species("slime", new MonsterStats(500, 1, 10), new MonsterStats(0, 0, 0), bite).Set("displayName", "슬라임");
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(slime, 1, 1, 1) });
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "companion_test").Set("displayName", "메아리 정글").Set("map", Map)
                .Set("encounters", table).Set("words", words).Set("encounterRate", 0f);
            var pebble = TestData.Skill("ara_pebble", SkillKind.Damage, SkillTarget.SingleEnemy, 12).Set("displayName", "돌팔매");
            var sprout = TestData.Skill("ara_sprout", SkillKind.Heal, SkillTarget.SingleAlly, 20).Set("displayName", "새싹 치유");
            var ara = ScriptableObject.CreateInstance<CompanionData>()
                .Set("companionId", "ara").Set("displayName", "아라").Set("role", "정글 소녀 · 회복·보조")
                .Set("description", "유적에 갇혀 고대 단어를 지키던 정글 소녀.")
                .Set("baseStats", new MonsterStats(50, 10, 10)).Set("basicSkill", pebble)
                .Set("learnedSkills", new List<CompanionSkill> { new CompanionSkill(1, sprout) });
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 30).Set("displayName", "휘두르기");
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 30, 10)).Set("basicSkill", strike), 5);
            session.Record.RecordBattle(true, 1, 0);
            var field = new GameObject("CompanionField").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f);
            return (field, session, ara);
        }

        [UnityTest]
        public IEnumerator MeetingACompanionAddsHerToTheParty()
        {
            var (field, session, ara) = Create();
            yield return null;
            yield return null;
            Assert.IsFalse(field.IsCompanionBadgeVisible, "동료가 없으면 배지 없음");

            field.StartCoroutine(field.MeetCompanion(ara));
            yield return null;
            Assert.IsTrue(field.CompanionCutscene.IsPlaying, "조우 연출");
            Assert.IsTrue(field.IsPanelOpen, "연출 중에는 움직일 수 없음");
            yield return WaitFor(() => field.CompanionCutscene.ShowsCard, 3f);
            Assert.IsTrue(field.CompanionCutscene.ShowsCard, "'동료를 얻었다!' 카드");
            Assert.IsNull(session.PartyCompanion, "[함께 가기]를 누르기 전");

            ActiveButton(field.transform, "CompanionJoinButton").onClick.Invoke();
            yield return WaitFor(() => !field.CompanionCutscene.IsPlaying, 2f);
            Assert.IsNotNull(session.PartyCompanion);
            Assert.AreEqual("아라", session.PartyCompanion.DisplayName);
            Assert.IsTrue(field.IsCompanionBadgeVisible, "HUD에 동료 배지");
            StringAssert.Contains("아라가 파티에 들어왔다", field.ToastMessage);

            // 가방 > 동료 탭: 쉬게 하기 → 배지가 사라짐 → 다시 함께 가기
            ActiveButton(field.transform, "CompanionBadge").onClick.Invoke(); // 배지를 누르면 가방 > 동료
            yield return null;
            var toggle = ActiveButton(field.transform, "CompanionPartyButton");
            Assert.IsNotNull(toggle, "동료 탭의 [파티에서 쉬게 하기]");
            toggle.onClick.Invoke();
            yield return null;
            Assert.IsNull(session.PartyCompanion);
            Assert.IsFalse(field.IsCompanionBadgeVisible);
            ActiveButton(field.transform, "CompanionPartyButton").onClick.Invoke();
            Assert.IsNotNull(session.PartyCompanion, "다시 함께 가기");
            Object.Destroy(field.gameObject);
        }

        [UnityTest]
        public IEnumerator CompanionTakesHerTurnInBattle()
        {
            var (field, session, ara) = Create();
            session.AddCompanion(ara);
            yield return null;
            yield return null;
            var enemies = new List<MonsterInstance> { new MonsterInstance(TestData.Species("dummy", new MonsterStats(500, 1, 10),
                new MonsterStats(0, 0, 0), TestData.Skill("tap", SkillKind.Damage, SkillTarget.SingleEnemy, 1)).Set("displayName", "허수아비"), 1) };
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            field.Battle.BeginBattle(enemies, words, _ => { }, null, FieldTheme.Meadow);
            var root = field.Battle.transform;
            yield return WaitFor(() => field.Battle.Engine != null && field.Battle.Engine.Phase == BattlePhase.ChoosingSkill, 5f);
            Assert.AreEqual(2, field.Battle.Engine.Party.Count, "주인공 + 동료");
            var card = System.Array.Find(root.GetComponentsInChildren<RectTransform>(true), t => t.name == "Party_1");
            Assert.IsTrue(card != null && card.gameObject.activeInHierarchy, "동료 카드");

            // 주인공 차례 → 기술 → 정답
            yield return AnswerTurn(field, root);
            yield return WaitFor(() => field.Battle.SkillTitle.StartsWith("아라의 차례"), 5f);
            StringAssert.StartsWith("아라의 차례", field.Battle.SkillTitle, "다음은 동료 차례");
            Assert.IsNotNull(ActiveButton(root, "FleeButton"), "동료 차례에도 도망가기·상처약");
            yield return AnswerTurn(field, root);
            Assert.GreaterOrEqual(field.Battle.Engine.CorrectAnswers, 2, "동료 기술도 단어 문제");
            Object.Destroy(field.gameObject);
        }

        // #78: 함께 다니는 동료가 필드에서 주인공 한 칸 뒤를 따라온다 (보이기만 하는 그림)
        [UnityTest]
        public IEnumerator CompanionFollowsOneTileBehind()
        {
            var (field, session, ara) = Create();
            session.AddCompanion(ara);
            yield return null;
            yield return null;
            Assert.IsTrue(field.Follower.Visible, "함께 다니는 동료는 필드에 보임");
            var start = field.PlayerCell;

            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= start.x + 2, 3f);
            yield return WaitFor(() => !field.IsMoving, 2f);
            yield return null;
            var hero = new Vector3(field.PlayerCell.x + 0.5f, field.PlayerCell.y + 0.5f, 0f);
            Assert.AreEqual(1f, Vector3.Distance(hero, field.Follower.Position), 0.05f, "한 칸 뒤");
            Assert.Less(field.Follower.Position.x, hero.x, "지나온 쪽(왼쪽)에");
            Assert.AreEqual(Direction.Right, field.Follower.Facing, "같은 쪽을 보며 걸어옴");

            // 쉬게 하면 사라짐
            session.SetPartyCompanion(null);
            yield return null;
            Assert.IsFalse(field.Follower.Visible, "쉬는 동료는 안 보임");
            Object.Destroy(field.gameObject);
        }

        // 지금 차례의 첫 기술을 골라(대상이 필요하면 첫 대상) 정답을 누른다
        private static IEnumerator AnswerTurn(FieldScreen field, Transform root)
        {
            yield return WaitFor(() => ActiveButton(root, "SkillButton_0") != null, 5f);
            ActiveButton(root, "SkillButton_0").onClick.Invoke();
            yield return null;
            ActiveButton(root, "Intensity_0")?.onClick.Invoke();
            ActiveButton(root, "Enemy_0")?.onClick.Invoke();
            yield return WaitFor(() => field.Battle.Engine.Phase == BattlePhase.AnsweringQuiz || ActiveButton(root, "CardConfirmButton") != null, 5f);
            ActiveButton(root, "CardConfirmButton")?.onClick.Invoke();
            yield return WaitFor(() => field.Battle.Engine.Phase == BattlePhase.AnsweringQuiz && field.Battle.Engine.CurrentQuestion != null, 5f);
            ActiveButton(root, $"Choice_{field.Battle.Engine.CurrentQuestion.CorrectIndex}").onClick.Invoke();
            yield return WaitFor(() => field.Battle.Engine.Phase != BattlePhase.AnsweringQuiz, 5f);
        }
    }
}
