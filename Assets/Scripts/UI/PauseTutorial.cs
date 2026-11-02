using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WordRPG.UI
{
    // 화면을 잠깐 멈추고 설명하는 튜토리얼 (#79, 사용자 요청 — 정글에서 처음 [선제공격] 기회가 올 때, Figma '튜토리얼 — 선제공격 1~3/3 (#79)').
    // 어두운 막 + 'Guide Box'(안내 · n / 전체 · 설명 · [다음]/[확인]). 버튼이나 글을 눌러야 넘어간다 (#145 사용자 — 읽는 동안 시간이 흐르지 않게).
    // 막은 아래 화면을 누르지 못하게 하고, 강조할 것(예: [선제공격] 버튼)은 RaiseBelow로 막보다 위에 두어 밝게 보여 준다 (누를 수 있는지는 부르는 쪽이 정함).
    // (#61 '막지 않는 한 줄 안내'와 달리 이건 새 조우 방식을 처음 만날 때만 — 사용자가 멈춤을 원함)
    public class PauseTutorial
    {
        private GameObject root;
        private Text counter, message;
        private Button next;
        private bool nextPressed;

        public bool IsShowing => root != null && root.activeSelf;
        public int Step { get; private set; } = -1;
        public string Message => IsShowing ? message.text : "";

        public static PauseTutorial Create(RectTransform parent)
        {
            var view = new PauseTutorial();
            var rect = UiKit.Stretch("PauseTutorial", parent);
            view.root = rect.gameObject;
            UiKit.Panel("Dim", rect, new Color(0f, 0f, 0f, 0.8f)).raycastTarget = true; // 화면 멈춤 — 아래를 못 누름 (Linear라 Figma 62%보다 높게)

            // Guide Box (920 폭, 높이 306) — 강조할 버튼 바로 위에 놓는다. 글 어디를 눌러도 [다음]과 같음
            var box = UiKit.RoundPanel("GuideBox", rect, Palette.PanelLight, UiKit.RadiusLg, 0.074f, 0.36f, 0.926f, 0.52f);
            UiKit.AddButton(box).onClick.AddListener(() => view.nextPressed = true);
            var edge = UiKit.Outline(UiKit.Panel("Edge", box.transform, Palette.Gold), UiKit.RadiusLg, 4);
            edge.raycastTarget = false;
            var tag = UiKit.Pill(UiKit.Panel("Tag", box.transform, Palette.Gold, 0.04f, 0.78f, 0.15f, 0.92f));
            tag.raycastTarget = false;
            UiKit.OneLine(UiKit.Label("Text", tag.transform, "안내", 24, Palette.OnAccent, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold));
            view.counter = UiKit.OneLine(UiKit.Label("Counter", box.transform, "", 24, Palette.TextDim, 0.7f, 0.78f, 0.96f, 0.92f, TextAnchor.MiddleRight));
            view.message = UiKit.Label("Message", box.transform, "", 34, Palette.Text, 0.04f, 0.36f, 0.96f, 0.76f,
                TextAnchor.UpperLeft, FontStyle.Bold, true, 24);
            view.next = UiKit.MakeButton("TutorialNextButton", box.transform, "다음", Palette.Gold, 40, 0.68f, 0.07f, 0.96f, 0.32f);
            UiKit.LabelOf(view.next).color = Palette.OnAccent;
            view.next.onClick.AddListener(() => view.nextPressed = true);
            view.root.SetActive(false);
            return view;
        }

        // 막을 target 바로 아래에 둔다 → target(과 그 뒤에 그려지는 것)은 막보다 위에서 밝게 보이고 눌린다
        public void RaiseBelow(Transform target)
        {
            if (target == null || target.parent != root.transform.parent) return;
            root.transform.SetSiblingIndex(target.GetSiblingIndex());
        }

        // 열려 있는 창(가방 등) 위에서 설명할 때 (#147)
        public void BringToFront() => root.transform.SetAsLastSibling();

        // 읽기만 하는 안내 (#124 처음 시작의 환영·첫 승리 뒤 목표)
        public IEnumerator Tell(IReadOnlyList<string> lines) => Run(lines, null, null, null);

        public void PressNext() => nextPressed = true; // 테스트·키보드용

        // lines: 단계별 설명 — 단계마다 [다음], 마지막은 [확인]. cancelWhen: 상황이 사라지면(전투 시작 등) 닫음.
        // onStep: 단계가 바뀔 때 (화면에 범위 표시 같은 것). done: 끝까지 읽었는지
        public IEnumerator Run(IReadOnlyList<string> lines, Func<bool> cancelWhen, Action<int> onStep, Action<bool> done)
        {
            root.SetActive(true);
            var label = UiKit.LabelOf(next);
            bool cancelled = false;
            for (int i = 0; i < lines.Count && !cancelled; i++)
            {
                Step = i;
                counter.text = lines.Count > 1 ? $"{i + 1} / {lines.Count}" : "";
                message.text = lines[i];
                label.text = i == lines.Count - 1 ? "확인" : "다음";
                onStep?.Invoke(i);
                nextPressed = false;
                while (!nextPressed && !cancelled)
                {
                    cancelled = cancelWhen != null && cancelWhen();
                    if (!cancelled) yield return null;
                }
            }
            label.text = "다음";
            Step = -1;
            root.SetActive(false);
            onStep?.Invoke(-1);
            done?.Invoke(!cancelled);
        }
    }
}
