using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Battle;
using WordRPG.Words;

namespace WordRPG.UI
{
    // [선제공격] QTE (#74 → 2026-10-16 사용자 "보통 게임의 QTE 기믹 같은 느낌으로", Figma 'Field — 선제공격 QTE·성공·놓침 (#74)').
    // 누르면 세상이 느려지고(몬스터 0.15배는 FieldScreen) 가장자리가 어두워지며 집중선 + 빨간 컷인 띠가 쓱.
    // 몬스터 둘레의 금색 원이 점선 과녁까지 줄어드는 것이 제한 시간 — 30% 남으면 빨강 + 째깍.
    // 영단어(이미 본 단어만 — Preemptive.QuizBooks) + 엄지 쪽 2×2 선택지. 고르면 성공(번쩍임·베기·피해 숫자·도장)이나
    // 놓침(빨간 테두리·흔들림·도장)을 잠깐 보여 준 뒤 결과를 알린다 (-1 = 시간 초과)
    public class PreemptView
    {
        private const float ResultSeconds = 0.9f;
        private const float IntroSeconds = 0.18f;
        private const float DangerRatio = 0.3f;
        private const float RingStartTiles = 5.5f, RingEndTiles = 1.8f; // Figma 540 → 176 (한 칸 98)
        private static readonly Vector2 DamageOffset = new Vector2(70f, 110f);
        private static readonly Color MissTint = new Color(0.3f, 0.02f, 0.03f);
        private static Sprite vignetteSprite, linesSprite;

        private GameObject root;
        private RectTransform rootRect, focusGroup, banner, wordRect, stamp;
        private CanvasGroup group, stampGroup;
        private Image vignette, lines, flash, slash;
        private UiRing closing, target;
        private TargetLock lockMark;
        private Text word, stampTitle, stampSub, damage;
        private readonly List<Button> choices = new List<Button>();
        private QuizQuestion question;
        private Action<int> answered;
        private Func<Vector2?> focus;
        private Func<float> tilePixels;
        private Vector2 lastFocus;
        private float limit, elapsed, shownTime, resultTimer, resultElapsed, lineTimer, lineAngle;
        private int picked = int.MinValue, strikeDamage, lastTick;
        private bool vibrate;

        public bool IsOpen => root != null && root.activeSelf;
        public QuizQuestion Question => question;
        public string WordText => IsOpen ? word.text : "";
        public string StampText => IsOpen && stamp.gameObject.activeSelf ? stampTitle.text : ""; // 테스트용
        public RectTransform Root => rootRect;

        public static PreemptView Create(RectTransform parent)
        {
            var view = new PreemptView();
            view.rootRect = UiKit.Stretch("PreemptView", parent);
            view.root = view.rootRect.gameObject;
            view.group = view.root.AddComponent<CanvasGroup>();
            UiKit.Panel("Blocker", view.rootRect, Color.clear).raycastTarget = true; // 필드를 누르지 못하게

            // 아래쪽 어둡게 (선택지가 잘 읽히게)
            var shade = UiKit.IconImage("BottomShade", view.rootRect, UiKit.FadeSprite(), 0, 0, 1, 0.5f);
            shade.preserveAspect = false;
            shade.color = new Color(0f, 0f, 0f, 0.95f);

            // 몬스터를 따라다니는 층: 비네트(가운데만 밝게) · 집중선 · 과녁 · 줄어드는 원 · 조준 괄호 · 베기 · 피해 숫자
            view.focusGroup = UiKit.Rect("Focus", view.rootRect, 0.5f, 0.5f, 0.5f, 0.5f);
            view.vignette = UiKit.IconImage("Vignette", view.focusGroup, VignetteSprite(), 0.5f, 0.5f, 0.5f, 0.5f);
            view.vignette.rectTransform.sizeDelta = new Vector2(4400f, 4400f);
            view.vignette.color = Color.black;
            view.lines = UiKit.IconImage("SpeedLines", view.focusGroup, SpeedLinesSprite(), 0.5f, 0.5f, 0.5f, 0.5f);
            view.lines.rectTransform.sizeDelta = new Vector2(3200f, 3200f);
            view.lines.color = new Color(1f, 1f, 1f, 0.07f); // Linear 색공간이라 흰색은 낮게 (Figma 0.16과 비슷하게 보임)
            view.target = UiRing.Create("Target", view.focusGroup, new Color(1f, 1f, 1f, 0.8f), 4f, 176f, 14);
            view.closing = UiRing.Create("ClosingRing", view.focusGroup, Palette.Gold, 10f, 540f);
            view.lockMark = TargetLock.Create(view.focusGroup, Palette.Gold, true);
            view.slash = UiKit.Pill(UiKit.Panel("Slash", view.focusGroup, Color.white, 0.5f, 0.5f, 0.5f, 0.5f));
            view.slash.raycastTarget = false;
            view.slash.rectTransform.sizeDelta = new Vector2(460f, 22f);
            view.slash.rectTransform.localEulerAngles = new Vector3(0f, 0f, 35f);
            view.damage = Outlined(UiKit.Display(UiKit.OneLine(UiKit.Label("Damage", view.focusGroup, "", 72, Palette.Bad,
                0.5f, 0.5f, 0.5f, 0.5f))));
            view.damage.rectTransform.sizeDelta = new Vector2(240f, 100f);

            // 컷인 띠 (Figma 'QTE Banner': 빨간 띠 + 금색 위아래 선, 4° 기울임) — 왼쪽에서 쓱 들어온다
            view.banner = UiKit.Rect("Banner", view.rootRect, 0.5f, 0.87f, 0.5f, 0.87f);
            view.banner.sizeDelta = new Vector2(1240f, 150f);
            view.banner.localEulerAngles = new Vector3(0f, 0f, 4f);
            var band = UiKit.Panel("Band", view.banner, Palette.Attack);
            band.raycastTarget = false;
            UiKit.Panel("EdgeTop", view.banner, Palette.Gold, 0, 1, 1, 1).rectTransform.sizeDelta = new Vector2(0f, 6f);
            UiKit.Panel("EdgeBottom", view.banner, Palette.Gold, 0, 0, 1, 0).rectTransform.sizeDelta = new Vector2(0f, 6f);
            var row = UiKit.Rect("Row", view.banner, 0, 0, 1, 1);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 28f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var title = Outlined(UiKit.Display(UiKit.OneLine(UiKit.Label("Title", row, "선제공격!", 86, Palette.Text, 0, 0, 1, 1))));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sub = UiKit.OneLine(UiKit.Label("Sub", row, "뜻을 맞히면 먼저 한 방!", 34, Palette.Gold, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold));
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 영단어 (이미 본 단어)
            UiKit.OneLine(UiKit.Label("Ask", view.rootRect, "이 단어의 뜻은?", 32, Palette.TextDim, 0.04f, 0.45f, 0.96f, 0.474f));
            view.word = Outlined(UiKit.Display(UiKit.Label("Word", view.rootRect, "", 132, Palette.Text, 0.04f, 0.36f, 0.96f, 0.447f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 56)));
            view.wordRect = view.word.rectTransform;

            // 2×2 선택지 (Figma 'Answer Choice', 엄지가 닿는 아래쪽)
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                float minX = i % 2 == 0 ? 0.033f : 0.511f, maxY = i < 2 ? 0.323f : 0.203f;
                var button = UiKit.MakeButton($"PreemptChoice_{i}", view.rootRect, "", Palette.Button, 42, minX, maxY - 0.107f, minX + 0.456f, maxY,
                    bestFit: true);
                UiKit.LabelOf(button).font = UiFonts.Bold;
                var colors = button.colors;
                colors.disabledColor = Color.white; // 정답/오답 색이 흐려지지 않게
                button.colors = colors;
                button.onClick.AddListener(() => view.Pick(index));
                view.choices.Add(button);
            }

            // 결과: 하얀 번쩍임 + 도장 (Figma 'QTE Stamp', 6° 기울임)
            view.flash = UiKit.Panel("Flash", view.rootRect, Color.clear);
            view.flash.raycastTarget = false;
            view.stamp = UiKit.Rect("Stamp", view.rootRect, 0.5f, 0.542f, 0.5f, 0.542f);
            view.stamp.sizeDelta = new Vector2(1040f, 230f);
            view.stamp.localEulerAngles = new Vector3(0f, 0f, 6f);
            view.stampGroup = view.stamp.gameObject.AddComponent<CanvasGroup>();
            view.stampTitle = Outlined(UiKit.Display(UiKit.OneLine(UiKit.Label("Title", view.stamp, "", 116, Palette.Gold, 0, 0.36f, 1, 1))));
            view.stampSub = Outlined(UiKit.Display(UiKit.OneLine(UiKit.Label("Sub", view.stamp, "", 46, Palette.Text, 0, 0, 1, 0.36f))));

            view.root.SetActive(false);
            return view;
        }

        // focusPoint: 몬스터 위치(이 창의 좌표, 가운데 기준) — 없으면 화면 위쪽 가운데. tileSize: 한 칸의 화면 크기(원·괄호 크기 기준, 매 프레임).
        // strikeDamage: 맞히면 띄울 피해 숫자. vibrate: 틀리면 진동 (설정)
        public void Show(QuizQuestion quiz, float seconds, Action<int> onAnswered, Func<Vector2?> focusPoint = null,
            Func<float> tileSize = null, int strikeDamage = 0, bool vibrate = false)
        {
            question = quiz;
            answered = onAnswered;
            focus = focusPoint;
            tilePixels = tileSize;
            this.strikeDamage = strikeDamage;
            this.vibrate = vibrate;
            limit = Mathf.Max(0.5f, seconds);
            elapsed = shownTime = resultTimer = resultElapsed = lineTimer = 0f;
            picked = int.MinValue;
            lastTick = int.MaxValue;
            lastFocus = new Vector2(0f, rootRect.rect.height * 0.15f);

            word.text = quiz.Prompt;
            for (int i = 0; i < choices.Count; i++)
            {
                bool has = i < quiz.Choices.Count;
                choices[i].gameObject.SetActive(has);
                if (!has) continue;
                var label = UiKit.LabelOf(choices[i]);
                label.text = quiz.Choices[i];
                label.color = Palette.Text;
                choices[i].image.color = Palette.Button;
                choices[i].interactable = true;
            }
            closing.gameObject.SetActive(true);
            closing.color = Palette.Gold;
            target.gameObject.SetActive(true);
            lockMark.SetActive(true);
            banner.gameObject.SetActive(true);
            vignette.color = Color.black;
            flash.color = Color.clear;
            slash.gameObject.SetActive(false);
            damage.gameObject.SetActive(false);
            stamp.gameObject.SetActive(false);
            rootRect.anchoredPosition = Vector2.zero;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            Tick(0f);
        }

        // FieldScreen.Update에서 실제 시간으로 (느려지는 것은 몬스터뿐)
        public void Tick(float dt)
        {
            if (!IsOpen) return;
            shownTime += dt;
            Follow();
            Intro();
            lineTimer += dt;
            if (lineTimer >= 0.07f) // 집중선이 파르르 (만화 효과선처럼)
            {
                lineTimer = 0f;
                lineAngle = (lineAngle + 47f) % 360f;
                lines.rectTransform.localEulerAngles = new Vector3(0f, 0f, lineAngle);
            }
            if (picked != int.MinValue)
            {
                Result(dt);
                return;
            }

            elapsed += dt;
            float remain = Mathf.Clamp01(1f - elapsed / limit);
            closing.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(RingEndTiles, RingStartTiles, remain) * TileSize;
            bool danger = remain < DangerRatio;
            closing.color = danger ? Palette.Bad : Palette.Gold;
            closing.Thickness = danger ? 12f + Mathf.Abs(Mathf.Sin(shownTime * 14f)) * 4f : 10f;
            if (danger)
            {
                int second = Mathf.CeilToInt(limit - elapsed);
                if (second != lastTick)
                {
                    lastTick = second;
                    Sound.Play(Sfx.Click); // 째깍
                }
            }
            lockMark.Root.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(shownTime * 3f) * 8f);
            if (elapsed >= limit) Pick(-1);
        }

        public void Pick(int index)
        {
            if (!IsOpen || picked != int.MinValue) return;
            picked = index;
            bool correct = question.IsCorrect(index);
            Sound.Play(correct ? Sfx.Critical : Sfx.Wrong);
            if (correct) Sound.Play(Sfx.Hit, 0.6f);
            else if (vibrate) Haptics.Vibrate();
            for (int i = 0; i < choices.Count; i++)
            {
                choices[i].interactable = false;
                if (i == question.CorrectIndex) choices[i].image.color = Palette.Good;
                else if (i == index) choices[i].image.color = Palette.Bad;
                else
                {
                    choices[i].image.color = Palette.Disabled;
                    UiKit.LabelOf(choices[i]).color = Palette.TextDim;
                }
            }
            closing.gameObject.SetActive(false);
            target.gameObject.SetActive(false);
            lockMark.SetActive(false);
            banner.gameObject.SetActive(false);

            stampTitle.text = correct ? "선제공격 성공!" : "놓쳤다!";
            stampTitle.color = correct ? Palette.Gold : Palette.Bad;
            stampSub.text = correct ? $"적 HP -{Preemptive.DamageRatio * 100f:0}%로 전투 시작" : index < 0 ? "시간 초과! 그냥 전투 시작" : "그냥 전투 시작";
            stampSub.color = correct ? Palette.Text : Palette.TextDim;
            stamp.gameObject.SetActive(true);
            if (correct)
            {
                slash.gameObject.SetActive(true);
                damage.text = strikeDamage > 0 ? $"-{strikeDamage}" : "";
                damage.gameObject.SetActive(strikeDamage > 0);
            }
            else vignette.color = MissTint;
            resultTimer = ResultSeconds;
            resultElapsed = 0f;
            Result(0f);
        }

        private float TileSize => Mathf.Max(20f, tilePixels != null ? tilePixels() : 90f);

        // 몬스터를 따라가고, 과녁·괄호 크기를 한 칸에 맞춤
        private void Follow()
        {
            var point = focus?.Invoke();
            if (point.HasValue) lastFocus = point.Value;
            focusGroup.anchoredPosition = lastFocus;
            float tile = TileSize;
            target.rectTransform.sizeDelta = Vector2.one * RingEndTiles * tile;
            lockMark.SetSize(1.5f * tile);
        }

        // 처음 0.18초: 전체가 빠르게 나타나고, 띠가 왼쪽에서 들어오고, 단어가 크게 찍히고, 선택지가 차례로 톡
        private void Intro()
        {
            group.alpha = Mathf.Clamp01(shownTime / 0.1f);
            float k = EaseOut(shownTime / IntroSeconds);
            banner.anchoredPosition = new Vector2(Mathf.Lerp(-1400f, 0f, k), 0f);
            wordRect.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, k);
            for (int i = 0; i < choices.Count; i++)
                choices[i].transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, EaseOut((shownTime - 0.06f - i * 0.04f) / 0.12f));
        }

        private void Result(float dt)
        {
            resultElapsed += dt;
            resultTimer -= dt;
            float k = EaseOut(resultElapsed / 0.16f);
            stamp.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, k);
            stampGroup.alpha = k;
            bool correct = question.IsCorrect(picked);
            if (correct)
            {
                flash.color = new Color(1f, 1f, 1f, 0.6f * Mathf.Clamp01(1f - resultElapsed / 0.25f));
                slash.rectTransform.localScale = new Vector3(Mathf.Clamp01(resultElapsed / 0.08f), 1f, 1f);
                slash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - (resultElapsed - 0.2f) / 0.25f));
                damage.rectTransform.anchoredPosition = DamageOffset + Vector2.up * Mathf.Min(1f, resultElapsed / 0.6f) * 60f;
            }
            else
            {
                // 화면 흔들림 (0.35초 동안 줄어듦)
                float shake = Mathf.Clamp01(1f - resultElapsed / 0.35f) * 18f;
                rootRect.anchoredPosition = new Vector2(Mathf.Sin(resultElapsed * 90f), Mathf.Cos(resultElapsed * 77f)) * shake;
            }
            if (resultTimer <= 0f) Finish();
        }

        private void Finish()
        {
            rootRect.anchoredPosition = Vector2.zero;
            root.SetActive(false);
            var callback = answered;
            answered = null;
            focus = null;
            tilePixels = null;
            callback?.Invoke(picked);
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        private static Text Outlined(Text label)
        {
            var outline = label.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(3f, -3f);
            return label;
        }

        // 가운데는 투명하고 바깥으로 갈수록 진해지는 흰 원 (Image.color로 검정·빨강을 입힌다). 반지름 0.09까지 밝고 0.32부터 진함.
        // Linear 색공간이라 검정 알파를 높게 (0.97)
        private static Sprite VignetteSprite()
        {
            if (vignetteSprite != null) return vignetteSprite;
            const int size = 256;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 0.97f, Mathf.InverseLerp(0.09f, 0.32f, d)));
            }
            return vignetteSprite = MakeSprite("qte_vignette", size, pixels);
        }

        // 가운데에서 바깥으로 뻗는 가는 집중선 (굵기·간격이 들쭉날쭉, 가운데는 비움)
        private static Sprite SpeedLinesSprite()
        {
            if (linesSprite != null) return linesSprite;
            const int size = 512, buckets = 120;
            var random = new System.Random(74);
            var widths = new float[buckets];
            for (int i = 0; i < buckets; i++) widths[i] = random.NextDouble() < 0.55 ? 0.15f + (float)random.NextDouble() * 0.45f : 0f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float slot = (Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 1f) % 1f * buckets;
                int bucket = Mathf.Min(buckets - 1, (int)slot);
                float half = widths[bucket] * 0.5f, offset = Mathf.Abs(slot - bucket - 0.5f);
                float a = half > 0f ? Mathf.Clamp01((half - offset) * 6f) : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, a * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.34f, d)));
            }
            return linesSprite = MakeSprite("qte_lines", size, pixels);
        }

        private static Sprite MakeSprite(string name, int size, Color[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }
    }

    // 몬스터 조준 괄호 (Figma 'Target Lock'): 네 모서리 ㄱ자. 빨강 = 선제공격 거리(필드), 금색 + 가운데 점 = QTE 중
    public class TargetLock
    {
        private const float Thickness = 9f;

        private readonly Image[] bars = new Image[8];
        private Image center;

        public RectTransform Root { get; private set; }
        public bool IsActive => Root.gameObject.activeSelf;

        public static TargetLock Create(Transform parent, Color color, bool showCenter)
        {
            var mark = new TargetLock { Root = UiKit.Rect("TargetLock", parent, 0.5f, 0.5f, 0.5f, 0.5f) };
            Vector2[] corners = { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
            for (int i = 0; i < 4; i++)
            for (int j = 0; j < 2; j++)
            {
                var corner = corners[i];
                var bar = UiKit.Panel($"Bar{i}{j}", mark.Root, color, corner.x, corner.y, corner.x, corner.y);
                bar.raycastTarget = false;
                bar.rectTransform.pivot = corner;
                mark.bars[i * 2 + j] = bar;
            }
            mark.center = UiKit.Pill(UiKit.Panel("Center", mark.Root, color, 0.5f, 0.5f, 0.5f, 0.5f));
            mark.center.raycastTarget = false;
            mark.center.rectTransform.sizeDelta = new Vector2(14f, 14f);
            mark.center.gameObject.SetActive(showCenter);
            mark.SetSize(150f);
            return mark;
        }

        public void SetSize(float size)
        {
            Root.sizeDelta = new Vector2(size, size);
            float length = size * 0.28f;
            for (int i = 0; i < 4; i++)
            {
                bars[i * 2].rectTransform.sizeDelta = new Vector2(length, Thickness);
                bars[i * 2 + 1].rectTransform.sizeDelta = new Vector2(Thickness, length);
            }
        }

        public void SetActive(bool active)
        {
            if (Root.gameObject.activeSelf != active) Root.gameObject.SetActive(active);
        }
    }
}
