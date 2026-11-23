using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 필드 > 비밀의 숲 황금 너구리 (#292 사용자, GDD 9-17 — 규칙 GoldRaccoon). 컷씬은 숲 보스 뒤 나무꾼처럼 입력을 막고 —
    // 너구리는 말하지 않음, 몸짓으로만 (#292 사용자: 말 안 하는 게 낫겠어):
    //  들어오면 앞에 나타나 춤추다 도망 → 쫓아가 옆에서 [잡기] = 동전이 '띠링' + 골드 → 또 춤추다 다음 자리로 도망 (3번) →
    //  마지막에 잡으면 동전이 파바바박 터지고 한 번 더 춤추며 인사하듯 사라짐. 하루 한 번 (현지 날짜), 중간에 나가면 다음에 그다음 자리부터
    public partial class FieldScreen
    {
        private const float RaccoonRunStep = 0.085f;   // 도망갈 때 한 칸 (주인공 달리기보다 빠르게)
        private const float RaccoonDanceSeconds = 1.2f;
        private const float RaccoonByeSeconds = 1.0f;  // 마지막 동전 뒤 인사하듯 한 번 더 춤추고 사라짐
        private const int RaccoonAppearAhead = 2;      // 들어오면 주인공 앞 몇 칸에 나타나는지
        private const int BurstCoins = 28;             // 마지막에 터지는 동전 (파바바박)
        private static readonly Color RaccoonSparkle = new Color(1f, 0.86f, 0.4f);

        private SpriteRenderer raccoonRenderer;
        private CharacterSheet raccoonSheet;
        private Sprite[] coinFrames;
        private Vector2Int? raccoonCell;          // 지금 서 있는 칸 (없으면 null)
        private Direction raccoonFacing = Direction.Down;
        private bool raccoonEntryPending;         // 방금 들어옴 — 춤추고 도망가는 컷씬을 기다림
        private float raccoonEntryAt;             // 들어와서 너구리를 잠깐 먼저 보여 준 뒤 춤 (화면이 밝아지고 0.5초)
        private bool raccoonDancing, raccoonRunning;
        private int raccoonStep;
        private readonly List<GameObject> raccoonCoins = new List<GameObject>();

        public Vector2Int? RaccoonCell => raccoonCell;                                                          // 테스트용
        public bool RaccoonVisible => raccoonRenderer != null && raccoonRenderer.gameObject.activeSelf;      // 테스트용
        public bool RaccoonBusy => raccoonEntryPending || raccoonDancing || raccoonRunning;                  // 테스트용
        public bool RaccoonRunning => raccoonRunning;                                                         // 테스트용
        public int RaccoonCatches { get; private set; }                                                       // 테스트용
        public int RaccoonCoinsShown { get; private set; }                                                    // 테스트용 (마지막에 터진 동전 수)

        // 지역에 들어올 때 (EnterArea): 오늘 아직 남아 있으면 주인공 앞(도망갈 길 위)에 세워 두고 컷씬을 기다림
        private void SetUpRaccoon(Vector2Int position)
        {
            raccoonEntryPending = raccoonDancing = raccoonRunning = false;
            ClearRaccoonCoins();
            var spot = GoldRaccoon.Spot(area, session.World, LocalClock());
            if (!spot.HasValue)
            {
                raccoonCell = null;
                if (raccoonRenderer != null) raccoonRenderer.gameObject.SetActive(false);
                return;
            }
            if (raccoonRenderer == null)
            {
                var go = new GameObject("GoldRaccoon", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                raccoonRenderer = go.GetComponent<SpriteRenderer>();
                raccoonRenderer.sortingOrder = 9; // 주인공(10) 바로 아래 (인물과 같음)
                raccoonSheet = new CharacterSheet("Art/NinjaAdventure/Field/gold_raccoon");
            }
            var road = FieldReach.PathTo(area.Map, position, spot.Value, RaccoonCanStand);
            var start = road.Count > RaccoonAppearAhead ? road[RaccoonAppearAhead - 1] : road.Count > 0 ? road[0] : spot.Value;
            raccoonCell = start;
            raccoonFacing = FacingToward(position - start);
            raccoonRenderer.color = Color.white;
            raccoonRenderer.transform.localScale = Vector3.one;
            raccoonRenderer.gameObject.SetActive(true);
            raccoonEntryPending = true;
            raccoonEntryAt = -1f;
            DrawRaccoon(CellCenter(start), 0);
        }

        // 너구리가 지나가거나 설 수 있는 칸 (주인공 칸·출입구는 빼고)
        private bool RaccoonCanStand(Vector2Int cell) =>
            area.Map.IsWalkable(cell) && area.Map.Get(cell) != FieldTile.Door && (walker == null || cell != walker.Position);

        // [확인] 버튼을 금빛으로 (옆에 서 있는 너구리)
        private bool IsNextToRaccoon() =>
            raccoonCell.HasValue && !RaccoonBusy && RaccoonVisible && walker != null && !walker.Rafting
            && Mathf.Abs(raccoonCell.Value.x - walker.Position.x) + Mathf.Abs(raccoonCell.Value.y - walker.Position.y) == 1;

        // 서 있는 너구리는 길을 막음 (주인공이 옆에 서서 [확인])
        private bool IsRaccoonAt(Vector2Int cell) => raccoonCell.HasValue && raccoonCell.Value == cell && RaccoonVisible;

        // UpdateNpcs에서 (서 있을 때만): 들어온 뒤 화면이 밝아지면 춤추고 도망
        private void MaybeStartRaccoonEntry()
        {
            if (!raccoonEntryPending || cutsceneRunning || springJumping || fieldTutorialRunning || NeedsPrologue || heroSetupRunning || moving) return;
            if (raccoonEntryAt < 0f) raccoonEntryAt = Time.unscaledTime + 0.5f * animationScale; // 너구리를 먼저 보고
            if (Time.unscaledTime < raccoonEntryAt) return;
            raccoonEntryPending = false;
            StartCoroutine(RaccoonEntryScene());
        }

        private IEnumerator RaccoonEntryScene()
        {
            BeginCutscene();
            FaceRaccoon();
            yield return RaccoonDance();
            yield return RaccoonFlee();
            EndCutscene();
        }

        // 옆에서 너구리를 보고 [확인] (바라보지 않아도 옆이면 — facingOnly=false)
        private bool TryCatchRaccoon(bool facingOnly)
        {
            if (!raccoonCell.HasValue || RaccoonBusy || walker.Rafting || !RaccoonVisible) return false;
            var offset = raccoonCell.Value - walker.Position;
            if (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) != 1) return false;
            var facing = FacingToward(offset);
            if (facingOnly && walker.Facing != facing) return false;
            interactions++;
            SetConfirmText("확인"); // 잡은 뒤 말을 넘기는 동안은 다시 [확인]
            StartCoroutine(RaccoonCaughtScene());
            return true;
        }

        private IEnumerator RaccoonCaughtScene()
        {
            BeginCutscene();
            FaceRaccoon();
            var now = LocalClock();
            bool last = GoldRaccoon.IsLastSpot(area, session.World, now);
            int gold = GoldRaccoon.Catch(area, session, now);
            RaccoonCatches++;
            SaveNow(); // 받은 골드·잡은 횟수는 바로 세이브
            var at = raccoonRenderer.transform.position;
            if (!last)
            {
                Sound.Play(Sfx.Coin); // 띠링
                StartCoroutine(Coins(at, 4, 1.2f, burst: false));
                RefreshHud(); // HUD 골드가 굴러 올라가고 '+100G'
                yield return WaitUnscaled(0.35f);
                yield return RaccoonDance();
                yield return RaccoonFlee();
            }
            else
            {
                yield return RaccoonFarewell(at, gold);
            }
            EndCutscene();
        }

        // 춤: 좌우로 몸을 흔들며 통통 뛰고 금빛 반짝 (말은 없음)
        private IEnumerator RaccoonDance()
        {
            raccoonDancing = true;
            StartCoroutine(FieldFx.Play(transform, raccoonRenderer.transform.position, Fx.Sparkle, 1.3f, animationScale, 12, tint: RaccoonSparkle));
            yield return WaitUnscaled(RaccoonDanceSeconds);
            raccoonDancing = false;
        }

        // 도망: 다음 자리로 쪼르르 — 카메라가 따라갔다가 주인공에게 돌아옴 (어디로 갔는지 보이게)
        private IEnumerator RaccoonFlee()
        {
            var spot = GoldRaccoon.Spot(area, session.World, LocalClock());
            if (!spot.HasValue || !raccoonCell.HasValue) yield break;
            Sound.Play(Sfx.Flee);
            var road = FieldReach.PathTo(area.Map, raccoonCell.Value, spot.Value, RaccoonCanStand);
            if (road.Count == 0 && raccoonCell.Value != spot.Value) road.Add(spot.Value); // 길이 없으면 (주인공이 막는 등) 그 자리로 바로
            raccoonRunning = true;
            var focus = cameraFocus ?? CameraTarget(player.position);
            foreach (var next in road)
            {
                var from = CellCenter(raccoonCell.Value);
                var to = CellCenter(next);
                raccoonFacing = FacingToward(next - raccoonCell.Value);
                float step = RaccoonRunStep * animationScale;
                for (float t = 0f; t < step; t += Time.unscaledDeltaTime)
                {
                    var at = Vector3.Lerp(from, to, t / step);
                    DrawRaccoon(at, raccoonStep + (t < step * 0.5f ? 0 : 1));
                    focus = Vector3.Lerp(focus, CameraTarget(at), 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime / Mathf.Max(0.01f, animationScale)));
                    cameraFocus = focus;
                    yield return null;
                }
                raccoonStep += 2;
                raccoonCell = next;
            }
            raccoonRunning = false;
            raccoonFacing = FacingToward(walker.Position - raccoonCell.Value);
            DrawRaccoon(CellCenter(raccoonCell.Value), 0);
            StartCoroutine(FieldFx.Play(transform, CellCenter(raccoonCell.Value), Fx.Sparkle, 1.1f, animationScale, 12, tint: RaccoonSparkle));
            yield return WaitUnscaled(0.45f);
            yield return PanCamera(null, 0.5f);
        }

        // 마지막: 동전이 파바바박 터져 주인공에게 쏟아지고 → 인사하듯 한 번 더 춤 → 반짝이며 사라짐 (내일까지 없음)
        private IEnumerator RaccoonFarewell(Vector3 at, int gold)
        {
            raccoonDancing = true;
            RaccoonCoinsShown = BurstCoins;
            yield return Coins(at, BurstCoins, 2.6f, burst: true);
            RefreshHud(); // '+1000G'
            yield return WaitUnscaled(RaccoonByeSeconds);
            raccoonDancing = false;
            Sound.Play(Sfx.Flee);
            StartCoroutine(FieldFx.Play(transform, raccoonRenderer.transform.position, Fx.Sparkle, 1.8f, animationScale, 12, tint: RaccoonSparkle));
            float seconds = 0.5f * animationScale;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                raccoonRenderer.color = new Color(1f, 1f, 1f, 1f - k);
                raccoonRenderer.transform.localScale = Vector3.one * (1f + 0.4f * k);
                raccoonRenderer.transform.position = at + Vector3.up * (0.6f * k);
                yield return null;
            }
            raccoonRenderer.gameObject.SetActive(false);
            raccoonCell = null;
        }

        // 동전: 너구리에게서 튀어 올랐다가 주인공에게 빨려 들어감. burst = 사방으로 파바바박 + 들어올 때마다 띠링 (점점 높게)
        private IEnumerator Coins(Vector3 from, int count, float power, bool burst)
        {
            if (coinFrames == null) coinFrames = CutCoinFrames();
            var coins = new List<(SpriteRenderer renderer, Vector3 land, float delay)>();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Coin", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                go.transform.position = from;
                var renderer = go.GetComponent<SpriteRenderer>();
                renderer.sortingOrder = 12; // 주인공(10)·말풍선 그림 위
                renderer.sprite = coinFrames.Length > 0 ? coinFrames[i % coinFrames.Length] : null;
                raccoonCoins.Add(go);
                float angle = burst ? (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f) : Random.Range(0.6f, 2.5f);
                float reach = power * (burst ? Random.Range(0.55f, 1f) : Random.Range(0.4f, 0.8f));
                coins.Add((renderer, from + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.7f, 0f) * reach, burst ? i * 0.012f : i * 0.05f));
            }
            if (burst)
            {
                for (int i = 0; i < 6; i++) Sound.Play(Sfx.Coin, 0.9f + i * 0.08f); // 파바바박
            }
            float rise = 0.42f * animationScale, gather = 0.4f * animationScale;
            int dings = 0;
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                bool alive = false;
                for (int i = 0; i < coins.Count; i++)
                {
                    var (renderer, land, delay) = coins[i];
                    if (renderer == null) continue;
                    float local = t - delay * animationScale;
                    if (local < 0f)
                    {
                        alive = true;
                        continue;
                    }
                    renderer.sprite = coinFrames.Length > 0 ? coinFrames[(int)(local / Mathf.Max(0.001f, 0.06f * animationScale) + i) % coinFrames.Length] : null;
                    if (local < rise)
                    {
                        float k = local / rise;
                        renderer.transform.position = Vector3.Lerp(from, land, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.9f);
                        alive = true;
                    }
                    else if (local < rise + gather)
                    {
                        float k = Mathf.SmoothStep(0f, 1f, (local - rise) / gather);
                        renderer.transform.position = Vector3.Lerp(land, player.position + Vector3.up * 0.2f, k);
                        alive = true;
                    }
                    else
                    {
                        if (burst && dings < 10 && i % 3 == 0) Sound.Play(Sfx.Coin, 1f + 0.06f * dings++);
                        Destroy(renderer.gameObject);
                    }
                }
                if (!alive) break;
                yield return null;
            }
            raccoonCoins.RemoveAll(c => c == null);
        }

        private Sprite[] CutCoinFrames()
        {
            var texture = Resources.Load<Texture2D>("Art/NinjaAdventure/Field/coin_spin");
            if (texture == null) return new Sprite[0];
            int size = texture.height;
            var frames = new Sprite[Mathf.Max(1, texture.width / size)];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i * size, 0, size, size), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
            return frames;
        }

        private void ClearRaccoonCoins()
        {
            foreach (var coin in raccoonCoins)
                if (coin != null) Destroy(coin);
            raccoonCoins.Clear();
        }

        // 매 프레임: 춤(좌우로 흔들며 통통) · 서 있을 땐 숨 쉬듯 들썩이고 주인공이 가까우면 쳐다봄
        private void UpdateRaccoon()
        {
            if (!raccoonCell.HasValue || !RaccoonVisible || raccoonRunning || !raccoonRenderer.gameObject.activeSelf) return;
            float now = Time.unscaledTime;
            var center = CellCenter(raccoonCell.Value);
            if (raccoonDancing)
            {
                raccoonFacing = Mathf.FloorToInt(now / 0.18f) % 2 == 0 ? Direction.Left : Direction.Right;
                float hop = Mathf.Abs(Mathf.Sin(now * Mathf.PI * 3.2f)) * 0.28f;
                DrawRaccoon(center + Vector3.up * hop, Mathf.FloorToInt(now * 8f));
                return;
            }
            if (walker != null && cutsceneRunning == false)
            {
                var offset = walker.Position - raccoonCell.Value;
                if (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) <= 4) raccoonFacing = FacingToward(offset);
            }
            float bob = Mathf.Abs(Mathf.Sin(now * 4f)) * 0.05f;
            DrawRaccoon(center + Vector3.up * bob, 0);
        }

        private void DrawRaccoon(Vector3 world, int frame)
        {
            if (raccoonRenderer == null) return;
            raccoonRenderer.transform.position = world;
            var sprite = raccoonSheet != null ? raccoonSheet.Get(raccoonFacing, frame) : null;
            raccoonRenderer.sprite = sprite != null ? sprite : PlaceholderArt.ForTile(FieldTile.Chest, area.Theme);
        }

        private void FaceRaccoon()
        {
            if (!raccoonCell.HasValue) return;
            walker.Face(FacingToward(raccoonCell.Value - walker.Position));
            playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
            raccoonFacing = FacingToward(walker.Position - raccoonCell.Value);
        }
    }
}
