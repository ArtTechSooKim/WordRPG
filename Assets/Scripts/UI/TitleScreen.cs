using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 타이틀 (Figma '타이틀 (#37)'): 도트 초원 풍경 배경(위아래 어둡게), 로고, 길 위의 주인공 + 둘레에 끼운 성유물,
    // 저장 요약, "화면을 터치하세요" — 어디든 누르면 바로 이어하기 (저장이 없으면 새 게임). 설정.
    // 처음부터 다시 하기·튜토리얼 다시 보기는 설정에서 (2026-10-06 사용자 결정). 시작하면 필드 씬으로
    public class TitleScreen : MonoBehaviour
    {
        [SerializeField] private string fieldScene = GameManager.FieldSceneName;

        private GameManager manager;
        private Action<bool> startOverride; // 테스트: 씬을 바꾸는 대신 (새 게임 여부)를 받는다
        private bool started;

        private RectTransform root;
        private GameObject saveCard;
        private Text saveLine1, saveLine2;
        private readonly List<(Image back, Text initial, Image sprite)> avatars = new List<(Image, Text, Image)>();
        private Image heroImage;
        private readonly List<(Image badge, Image icon)> relicBadges = new List<(Image, Image)>();
        private Text tapLabel;
        private ConfirmDialog dialog;
        private SettingsView settingsView;
        private readonly List<(RectTransform rt, float baseY, float phase)> floaters = new List<(RectTransform, float, float)>();

        public bool IsSettingsOpen => settingsView != null && settingsView.IsOpen;
        public bool IsStarted => started;

        public void Configure(Action<bool> onStart) => startOverride = onStart;

        private void Start()
        {
            manager = GameManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[TitleScreen] GameManager가 없습니다");
                return;
            }
            Build();
            Refresh();
            Sound.PlayMusic(Music.Title);
        }

        // 뒤로가기: 설정·확인 창을 닫고, 아무것도 없으면 '게임을 끝낼까요?'
        public void HandleBack()
        {
            if (dialog == null) return;
            if (dialog.IsOpen) dialog.Hide();
            else if (settingsView.IsOpen) settingsView.Hide();
            else if (GameManager.CanQuit)
                dialog.Show("게임을 끝낼까요?", "다음에 켜면 이어서 할 수 있어요.", "끝내기", false, GameManager.QuitGame);
        }

        public bool IsDialogOpen => dialog != null && dialog.IsOpen;

        private void Update()
        {
            if (FieldScreen.BackPressed()) HandleBack();
            // 키보드(PC)는 Enter·Space로 시작
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) OnTap();
            // 끼운 성유물 배지가 천천히 오르내리고, "화면을 터치하세요"가 숨 쉬듯 깜빡인다
            float t = Time.unscaledTime;
            if (tapLabel != null) tapLabel.color = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 1.8f)));
            foreach (var (rt, baseY, phase) in floaters)
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY + Mathf.Sin(t * 1.2f + phase) * 10f);
        }

        // ------------------------------------------------------------------ 화면

        private void Build()
        {
            UiKit.EnsureEventSystem();
            var canvasGo = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            UiKit.Panel("Background", canvasGo.transform, Palette.Background);
            BuildScene(canvasGo.transform);
            root = UiKit.Stretch("SafeArea", canvasGo.transform);
            UiKit.ApplySafeArea(root);

            // 로고 (게임 이름: 영단어RPG) — 풍경 위에서도 또렷하게 그림자
            var logo = UiKit.Display(UiKit.Label("Logo", root, "영단어RPG", 168, Palette.Gold, 0, 0.7f, 1, 0.86f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 96));
            var logoShadow = logo.gameObject.AddComponent<Shadow>();
            logoShadow.effectColor = new Color(0, 0, 0, 0.6f);
            logoShadow.effectDistance = new Vector2(0, -8);
            var subtitle = UiKit.Label("Subtitle", root, "영단어와 함께 떠나는 모험", 40, Palette.Text, 0, 0.655f, 1, 0.7f,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            subtitle.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);

            // 길 위에 선 주인공 (도트 그대로 크게) + 발밑 그림자
            var ground = UiKit.Pill(UiKit.Panel("HeroShadow", root, new Color(0, 0, 0, 0.35f), 0, 1, 0, 1));
            ground.raycastTarget = false;
            ground.rectTransform.sizeDelta = new Vector2(220, 50);
            Place(ground.rectTransform, 540, 1175);
            heroImage = UiKit.IconImage("Hero", root, null, 0, 1, 0, 1);
            heroImage.rectTransform.sizeDelta = new Vector2(192, 192);
            Place(heroImage.rectTransform, 540, 1071);

            // 끼운 성유물 3칸: 주인공 둘레에 둥실 떠 있는 금테 배지
            var spots = new (float x, float y)[] { (250, 900), (830, 900), (540, 760) };
            for (int i = 0; i < spots.Length; i++)
            {
                var badge = UiKit.Pill(UiKit.Panel($"RelicBadge_{i}", root, new Color(Palette.PanelLight.r, Palette.PanelLight.g, Palette.PanelLight.b, 0.92f), 0, 1, 0, 1));
                badge.raycastTarget = false;
                badge.rectTransform.sizeDelta = new Vector2(150, 150);
                Place(badge.rectTransform, spots[i].x, spots[i].y);
                var ring = UiKit.Outline(UiKit.Panel("Ring", badge.transform, Palette.Gold), 75, 4);
                ring.gameObject.AddComponent<AutoPill>();
                ring.raycastTarget = false;
                var icon = UiKit.IconImage("Icon", badge.transform, null, 0.13f, 0.13f, 0.87f, 0.87f);
                relicBadges.Add((badge, icon));
                floaters.Add((badge.rectTransform, -spots[i].y, i * 2.1f));
            }

            // 저장 요약 카드
            var save = UiKit.RoundPanel("SaveCard", root, Palette.Panel, UiKit.RadiusMd, 0.067f, 0.265f, 0.933f, 0.328f);
            save.raycastTarget = false;
            saveCard = save.gameObject;
            for (int i = 0; i < 3; i++) // 주인공 + 끼운 성유물 두 개
            {
                var back = UiKit.Pill(UiKit.Panel($"Avatar_{i}", save.transform, Palette.PanelLight, 0, 0.5f, 0, 0.5f));
                back.raycastTarget = false;
                back.rectTransform.sizeDelta = new Vector2(72, 72);
                back.rectTransform.anchoredPosition = new Vector2(64 + i * 58, 0);
                var initial = UiKit.Display(UiKit.Label("Initial", back.transform, "", 34, Palette.Text, 0, 0, 1, 1));
                var sprite = UiKit.IconImage("Sprite", back.transform, null, 0.08f, 0.08f, 0.92f, 0.92f);
                avatars.Add((back, initial, sprite));
            }
            saveLine1 = UiKit.Label("Line1", save.transform, "", 34, Palette.Text, 0.25f, 0.5f, 0.98f, 0.92f,
                TextAnchor.MiddleLeft, FontStyle.Bold, true, 20);
            saveLine2 = UiKit.Label("Line2", save.transform, "", 30, Palette.TextDim, 0.25f, 0.08f, 0.98f, 0.5f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);

            // 화면 어디든 누르면 시작 (설정 버튼·창은 그 위에 있어 따로 눌림)
            var tapArea = UiKit.Panel("TapToStart", root, Color.clear);
            UiKit.AddButton(tapArea).onClick.AddListener(OnTap);
            tapLabel = UiKit.Display(UiKit.Label("TapLabel", root, "화면을 터치하세요", 56, Color.white, 0, 0.165f, 1, 0.215f));
            tapLabel.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);

            var settingsButton = UiKit.IconButton("SettingsButton", root, "settings", "설정", Palette.PanelLight, 1, 1, 1, 1);
            var settingsRect = (RectTransform)settingsButton.transform;
            settingsRect.pivot = new Vector2(1, 1);
            settingsRect.sizeDelta = new Vector2(120, 120);
            settingsRect.anchoredPosition = new Vector2(-24, -40);
            settingsButton.onClick.AddListener(OpenSettings);

            UiKit.Label("Footer", root, $"v{Application.version}   ·   글꼴 Jua · Noto Sans KR (SIL OFL 1.1)", 24, Palette.TextDim,
                0, 0.01f, 1, 0.04f);

            dialog = ConfirmDialog.Create(root);
            settingsView = SettingsView.Create(root);
        }

        // 배경: 초원 타일로 그린 도트 풍경을 화면을 덮게 (도트 그대로) + 위(로고)·아래(버튼) 어둡게
        private static void BuildScene(Transform canvas)
        {
            var texture = Resources.Load<Texture2D>("Art/NinjaAdventure/Title/title_scene");
            if (texture == null) return;
            var holder = UiKit.Stretch("Scene", canvas);
            var scene = UiKit.IconImage("Image", holder, Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 16), 0.5f, 0.5f, 0.5f, 0.5f);
            var fitter = scene.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = texture.width / (float)texture.height;
            var shade = UiKit.Panel("Shade", holder, Color.white);
            shade.sprite = ShadeSprite();
            shade.raycastTarget = false;
        }

        // 위아래는 진하게, 가운데(주인공 자리)는 풍경이 보이게 — Figma 'shade' 그라데이션과 같은 값
        private static Sprite ShadeSprite()
        {
            var stops = new (float pos, float alpha)[] { (0f, 0.97f), (0.32f, 0.85f), (0.42f, 0.1f), (0.58f, 0.05f), (0.7f, 0.55f), (1f, 0.92f) };
            const int height = 128;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "title_shade" };
            for (int y = 0; y < height; y++)
            {
                float t = y / (height - 1f); // 0 = 아래, 1 = 위
                float alpha = stops[stops.Length - 1].alpha;
                for (int i = 1; i < stops.Length; i++)
                {
                    if (t > stops[i].pos) continue;
                    var (p0, a0) = stops[i - 1];
                    var (p1, a1) = stops[i];
                    alpha = Mathf.Lerp(a0, a1, (t - p0) / Mathf.Max(0.0001f, p1 - p0));
                    break;
                }
                var c = Palette.Background;
                texture.SetPixel(0, y, new Color(c.r, c.g, c.b, alpha));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f), 100);
        }

        // Figma 좌표(1080 폭, 위에서부터 y)로 놓되 가로는 비율로 — 화면 폭이 달라도 좌우 균형 유지
        private static void Place(RectTransform rt, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(x / 1080f, 1f);
            rt.anchoredPosition = new Vector2(0, -y);
        }

        // 저장 요약은 저장이 있을 때만
        private void Refresh()
        {
            bool hasSave = manager.HasSave;
            var session = manager.Session;
            var hero = session.Hero;
            heroImage.sprite = UiKit.HeroPortrait(hero.Data);
            heroImage.enabled = heroImage.sprite != null;
            for (int i = 0; i < relicBadges.Count; i++)
            {
                var relic = hero.SlotAt(i);
                relicBadges[i].badge.gameObject.SetActive(relic != null);
                if (relic == null) continue;
                relicBadges[i].icon.sprite = UiKit.RelicIcon(relic.Data);
                relicBadges[i].icon.enabled = relicBadges[i].icon.sprite != null;
            }
            for (int i = 0; i < avatars.Count; i++)
            {
                // 저장 요약: 0 = 주인공, 1·2 = 끼운 성유물 (없으면 숨김)
                var relic = i == 0 ? null : hero.SlotAt(i - 1);
                bool has = i == 0 || relic != null;
                avatars[i].back.gameObject.SetActive(has && hasSave);
                if (!has) continue;
                var sprite = i == 0 ? UiKit.HeroPortrait(hero.Data) : UiKit.RelicIcon(relic.Data);
                string name = i == 0 ? hero.DisplayName : relic.Data.DisplayName;
                string initial = name.Length > 0 ? name.Substring(0, 1) : "?";
                var color = i == 0 ? Palette.PanelLight : Color.Lerp(Palette.PanelLight, relic.Data.PlaceholderColor, 0.22f);
                avatars[i].back.color = color;
                avatars[i].initial.text = initial;
                avatars[i].initial.enabled = sprite == null;
                avatars[i].sprite.sprite = sprite;
                avatars[i].sprite.enabled = sprite != null;
            }

            saveCard.SetActive(hasSave);
            if (manager.SaveBlocked)
            {
                // 더 새 버전 앱의 기록: 지우지 않고 그대로 있다고 알리고 시작은 막는다 (#48)
                for (int i = 0; i < avatars.Count; i++) avatars[i].back.gameObject.SetActive(false);
                saveLine1.text = "더 새 버전 앱에서 저장한 기록이 있어요";
                saveLine2.text = "기록은 그대로 있어요 · 앱을 최신 버전으로 업데이트해 주세요";
                tapLabel.text = "앱을 업데이트해 주세요";
                return;
            }
            tapLabel.text = "화면을 터치하세요";
            if (hasSave)
            {
                var area = manager.Database != null && session.World.AreaId != null ? manager.Database.FindArea(session.World.AreaId) : null;
                string where = area != null ? area.DisplayName : "초원";
                saveLine1.text = $"{where}  ·  {hero.DisplayName} Lv{hero.Level}  ·  성유물 {hero.Relics.Count}개";
                string total = area != null && area.Words != null ? $" / {area.Words.Words.Count}" : "";
                int battles = session.Record.BattlesFought;
                saveLine2.text = $"발견한 단어 {session.Vocabulary.DiscoveredCount}{total}   ·   전투 {battles}번";
            }
        }

        // 화면을 누르면: 저장이 있으면 이어하기, 없으면 새 게임
        private void OnTap()
        {
            if (started || dialog.IsOpen || settingsView.IsOpen) return;
            if (manager.SaveBlocked) return; // 더 새 버전 앱의 기록 — 업데이트해야 이어할 수 있음
            Begin(!manager.HasSave);
        }

        // 설정: 처음부터 다시 하기(두 번 확인 → 저장을 지우고 다시 "화면을 터치하세요"), 튜토리얼 다시 보기(안내를 지우고 바로 시작)
        private void OpenSettings()
        {
            settingsView.Show(manager.Settings, manager.SaveSettings, () =>
            {
                manager.DeleteSave();
                Refresh();
            }, () =>
            {
                manager.Session.Tutorials.Clear();
                Begin(!manager.HasSave);
            });
        }

        private void Begin(bool newGame)
        {
            if (started || manager.SaveBlocked) return;
            started = true;
            manager.MarkPlaying();
            manager.Save(); // 새 게임도 바로 저장 → 다음에 켜면 [이어하기]
            if (startOverride != null)
            {
                startOverride(newGame);
                return;
            }
            SceneManager.LoadScene(fieldScene);
        }
    }
}
