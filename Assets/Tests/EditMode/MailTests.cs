using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Game;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Save;

namespace WordRPG.Tests
{
    // 메일함 (#275 사용자: 업데이트마다 개발자의 편지·선물 — 다시 읽기, 지우기, 읽음 표시)
    public class MailTests
    {
        private static readonly DateTime Today = new DateTime(2026, 12, 5, 20, 0, 0);
        private static readonly Heroes.HeroData HeroData = TestData.Hero(new MonsterStats(30, 10, 10));

        private static MailData Mail(string id, MailDelivery when = MailDelivery.ByEvent, string from = "", string to = "", params ItemGift[] gifts)
        {
            var mail = ScriptableObject.CreateInstance<MailData>();
            mail.Configure(id, "개발자", id + " 제목", "개발자의 편지", id + " 큰 제목", "", "첫 문단\n둘째 문단", "맺음말", "From. 개발자", gifts, when, from, to);
            return mail;
        }

        private static Func<string, MailData> Finder(params MailData[] mails) => id => mails.FirstOrDefault(m => m.MailId == id);

        [Test]
        public void LaunchMailsComeOnceInTheirDatesAndDeletedOnesStayGone()
        {
            var session = GameSession.NewGame(HeroData);
            var always = Mail("always", MailDelivery.OnLaunch);
            var december = Mail("december", MailDelivery.OnLaunch, "2026-12-01", "2026-12-31");
            var byEvent = Mail("event");
            var mails = new[] { always, december, byEvent };

            Assert.AreEqual(1, Mails.DeliverDue(session, mails, new DateTime(2026, 11, 30)), "11월엔 언제나 메일만");
            Assert.AreEqual(1, Mails.DeliverDue(session, mails, Today), "12월 메일");
            Assert.AreEqual(0, Mails.DeliverDue(session, mails, Today), "한 번만");
            Assert.IsNull(session.Mailbox.Find("event"), "다른 일이 보내는 메일은 켤 때 오지 않음");
            Assert.AreEqual(new DateTime(2026, 12, 31).Date, DateTime.ParseExact(december.ToDate, "yyyy-MM-dd", null));

            Mails.Delete(session, always);
            Assert.AreEqual(0, Mails.DeliverDue(session, mails, new DateTime(2027, 1, 2)), "지운 메일은 다시 오지 않음");
            CollectionAssert.AreEqual(new[] { "december" }, Mails.Visible(session, Finder(mails)).Select(v => v.mail.MailId).ToArray());
        }

        [Test]
        public void NewestFirstUnreadDotAndReadMark()
        {
            var session = GameSession.NewGame(HeroData);
            var first = Mail("first");
            var second = Mail("second");
            var find = Finder(first, second);
            Assert.IsFalse(Mails.NeedsAttention(session, find), "빈 메일함");

            Mails.Deliver(session, first, new DateTime(2026, 12, 1));
            Mails.Deliver(session, second, Today);
            CollectionAssert.AreEqual(new[] { "second", "first" }, Mails.Visible(session, find).Select(v => v.mail.MailId).ToArray(), "최근에 받은 것부터");
            Assert.AreEqual("12월 5일", Mails.DateLabel(session.Mailbox.Find("second")));
            Assert.AreEqual(2, Mails.UnreadCount(session, find));
            Assert.IsTrue(Mails.NeedsAttention(session, find));

            Mails.MarkRead(session, first);
            Mails.MarkRead(session, second);
            Assert.AreEqual(0, Mails.UnreadCount(session, find));
            Assert.IsFalse(Mails.NeedsAttention(session, find), "다 읽으면 점이 꺼짐");
            Assert.IsFalse(Mails.Deliver(session, first, Today), "이미 받은 메일");
        }

        [Test]
        public void AttachedGiftsAreClaimedOnceAndDeletingKeepsThem()
        {
            var potion = TestData.Item("potion_great");
            var session = GameSession.NewGame(HeroData);
            var gift = Mail("gift", MailDelivery.ByEvent, "", "", new ItemGift(potion, 2));
            var find = Finder(gift);
            Mails.Deliver(session, gift, Today);
            Mails.MarkRead(session, gift);
            Assert.IsTrue(Mails.NeedsAttention(session, find), "읽었어도 선물을 안 받았으면 점");

            Assert.AreEqual(2, Mails.Claim(session, gift).Single().Count);
            Assert.IsEmpty(Mails.Claim(session, gift), "한 번만");
            Assert.AreEqual(2, session.Inventory.GetCount(potion));
            Assert.IsFalse(Mails.NeedsAttention(session, find));

            var other = GameSession.NewGame(HeroData);
            Mails.Deliver(other, gift, Today);
            var given = Mails.Delete(other, gift);
            Assert.AreEqual(2, other.Inventory.GetCount(potion), "안 받은 선물은 가방에 넣고 지움");
            Assert.AreEqual(1, given.Count);
            Assert.IsEmpty(Mails.Visible(other, find));
        }

        [Test]
        public void MailboxIsSavedAndOldSavesStartEmpty()
        {
            var session = GameSession.NewGame(HeroData);
            var read = Mail("read");
            var unread = Mail("unread");
            var deleted = Mail("deleted");
            foreach (var mail in new[] { read, unread, deleted }) Mails.Deliver(session, mail, Today);
            Mails.MarkRead(session, read);
            Mails.Delete(session, deleted);

            var json = JsonUtility.ToJson(session.ToSaveData(DateTime.UtcNow));
            var loaded = GameSession.FromSaveData(JsonUtility.FromJson<SaveData>(json), null, HeroData);
            Assert.IsTrue(loaded.Mailbox.Find("read").Read);
            Assert.IsFalse(loaded.Mailbox.Find("unread").Read);
            Assert.IsTrue(loaded.Mailbox.Find("deleted").Deleted);
            Assert.AreEqual("2026-12-05", loaded.Mailbox.Find("unread").ReceivedOn);

            // 1.3까지의 세이브엔 메일함 칸이 없음 → 빈 메일함
            int at = json.IndexOf(",\"mailbox\"", StringComparison.Ordinal);
            Assert.Greater(at, 0, "세이브에 메일함 칸");
            int end = json.IndexOf("]}", at, StringComparison.Ordinal) + 2;
            var old = JsonUtility.FromJson<SaveData>(json.Remove(at, end - at));
            Assert.IsEmpty(GameSession.FromSaveData(old, null, HeroData).Mailbox.All);
        }

        [Test]
        public void UnknownMailIdsAreKeptButNotShown()
        {
            var session = GameSession.NewGame(HeroData);
            session.Mailbox.Deliver("removed_mail", Today);
            Assert.IsEmpty(Mails.Visible(session, Finder()), "데이터에서 빠진 메일은 안 보임");
            Assert.IsFalse(Mails.NeedsAttention(session, Finder()));
            Assert.IsNotNull(session.Mailbox.Find("removed_mail"), "기록은 남김");
        }

        // 실제 데이터: 메일 id는 겹치지 않고, 모두 GameDatabase에 있음 (세이브에서 찾을 수 있게)
        [Test]
        public void EveryMailIsInTheDatabaseWithAUniqueId()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var mails = AssetDatabase.FindAssets("t:MailData", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<MailData>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.Greater(mails.Count, 0);
            Assert.AreEqual(mails.Count, mails.Select(m => m.MailId).Distinct().Count(), "mailId 중복");
            foreach (var mail in mails)
            {
                Assert.IsFalse(string.IsNullOrEmpty(mail.MailId), $"{mail.name}: mailId 없음");
                Assert.AreSame(mail, database.FindMail(mail.MailId), $"{mail.MailId}가 GameDatabase에 없음 (WordRPG > Data > Refresh Game Database)");
                Assert.IsFalse(string.IsNullOrWhiteSpace(mail.Subject), $"{mail.MailId}: 목록 제목 없음");
                if (!string.IsNullOrEmpty(mail.Art)) Assert.IsNotNull(Resources.Load<Sprite>("Art/Event/" + mail.Art), $"{mail.MailId}: 그림 {mail.Art} 없음");
                foreach (var line in mail.Attachments)
                    Assert.AreSame(line.Item, database.FindItem(line.Item.ItemId), $"{mail.MailId}: 선물 {line.Item.ItemId}가 GameDatabase에 없음");
                if (mail.Delivery == MailDelivery.ByEvent)
                    Assert.IsTrue(database.SeasonalEvents.Any(e => e.Letter == mail), $"{mail.MailId}: 보내는 곳이 없는 메일 (OnLaunch로 하거나 선물 퀴즈 편지로)");
                if (mail.Delivery == MailDelivery.AfterUpdateNotice)
                {
                    var notice = UpdateNotices.WithMail().FirstOrDefault(n => n.MailId == mail.MailId);
                    Assert.IsNotNull(notice, $"{mail.MailId}: 이 메일을 단 업데이트 안내가 없음");
                    Assert.AreEqual(notice.Title, mail.Title, $"{mail.MailId}: 안내 창과 같은 제목 (WordRPG > Data > Build Update Mails)");
                    Assert.AreEqual(notice.Message, mail.Body, $"{mail.MailId}: 안내 창과 같은 글 (WordRPG > Data > Build Update Mails)");
                }
            }
            foreach (var notice in UpdateNotices.WithMail())
                Assert.IsNotNull(database.FindMail(notice.MailId), $"{notice.Id}: 메일 {notice.MailId} 에셋 없음 (WordRPG > Data > Build Update Mails)");
        }

        // #286 사용자: 1.4 업데이트 안내가 처음엔 창으로 잘 나오는데 메일함에도 남았으면 — 업데이트한 사람(안내를 본 세이브)만, 새 게임은 받지 않음
        [Test]
        public void UpdateNoticeStaysInTheMailboxOnlyForPlayersWhoUpdated()
        {
            var notice = UpdateNotices.Courses;
            Assert.AreEqual("update_1_4", notice.MailId);
            var letter = Mail(notice.MailId, MailDelivery.AfterUpdateNotice);
            var find = Finder(letter);

            // 1.3 세이브 (안내도 메일함도 없음) → 1.4를 켬
            var json = JsonUtility.ToJson(GameSession.NewGame(HeroData).ToSaveData(DateTime.UtcNow));
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"notices\":\\[[^\\]]*\\]", "\"notices\":[\"update_1_2_cave\",\"update_1_3_people\"]");
            int at = json.IndexOf(",\"mailbox\"", StringComparison.Ordinal);
            json = json.Remove(at, json.IndexOf("]}", at, StringComparison.Ordinal) + 2 - at);
            var updated = GameSession.FromSaveData(JsonUtility.FromJson<SaveData>(json), null, HeroData);
            Assert.AreEqual(0, Mails.DeliverUpdateNotes(updated, Today), "타이틀에서 안내를 보기 전엔 없음");
            Assert.AreSame(notice, updated.Notices.NextUnseen());

            updated.Notices.Mark(notice.Id); // 타이틀에서 안내 창
            Assert.AreEqual(1, Mails.DeliverUpdateNotes(updated, Today), "필드에 들어오면 메일함에");
            Assert.AreEqual(0, Mails.DeliverUpdateNotes(updated, Today), "한 번만");
            Assert.AreEqual(notice.MailId, Mails.Visible(updated, find).Single().mail.MailId);
            Assert.IsTrue(Mails.NeedsAttention(updated, find), "안 읽음 → 빨간 점");
            Mails.Delete(updated, letter);
            Assert.AreEqual(0, Mails.DeliverUpdateNotes(updated, Today), "지우면 다시 오지 않음");

            // 새 게임: 안내도 메일도 없음 (세이브를 거쳐도)
            var fresh = GameSession.NewGame(HeroData);
            Assert.AreEqual(0, Mails.DeliverUpdateNotes(fresh, Today));
            var reloaded = GameSession.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(fresh.ToSaveData(DateTime.UtcNow))), null, HeroData);
            Assert.AreEqual(0, Mails.DeliverUpdateNotes(reloaded, Today));
            Assert.IsEmpty(Mails.Visible(reloaded, find));
            Assert.IsFalse(Mails.NeedsAttention(reloaded, find));
        }
    }
}
