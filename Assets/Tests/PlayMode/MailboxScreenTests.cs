using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Monsters;
using WordRPG.Save;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 업데이트 안내 메일 (#286 사용자: 1.4 업데이트 안내가 처음엔 창으로 잘 나오는데 메일함에도 남았으면):
    // 타이틀에서 안내를 본 세이브(= 업데이트한 사람)는 필드에 들어오면 메일함에 같은 글 — 빨간 점 → [메일함] → 다시 읽기
    public class MailboxScreenTests
    {
        private const string Map = "#######\n#..P..#\n#######";
        private static readonly DateTime November = new DateTime(2026, 11, 23, 20, 0, 0, DateTimeKind.Local);

        private GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (go != null) UnityEngine.Object.Destroy(go);
        }

        // 1.3까지의 세이브를 1.4로 켜고 타이틀에서 안내 창을 본 상태 (세이브에 메일함 칸이 없음)
        private static GameSession UpdatedSession()
        {
            var hero = TestData.Hero(new MonsterStats(100, 30, 10));
            var played = GameSession.NewGame(hero, 3);
            played.Record.RecordBattle(true, 3, 0);
            var json = JsonUtility.ToJson(played.ToSaveData(DateTime.UtcNow));
            int at = json.IndexOf(",\"mailbox\"", StringComparison.Ordinal);
            json = json.Remove(at, json.IndexOf("]}", at, StringComparison.Ordinal) + 2 - at);
            var session = GameSession.FromSaveData(JsonUtility.FromJson<SaveData>(json), null, hero);
            session.Notices.Mark(UpdateNotices.Courses.Id); // 타이틀에서 본 안내
            return session;
        }

        private FieldScreen CreateField(GameSession session, MailData mail)
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "mail_test").Set("displayName", "초원").Set("map", Map).Set("words", words);
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new Items.ItemData[0], new[] { area }, newMails: new[] { mail });
            go = new GameObject("MailFieldUnderTest");
            var field = go.AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, gameDatabase: database);
            field.LocalClock = () => November;
            return field;
        }

        private static IEnumerator Press(FieldScreen field, string name)
        {
            UnityEngine.UI.Button button = null;
            yield return WaitFor(() => (button = ActiveButton(field.transform, name)) != null, 3f);
            Assert.IsNotNull(button, $"{name} 버튼이 없음");
            button.onClick.Invoke();
            yield return null;
        }

        private static MailData UpdateMail()
        {
            var notice = UpdateNotices.Courses;
            var mail = ScriptableObject.CreateInstance<MailData>();
            mail.Configure(notice.MailId, "개발자", notice.Title, "업데이트 안내", notice.Title, "", notice.Message, "", "", null, MailDelivery.AfterUpdateNotice);
            return mail;
        }

        [UnityTest]
        public IEnumerator UpdateNoticeWaitsInTheMailbox()
        {
            var session = UpdatedSession();
            var field = CreateField(session, UpdateMail());
            yield return null;
            Assert.IsNotNull(session.Mailbox.Find("update_1_4"), "필드에 들어오면 메일함에");
            Assert.IsTrue(field.MailDotVisible, "안 읽은 안내 → 빨간 점");

            yield return Press(field, "MailButton");
            var mailbox = field.MailboxScreen;
            Assert.AreEqual(1, mailbox.RowCount);
            StringAssert.Contains("1.4 업데이트 안내", mailbox.RowText(0));
            Assert.IsTrue(mailbox.RowIsUnread(0));

            yield return Press(field, "MailRow_update_1_4");
            Assert.IsTrue(mailbox.IsReading);
            Assert.AreEqual("1.4 업데이트 안내", mailbox.LetterTitle);
            StringAssert.Contains("외울 단어장을 고를 수 있어요", mailbox.LetterText);
            StringAssert.Contains("뒤섞인 이정표", mailbox.LetterText);
            Assert.IsTrue(session.Mailbox.Find("update_1_4").Read);
            field.HandleBack();
            yield return null;
            field.HandleBack();
            yield return null;
            Assert.IsFalse(field.MailDotVisible, "읽으면 점이 꺼짐");
        }

        [UnityTest]
        public IEnumerator NewGamesDoNotGetTheUpdateNotice()
        {
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 30, 10)), 3);
            session.Record.RecordBattle(true, 3, 0);
            var field = CreateField(session, UpdateMail());
            yield return null;
            Assert.IsFalse(field.MailDotVisible, "새로 시작한 사람은 업데이트한 사람이 아님");
            yield return Press(field, "MailButton");
            Assert.AreEqual(0, field.MailboxScreen.RowCount);
        }
    }
}
