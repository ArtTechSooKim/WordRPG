using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 확인문제 화면 모양 (#280·#281, Figma '힐끔해골 확인문제 (#281)'): 꼬리표 '확인문제 · 제n장 …' · 보기는 한 줄씩 위에서부터 (긴 영어 답 · 5지선다 ① … ⑤)
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
                var rt = (RectTransform)buttons[i].transform;
                rt.anchorMin = new Vector2(minX, top - step + gap);
                rt.anchorMax = new Vector2(maxX, top);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
        }
    }
}
