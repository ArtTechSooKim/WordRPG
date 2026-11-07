using UnityEngine;
using WordRPG.Field;
using WordRPG.Items;

namespace WordRPG.UI
{
    // 필드 — 말을 잃어 가는 사람들 (#209, GDD 9-10)
    public partial class FieldScreen
    {
        private const float NpcLineSeconds = 4f;
        private const float ShopPitchSeconds = 3.5f;

        private FieldNpcs npcs;
        private readonly ShopPitch shopPitch = new ShopPitch();

        public FieldNpcs Npcs => npcs;

        // 지금 말풍선이 붙은 인물 (없으면 주인공) — 말풍선이 말하는 캐릭터를 기억하므로 따로 맞춰 둘 값이 없다
        // (#231: 예전엔 여기서 따로 기억해서 #224·#227처럼 어긋났음)
        public NpcView TalkingNpc => bubble != null && bubble.IsVisible && bubble.FromNpc ? npcs.ViewOf(bubble.Speaker) : null;

        private void SetUpNpcs(Vector2Int position)
        {
            if (bubble != null && bubble.FromNpc) bubble.Hide(); // 인물 그림을 새로 만드니 하던 말도 닫음
            npcs.Build(area, session, position, rng);
        }

        private void UpdateNpcs(float dt)
        {
            if (inBattle || transitioning || IsPanelOpen) return;
            npcs.Tick(dt, walker.Position, TalkingNpc);
            MaybeStartSpringLeap();
            UpdateShopPitch();
        }

        // 상점 근처를 지나가면 상인이 부른다 (#210)
        private void UpdateShopPitch()
        {
            var merchant = npcs.Merchant;
            if (merchant == null) return;
            var offset = walker.Position - merchant.Motion.Cell;
            string line = shopPitch.Update(area.Shop.Pitches, Mathf.Abs(offset.x) + Mathf.Abs(offset.y), Time.unscaledTime, rng);
            // 첫 전투 전엔 부르지 않음 (#232 사용자: 분신이 풀숲으로 이끄는 안내에서 시선을 빼앗음)
            if (line == null || bubble.IsVisible || fieldTutorialRunning || NeedsPrologue || FirstFightPending) return;
            bubble.Speak(merchant.Body, merchant.Data.DisplayName, line, ShopPitchSeconds);
            bubbleKeepUntil = Time.unscaledTime + ShopPitchSeconds;
        }

        private bool TryTalkToNpc(bool facingOnly)
        {
            if (walker.Rafting) return false;
            var view = npcs.FindTalkable(walker.Position, walker.Facing, facingOnly);
            if (view == null) return false;
            var offset = view.Motion.Cell - walker.Position;
            foreach (var direction in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
                if (direction.ToOffset() == offset) walker.Face(direction);
            playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
            npcs.Refresh(area, session);
            interactions++;
            bubble.Speak(view.Body, view.Data.DisplayName, NpcLines.Render(view.Stage.Line, session.Hero.DisplayName), NpcLineSeconds);
            bubbleKeepUntil = Time.unscaledTime + 0.3f;
            return true;
        }
    }
}
