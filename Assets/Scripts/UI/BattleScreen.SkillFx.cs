using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Battle;
using WordRPG.Monsters;

namespace WordRPG.UI
{
    // 전투 > 기술 연출 (#137 사용자: "기술마다 다른 효과 · 회복과 보호막이 더 역동적으로" — Figma '기술 연출 (#137)').
    // 어떤 그림을 쓸지는 표 SkillLooks, 여기서는 그리기만. 모두 StartCoroutine으로 겹쳐 돌려서 사건마다 기다리는 시간은 그대로 (전투 속도 같음).
    // 기록(Cue)은 사건 순서를 못 박는 PlayMode BattleEventPlaybackTests용 — 몸짓·날아감·회복 빛·보호막 막·물결·깨짐
    public partial class BattleScreen
    {
        private IReadOnlyList<BattleEvent> playingEvents; // 지금 연출 중인 사건 목록 (기술의 대상 찾기)
        private BattleEvent highlightedUse;               // 형광펜은 기술 한 번에 한 줄
        private string lastWord;                          // 방금 맞힌 단어 (글자 소나기)
        private readonly Dictionary<RectTransform, Coroutine> cardMotion = new Dictionary<RectTransform, Coroutine>();
        private readonly Dictionary<RectTransform, Vector2> cardHome = new Dictionary<RectTransform, Vector2>();

        private static readonly Color LetterGold = new Color(1f, 0.85f, 0.4f);
        private static readonly Color LetterTeal = new Color(0.55f, 0.95f, 0.9f);

        // ------------------------------------------------------------------ 기술을 쓰는 순간

        // 몸짓(달려가 덮침 · 마법진) + 날아가는 그림 — 맞는 효과는 이어지는 피해·회복·보호막 사건이 그림
        private void PlaySkillStart(BattleEvent e)
        {
            if (e.Actor == null || !viewOf.TryGetValue(e.Actor, out var actorView)) return;
            var look = SkillLooks.Of(e.Skill, e.Actor.IsPlayerSide);
            var targets = TargetsOf(e);
            BattleUnit foe = targets.Find(t => t.IsPlayerSide != e.Actor.IsPlayerSide);
            if (look.Motion == SkillMotion.Lunge && foe != null && viewOf.TryGetValue(foe, out var foeView))
            {
                Cue($"몸짓 Lunge {e.Actor.DisplayName}");
                MoveCard(actorView.Root, Lunge(actorView.Root, CenterOf(foeView)));
            }
            else if (look.Motion != SkillMotion.None)
            {
                Cue($"몸짓 Cast {e.Actor.DisplayName}");
                PlayFx(e.Actor, Fx.Circle, 1f, 1.3f, look.Glow, new Vector2(0f, -CardHeight(actorView) * 0.5f), 0.38f);
                MoveCard(actorView.Root, Swell(actorView.Root));
            }
            if (look.Projectile is Fx projectile)
            {
                foreach (var target in targets)
                {
                    if (target == e.Actor || !viewOf.TryGetValue(target, out var targetView)) continue;
                    Cue($"날아감 {projectile} {e.Actor.DisplayName}→{target.DisplayName}");
                    StartCoroutine(Fly(CenterOf(actorView), CenterOf(targetView), projectile));
                }
            }
        }

        // 이 기술의 대상 = 뒤따르는 피해·회복·보호막 사건 (다음 기술·문제·라운드 전까지)
        private List<BattleUnit> TargetsOf(BattleEvent used)
        {
            var list = new List<BattleUnit>();
            if (playingEvents == null) return list;
            int start = -1;
            for (int i = 0; i < playingEvents.Count; i++)
                if (ReferenceEquals(playingEvents[i], used)) start = i;
            if (start < 0) return list;
            for (int i = start + 1; i < playingEvents.Count; i++)
            {
                var e = playingEvents[i];
                if (e.Type == BattleEventType.SkillUsed || e.Type == BattleEventType.SkillFailed || e.Type == BattleEventType.QuizAnswered
                    || e.Type == BattleEventType.RoundStarted || e.Type == BattleEventType.Charging) break;
                bool hit = e.Type == BattleEventType.Damage || e.Type == BattleEventType.Heal || e.Type == BattleEventType.Shield;
                if (hit && e.Actor == used.Actor && e.Target != null && !list.Contains(e.Target)) list.Add(e.Target);
            }
            return list;
        }

        // ------------------------------------------------------------------ 맞는 순간 (피해)

        // 기술의 효과 그림 + 크리티컬 폭발 + 번쩍 + 흔들림 + 특수 연출, 보호막이 받아 냈으면 물결 / 다 깎였으면 깨짐
        private void PlayHit(BattleEvent e, bool shieldBroke)
        {
            var look = SkillLooks.Of(e.Skill, e.Actor == null || e.Actor.IsPlayerSide);
            if (e.Amount <= 0)
            {
                PlayFx(e.Target, Fx.Shield);
            }
            else
            {
                PlayFx(e.Target, look.Hit, 1f, look.HitScale, look.Tint);
                if (e.IsCritical) PlayFx(e.Target, Fx.Explosion);
                if (look.Flash.a > 0f) StartCoroutine(ScreenFlash(look.Flash));
                if (look.Shake > 0f && viewOf.TryGetValue(e.Target, out var view))
                    MoveCard(view.Root, ShakeRoutine(view.Root, look.Shake * (e.IsCritical ? 1.5f : 1f), 0.3f));
                if (look.Extra == SkillExtra.Letters)
                {
                    Cue($"글자 소나기 {e.Target.DisplayName}");
                    StartCoroutine(LetterRain(e.Target));
                }
                if (look.Extra == SkillExtra.Rings)
                {
                    Cue($"소리 고리 {e.Target.DisplayName}");
                    StartCoroutine(Rings(e.Target, look.Tint));
                }
                if (look.Extra == SkillExtra.Highlight && !ReferenceEquals(highlightedUse, CurrentUse(e)))
                {
                    Cue("형광펜");
                    highlightedUse = CurrentUse(e);
                    StartCoroutine(HighlightSweep(look.Tint));
                }
            }
            if (e.Absorbed <= 0) return;
            if (shieldBroke)
            {
                Cue($"보호막 깨짐 {e.Target.DisplayName}");
                StartCoroutine(Shatter(e.Target));
            }
            else
            {
                Cue($"보호막 물결 {e.Target.DisplayName}");
                StartCoroutine(Ripple(e.Target));
            }
        }

        // 이 피해를 낸 기술 사건 (형광펜을 기술 한 번에 한 줄만 긋기 위해)
        private BattleEvent CurrentUse(BattleEvent hit)
        {
            BattleEvent use = null;
            if (playingEvents == null) return null;
            foreach (var e in playingEvents)
            {
                if (ReferenceEquals(e, hit)) break;
                if (e.Type == BattleEventType.SkillUsed) use = e;
            }
            return use;
        }

        // ------------------------------------------------------------------ 회복

        // 발밑 마법진 + 빛기둥 → 잎·빛 알갱이가 솟고 카드가 빛남 → HP 막대가 차오름 (Figma '기술 연출 (#137)' 회복 ①~④)
        private void PlayRecovery(BattleUnit target, int hpBefore, SkillLook look)
        {
            if (!viewOf.TryGetValue(target, out var view)) return;
            Cue($"회복 빛 {target.DisplayName}");
            float h = CardHeight(view);
            PlayFx(target, Fx.Circle, 1f, 1.5f, look.Glow, new Vector2(0f, -h * 0.55f), 0.38f);
            PlayFx(target, look.Hit, 1f, 1f, look.Hit == Fx.Heal ? (Color?)null : look.Tint);
            StartCoroutine(LightColumn(view, look.Glow));
            StartCoroutine(CardGlow(view, look.Glow));
            StartCoroutine(RisingParticles(view, look));
            StartCoroutine(FillHp(view, hpBefore, shown[target].hp, shown[target].shield));
        }

        // 카드 아래에서 위로 솟는 빛기둥 (밝아졌다 사라짐)
        private IEnumerator LightColumn(UnitView view, Color color)
        {
            var image = UiKit.Panel("HealColumn", view.Root, Color.clear, 0.2f, 0.02f, 0.8f, 1.15f);
            image.sprite = UiKit.FadeSprite();
            image.raycastTarget = false;
            float duration = 0.6f * animationScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (image == null) yield break;
                float k = t / Mathf.Max(0.0001f, duration);
                float alpha = k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f;
                image.color = new Color(color.r, color.g, color.b, 0.55f * alpha);
                image.rectTransform.localScale = new Vector3(Mathf.Lerp(0.5f, 1f, Mathf.Min(1f, k * 3f)), 1f, 1f);
                yield return null;
            }
            if (image != null) Destroy(image.gameObject);
        }

        // 카드 뒤에서 빛이 두 번 번짐
        private IEnumerator CardGlow(UnitView view, Color color)
        {
            var glow = UiKit.IconImage("CardGlow", view.Root, UiKit.GlowSprite(), -0.25f, -0.25f, 1.25f, 1.25f);
            glow.preserveAspect = false;
            glow.transform.SetAsFirstSibling(); // 카드 뒤 → 둘레로 빛이 보임
            float duration = 0.75f * animationScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (glow == null) yield break;
                float k = t / Mathf.Max(0.0001f, duration);
                float alpha = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * (1f - k * 0.5f);
                glow.color = new Color(color.r, color.g, color.b, 0.9f * alpha);
                yield return null;
            }
            if (glow != null) Destroy(glow.gameObject);
        }

        // 잎·빛 알갱이(물은 물방울)가 카드 아래쪽에서 흔들리며 위로 솟았다 사라짐
        private IEnumerator RisingParticles(UnitView view, SkillLook look)
        {
            var frames = look.Particle is Fx particle ? BattleFx.Frames(particle) : null;
            var rect = view.Root.rect;
            for (int i = 0; i < 8; i++)
            {
                Sprite sprite = frames != null && frames.Length > 0 ? frames[Random.Range(0, frames.Length)] : UiKit.GlowSprite();
                var image = UiKit.IconImage("HealParticle", root, sprite, 0.5f, 0.5f, 0.5f, 0.5f);
                float size = frames != null ? Random.Range(44f, 64f) : Random.Range(26f, 40f);
                float aspect = sprite.rect.height / Mathf.Max(1f, sprite.rect.width);
                image.rectTransform.sizeDelta = new Vector2(size, size * aspect);
                image.color = look.Particle == Fx.Leaf ? Color.white : look.Glow;
                var start = view.Root.TransformPoint(new Vector3(Random.Range(rect.xMin + 40f, rect.xMax - 40f), rect.yMin + rect.height * Random.Range(0.1f, 0.4f), 0f));
                StartCoroutine(Rise(image, start, Random.Range(180f, 280f), i * 0.03f));
            }
            yield break;
        }

        private IEnumerator Rise(Image image, Vector3 start, float distance, float delay)
        {
            image.enabled = false;
            for (float d = 0f; d < delay * animationScale; d += Time.unscaledDeltaTime) yield return null;
            if (image == null) yield break;
            image.enabled = true;
            float duration = 0.7f * animationScale, sway = Random.Range(-30f, 30f), spin = Random.Range(-120f, 120f);
            var from = Local(start);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (image == null) yield break;
                float k = t / Mathf.Max(0.0001f, duration);
                image.rectTransform.localPosition = from + new Vector3(Mathf.Sin(k * Mathf.PI * 2f) * sway, distance * k, 0f);
                image.rectTransform.localEulerAngles = new Vector3(0f, 0f, spin * k);
                var c = image.color;
                image.color = new Color(c.r, c.g, c.b, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                yield return null;
            }
            if (image != null) Destroy(image.gameObject);
        }

        // HP 막대가 이전 값에서 미끄러지듯 차오르고 끝에 하얀 빛이 따라감 (값은 이미 맞춰 두었고 화면만 다시 그림)
        private IEnumerator FillHp(UnitView view, int from, int to, int shield)
        {
            var head = UiKit.IconImage("HpHead", root, UiKit.GlowSprite(), 0.5f, 0.5f, 0.5f, 0.5f);
            head.rectTransform.sizeDelta = new Vector2(56f, 56f);
            float duration = 0.45f * animationScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = 1f - (1f - t / Mathf.Max(0.0001f, duration)) * (1f - t / Mathf.Max(0.0001f, duration));
                view.Sync(Mathf.RoundToInt(Mathf.Lerp(from, to, k)), shield);
                if (head != null) head.rectTransform.position = view.HpEdgeWorld;
                yield return null;
            }
            view.Sync(to, shield);
            if (head != null) Destroy(head.gameObject);
        }

        // ------------------------------------------------------------------ 보호막

        // 막이 작게 → 크게(1.12배) 튕기며 덮고 효과 그림, 남아 있는 동안은 카드 둘레 테두리가 숨 쉬듯 (UnitView.PulseShield)
        private void PlayShield(BattleUnit target, SkillLook look)
        {
            if (!viewOf.TryGetValue(target, out var view)) return;
            Cue($"보호막 막 {target.DisplayName}");
            view.SetShieldColor(look.Glow);
            StartCoroutine(Dome(view, look.Glow));
            if (look.Extra == SkillExtra.Rings)
            {
                Cue($"소리 고리 {target.DisplayName}");
                StartCoroutine(Rings(target, look.Tint));
            }
        }

        private IEnumerator Dome(UnitView view, Color color)
        {
            float diameter = Mathf.Max(view.Root.rect.width, view.Root.rect.height) * 1.08f;
            var holder = UiKit.Rect("ShieldDome", view.Root, 0.5f, 0.5f, 0.5f, 0.5f);
            var fill = UiKit.IconImage("Fill", holder, UiKit.GlowSprite(), 0.5f, 0.5f, 0.5f, 0.5f);
            fill.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            var ring = UiRing.Create("Ring", holder, color, 8f, diameter);
            var shine = UiKit.IconImage("Shine", holder, UiKit.GlowSprite(), 0.5f, 0.5f, 0.5f, 0.5f);
            shine.rectTransform.sizeDelta = new Vector2(diameter * 0.32f, diameter * 0.12f);
            shine.rectTransform.anchoredPosition = new Vector2(-diameter * 0.2f, diameter * 0.3f);
            shine.rectTransform.localEulerAngles = new Vector3(0f, 0f, 28f);
            float pop = 0.18f * animationScale, settle = 0.12f * animationScale, hold = 0.25f * animationScale, fade = 0.3f * animationScale;
            float total = pop + settle + hold + fade;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                if (holder == null) yield break;
                float scale = t < pop ? Mathf.Lerp(0.45f, 1.12f, 1f - (1f - t / pop) * (1f - t / pop))
                    : t < pop + settle ? Mathf.Lerp(1.12f, 1f, (t - pop) / settle) : 1f;
                float alpha = t < pop + settle + hold ? 1f : 1f - (t - pop - settle - hold) / Mathf.Max(0.0001f, fade);
                holder.localScale = Vector3.one * scale;
                fill.color = new Color(color.r, color.g, color.b, 0.22f * alpha);
                ring.color = new Color(color.r, color.g, color.b, 0.95f * alpha);
                shine.color = new Color(1f, 1f, 1f, 0.6f * alpha);
                yield return null;
            }
            if (holder != null) Destroy(holder.gameObject);
        }

        // 보호막이 받아 냄: 막이 한 번 물결치며 퍼짐
        private IEnumerator Ripple(BattleUnit target)
        {
            if (!viewOf.TryGetValue(target, out var view)) yield break;
            float diameter = Mathf.Max(view.Root.rect.width, view.Root.rect.height);
            var ring = UiRing.Create("ShieldRipple", view.Root, Color.white, 7f, diameter);
            float duration = 0.35f * animationScale;
            var color = view.ShieldColor;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (ring == null) yield break;
                float k = t / Mathf.Max(0.0001f, duration);
                ring.rectTransform.sizeDelta = Vector2.one * diameter * Mathf.Lerp(0.95f, 1.2f, k);
                ring.color = new Color(color.r, color.g, color.b, 0.9f * (1f - k));
                yield return null;
            }
            if (ring != null) Destroy(ring.gameObject);
        }

        // 보호막이 다 깎임: 조각이 사방으로 흩어지고 하얀 고리
        private IEnumerator Shatter(BattleUnit target)
        {
            if (!viewOf.TryGetValue(target, out var view)) yield break;
            var color = view.ShieldColor;
            var center = CenterOf(view);
            var local = Local(center);
            var shards = new List<(Image image, Vector3 dir, float spin)>();
            for (int i = 0; i < 12; i++)
            {
                var shard = UiKit.Panel("Shard", root, color, 0.5f, 0.5f, 0.5f, 0.5f);
                shard.raycastTarget = false;
                shard.rectTransform.sizeDelta = new Vector2(Random.Range(14f, 22f), Random.Range(26f, 40f));
                float angle = i / 12f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                shards.Add((shard, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(200f, 300f), Random.Range(-360f, 360f)));
            }
            var ring = UiRing.Create("ShatterRing", root, Color.white, 6f, 200f);
            ring.rectTransform.position = center;
            float duration = 0.5f * animationScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / Mathf.Max(0.0001f, duration), ease = 1f - (1f - k) * (1f - k);
                foreach (var (image, dir, spin) in shards)
                {
                    if (image == null) continue;
                    image.rectTransform.localPosition = local + dir * ease;
                    image.rectTransform.localEulerAngles = new Vector3(0f, 0f, spin * k);
                    image.color = new Color(color.r, color.g, color.b, 1f - k);
                }
                if (ring != null)
                {
                    ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(200f, 520f, ease);
                    ring.color = new Color(1f, 1f, 1f, 0.8f * (1f - k));
                }
                yield return null;
            }
            foreach (var (image, _, _) in shards) if (image != null) Destroy(image.gameObject);
            if (ring != null) Destroy(ring.gameObject);
        }

        // ------------------------------------------------------------------ 특수 연출

        // 방금 맞힌 단어의 글자가 맞은 카드 위로 쏟아짐 (글자 소나기 — 단어가 없으면 알파벳 아무거나)
        private IEnumerator LetterRain(BattleUnit target)
        {
            if (!viewOf.TryGetValue(target, out var view)) yield break;
            string letters = string.IsNullOrEmpty(lastWord) ? "WORDRPG" : lastWord.ToUpperInvariant().Replace(" ", "");
            int count = Mathf.Min(8, Mathf.Max(5, letters.Length));
            var rect = view.Root.rect;
            for (int i = 0; i < count; i++)
            {
                string letter = letters[i % letters.Length].ToString();
                var label = UiKit.Display(UiKit.OneLine(UiKit.Label("Letter", root, letter, Random.Range(64, 92), i % 2 == 0 ? LetterGold : LetterTeal,
                    0.5f, 0.5f, 0.5f, 0.5f)));
                label.rectTransform.sizeDelta = new Vector2(120f, 120f);
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.08f, 0.06f, 0.16f, 0.9f);
                outline.effectDistance = new Vector2(3f, -3f);
                var land = view.Root.TransformPoint(new Vector3(Random.Range(rect.xMin + 30f, rect.xMax - 30f), Random.Range(rect.yMin + rect.height * 0.3f, rect.yMax - 40f), 0f));
                StartCoroutine(Fall(label, land, i * 0.045f));
            }
        }

        private IEnumerator Fall(Text label, Vector3 land, float delay)
        {
            label.enabled = false;
            for (float d = 0f; d < delay * animationScale; d += Time.unscaledDeltaTime) yield return null;
            if (label == null) yield break;
            label.enabled = true;
            float tilt = Random.Range(-25f, 25f);
            var to = Local(land);
            float drop = 0.3f * animationScale, stay = 0.25f * animationScale;
            for (float t = 0f; t < drop + stay; t += Time.unscaledDeltaTime)
            {
                if (label == null) yield break;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, drop));
                label.rectTransform.localPosition = to + new Vector3(0f, 260f * (1f - k * k), 0f);
                label.rectTransform.localEulerAngles = new Vector3(0f, 0f, tilt * (1f - k));
                var c = label.color;
                label.color = new Color(c.r, c.g, c.b, t < drop ? 1f : 1f - (t - drop) / Mathf.Max(0.0001f, stay));
                yield return null;
            }
            if (label != null) Destroy(label.gameObject);
        }

        // 형광 한 줄이 적 줄을 왼쪽에서 오른쪽으로 그어짐 (형광펜 긋기)
        private IEnumerator HighlightSweep(Color color)
        {
            float left = float.MaxValue, right = float.MinValue, y = 0f;
            int n = 0;
            foreach (var enemy in engine.Enemies)
            {
                if (!viewOf.TryGetValue(enemy, out var view)) continue;
                var corners = new Vector3[4];
                view.Root.GetWorldCorners(corners);
                Vector3 min = Local(corners[0]), max = Local(corners[2]);
                left = Mathf.Min(left, min.x);
                right = Mathf.Max(right, max.x);
                y += (min.y + max.y) * 0.5f;
                n++;
            }
            if (n == 0) yield break;
            var band = UiKit.Panel("Highlight", root, color, 0.5f, 0.5f, 0.5f, 0.5f);
            band.raycastTarget = false;
            var rt = band.rectTransform;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.localPosition = new Vector3(left - 30f, y / n + 20f, 0f);
            rt.localEulerAngles = new Vector3(0f, 0f, -6f);
            float width = right - left + 60f;
            float sweep = 0.22f * animationScale, hold = 0.2f * animationScale, fade = 0.3f * animationScale;
            for (float t = 0f; t < sweep + hold + fade; t += Time.unscaledDeltaTime)
            {
                if (band == null) yield break;
                rt.sizeDelta = new Vector2(width * Mathf.Clamp01(t / Mathf.Max(0.0001f, sweep)), 78f);
                float alpha = t < sweep + hold ? 0.7f : 0.7f * (1f - (t - sweep - hold) / Mathf.Max(0.0001f, fade));
                band.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }
            if (band != null) Destroy(band.gameObject);
        }

        // 소리 고리가 세 겹으로 퍼짐 (울음·메아리)
        private IEnumerator Rings(BattleUnit target, Color color)
        {
            if (!viewOf.TryGetValue(target, out var view)) yield break;
            float diameter = Mathf.Max(view.Root.rect.width, view.Root.rect.height) * 1.35f;
            for (int i = 0; i < 3; i++)
            {
                var ring = UiRing.Create("SoundRing", view.Root, color, 6f, 80f);
                StartCoroutine(Expand(ring, diameter, color, 0.45f));
                for (float d = 0f; d < 0.1f * animationScale; d += Time.unscaledDeltaTime) yield return null;
            }
        }

        private IEnumerator Expand(UiRing ring, float diameter, Color color, float seconds)
        {
            float duration = seconds * animationScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (ring == null) yield break;
                float k = t / Mathf.Max(0.0001f, duration);
                ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(80f, diameter, 1f - (1f - k) * (1f - k));
                ring.color = new Color(color.r, color.g, color.b, 0.9f * (1f - k));
                yield return null;
            }
            if (ring != null) Destroy(ring.gameObject);
        }

        // 화면 전체가 잠깐 번쩍 (번개·방망이·빛)
        private IEnumerator ScreenFlash(Color color)
        {
            Cue($"번쩍 #{ColorUtility.ToHtmlStringRGB(color)}");
            if (finisherFlash == null) yield break;
            finisherFlash.gameObject.SetActive(true);
            float duration = 0.22f * animationScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (finisherFlash == null) yield break;
                finisherFlash.color = new Color(color.r, color.g, color.b, color.a * (1f - t / Mathf.Max(0.0001f, duration)));
                yield return null;
            }
            // 보스 결정타 연출 중이면 그쪽이 치움
            if (finisherFlash != null && (finisherShade == null || !finisherShade.gameObject.activeSelf))
                finisherFlash.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ 카드 몸짓

        // 한 카드에 몸짓은 하나씩 — 새 몸짓이 오면 앞의 것을 멈추고 제자리에서 시작
        private void MoveCard(RectTransform card, IEnumerator motion)
        {
            if (cardMotion.TryGetValue(card, out var running) && running != null)
            {
                StopCoroutine(running);
                if (cardHome.TryGetValue(card, out var home)) card.anchoredPosition = home;
                card.localScale = Vector3.one;
            }
            cardHome[card] = card.anchoredPosition;
            cardMotion[card] = StartCoroutine(RunMotion(card, motion));
        }

        private IEnumerator RunMotion(RectTransform card, IEnumerator motion)
        {
            yield return motion;
            cardMotion.Remove(card);
        }

        // 맞힐 상대 쪽으로 달려갔다(조금 커짐) 잠깐 머문 뒤 돌아옴 — 머무는 동안 맞는 효과가 나옴
        private IEnumerator Lunge(RectTransform card, Vector3 toward)
        {
            var home = card.anchoredPosition;
            var parent = (RectTransform)card.parent;
            Vector2 delta = parent.InverseTransformVector(toward - card.TransformPoint(card.rect.center));
            Vector2 offset = delta.normalized * Mathf.Min(delta.magnitude * 0.45f, 280f);
            float go = 0.22f * animationScale, hold = 0.3f * animationScale, back = 0.2f * animationScale;
            for (float t = 0f; t < go + hold + back; t += Time.unscaledDeltaTime)
            {
                if (card == null) yield break;
                float k = t < go ? 1f - (1f - t / go) * (1f - t / go) : t < go + hold ? 1f : 1f - (t - go - hold) / Mathf.Max(0.0001f, back);
                card.anchoredPosition = home + offset * k;
                card.localScale = Vector3.one * (1f + 0.06f * k);
                yield return null;
            }
            if (card == null) yield break;
            card.anchoredPosition = home;
            card.localScale = Vector3.one;
        }

        // 제자리에서 살짝 부풀었다 돌아옴 (마법진을 그리는 몸짓)
        private IEnumerator Swell(RectTransform card)
        {
            float duration = 0.3f * animationScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (card == null) yield break;
                card.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(t / Mathf.Max(0.0001f, duration) * Mathf.PI));
                yield return null;
            }
            if (card != null) card.localScale = Vector3.one;
        }

        private IEnumerator ShakeRoutine(RectTransform card, float amplitude, float seconds)
        {
            var home = card.anchoredPosition;
            float duration = seconds * animationScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (card == null) yield break;
                float fall = 1f - t / Mathf.Max(0.0001f, duration);
                card.anchoredPosition = home + new Vector2(Mathf.Sin(t * 80f), Mathf.Cos(t * 63f) * 0.5f) * amplitude * fall;
                yield return null;
            }
            if (card != null) card.anchoredPosition = home;
        }

        // 쓰는 쪽에서 맞는 쪽으로 포물선을 그리며 날아감 (돌팔매) — 칸을 돌려 가며 빙글빙글
        private IEnumerator Fly(Vector3 from, Vector3 to, Fx fx)
        {
            var frames = BattleFx.Frames(fx);
            if (frames == null || frames.Length == 0) yield break;
            var image = UiKit.IconImage($"Fly_{fx}", root, frames[0], 0.5f, 0.5f, 0.5f, 0.5f);
            image.rectTransform.sizeDelta = new Vector2(96f, 96f);
            Vector3 a = Local(from), b = Local(to);
            var control = (a + b) * 0.5f + new Vector3(180f, 120f, 0f);
            float duration = 0.4f * animationScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (image == null) yield break;
                float k = t / Mathf.Max(0.0001f, duration);
                image.rectTransform.localPosition = (1 - k) * (1 - k) * a + 2 * (1 - k) * k * control + k * k * b;
                image.sprite = frames[Mathf.FloorToInt(t / Mathf.Max(0.0001f, 0.06f * animationScale)) % frames.Length];
                image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -540f * k);
                yield return null;
            }
            if (image != null) Destroy(image.gameObject);
        }

        // ------------------------------------------------------------------ 도우미

        private static Vector3 CenterOf(UnitView view) => view.Root.TransformPoint(view.Root.rect.center);

        // 화면 좌표(월드) → 전투 화면 root 안 좌표 — 움직임은 이 좌표(캔버스 기준 px)로 해야 캔버스 방식(오버레이·카메라)과 상관없이 맞음
        private Vector3 Local(Vector3 world) => root.InverseTransformPoint(world);

        private static float CardHeight(UnitView view) => view.Root.rect.height;

        // 보호막 테두리가 숨 쉬듯 (Update에서 매 프레임)
        private void PulseShieldAuras()
        {
            foreach (var view in viewOf.Values) view.PulseShield(Time.unscaledTime);
        }
    }
}
