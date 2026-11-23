using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 힐끔해골 (#281 → #283 친구 의견, Figma '힐끔해골 — 말 걸기 (#283)' · '힐끔해골의 방 — 문제 (#283)'):
    //  · 말 걸기: 필드 아래 카드 — 얼굴 · '…키킥. ○○ 문제인데… 한번 풀어 볼래?' · [풀어보기] [안 할래]
    //  · [풀어보기] → 화면이 하얗게 번쩍 → 해골의 방(어두운 보랏빛, 해골이 크게): 문제 n / 3 · 받은 골드 · 문제 카드 · 보기 한 줄씩(4~5개)
    //    틀리면 그 보기만 꺼지고 해골이 고개를 기울여 깔봄('키킥… 그것도 몰라…?'), 맞히면 '+20G'(처음 바로 맞힌 문제만) 또는 '정답!'
    //  · 끝: 많이 맞혔으면 해골이 옆으로 슬금슬금 도망, 아니면 키킥 웃음 → [확인] → 번쩍 → 필드
    public class SkullQuizView
    {
        private static readonly Color RoomColor = new Color32(20, 15, 31, 255);
        private static readonly Color CardColor = new Color32(28, 24, 38, 255);
        private static readonly Color InviteColor = new Color32(42, 37, 54, 255);
        private static readonly Color Lavender = new Color32(185, 168, 216, 255);
        private static readonly Color Bone = new Color32(233, 228, 214, 255);
        private static readonly Color Glow = new Color32(122, 95, 168, 255);

        private GameObject root, invite, room;
        private Image flash, skull, edge;
        private Text inviteLine, line, gold, progress, question, answer, tag, note, stampText;
        private readonly List<Button> choices = new List<Button>();
        private Button done;
        private RectTransform stamp, skullRect;
        private Action<int> onPick;
        private Action onPlay, onLeave, onClose;
        private int shaking = -1;
        private float shakeTime, stampTime, flashTime = 1f, sneerTime = 1f, fleeTime = -1f, time;
        private Vector2 shakeHome;

        public const float FlashSeconds = 0.35f, FleeSeconds = 1.4f;

        public bool IsOpen => root != null && root.activeSelf;
        public bool IsInviting => IsOpen && invite.activeSelf;
        public bool InRoom => IsOpen && room.activeSelf;
        public bool IsComplete => InRoom && done.gameObject.activeSelf;
        public bool IsFleeing => InRoom && fleeTime >= 0f;
        public bool IsSneering => InRoom && sneerTime < 1f;
        public string InviteLine => IsInviting ? inviteLine.text : "";
        public string Question => InRoom ? question.text : "";
        public string Answer => InRoom ? answer.text : "";
        public string Line => InRoom ? line.text : "";
        public string Gold => InRoom ? gold.text : "";
        public string Tag => InRoom ? tag.text : "";
        public string Stamp => InRoom && stamp.gameObject.activeSelf ? stampText.text : "";
        public int ChoiceCount { get { int n = 0; foreach (var c in choices) if (c.gameObject.activeSelf) n++; return n; } }
        public string ChoiceText(int index) => UiKit.LabelOf(choices[index]).text;
        public bool ChoiceEnabled(int index) => choices[index].interactable;

        public static SkullQuizView Create(RectTransform parent)
        {
            var view = new SkullQuizView();
            var rect = UiKit.Stretch("SkullQuiz", parent);
            view.root = rect.gameObject;
            var skullSprite = SkullSprite();

            // --- 말 걸기 (필드 위 아래쪽 카드) ---
            view.invite = UiKit.Stretch("Invite", rect).gameObject;
            UiKit.Panel("Dim", view.invite.transform, new Color(0f, 0f, 0f, 0.35f)).raycastTarget = true;
            var card = UiKit.RoundPanel("Card", view.invite.transform, InviteColor, UiKit.RadiusLg, 0.065f, 0.075f, 0.935f, 0.235f);
            UiKit.Outline(UiKit.Panel("Edge", card.transform, Lavender), UiKit.RadiusLg, 5).raycastTarget = false;
            var face = UiKit.IconImage("Face", card.transform, Resources.Load<Sprite>("Art/NinjaAdventure/Npcs/" + ExamSkull.ArtId + "_face"), 0, 1, 0, 1);
            face.rectTransform.pivot = new Vector2(0, 1);
            face.rectTransform.sizeDelta = new Vector2(96, 96);
            face.rectTransform.anchoredPosition = new Vector2(32, -32);
            face.raycastTarget = false;
            UiKit.Display(UiKit.OneLine(UiKit.Label("Name", card.transform, ExamSkull.DisplayName, 36, Bone, 0.17f, 0.78f, 0.9f, 0.94f, TextAnchor.MiddleLeft)));
            view.inviteLine = UiKit.Label("Line", card.transform, "", 32, Lavender, 0.17f, 0.4f, 0.96f, 0.78f, TextAnchor.UpperLeft, FontStyle.Normal, true, 20);
            var play = UiKit.MakeButton("SkullPlay", card.transform, "풀어보기", Palette.Gold, 40, 0.034f, 0.08f, 0.587f, 0.4f);
            UiKit.LabelOf(play).color = Palette.OnAccent;
            play.onClick.AddListener(() => view.onPlay?.Invoke());
            var leave = UiKit.MakeButton("SkullLeave", card.transform, "안 할래", Palette.Button, 40, 0.609f, 0.08f, 0.966f, 0.4f);
            leave.onClick.AddListener(() => view.onLeave?.Invoke());

            // --- 해골의 방 (화면 전체) ---
            view.room = UiKit.Stretch("Room", rect).gameObject;
            UiKit.Panel("Bg", view.room.transform, RoomColor).raycastTarget = true;
            var glow = UiKit.IconImage("Glow", view.room.transform, UiKit.GlowSprite(), 0.5f, 0.84f, 0.5f, 0.84f);
            glow.rectTransform.sizeDelta = new Vector2(900, 900);
            glow.color = new Color(Glow.r, Glow.g, Glow.b, 0.45f);
            glow.raycastTarget = false;
            view.skull = UiKit.IconImage("Skull", view.room.transform, skullSprite, 0.5f, 0.84f, 0.5f, 0.84f);
            view.skullRect = view.skull.rectTransform;
            view.skullRect.sizeDelta = new Vector2(288, 288);
            view.skull.raycastTarget = false;
            UiKit.Display(UiKit.OneLine(UiKit.Label("Name", view.room.transform, ExamSkull.DisplayName, 44, Bone, 0f, 0.735f, 1f, 0.77f)));
            view.line = UiKit.Label("Line", view.room.transform, "", 32, Lavender, 0.05f, 0.69f, 0.95f, 0.735f, TextAnchor.MiddleCenter, FontStyle.Normal, true, 20);
            view.progress = UiKit.OneLine(UiKit.Label("Progress", view.room.transform, "", 28, Palette.TextDim, 0.04f, 0.94f, 0.5f, 0.98f,
                TextAnchor.MiddleLeft, FontStyle.Bold));
            view.gold = UiKit.OneLine(UiKit.Label("Gold", view.room.transform, "", 28, Palette.TextDim, 0.5f, 0.94f, 0.86f, 0.98f,
                TextAnchor.MiddleRight, FontStyle.Bold));
            var closeImage = UiKit.Pill(UiKit.Panel("SkullClose", view.room.transform, CardColor, 0.955f, 0.96f, 0.955f, 0.96f));
            closeImage.rectTransform.sizeDelta = new Vector2(64, 64);
            closeImage.rectTransform.anchoredPosition = new Vector2(-32, 0);
            var close = UiKit.AddButton(closeImage);
            var x = UiKit.Label("X", closeImage.transform, "×", 30, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold);
            x.font = UiFonts.Bold; // ✕는 Noto에도 없어 ×로
            close.onClick.AddListener(() => view.onClose?.Invoke());

            var qcard = UiKit.RoundPanel("Card", view.room.transform, CardColor, UiKit.RadiusMd, 0.065f, 0.48f, 0.935f, 0.677f);
            qcard.raycastTarget = false;
            view.edge = UiKit.Outline(UiKit.Panel("Edge", qcard.transform, Lavender), UiKit.RadiusMd, 3);
            view.edge.raycastTarget = false;
            view.question = UiKit.Label("Question", qcard.transform, "", 42, Palette.Text, 0.04f, 0.34f, 0.96f, 0.95f,
                TextAnchor.MiddleCenter, FontStyle.Bold, true, 22);
            view.answer = UiKit.Label("Answer", qcard.transform, "", 44, Palette.Gold, 0.04f, 0.19f, 0.96f, 0.35f,
                TextAnchor.MiddleCenter, FontStyle.Bold, true, 22);
            view.tag = UiKit.OneLine(UiKit.Label("Tag", qcard.transform, "", 30, Lavender, 0.04f, 0.04f, 0.96f, 0.18f,
                TextAnchor.MiddleCenter, FontStyle.Bold));
            view.note = UiKit.OneLine(UiKit.Label("Note", view.room.transform, "", 26, Palette.TextDim, 0.065f, 0.13f, 0.935f, 0.16f,
                TextAnchor.MiddleCenter));

            view.stamp = UiKit.Rect("Stamp", view.room.transform, 0.8f, 0.69f, 0.8f, 0.69f);
            view.stamp.sizeDelta = new Vector2(300, 120);
            view.stamp.localEulerAngles = new Vector3(0, 0, 10);
            view.stampText = UiKit.Display(UiKit.OneLine(UiKit.Label("Text", view.stamp, "", 72, Palette.Gold, 0, 0, 1, 1)));
            var outline = view.stampText.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(3f, -3f);

            for (int i = 0; i < ExamLayout.MaxChoices; i++)
            {
                int index = i;
                var button = UiKit.MakeButton($"SkullChoice_{i}", view.room.transform, "", Palette.Button, 40, 0.065f, 0.4f, 0.935f, 0.45f, bestFit: true);
                UiKit.LabelOf(button).font = UiFonts.Bold;
                OneLineFit.Apply(UiKit.LabelOf(button), OneLineFit.ReadableMin(40)); // 한 줄에 들어가면 한 줄로 (#287)
                var colors = button.colors;
                colors.disabledColor = Color.white; // 정답·오답·꺼짐 색이 흐려지지 않게
                button.colors = colors;
                button.onClick.AddListener(() => view.onPick?.Invoke(index));
                view.choices.Add(button);
            }
            view.done = UiKit.MakeButton("SkullDone", view.room.transform, "확인", Palette.Gold, 44, 0.222f, 0.05f, 0.778f, 0.11f);
            UiKit.LabelOf(view.done).color = Palette.OnAccent;
            view.done.onClick.AddListener(() => view.onClose?.Invoke());

            // 번쩍 (방에 들어갈 때·나올 때)
            view.flash = UiKit.Panel("Flash", rect, new Color(1f, 1f, 1f, 0f));
            view.flash.raycastTarget = false;

            view.root.SetActive(false);
            return view;
        }

        // 해골 정면 그림 (팩 시트 맨 위 왼쪽 칸)
        private static Sprite SkullSprite()
        {
            var sheet = Resources.Load<Texture2D>("Art/NinjaAdventure/Npcs/" + ExamSkull.ArtId);
            if (sheet == null) return null;
            return Sprite.Create(sheet, new Rect(0, sheet.height - 16, 16, 16), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
        }

        // 말 걸기 카드
        public void ShowInvite(string topic, Action play, Action leave)
        {
            onPlay = play;
            onLeave = leave;
            inviteLine.text = topic.Length > 0 ? $"…키킥. {topic} 문제인데…\n한번 풀어 볼래?" : "…키킥. 문제 하나… 한번 풀어 볼래?";
            invite.SetActive(true);
            room.SetActive(false);
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        // [풀어보기] → 번쩍이며 해골의 방으로
        public void EnterRoom(Action<int> pick, Action closed)
        {
            onPick = pick;
            onClose = closed;
            invite.SetActive(false);
            room.SetActive(true);
            fleeTime = -1f;
            sneerTime = 1f;
            ResetSkull();
            Flash();
        }

        public void Flash() => flashTime = 0f;

        public void ShowQuestion(QuizQuestion quiz, int index, int count, int goldEarned, string say)
        {
            StopShake();
            question.text = quiz.Prompt;
            answer.text = "";
            tag.text = ExamLayout.Tag(quiz.Exam);
            line.text = say;
            gold.text = goldEarned > 0 ? $"받은 골드 +{goldEarned}G" : "";
            note.text = $"처음 바로 맞힌 문제마다 {ExamSkull.GoldPerQuestion}G · 틀려도 다시 고르면 돼요";
            progress.text = $"문제 {index + 1} / {count}";
            stamp.gameObject.SetActive(false);
            done.gameObject.SetActive(false);
            edge.color = Lavender;
            ExamLayout.Column(choices, quiz.Choices.Count, 0.065f, 0.935f, 0.17f, 0.45f, 0.01f);
            for (int i = 0; i < choices.Count; i++)
            {
                bool used = i < quiz.Choices.Count;
                choices[i].gameObject.SetActive(used);
                if (!used) continue;
                UiKit.LabelOf(choices[i]).text = quiz.Choices[i];
                choices[i].image.color = Palette.Button;
                choices[i].interactable = true;
            }
        }

        // 틀림: 그 보기만 빨갛게 흔들고 꺼 둠 + 해골이 고개를 기울여 깔봄
        public void MarkWrong(int index, string say)
        {
            if (index < 0 || index >= choices.Count) return;
            choices[index].image.color = Palette.Bad;
            choices[index].interactable = false;
            line.text = say;
            StopShake();
            shaking = index;
            shakeTime = 0.35f;
            shakeHome = choices[index].GetComponent<RectTransform>().anchoredPosition;
            sneerTime = 0f;
        }

        public void MarkCorrect(int index, string correctAnswer, int goldNow, int goldEarned, string say)
        {
            StopShake();
            for (int i = 0; i < choices.Count; i++)
            {
                choices[i].interactable = false;
                choices[i].image.color = i == index ? Palette.Good : Palette.Disabled;
            }
            answer.text = correctAnswer;
            line.text = say;
            gold.text = goldEarned > 0 ? $"받은 골드 +{goldEarned}G" : "";
            stampText.text = goldNow > 0 ? $"+{goldNow}G" : "정답!";
            stampText.color = goldNow > 0 ? Palette.Gold : Palette.Good;
            stamp.gameObject.SetActive(true);
            stampTime = 0f;
        }

        // 끝: 많이 맞혔으면 해골이 옆으로 슬금슬금 도망, 아니면 깔보며 웃음 → [확인]
        public void ShowEnd(int firstTry, int count, int goldEarned, bool flees, string say)
        {
            StopShake();
            question.text = $"바로 맞힌 문제 {firstTry} / {count}";
            answer.text = goldEarned > 0 ? $"+{goldEarned}G" : "";
            tag.text = goldEarned > 0 ? "처음 맞힌 문제의 골드를 받았다" : "골드는 처음 바로 맞힌 문제에만";
            line.text = say;
            gold.text = goldEarned > 0 ? $"받은 골드 +{goldEarned}G" : "";
            progress.text = $"문제 {count} / {count}";
            stamp.gameObject.SetActive(false);
            edge.color = Palette.Gold;
            foreach (var choice in choices) choice.gameObject.SetActive(false);
            done.gameObject.SetActive(true);
            if (flees) fleeTime = 0f;
            else sneerTime = 0f;
        }

        public void Hide()
        {
            StopShake();
            root.SetActive(false);
        }

        public void Tick(float dt)
        {
            if (!IsOpen) return;
            time += dt;
            if (flashTime < 1f)
            {
                flashTime += dt / FlashSeconds;
                flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - flashTime));
            }
            if (InRoom) AnimateSkull(dt);
            if (stamp.gameObject.activeSelf && stampTime < 0.3f)
            {
                stampTime += dt;
                stamp.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, Mathf.Clamp01(stampTime / 0.3f));
            }
            if (shaking < 0) return;
            shakeTime -= dt;
            var rt = choices[shaking].GetComponent<RectTransform>();
            if (shakeTime <= 0f)
            {
                StopShake();
                return;
            }
            rt.anchoredPosition = shakeHome + new Vector2(Mathf.Sin(shakeTime * 60f) * 14f * (shakeTime / 0.35f), 0f);
        }

        // 평소: 숨죽여 웃듯 작게 들썩 · 깔봄: 고개를 기울이고 내려다봄(1.2초) · 도망: 옆으로 한 걸음씩 슬금슬금 빠져나가며 흐려짐
        private void AnimateSkull(float dt)
        {
            if (fleeTime >= 0f)
            {
                fleeTime += dt;
                float k = Mathf.Clamp01(fleeTime / FleeSeconds);
                float steps = Mathf.Floor(k * 7f) / 7f; // 한 걸음씩 (슬금슬금)
                float hop = Mathf.Abs(Mathf.Sin(fleeTime * 22f)) * 10f * (1f - k);
                skullRect.anchoredPosition = new Vector2(Mathf.Lerp(0f, 700f, steps * steps), hop);
                skullRect.localEulerAngles = new Vector3(0, 0, Mathf.Sin(fleeTime * 22f) * 4f);
                skull.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((k - 0.6f) / 0.4f));
                return;
            }
            if (sneerTime < 1f)
            {
                sneerTime += dt / 1.2f;
                float s = Mathf.Sin(Mathf.Clamp01(sneerTime) * Mathf.PI);
                skullRect.localEulerAngles = new Vector3(0, 0, -14f * s);
                skullRect.localScale = Vector3.one * (1f + 0.08f * s);
                skullRect.anchoredPosition = new Vector2(0f, 18f * s + Mathf.Abs(Mathf.Sin(time * 14f)) * 6f * s); // 고개를 들고 키득
                return;
            }
            skullRect.localEulerAngles = Vector3.zero;
            skullRect.localScale = Vector3.one;
            skullRect.anchoredPosition = new Vector2(0f, Mathf.Abs(Mathf.Sin(time * 9f)) * 4f);
        }

        private void ResetSkull()
        {
            skullRect.anchoredPosition = Vector2.zero;
            skullRect.localEulerAngles = Vector3.zero;
            skullRect.localScale = Vector3.one;
            skull.color = Color.white;
        }

        public void Pick(int index) => onPick?.Invoke(index); // 테스트용

        private void StopShake()
        {
            if (shaking >= 0) choices[shaking].GetComponent<RectTransform>().anchoredPosition = shakeHome;
            shaking = -1;
        }
    }
}
