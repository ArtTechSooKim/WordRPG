using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 필드 — 말을 잃어 가는 사람들 (#209, GDD 9-10)
    public partial class FieldScreen
    {
        private const float NpcLineSeconds = 4f;

        private FieldNpcs npcs;
        private NpcView talkingNpc;

        public FieldNpcs Npcs => npcs;

        private void SetUpNpcs(Vector2Int position)
        {
            talkingNpc = null;
            npcs.Build(area, session, position, rng);
        }

        private void UpdateNpcs(float dt)
        {
            if (talkingNpc != null && !bubble.IsVisible) talkingNpc = null;
            if (inBattle || transitioning || IsPanelOpen) return;
            npcs.Tick(dt, walker.Position, talkingNpc);
        }

        // 말풍선은 말하는 인물의 머리 위에
        private Vector3 BubbleAnchor() => (talkingNpc != null ? talkingNpc.Position : player.position) + Vector3.up * 0.45f;

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
            talkingNpc = view;
            bubble.Speak(view.Data.DisplayName, NpcLines.Render(view.Stage.Line, session.Hero.DisplayName), NpcLineSeconds);
            bubbleKeepUntil = Time.unscaledTime + 0.3f;
            return true;
        }
    }
}
