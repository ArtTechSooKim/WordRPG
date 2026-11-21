using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 확인문제 화면 모양 (#280, Figma '보스전 확인문제 (#280)'·'5지선다'·'석판 확인문제'·'반딧돌 확인문제'):
    // 긴 한국어 문제 = 본문 굵게(길면 줄어듦), 보기 = 한 줄씩 위에서부터 (긴 영어 답 · 5지선다 ① … ⑤).
    // 단어 문제로 돌아가면 원래 모양(LabelLook·저장해 둔 자리)으로
    public static class ExamLayout
    {
        public const int MaxChoices = 5;

        public static string Tag(ExamQuestion exam) => exam != null && exam.ChapterTitle.Length > 0 ? $"확인문제 · {exam.ChapterTitle}" : "확인문제";

        // 보기 버튼을 minY~maxY 사이에 위에서부터 count줄로 (나머지는 그대로 — 부르는 쪽이 끔)
        public static void Column(IList<Button> buttons, int count, float minX, float maxX, float minY, float maxY, float gap)
        {
            if (count <= 0) return;
            float step = (maxY - minY + gap) / count;
            for (int i = 0; i < buttons.Count && i < count; i++)
            {
                float top = maxY - i * step;
                Place(buttons[i], minX, top - step + gap, maxX, top);
            }
        }

        public static void Place(Button button, float minX, float minY, float maxX, float maxY)
        {
            var rt = (RectTransform)button.transform;
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // 버튼 자리 저장·되돌리기 (단어 문제의 2×2)
        public static List<(Vector2 min, Vector2 max)> Anchors(IList<Button> buttons)
        {
            var list = new List<(Vector2, Vector2)>();
            foreach (var button in buttons)
            {
                var rt = (RectTransform)button.transform;
                list.Add((rt.anchorMin, rt.anchorMax));
            }
            return list;
        }

        public static void Restore(IList<Button> buttons, List<(Vector2 min, Vector2 max)> anchors)
        {
            for (int i = 0; i < buttons.Count && i < anchors.Count; i++)
                Place(buttons[i], anchors[i].min.x, anchors[i].min.y, anchors[i].max.x, anchors[i].max.y);
        }

        // 글 모양 한 벌 (글꼴·크기·줄어드는 범위·자리·색)
        public struct LabelLook
        {
            public Font Font;
            public int Size, MinSize, MaxSize;
            public bool BestFit;
            public Vector2 Min, Max;
            public Color Color;

            public static LabelLook Of(Text label)
            {
                var rt = label.rectTransform;
                return new LabelLook
                {
                    Font = label.font, Size = label.fontSize, MinSize = label.resizeTextMinSize, MaxSize = label.resizeTextMaxSize,
                    BestFit = label.resizeTextForBestFit, Min = rt.anchorMin, Max = rt.anchorMax, Color = label.color,
                };
            }

            public void ApplyTo(Text label)
            {
                label.font = Font;
                label.fontSize = Size;
                label.resizeTextForBestFit = BestFit;
                label.resizeTextMinSize = MinSize;
                label.resizeTextMaxSize = MaxSize;
                label.color = Color;
                var rt = label.rectTransform;
                rt.anchorMin = Min;
                rt.anchorMax = Max;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
        }

        // 문제 글: 본문 굵게, size에서 시작해 minSize까지 줄어듦, 여러 줄
        public static LabelLook Question(Text label, int size, int minSize, Vector2 min, Vector2 max, Color color)
        {
            var look = LabelLook.Of(label);
            look.Font = UiFonts.Bold;
            look.Size = size;
            look.MaxSize = size;
            look.MinSize = minSize;
            look.BestFit = true;
            look.Min = min;
            look.Max = max;
            look.Color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return look;
        }
    }
}
