using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WordRPG.UI
{
    // 가로로 늘어선 알약 칩들을 한 줄에 (#287 사용자: 단어장 고르기의 비즈니스 예시 단어가 두 줄로 꺾임 — 통일감 있게 한 줄로).
    // 칩 글은 줄을 바꾸지 않고, 줄마다 칩이 다 들어가는 가장 큰 글자 크기를 찾아 모든 줄에 같은 크기를 씀 (카드마다 크기가 다르면 통일감이 깨짐).
    // 화면 너비(캔버스 크기)가 바뀌면 다시 맞춤
    public class ChipRowFit : MonoBehaviour
    {
        private class Row
        {
            public RectTransform Rect;
            public float Spacing;
            public readonly List<(Text Label, float Padding)> Chips = new List<(Text, float)>();
        }

        private readonly List<Row> rows = new List<Row>();
        private int maxSize, minSize;
        private float fittedWidth = -1f, fittedScale = -1f;

        public int FontSize { get; private set; }

        public static ChipRowFit On(GameObject host, int maxSize, int minSize)
        {
            if (!host.TryGetComponent(out ChipRowFit fit)) fit = host.AddComponent<ChipRowFit>();
            fit.maxSize = maxSize;
            fit.minSize = Mathf.Min(minSize, maxSize);
            fit.FontSize = maxSize;
            return fit;
        }

        public void Clear()
        {
            rows.Clear();
            fittedWidth = -1f;
        }

        public void AddRow(RectTransform row, float spacing) => rows.Add(new Row { Rect = row, Spacing = spacing });

        // 마지막 줄에 칩 하나 (padding = 칩 안 글자 양옆 여백 합)
        public void AddChip(Text label, float padding)
        {
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
            rows[rows.Count - 1].Chips.Add((label, padding));
        }

        private void LateUpdate() => Refit(false);

        public void Refit(bool force)
        {
            if (rows.Count == 0 || rows[0].Chips.Count == 0) return;
            float width = rows[0].Rect.rect.width;
            float scale = rows[0].Chips[0].Label.pixelsPerUnit;
            if (width <= 1f || (!force && width == fittedWidth && scale == fittedScale)) return;
            fittedWidth = width;
            fittedScale = scale;
            int size = maxSize;
            while (size > minSize && !AllFit(size)) size--;
            FontSize = size;
            foreach (var row in rows)
                foreach (var chip in row.Chips)
                    chip.Label.fontSize = size;
        }

        private bool AllFit(int size)
        {
            foreach (var row in rows)
            {
                float need = row.Spacing * Mathf.Max(0, row.Chips.Count - 1);
                foreach (var chip in row.Chips) need += OneLineFit.OneLineWidth(chip.Label, chip.Label.text, size) + chip.Padding;
                if (need > row.Rect.rect.width - 1f) return false;
            }
            return true;
        }

        // 줄마다 칩이 줄 안에 다 들어가 있는지 (테스트용)
        public bool EveryRowFits() => AllFit(FontSize);
    }
}
