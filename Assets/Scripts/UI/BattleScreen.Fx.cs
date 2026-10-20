using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 전투 > 연출: 콤보 글자, 떠오르는 숫자, 배경, 효과, 보스 결정타(번쩍·어둡게·도장)
    public partial class BattleScreen
    {
        // 글자가 크게 찍히듯 줄어들며 나타났다가 잠깐 머문 뒤 위로 떠오르며 사라진다. 단계가 오를수록 크고 효과음이 높다
        private IEnumerator ComboRoutine(int streak)
        {
            Cue($"콤보 글자 {streak}");
            int step = Combo.Step(streak);
            if (step <= 0) yield break;
            var color = ComboColors[step - 1];
            LastComboText = Combo.Label(streak);

            var group = UiKit.Rect("Combo", root, 0.5f, 0.25f, 0.5f, 0.25f); // 주인공 카드 아래 (연출 중엔 기술 패널이 숨어 비어 있음)
            group.sizeDelta = new Vector2(1000, 300);
            group.localEulerAngles = new Vector3(0, 0, 6f);
            var glow = UiKit.IconImage("Glow", group, UiKit.GlowSprite(), 0.5f, 0.5f, 0.5f, 0.5f);
            glow.preserveAspect = false;
            glow.rectTransform.sizeDelta = new Vector2(900, 330);
            var label = UiKit.Display(UiKit.OneLine(UiKit.Label("Label", group, Combo.Label(streak), 84 + (step - 1) * 7, color,
                0, 0.35f, 1, 1)));
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(0, -8);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(26, 20, 48, 255);
            outline.effectDistance = new Vector2(5, -5);
            int bonus = Mathf.RoundToInt(Combo.DamageBonus(streak, battleConfig.ComboBonusPerStep) * 100f);
            var detail = UiKit.OneLine(UiKit.Label("Detail", group, $"{streak} 연속 정답 · 피해 +{bonus}%", 32, Color.white,
                0, 0, 1, 0.35f, TextAnchor.MiddleCenter, FontStyle.Bold));
            var detailOutline = detail.gameObject.AddComponent<Outline>();
            detailOutline.effectColor = new Color32(26, 20, 48, 255);
            detailOutline.effectDistance = new Vector2(3, -3);
            var group2 = group.gameObject.AddComponent<CanvasGroup>();
            group2.blocksRaycasts = false;
            Sound.Play(Sfx.Combo, 1f + 0.08f * (step - 1));

            float pop = 0.18f * animationScale, hold = 0.6f * animationScale, fade = 0.35f * animationScale;
            for (float t = 0; t < pop; t += Time.unscaledDeltaTime)
            {
                float k = t / pop;
                float overshoot = 1f + 0.18f * Mathf.Sin(k * Mathf.PI);
                group.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, k) * overshoot;
                group2.alpha = k;
                glow.color = new Color(color.r, color.g, color.b, 0.35f * k);
                yield return null;
            }
            group.localScale = Vector3.one;
            group2.alpha = 1f;
            for (float t = 0; t < hold; t += Time.unscaledDeltaTime)
            {
                glow.color = new Color(color.r, color.g, color.b, 0.3f + 0.08f * Mathf.Sin(t * 12f));
                yield return null;
            }
            var start = group.anchoredPosition;
            for (float t = 0; t < fade; t += Time.unscaledDeltaTime)
            {
                float k = t / fade;
                group.anchoredPosition = start + Vector2.up * (70f * k);
                group2.alpha = 1f - k;
                yield return null;
            }
            Destroy(group.gameObject);
        }

        private void Float(BattleUnit unit, string text, Color color, int size)
        {
            Cue($"숫자 {unit?.DisplayName} {text} #{ColorUtility.ToHtmlStringRGB(color)} {size}");
            if (!viewOf.TryGetValue(unit, out var view)) return;
            StartCoroutine(FloatRoutine(view.Root, text, color, size));
        }

        private IEnumerator FloatRoutine(RectTransform target, string text, Color color, int size)
        {
            // 큰 숫자(보스 결정타)는 칸보다 커도 잘리지 않게 한 줄로 넘쳐 그린다
            var label = UiKit.Display(UiKit.OneLine(UiKit.Label("Float", root, text, size + 8, color, 0.5f, 0.5f, 0.5f, 0.5f,
                TextAnchor.MiddleCenter)));
            var rt = label.rectTransform;
            rt.sizeDelta = new Vector2(560, 100);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.85f);
            outline.effectDistance = new Vector2(3, -3);

            Vector3 start = target.TransformPoint(target.rect.center);
            float duration = 0.9f * animationScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                rt.position = start + Vector3.up * (140f * k * canvas.scaleFactor);
                var c = label.color;
                c.a = 1f - k * k;
                label.color = c;
                outline.effectColor = new Color(0, 0, 0, 0.85f * c.a);
                yield return null;
            }
            Destroy(label.gameObject);
        }

        // ------------------------------------------------------------------ 전투 배경 (지역 타일)

        // 적이 서는 위쪽에 지역 바닥(초원 = 풀숲, 서고 = 돌바닥)을 깔고 맨 위에 벽 한 줄(덤불·책장), 아래로 갈수록 어둡게
        private void BuildBackdrop(Transform parent)
        {
            var area = UiKit.Rect("Backdrop", parent, 0, 0.6f, 1, 1);
            backdropFloor = UiKit.Panel("Floor", area, Color.white);
            backdropWall = UiKit.Panel("Wall", area, Color.white, 0, 0.86f, 1, 1);
            foreach (var image in new[] { backdropFloor, backdropWall })
            {
                image.type = Image.Type.Tiled;
                image.pixelsPerUnitMultiplier = 100f / 120f; // 한 칸 = 120px
                image.raycastTarget = false;
            }
            backdropTint = UiKit.Panel("Tint", area, new Color(0, 0, 0, 0.72f)); // Linear 색공간이라 알파를 높게 잡아야 눈에 보이는 만큼 어두워진다
            backdropTint.raycastTarget = false;
            var topScrim = UiKit.Panel("TopScrim", area, new Color(0, 0, 0, 0.75f), 0, 0.86f, 1, 1); // 위쪽 글자·도감 버튼이 잘 보이게
            topScrim.raycastTarget = false;
            var fade = UiKit.Panel("Fade", area, Palette.Background, 0, 0, 1, 0.55f);
            fade.sprite = UiKit.FadeSprite();
            fade.raycastTarget = false;
            SetBackdrop(FieldTheme.Meadow, false);
        }

        // 보스전은 보랏빛으로 더 어둡게
        public void SetBackdrop(FieldTheme theme, bool boss)
        {
            if (backdropFloor == null) return;
            backdropFloor.sprite = FieldArt.ForTile(ThemeLooks.Of(theme).BattleFloor, theme, false); // 테마 표 (#118) — 서고는 돌바닥, 숲은 고사리
            backdropWall.sprite = FieldArt.ForTile(FieldTile.Wall, theme, false);
            backdropTint.color = boss ? new Color(0.2f, 0.04f, 0.3f, 0.8f) : new Color(0, 0, 0, 0.72f);
        }

        // 카드 위에 효과 애니메이션 (기다리지 않고 바로 다음 연출로)
        // slow: 효과를 몇 배 느리게 (보스 결정타), scale: 크기 배율
        private void PlayFx(BattleUnit unit, Fx fx, float slow = 1f, float scale = 1f)
        {
            Cue($"효과 {fx} {unit?.DisplayName}" + (slow != 1f || scale != 1f ? $" 느리게{CueNumber(slow)} 크기{CueNumber(scale)}" : ""));
            if (unit == null || !viewOf.TryGetValue(unit, out var view)) return;
            float size = (unit.IsPlayerSide ? 200f : 300f) * scale;
            StartCoroutine(BattleFx.Play(view.Root, fx, size, animationScale * slow));
        }

        private static bool Has(IReadOnlyList<BattleEvent> events, BattleEventType type, BattleUnit target = null)
        {
            foreach (var e in events)
                if (e.Type == type && (target == null || e.Target == target)) return true;
            return false;
        }

        // 보스전에서 아군이 마지막 적(보스)을 쓰러뜨리는 한 방 (여러 번 때리는 기술이면 마지막 타격만)
        private bool IsBossFinisher(IReadOnlyList<BattleEvent> events, BattleEvent e)
        {
            if (!bossBattle || e.Actor == null || !e.Actor.IsPlayerSide || e.Target == null || e.Target.IsPlayerSide || e.Amount <= 0) return false;
            if (!Has(events, BattleEventType.Defeated, e.Target) || !Has(events, BattleEventType.Victory)) return false;
            BattleEvent last = null;
            foreach (var other in events)
                if (other.Type == BattleEventType.Damage && other.Target == e.Target && other.Amount > 0) last = other;
            return ReferenceEquals(last, e);
        }

        // 하얀 번쩍임 → 느린 화면(가장자리 어둡게, 보스 카드가 커지며 흔들림, 느린 큰 타격 효과, 낮은 소리) → '결정타!' 도장
        private IEnumerator BossFinisher(BattleEvent e)
        {
            Cue($"결정타 {e.Target.DisplayName}");
            var d = shown[e.Target];
            shown[e.Target] = (Mathf.Max(0, d.hp - e.Amount), Mathf.Max(0, d.shield - e.Absorbed));
            FinishersPlayed++;
            Log($"결정타! {e.Target.DisplayName} 피해 {e.Amount}");
            // 보스(적 칸)만 밝게 남기고 나머지를 어둡게: 적 칸을 어두운 막 바로 위로
            if (enemyAreaIndex < 0)
            {
                enemyAreaIndex = enemyArea.GetSiblingIndex();
                enemyArea.SetSiblingIndex(finisherShade.transform.GetSiblingIndex());
            }
            Sound.Play(Sfx.Critical, 0.55f);
            Sound.Play(Sfx.Hit, 0.6f);
            if (settings != null && settings.Vibration) Haptics.Vibrate();
            PlayFx(e.Target, Fx.Explosion, 3f, 1.8f);
            Float(e.Target, $"-{e.Amount}", Palette.Bad, 96);
            StartCoroutine(Flash());
            StartCoroutine(FadeShade(0.75f, 0.25f)); // Linear 색공간이라 알파를 높게
            StartCoroutine(StampRoutine());

            var card = viewOf.TryGetValue(e.Target, out var view) ? view.Root : null;
            var home = card != null ? card.anchoredPosition : Vector2.zero;
            float duration = 1.5f * animationScale, t = 0f;
            bool synced = false;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
                if (card != null)
                {
                    card.localScale = Vector3.one * (1f + 0.22f * Mathf.Sin(Mathf.Min(1f, k * 2f) * Mathf.PI * 0.5f));
                    float shake = 14f * (1f - k);
                    card.anchoredPosition = home + new Vector2(Mathf.Sin(t * 70f) * shake, Mathf.Cos(t * 53f) * shake * 0.6f);
                }
                if (!synced && k > 0.35f)
                {
                    Sync(e.Target); // HP 바는 느린 화면 중간에 0으로
                    synced = true;
                }
                yield return null;
            }
            if (!synced) Sync(e.Target);
            if (card != null)
            {
                card.localScale = Vector3.one;
                card.anchoredPosition = home;
            }
        }

        private IEnumerator Flash()
        {
            finisherFlash.gameObject.SetActive(true);
            float duration = 0.35f * animationScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                finisherFlash.color = new Color(1f, 1f, 1f, 0.85f * (1f - t / Mathf.Max(0.0001f, duration)));
                yield return null;
            }
            finisherFlash.gameObject.SetActive(false);
        }

        // 화면을 어둡게(느린 화면 동안) / 다시 밝게
        private IEnumerator FadeShade(float to, float seconds)
        {
            Cue($"어둡게 {CueNumber(to)} {CueNumber(seconds)}");
            finisherShade.gameObject.SetActive(true);
            float from = finisherShade.color.a, duration = seconds * animationScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                finisherShade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / Mathf.Max(0.0001f, duration)));
                yield return null;
            }
            finisherShade.color = new Color(0f, 0f, 0f, to);
            if (to <= 0.001f) ResetFinisher();
        }

        // 결정타 연출 층을 치우고 적 칸을 원래 자리로
        private void ResetFinisher()
        {
            if (finisherShade == null) return;
            finisherShade.color = new Color(0f, 0f, 0f, 0f);
            finisherShade.gameObject.SetActive(false);
            finisherFlash.gameObject.SetActive(false);
            finisherStamp.gameObject.SetActive(false);
            if (enemyAreaIndex >= 0) enemyArea.SetSiblingIndex(enemyAreaIndex);
            enemyAreaIndex = -1;
        }

        // '결정타!'가 크게 찍히듯 줄어들었다가 잠시 머문 뒤 사라진다
        private IEnumerator StampRoutine()
        {
            var rt = finisherStamp.rectTransform;
            finisherStamp.gameObject.SetActive(true);
            float pop = 0.25f * animationScale, hold = 1.1f * animationScale, fade = 0.3f * animationScale;
            for (float t = 0f; t < pop + hold + fade; t += Time.unscaledDeltaTime)
            {
                float scale = t < pop ? Mathf.Lerp(2.4f, 1f, t / Mathf.Max(0.0001f, pop)) : 1f;
                float alpha = t < pop + hold ? Mathf.Clamp01(t / Mathf.Max(0.0001f, pop)) : 1f - (t - pop - hold) / Mathf.Max(0.0001f, fade);
                rt.localScale = Vector3.one * scale;
                finisherStamp.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, alpha);
                yield return null;
            }
            finisherStamp.gameObject.SetActive(false);
        }
    }
}
