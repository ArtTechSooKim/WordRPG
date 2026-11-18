using UnityEngine;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 필드 — 철마다 여는 선물 퀴즈 (#274 사용자: 12월 1~31일에 켠 사람에게 크리스마스 단어 3문제 — 기본 선물 + 맞힌 문제마다 하나 더,
    // 선물은 상자를 열 때까지 비밀, 받은 뒤 개발자의 편지).
    // 그 시기에 들어오면 바로 뜸 (#275 사용자 — 처음 시작 흐름·걷기 안내 중에는 끝난 뒤). [나중에]면 다음에 켤 때 다시.
    // 다 풀면 그 자리에서 소지품에 넣고 저장 → 올해는 다시 안 뜸 (규칙 GiftEvents, 받은 기록 = WorldState 이벤트). 편지는 메일함에도
    public partial class FieldScreen
    {
        private const float GiftOfferDelay = 0.3f; // 화면이 다 그려진 뒤 바로

        private GiftQuizView giftView;
        private GiftEvent giftEvent;
        private GiftQuiz giftQuiz;
        private bool giftOfferDone; // 이번에 필드를 연 동안 이미 띄웠거나 띄울 것이 없음
        private float giftIdle;

        public GiftQuizView GiftView => giftView; // 테스트용
        public bool GiftOpen => giftView != null && giftView.IsOpen;

        // 필드에 가만히 서 있을 때마다 부름. 창을 띄웠으면 true
        private bool UpdateGiftOffer(float dt)
        {
            if (giftOfferDone) return false;
            if (database == null || session == null || database.SeasonalEvents.Count == 0)
            {
                giftOfferDone = true;
                return false;
            }
            if (fieldTutorialRunning || guide.IsShowing) return false; // 걷기·첫 전투 안내를 가리지 않게
            giftIdle += dt;
            if (giftIdle < GiftOfferDelay) return false;
            giftOfferDone = true;
            var gift = GiftEvents.Offer(session, database.SeasonalEvents, LocalClock());
            if (gift == null) return false;
            OpenGift(gift);
            return true;
        }

        // 안내 창부터 (테스터 메뉴도 이것으로 — 날짜와 상관없이)
        public void OpenGift(GiftEvent gift)
        {
            if (gift == null) return;
            if (giftView == null) giftView = GiftQuizView.Create(hudRoot);
            giftEvent = gift;
            giftQuiz = null;
            giftView.ShowIntro(gift, BeginGiftQuiz, () =>
            {
                giftView.Hide();
                ShowToast($"{gift.EndLabel}까지 다시 켜면 받을 수 있어요", 3f);
            });
        }

        private void BeginGiftQuiz()
        {
            giftQuiz = new GiftQuiz(giftEvent.Words.Words, giftEvent.Questions, rng);
            giftView.ShowQuestion(giftEvent, giftQuiz, PickGiftAnswer);
        }

        private void PickGiftAnswer(int index)
        {
            var answer = giftQuiz.Answer(index);
            if (answer == GiftAnswer.Ignored) return;
            Sound.Play(answer == GiftAnswer.Correct ? Sfx.Correct : Sfx.Wrong);
            giftView.ShowAnswer(giftEvent, giftQuiz, NextGiftQuestion);
        }

        private void NextGiftQuestion()
        {
            giftQuiz.Next();
            if (!giftQuiz.IsComplete)
            {
                giftView.ShowQuestion(giftEvent, giftQuiz, PickGiftAnswer);
                return;
            }
            // 다 풀면 바로 소지품에 넣고 저장 (선물 상자를 여는 연출·편지 중에 꺼져도 받은 것) — 무엇인지는 상자를 열 때 보여 줌
            var given = GiftEvents.Claim(session, giftEvent, giftQuiz.CorrectCount, LocalClock());
            SaveNow();
            RefreshHud();
            var letter = giftEvent.Letter;
            giftView.ShowUnwrap(giftEvent, giftQuiz, given, () => giftView.Hide(), () =>
            {
                Mails.MarkRead(session, letter); // 여기서 읽었으면 메일함에서도 읽은 것으로 (#275)
                SaveNow();
                RefreshMailDot();
            });
        }
    }
}
