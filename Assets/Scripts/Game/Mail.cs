using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace WordRPG.Game
{
    // 메일함 (#275 사용자: "메일함을 만들어서 업데이트마다 이런 비슷한 걸 할 수 있도록" — 개발자의 편지·선물을 다시 읽고, 지우고, 읽음 표시).
    // 메일 한 통 = MailData (데이터). 받은 메일·읽음·선물 받음·지움 = 세이브의 Mailbox (mailId로 — 출시 뒤 바꾸지 말 것)
    public enum MailDelivery
    {
        ByEvent = 0,  // 다른 일이 보냄 (예: 크리스마스 선물 퀴즈를 마치면 개발자의 편지 — GiftEvent.Letter)
        OnLaunch = 1  // 게임을 켜면 (fromDate ~ toDate 사이에만, 비우면 언제나) — 업데이트 편지·선물
    }

    [CreateAssetMenu(fileName = "NewMail", menuName = "WordRPG/Mail", order = 31)]
    public class MailData : ScriptableObject
    {
        [Tooltip("세이브에 적히는 id — 출시 뒤 바꾸지 말 것 (바꾸면 받은 메일이 사라지고 다시 옴)")]
        [SerializeField] private string mailId;
        [SerializeField] private string sender = "개발자";
        [Tooltip("메일함 목록의 제목. 예: 크리스마스 편지")]
        [SerializeField] private string subject;
        [Header("편지")]
        [SerializeField] private string tag;     // 꼬리표. 예: "개발자의 편지"
        [SerializeField] private string title;   // 큰 제목
        [Tooltip("편지 머리 그림 — Resources/Art/Event의 이름 (예: tree). 비우면 그림 없음")]
        [SerializeField] private string art;
        [TextArea(6, 30)]
        [SerializeField] private string body;    // 문단은 줄바꿈으로
        [SerializeField] private string closing; // 맺음말 (금빛 한 줄)
        [SerializeField] private string sign;    // 보낸 사람 (오른쪽)
        [Tooltip("메일에 붙은 선물 — 메일에서 [선물 받기]")]
        [SerializeField] private List<ItemGift> attachments = new List<ItemGift>();
        [Header("보내기")]
        [SerializeField] private MailDelivery delivery = MailDelivery.ByEvent;
        [Tooltip("OnLaunch: 이 날부터 (yyyy-MM-dd, 현지 날짜). 비우면 처음부터")]
        [SerializeField] private string fromDate;
        [Tooltip("OnLaunch: 이 날까지 (끝 날 포함). 비우면 끝없이")]
        [SerializeField] private string toDate;

        public string MailId => mailId;
        public string Sender => sender;
        public string Subject => subject;
        public string Tag => tag;
        public string Title => title;
        public string Art => art;
        public string Body => body;
        public string Closing => closing;
        public string Sign => sign;
        public IReadOnlyList<ItemGift> Attachments => attachments ?? (IReadOnlyList<ItemGift>)new List<ItemGift>();
        public MailDelivery Delivery => delivery;
        public string FromDate => fromDate;
        public string ToDate => toDate;

        public bool HasAttachments
        {
            get
            {
                foreach (var line in Attachments)
                    if (line != null && line.Item != null && line.Count > 0) return true;
                return false;
            }
        }

        // OnLaunch 메일을 오늘 보낼 때인지 (현지 날짜)
        public bool IsDue(DateTime localNow)
        {
            if (delivery != MailDelivery.OnLaunch) return false;
            var today = localNow.Date;
            if (Mails.TryParseDate(fromDate, out var from) && today < from) return false;
            if (Mails.TryParseDate(toDate, out var to) && today > to) return false;
            return true;
        }

#if UNITY_EDITOR
        // 샘플 데이터 생성기·테스트 전용
        public void Configure(string id, string from, string subjectText, string tagText, string titleText, string artName,
            string bodyText, string closingText, string signText, IEnumerable<ItemGift> gifts = null,
            MailDelivery when = MailDelivery.ByEvent, string fromDay = "", string toDay = "")
        {
            mailId = id;
            sender = from;
            subject = subjectText;
            tag = tagText;
            title = titleText;
            art = artName;
            body = bodyText;
            closing = closingText;
            sign = signText;
            attachments = new List<ItemGift>(gifts ?? new ItemGift[0]);
            delivery = when;
            fromDate = fromDay;
            toDate = toDay;
        }
#endif
    }

    // 받은 메일 한 통의 상태 (세이브)
    [Serializable]
    public class MailState
    {
        [SerializeField] private string mailId;
        [SerializeField] private string receivedOn; // yyyy-MM-dd (현지)
        [SerializeField] private bool read;
        [SerializeField] private bool claimed;      // 붙은 선물을 받았는지
        [SerializeField] private bool deleted;      // 지웠어도 기록은 남김 (다시 오지 않게)

        public string MailId => mailId;
        public string ReceivedOn => receivedOn ?? "";
        public bool Read => read;
        public bool Claimed => claimed;
        public bool Deleted => deleted;

        private MailState() { } // Unity 직렬화용

        public MailState(string id, DateTime localNow)
        {
            mailId = id;
            receivedOn = localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        internal void MarkRead() => read = true;
        internal void MarkClaimed() => claimed = true;
        internal void MarkDeleted()
        {
            deleted = true;
            read = true;
        }
    }

    // 세이브에 들어가는 메일함 (받은 순서대로)
    [Serializable]
    public class Mailbox
    {
        [SerializeField] private List<MailState> mails = new List<MailState>();

        public IReadOnlyList<MailState> All => mails ?? (IReadOnlyList<MailState>)new List<MailState>();

        public MailState Find(string mailId)
        {
            if (mails == null || string.IsNullOrEmpty(mailId)) return null;
            foreach (var mail in mails)
                if (mail != null && mail.MailId == mailId) return mail;
            return null;
        }

        // 처음 받는 메일만 넣음 (지운 메일도 다시 오지 않음). 새로 받았으면 true
        public bool Deliver(string mailId, DateTime localNow)
        {
            if (string.IsNullOrEmpty(mailId) || Find(mailId) != null) return false;
            if (mails == null) mails = new List<MailState>();
            mails.Add(new MailState(mailId, localNow));
            return true;
        }
    }

    // 메일함 규칙 (#275)
    public static class Mails
    {
        // 게임을 켤 때: 보낼 때가 된 OnLaunch 메일을 받음. 반환 = 새로 받은 수
        public static int DeliverDue(GameSession session, IEnumerable<MailData> mails, DateTime localNow)
        {
            if (session == null || mails == null) return 0;
            int count = 0;
            foreach (var mail in mails)
                if (mail != null && mail.IsDue(localNow) && session.Mailbox.Deliver(mail.MailId, localNow)) count++;
            return count;
        }

        public static bool Deliver(GameSession session, MailData mail, DateTime localNow) =>
            session != null && mail != null && session.Mailbox.Deliver(mail.MailId, localNow);

        // 메일함에 보이는 메일 (지우지 않은 것, 데이터가 있는 것) — 최근에 받은 것부터
        public static List<(MailData mail, MailState state)> Visible(GameSession session, Func<string, MailData> find)
        {
            var list = new List<(MailData, MailState)>();
            if (session == null || find == null) return list;
            var all = session.Mailbox.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var state = all[i];
                if (state == null || state.Deleted) continue;
                var mail = find(state.MailId);
                if (mail != null) list.Add((mail, state));
            }
            return list;
        }

        public static bool IsUnclaimed(MailData mail, MailState state) =>
            mail != null && state != null && !state.Deleted && !state.Claimed && mail.HasAttachments;

        // 메일함 버튼의 빨간 점: 안 읽은 메일이나 안 받은 선물이 있으면
        public static bool NeedsAttention(GameSession session, Func<string, MailData> find)
        {
            foreach (var (mail, state) in Visible(session, find))
                if (!state.Read || IsUnclaimed(mail, state)) return true;
            return false;
        }

        public static int UnreadCount(GameSession session, Func<string, MailData> find)
        {
            int count = 0;
            foreach (var (_, state) in Visible(session, find))
                if (!state.Read) count++;
            return count;
        }

        public static void MarkRead(GameSession session, MailData mail)
        {
            var state = mail != null ? session?.Mailbox.Find(mail.MailId) : null;
            state?.MarkRead();
        }

        // 붙은 선물을 소지품에 (한 번만). 반환 = 받은 것
        public static List<ItemGift> Claim(GameSession session, MailData mail)
        {
            var given = new List<ItemGift>();
            var state = mail != null ? session?.Mailbox.Find(mail.MailId) : null;
            if (!IsUnclaimed(mail, state)) return given;
            foreach (var line in mail.Attachments)
            {
                if (line == null || line.Item == null || line.Count <= 0) continue;
                session.Inventory.Add(line.Item, line.Count);
                given.Add(line);
            }
            state.MarkClaimed();
            return given;
        }

        // 지우기: 안 받은 선물이 있으면 먼저 소지품에 넣고 지움 (선물을 잃지 않게). 반환 = 그때 받은 것
        public static List<ItemGift> Delete(GameSession session, MailData mail)
        {
            var state = mail != null ? session?.Mailbox.Find(mail.MailId) : null;
            if (state == null || state.Deleted) return new List<ItemGift>();
            var given = Claim(session, mail);
            state.MarkDeleted();
            return given;
        }

        // 목록에 보이는 받은 날: "12월 5일"
        public static string DateLabel(MailState state) =>
            TryParseDate(state?.ReceivedOn, out var day) ? $"{day.Month}월 {day.Day}일" : "";

        public static bool TryParseDate(string text, out DateTime day) =>
            DateTime.TryParseExact(text ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
    }
}
