#if WORDRPG_TESTER || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 테스터 메뉴 (#229): 설정 > 정보 칸의 [테스터 메뉴]로 연다. 테스트 빌드(TestFlight)·에디터에만 있는 개발용 화면 —
    // 플레이어는 볼 수 없으므로 Figma에 그리지 않고 설정 화면과 같은 판·버튼으로 만든다.
    // 주인공(레벨 ±5 · 골드 +10,000 · 재료 ×10 · 살금살금 상시 · 무료 상태로 #251) · 지역까지 건너뛰기(앞 지역 보스를 이긴 것으로 + 그 지역 시작 지점으로) · 단어 발견(+25 · 모두)
    public class TesterMenuView
    {
        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public string Status => status.text;   // 테스트용
        public string Message => message.text; // 테스트용

        private Text status, message;
        private Button quietButton, freeButton;
        private Entitlements owned;
        private RectTransform areaSection, wordSection;
        private readonly List<GameObject> built = new List<GameObject>(); // Show마다 다시 만드는 지역 버튼·단어장 줄
        private GameSession session;
        private GameDatabase database;
        private DateTime nowUtc;
        private Action onChanged;
        private Action<FieldArea> onTravel;

        private IEnumerable<FieldArea> Areas => database != null ? database.Areas : Enumerable.Empty<FieldArea>();

        public static TesterMenuView Create(Transform parent)
        {
            var view = new TesterMenuView();
            var root = UiKit.Stretch("TesterMenuView", parent);
            view.Root = root.gameObject;
            view.Root.AddComponent<Image>().color = Palette.Background; // 뒤쪽 설정 화면을 가리고 막는다

            UiKit.Display(UiKit.Label("Title", root, "테스터 메뉴", 56, Palette.Gold, 0, 0.94f, 1, 0.99f));
            UiKit.Label("Sub", root, "테스트 빌드에만 있어요 — 앱스토어 버전에는 없음", 26, Palette.TextDim, 0.03f, 0.905f, 0.97f, 0.94f);

            var hero = Section(root, "주인공", 0.665f, 0.895f);
            view.status = UiKit.Label("Status", hero, "", 30, Palette.Text, 0.035f, 0.6f, 0.965f, 0.82f,
                TextAnchor.MiddleLeft, FontStyle.Bold, true, 20);
            AddButton(hero, "TesterLevelUp", $"레벨 +{TesterTools.LevelStep}", 0.035f, 0.32f, 0.33f, 0.58f, view.LevelUp);
            AddButton(hero, "TesterLevelDown", $"레벨 -{TesterTools.LevelStep}", 0.35f, 0.32f, 0.65f, 0.58f, view.LevelDown);
            AddButton(hero, "TesterGold", $"골드 +{TesterTools.GoldStep:N0}", 0.67f, 0.32f, 0.965f, 0.58f, view.Gold);
            AddButton(hero, "TesterMaterials", $"재료 ×{TesterTools.MaterialStep}", 0.035f, 0.04f, 0.33f, 0.3f, view.Materials);
            view.quietButton = AddButton(hero, "TesterQuiet", "", 0.35f, 0.04f, 0.65f, 0.3f, view.Quiet);
            view.freeButton = AddButton(hero, "TesterFree", "", 0.67f, 0.04f, 0.965f, 0.3f, view.Free);

            view.areaSection = Section(root, "지역까지 건너뛰기", 0.38f, 0.655f);
            UiKit.Label("Hint", view.areaSection, "앞 지역 보스를 모두 이긴 것으로 하고(보스 보상·유적 동료도) 그 지역 시작 지점으로 가요",
                24, Palette.TextDim, 0.035f, 0.71f, 0.965f, 0.85f, TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);

            view.wordSection = Section(root, "단어 발견", 0.165f, 0.37f);
            UiKit.Label("Hint", view.wordSection, "문제 없이 찾은 것으로 해요 (단어장 순서대로, 곧 복습 문제로 나옴)",
                24, Palette.TextDim, 0.035f, 0.66f, 0.965f, 0.8f, TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);

            view.message = UiKit.Label("Message", root, "", 30, Palette.Gold, 0.03f, 0.1f, 0.97f, 0.16f,
                TextAnchor.MiddleCenter, FontStyle.Bold, true, 20);

            var close = UiKit.MakeButton("TesterCloseButton", root, "닫기", Palette.Neutral, 44, 0.05f, 0.015f, 0.95f, 0.095f);
            close.onClick.AddListener(view.Hide);
            view.Root.SetActive(false);
            return view;
        }

        // changed: 값을 바꿀 때마다 (저장·HUD). travel: 지역 버튼을 누르면 (건너뛴 뒤 그 지역 시작 지점으로)
        public void Show(GameSession gameSession, GameDatabase gameDatabase, DateTime now, Action changed, Action<FieldArea> travel, Entitlements entitlements = null)
        {
            owned = entitlements;
            session = gameSession;
            database = gameDatabase;
            nowUtc = now;
            onChanged = changed;
            onTravel = travel;
            message.text = "";
            Rebuild();
            Refresh();
            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
        }

        public void Hide() => Root.SetActive(false);

        // 지역 버튼 (2줄씩) · 단어장 줄 — 데이터에 따라 수가 바뀌므로 열 때마다 새로
        private void Rebuild()
        {
            foreach (var go in built) UnityEngine.Object.Destroy(go);
            built.Clear();

            var areas = TesterTools.StoryAreas(Areas);
            const float top = 0.69f, bottom = 0.04f, gap = 0.025f;
            int rows = Mathf.Max(1, (areas.Count + 1) / 2);
            float h = (top - bottom - (rows - 1) * gap) / rows;
            for (int i = 0; i < areas.Count; i++)
            {
                var target = areas[i];
                float maxY = top - (i / 2) * (h + gap);
                float minX = i % 2 == 0 ? 0.035f : 0.51f, maxX = i % 2 == 0 ? 0.49f : 0.965f;
                var button = AddButton(areaSection, $"TesterSkip_{target.AreaId}", target.DisplayName, minX, maxY - h, maxX, maxY,
                    () => Skip(target));
                built.Add(button.gameObject);
            }

            var books = TesterTools.Books(Areas);
            const float wordTop = 0.62f, rowGap = 0.04f;
            float rowH = Mathf.Min(0.28f, (wordTop - 0.04f - (books.Count - 1) * rowGap) / Mathf.Max(1, books.Count));
            for (int i = 0; i < books.Count; i++)
            {
                var book = books[i];
                float maxY = wordTop - i * (rowH + rowGap);
                var row = UiKit.Stretch($"TesterBook_{book.RegionId}", wordSection);
                row.anchorMin = new Vector2(0, maxY - rowH);
                row.anchorMax = new Vector2(1, maxY);
                UiKit.Label("Name", row, book.RegionName, 26, Palette.Text,
                    0.035f, 0.5f, 0.56f, 1f, TextAnchor.LowerLeft, FontStyle.Bold, true, 18);
                UiKit.Label("Count", row, "", 26, Palette.TextDim, 0.035f, 0f, 0.56f, 0.5f, TextAnchor.UpperLeft);
                AddButton(row, $"TesterWords_{book.RegionId}", $"+{TesterTools.WordStep}", 0.58f, 0.05f, 0.765f, 0.95f,
                    () => Words(book, TesterTools.WordStep));
                AddButton(row, $"TesterWordsAll_{book.RegionId}", "모두", 0.78f, 0.05f, 0.965f, 0.95f,
                    () => Words(book, book.Words.Count));
                built.Add(row.gameObject);
            }
        }

        private void Refresh()
        {
            var hero = session.Hero;
            string party = string.Concat(session.Companions.Select(c => $" · {c.DisplayName} Lv.{c.Level}"));
            status.text = $"{hero.DisplayName} Lv.{hero.Level}{party} · {session.Inventory.Gold:N0}골드 · 성유물 {hero.Relics.Count}개";
            UiKit.LabelOf(quietButton).text = TesterTools.IsAlwaysQuiet(session) ? "살금살금 끄기" : "살금살금 상시";
            UiKit.LabelOf(freeButton).text = owned != null && owned.TesterFree ? "정식판으로" : "무료 상태로";
            foreach (var book in TesterTools.Books(Areas))
            {
                var row = wordSection.Find($"TesterBook_{book.RegionId}");
                if (row == null) continue;
                var progress = Dex.GetProgress(book, session.Vocabulary);
                row.Find("Count").GetComponent<Text>().text = $"찾은 단어 {progress.Discovered} / {progress.Total}";
            }
        }

        private void LevelUp()
        {
            int before = session.Hero.Level;
            int gained = TesterTools.LevelUp(session);
            Changed(gained > 0 ? $"레벨 {before} → {session.Hero.Level}" : "이미 최고 레벨이에요");
        }

        private void LevelDown()
        {
            int before = session.Hero.Level;
            int dropped = TesterTools.LevelDown(session);
            Changed(dropped > 0 ? $"레벨 {before} → {session.Hero.Level}" : "이미 레벨 1이에요");
        }

        private void Gold()
        {
            TesterTools.GiveGold(session);
            Changed($"골드 +{TesterTools.GoldStep:N0}");
        }

        private void Quiet() => Changed(TesterTools.ToggleAlwaysQuiet(session) ? "살금살금 상시 켬 — 몬스터가 나오지 않아요" : "살금살금 상시 끔");

        private void Free()
        {
            if (owned == null) Changed("이 실행에는 결제가 연결되어 있지 않아요");
            else if (owned.TesterFree)
            {
                owned.SetTesterFree(false);
                Changed("정식판으로 되돌렸어요");
            }
            else if (!owned.HasFullVersion) Changed("이미 무료 상태예요");
            else
            {
                TesterTools.GoFree(session, owned);
                Changed("무료 상태예요 — 사면 결제 없이 정식판이 되고 특전을 다시 받아요");
            }
        }

        private void Materials()
        {
            int kinds = TesterTools.GiveMaterials(session, database != null ? database.Items : Enumerable.Empty<Items.ItemData>());
            Changed($"강화 재료 {kinds}종류 ×{TesterTools.MaterialStep}");
        }

        private void Words(WordDatabase book, int count)
        {
            int found = TesterTools.DiscoverWords(session, book, count, nowUtc);
            Changed(found > 0 ? $"{book.RegionName} 단어 {found}개를 찾았어요" : "이미 다 찾았어요");
        }

        private void Skip(FieldArea target)
        {
            int bosses = TesterTools.SkipTo(session, Areas, target);
            var travel = onTravel;
            Hide();
            travel?.Invoke(target);
            Debug.Log($"[Tester] {target.DisplayName}까지 건너뛰기 — 보스 {bosses}마리를 이긴 것으로");
        }

        private void Changed(string text)
        {
            onChanged?.Invoke();
            message.text = text;
            Refresh();
        }

        // 설정 화면과 같은 둥근 판 + 금색 소제목
        private static RectTransform Section(RectTransform root, string title, float minY, float maxY)
        {
            var panel = UiKit.RoundPanel($"Section_{title}", root, Palette.Panel, UiKit.RadiusLg, 0.022f, minY, 0.978f, maxY);
            float header = Mathf.Min(0.3f, 60f / ((maxY - minY) * 1920f));
            UiKit.Label("Header", panel.transform, title, 30, Palette.Gold, 0.035f, 1f - header - 0.04f, 0.8f, 0.98f,
                TextAnchor.MiddleLeft);
            return panel.rectTransform;
        }

        private static Button AddButton(Transform parent, string name, string text, float minX, float minY, float maxX, float maxY,
            Action click)
        {
            var button = UiKit.MakeButton(name, parent, text, Palette.Neutral, 34, minX, minY, maxX, maxY, true);
            button.onClick.AddListener(() => click());
            return button;
        }
    }
}
#endif
