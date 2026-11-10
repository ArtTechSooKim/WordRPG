#if WORDRPG_TESTER || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using WordRPG.Field;
using WordRPG.Items;
using WordRPG.Words;

namespace WordRPG.Game
{
    // 테스트 빌드인지 (#229). TestFlight용 빌드(MobileBuild.BuildIosTester → WORDRPG_TESTER)만 늘 켜짐.
    // 에디터에서는 기본 꺼짐 — 테스트에서만 켬. 앱스토어에 내는 빌드(BuildIos)에는 이 파일이 아예 들어가지 않는다
    public static class TesterBuild
    {
#if WORDRPG_TESTER
        public static bool Enabled => true;
#else
        public static bool Enabled { get; set; }
#endif
    }

    // 테스터 메뉴 규칙 (#229 사용자: 다른 사람에게 보여 주려고 처음부터 다시 했더니 레벨을 다시 올리기 귀찮음).
    // 레벨·골드·재료 · 지역까지 건너뛰기 · 단어 발견을 빨리 채운다. 진짜 게임 규칙(경험치·보스 보상·발견)을 그대로 거쳐 세이브가 어긋나지 않게
    public static class TesterTools
    {
        public const int LevelStep = 5;
        public const int GoldStep = 10000;          // #251 사용자: 10,000 단위로
        public const int AlwaysQuietSteps = 999999; // 살금살금 상시 (#251): 끝나지 않는 걸음 수 (물약과 같은 규칙을 그대로 씀, 알약에 숫자로 보임)
        public const int MaterialStep = 10;
        public const int WordStep = 25;

        // 주인공과 얻은 동료 모두 levels만큼 (최고 레벨에서 멈춤) + HP 회복. 반환 = 주인공이 오른 레벨 수
        public static int LevelUp(GameSession session, int levels = LevelStep)
        {
            int gained = 0;
            for (int i = 0; i < levels; i++)
            {
                if (!session.Hero.IsMaxLevel) gained += session.Hero.GainExp(session.Hero.ExpToNextLevel);
                foreach (var companion in session.Companions)
                    if (!companion.IsMaxLevel) companion.GainExp(companion.ExpToNextLevel);
            }
            session.RestoreHero();
            return gained;
        }

        // 주인공과 동료 모두 levels만큼 내림 (1에서 멈춤, 경험치 0) + HP 회복 (#251 사용자). 반환 = 주인공이 내린 레벨 수
        public static int LevelDown(GameSession session, int levels = LevelStep)
        {
            int before = session.Hero.Level;
            session.Hero.TesterSetLevel(before - levels);
            foreach (var companion in session.Companions) companion.TesterSetLevel(companion.Level - levels);
            session.RestoreHero();
            return before - session.Hero.Level;
        }

        public static void GiveGold(GameSession session, int amount = GoldStep) => session.Inventory.AddGold(amount);

        public static bool IsAlwaysQuiet(GameSession session) => session.Effects.QuietStepsLeft > AlwaysQuietSteps / 2;

        // 살금살금 상시 켜기·끄기 (#251 사용자). 반환 = 켜졌는지
        public static bool ToggleAlwaysQuiet(GameSession session)
        {
            bool on = !IsAlwaysQuiet(session);
            session.Effects.TesterSetQuietSteps(on ? AlwaysQuietSteps : 0);
            return on;
        }

        // 무료 상태로 (#251 사용자: 이미 산 계정이라 무료 상태 화면과 사는 과정을 볼 수 없음). 이 세이브의 특전 받은 기록도 지워서 다시 사면 받음
        public static void GoFree(GameSession session, Entitlements owned)
        {
            session.World.TesterForgetEvent(FullVersionGift.EventId);
            owned.SetTesterFree(true);
        }

        // 성유물 강화 재료(ItemKind.Material) 종류마다 count개. 반환 = 재료 종류 수
        public static int GiveMaterials(GameSession session, IEnumerable<ItemData> items, int count = MaterialStep)
        {
            int kinds = 0;
            foreach (var item in items)
            {
                if (item == null || item.Kind != ItemKind.Material) continue;
                session.Inventory.Add(item, count);
                kinds++;
            }
            return kinds;
        }

        // 이야기 순서대로 지역 (건너뛰기 버튼 순서)
        public static List<FieldArea> StoryAreas(IEnumerable<FieldArea> areas) =>
            areas.Where(a => a != null && a.StoryOrder > 0).OrderBy(a => a.StoryOrder).ToList();

        // target까지 건너뛰기: 이야기 순서가 target보다 앞인 지역의 보스를 이긴 것으로 하고, 보스 보상(성유물·기술문서)과
        // 유적 동료를 받는다 (이긴 보스·얻은 것은 다시 주지 않음) + HP 회복 (성유물을 끼우면 최대 HP가 늘어서). 반환 = 새로 이긴 것으로 한 보스 수
        public static int SkipTo(GameSession session, IEnumerable<FieldArea> areas, FieldArea target)
        {
            int bosses = 0;
            foreach (var area in StoryAreas(areas).Where(a => a.StoryOrder < target.StoryOrder))
            {
                if (area.Boss != null && !session.World.IsBossDefeated(area.BossId))
                {
                    session.World.MarkBossDefeated(area.BossId);
                    session.GrantRelic(area.Boss.RewardRelic);
                    var item = area.Boss.RewardItem;
                    if (item != null && session.Inventory.GetCount(item) == 0) session.Inventory.Add(item);
                    bosses++;
                }
                if (area.ShrineCompanion != null) session.AddCompanion(area.ShrineCompanion);
            }
            session.RestoreHero();
            return bosses;
        }

        // 지역들이 쓰는 단어장 (이야기 순서, 같은 단어장은 한 번)
        public static List<WordDatabase> Books(IEnumerable<FieldArea> areas) =>
            StoryAreas(areas).Select(a => a.Words).Where(w => w != null).Distinct().ToList();

        // 아직 못 찾은 단어를 단어장 순서대로 count개 발견 (보스의 사전처럼 문제 없이 — 곧 복습 문제로 나옴). 반환 = 새로 찾은 수
        public static int DiscoverWords(GameSession session, WordDatabase book, int count, DateTime nowUtc)
        {
            int found = 0;
            foreach (var word in book.Words)
            {
                if (found >= count) break;
                if (session.Vocabulary.Discover(word.Id, nowUtc)) found++;
            }
            return found;
        }
    }

    // 테스터 메뉴 [무료 상태로]일 때만 결제 없이 산 것처럼 (#251): 이미 산 계정은 진짜 결제 창으로 다시 살 수 없음. 무료 상태가 아니면 진짜 스토어 그대로
    public class TesterFreeStore : IStore
    {
        private readonly IStore real;
        private readonly Entitlements owned;

        public TesterFreeStore(IStore real, Entitlements owned)
        {
            this.real = real;
            this.owned = owned;
        }

        public bool IsReady => owned.TesterFree || real.IsReady;
        public string FullVersionPrice => real.FullVersionPrice;

        public void BuyFullVersion(Action<StoreResult> done)
        {
            if (!FinishFree(StoreResult.Purchased, done)) real.BuyFullVersion(done);
        }

        public void Restore(Action<StoreResult> done)
        {
            if (!FinishFree(StoreResult.Restored, done)) real.Restore(done);
        }

        private bool FinishFree(StoreResult result, Action<StoreResult> done)
        {
            if (!owned.TesterFree) return false;
            owned.SetTesterFree(false);
            done?.Invoke(result);
            return true;
        }
    }
}
#endif
