using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Field;
using WordRPG.Heroes;

namespace WordRPG.UI
{
    // 주인공 모습 고르기 (#124 사용자: 디아블로처럼 처음에 캐릭터를 고름 — 모습만 다르고 능력은 같음, Figma '캐릭터 고르기 (#124)').
    // 위: 고른 모습이 4방향(아래·왼쪽·위·오른쪽)으로 나란히 걷는 큰 미리보기 + 이름표 / 가운데: 얼굴 카드 4×2 / 아래: [이 모습으로 시작]
    public class CharacterSelectView
    {
        private static readonly Direction[] Turn = { Direction.Down, Direction.Left, Direction.Up, Direction.Right };

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public string Selected { get; private set; }
        public IReadOnlyList<HeroLook> Looks => looks;

        private readonly List<HeroLook> looks = new List<HeroLook>();
        private readonly List<Image> cardEdges = new List<Image>();
        private readonly List<CharacterSheet> sheets = new List<CharacterSheet>();
        private RectTransform cards;
        private Image[] preview;
        private Text lookName, note;
        private Action<string> chosen;
        private float clock;

        public static CharacterSelectView Create(Transform parent)
        {
            var view = new CharacterSelectView();
            var root = UiKit.Stretch("CharacterSelectView", parent);
            view.Root = root.gameObject;
            view.Root.AddComponent<Image>().color = Palette.Background; // 뒤 화면(필드)을 가리고 터치도 막음

            UiKit.Display(UiKit.Label("Title", root, "모습을 골라 주세요", 60, Palette.Gold, 0, 0.92f, 1, 0.985f));
            UiKit.Label("Subtitle", root, "모습만 다르고 능력은 같아요", 30, Palette.TextDim, 0, 0.88f, 1, 0.92f);

            var stage = UiKit.RoundPanel("Preview", root, Palette.Panel, UiKit.RadiusLg, 0.056f, 0.635f, 0.944f, 0.88f);
            view.preview = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float x = 0.0625f + i * 0.225f;
                view.preview[i] = UiKit.IconImage($"Sprite_{i}", stage.transform, null, x, 0.42f, x + 0.2f, 0.92f);
            }
            view.lookName = UiKit.Display(UiKit.Label("LookName", stage.transform, "", 56, Palette.Text, 0, 0.17f, 1, 0.38f));
            UiKit.Label("PreviewNote", stage.transform, "필드에서 이 모습으로 걸어 다녀요", 28, Palette.TextDim, 0, 0.03f, 1, 0.17f);

            view.cards = UiKit.Rect("Cards", root, 0.056f, 0.287f, 0.944f, 0.605f);
            view.note = UiKit.Label("Note", root, "", 26, Palette.TextDim, 0, 0.12f, 1, 0.15f);
            var start = UiKit.MakeButton("CharacterStartButton", root, "이 모습으로 시작", Palette.Evolve, 52, 0.056f, 0.035f, 0.944f, 0.1f);
            start.onClick.AddListener(view.Confirm);
            view.Root.SetActive(false);
            return view;
        }

        // updating: 1.0 세이브로 업데이트한 사람 (지금 모습이 골라져 있음)
        public void Show(IReadOnlyList<HeroLook> options, string current, bool updating, Action<string> onChosen)
        {
            looks.Clear();
            looks.AddRange(options);
            if (looks.Count == 0) looks.Add(new HeroLook(HeroLook.Fallback, "소년"));
            chosen = onChosen;
            BuildCards();
            note.text = updating ? "업데이트로 모습과 이름을 정할 수 있게 됐어요 · 지금 모습이 골라져 있어요" : "";
            Root.SetActive(true);
            Select(looks.Exists(l => l.LookId == current) ? current : looks[0].LookId);
        }

        public void Select(string lookId)
        {
            Selected = lookId;
            int index = looks.FindIndex(l => l.LookId == lookId);
            for (int i = 0; i < cardEdges.Count; i++) cardEdges[i].enabled = i == index;
            lookName.text = index >= 0 ? looks[index].DisplayName : "";
            clock = 0f;
            Tick(0f);
        }

        // 미리보기: 네 방향으로 제자리 걷기
        public void Tick(float dt)
        {
            if (!IsOpen) return;
            clock += dt;
            int index = looks.FindIndex(l => l.LookId == Selected);
            if (index < 0 || index >= sheets.Count || !sheets[index].IsLoaded) return;
            int step = Mathf.FloorToInt(clock * PlayerArt.FramesPerSecond);
            for (int i = 0; i < preview.Length; i++)
            {
                preview[i].sprite = sheets[index].Get(Turn[i], step);
                preview[i].enabled = preview[i].sprite != null;
            }
        }

        public void Confirm()
        {
            if (!IsOpen || string.IsNullOrEmpty(Selected)) return;
            Root.SetActive(false);
            chosen?.Invoke(Selected);
        }

        private void BuildCards()
        {
            foreach (Transform child in cards) UnityEngine.Object.Destroy(child.gameObject);
            cardEdges.Clear();
            sheets.Clear();
            const int columns = 4;
            int rows = Mathf.CeilToInt(looks.Count / (float)columns);
            for (int i = 0; i < looks.Count; i++)
            {
                var look = looks[i];
                int col = i % columns, row = i / columns;
                float w = 1f / columns, h = 1f / Mathf.Max(1, rows);
                var card = UiKit.MakeButton($"LookCard_{look.LookId}", cards, "", Palette.PanelLight, 30,
                    col * w + 0.008f, 1f - (row + 1) * h + 0.02f, (col + 1) * w - 0.008f, 1f - row * h - 0.02f);
                UiKit.IconImage("Face", card.transform, PlayerArt.Face(look.LookId), 0.1f, 0.26f, 0.9f, 0.94f);
                UiKit.OneLine(UiKit.Label("Name", card.transform, look.DisplayName, 30, Palette.Text, 0, 0.03f, 1, 0.24f,
                    TextAnchor.MiddleCenter, FontStyle.Bold, true, 20));
                var edge = UiKit.Outline(UiKit.Panel("Edge", card.transform, Palette.Gold), UiKit.RadiusMd, 6);
                edge.raycastTarget = false;
                edge.enabled = false;
                cardEdges.Add(edge);
                sheets.Add(new CharacterSheet(PlayerArt.SheetPathOf(look.LookId)));
                string id = look.LookId;
                card.onClick.AddListener(() => Select(id));
            }
        }
    }

    // 이름 정하기 (#124 사용자: "깃펜으로 떠오른 이름을 적자", Figma '이름 정하기 (#124)'): 어두운 막 + 금테 창 + 입력 칸 + [확인].
    // 한글·영어·숫자 8글자 (HeroNames) — 비어 있으면 [확인]이 눌리지 않음
    public class NameEntryView
    {
        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public InputField Input { get; private set; }
        public Button OkButton { get; private set; }

        private Text title;
        private Action<string> entered;

        public static NameEntryView Create(Transform parent)
        {
            var view = new NameEntryView();
            var root = UiKit.Stretch("NameEntryView", parent);
            view.Root = root.gameObject;
            UiKit.Panel("Dim", root, new Color(0f, 0f, 0f, 0.8f)).raycastTarget = true;

            var box = UiKit.RoundPanel("NameDialog", root, Palette.Panel, UiKit.RadiusLg, 0.074f, 0.343f, 0.926f, 0.677f);
            UiKit.Outline(UiKit.Panel("Edge", box.transform, Palette.Gold), UiKit.RadiusLg, 4).raycastTarget = false;
            view.title = UiKit.Display(UiKit.Label("Title", box.transform, "", 52, Palette.Gold, 0.04f, 0.66f, 0.96f, 0.94f));

            var field = UiKit.RoundPanel("NameField", box.transform, Palette.PanelLight, UiKit.RadiusMd, 0.087f, 0.41f, 0.913f, 0.6f);
            UiKit.Outline(UiKit.Panel("Edge", field.transform, Palette.TextDim), UiKit.RadiusMd, 3).raycastTarget = false;
            var text = UiKit.Label("Text", field.transform, "", 48, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleLeft, FontStyle.Bold);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Pad(text.rectTransform, 32, 4, 32, 4);
            var placeholder = UiKit.Label("Placeholder", field.transform, "이름", 44, Palette.TextDim, 0, 0, 1, 1, TextAnchor.MiddleLeft);
            UiKit.Pad(placeholder.rectTransform, 32, 4, 32, 4);
            view.Input = field.gameObject.AddComponent<InputField>();
            view.Input.textComponent = text;
            view.Input.placeholder = placeholder;
            view.Input.lineType = InputField.LineType.SingleLine;
            view.Input.characterLimit = HeroNames.MaxLength + 4; // 입력하는 동안의 자모·빈칸 여유 (확인할 때 HeroNames.Clean)
            view.Input.onValueChanged.AddListener(_ => view.Refresh());

            UiKit.Label("Hint", box.transform, $"한글·영어·숫자 {HeroNames.MaxLength}글자까지", 28, Palette.TextDim, 0, 0.29f, 1, 0.38f);
            view.OkButton = UiKit.MakeButton("NameOkButton", box.transform, "확인", Palette.Evolve, 52, 0.087f, 0.06f, 0.913f, 0.25f);
            view.OkButton.onClick.AddListener(view.Confirm);
            view.Root.SetActive(false);
            return view;
        }

        public void Show(string prompt, string initial, Action<string> onEntered)
        {
            title.text = prompt;
            Input.text = initial ?? "";
            entered = onEntered;
            Root.SetActive(true);
            Refresh();
            Input.ActivateInputField();
        }

        public void Confirm()
        {
            if (!IsOpen || !HeroNames.IsValid(Input.text)) return;
            Root.SetActive(false);
            entered?.Invoke(HeroNames.Clean(Input.text));
        }

        private void Refresh()
        {
            bool ok = HeroNames.IsValid(Input.text);
            OkButton.interactable = ok;
            UiKit.SetColor(OkButton, ok ? Palette.Evolve : Palette.Disabled);
        }
    }
}
