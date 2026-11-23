using UnityEngine;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 필드 — 메일함 (#275 사용자: 업데이트마다 개발자의 편지·선물을 보낼 수 있게, 다시 읽기·지우기·읽음 표시).
    // 오른쪽 메뉴 [메일함] (안 읽은 메일·안 받은 선물이 있으면 빨간 점 — 튜토리얼과 상관없이) → MailboxView.
    // 켤 때 보낼 때가 된 메일(OnLaunch)을 받음. 크리스마스 편지는 선물 퀴즈를 마치면 옴 (GiftEvents.Claim)
    public partial class FieldScreen
    {
        private MailboxView mailboxView;
        private RectTransform mailDot;

        public MailboxView MailboxScreen => mailboxView;                                   // 테스트용
        public bool MailDotVisible => mailDot != null && mailDot.gameObject.activeSelf;   // 테스트용
        private bool MailboxOpen => mailboxView != null && mailboxView.IsOpen;

        private MailData FindMail(string mailId) => database != null ? database.FindMail(mailId) : null;

        // 필드를 열 때 한 번: 업데이트 편지 같은 OnLaunch 메일 + 타이틀에서 본 업데이트 안내 (#286 — 창을 닫아도 메일함에 남게)
        private void DeliverMail()
        {
            if (database == null || session == null) return;
            int arrived = Mails.DeliverDue(session, database.Mails, LocalClock()) + Mails.DeliverUpdateNotes(session, LocalClock());
            if (arrived > 0) SaveNow();
            RefreshMailDot();
        }

        private void OpenMailbox()
        {
            if (mailboxView == null) mailboxView = MailboxView.Create(hudRoot);
            mailboxView.Show(session, FindMail, MailChanged);
        }

        // 읽음·선물·지우기마다: 저장 + 점 + HUD (선물이면 골드·물약 알약)
        private void MailChanged()
        {
            SaveNow();
            RefreshHud();
        }

        private void RefreshMailDot()
        {
            if (mailDot != null) mailDot.gameObject.SetActive(session != null && Mails.NeedsAttention(session, FindMail));
        }
    }
}
