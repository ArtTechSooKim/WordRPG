using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 말풍선 꼬리 (#231 사용자: 화면 왼쪽의 어르신이 긴 말을 하면 말풍선이 화면 안으로 밀리면서 꼬리가 가운데 주인공을 가리켰음
    // → 꼬리 끝은 늘 말하는 캐릭터 머리에, 몸통만 화면 안으로). 실제로 놓인 꼬리·몸통 자리를 잰다
    public class SpeechBubbleTests
    {
        private const string LongLine = "샘물은 땅 밑으로 다 이어져 있지. 옛날엔 다들 이렇게 다녔어.";
        private const float Tolerance = 2f; // 말풍선 판 좌표 (1080 기준 화면에서 2px)

        private GameObject root;
        private Camera cam;
        private Canvas canvas;
        private SpeechBubble bubble;
        private Transform hero;

        // 꼬리 끝과 말하는 캐릭터 머리(뒤집혔으면 발) 사이 거리 — 화면 테스트에서도 씀
        public static float TailGap(SpeechBubble bubble, Camera camera)
        {
            var layer = (RectTransform)bubble.Root.parent;
            var target = bubble.Flipped ? SpeechBubble.FeetOf(bubble.Speaker) : SpeechBubble.HeadOf(bubble.Speaker);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, camera.WorldToScreenPoint(target), null, out var point);
            return Vector2.Distance(point, bubble.TailTip());
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("BubbleTest");
            var camGo = new GameObject("Camera", typeof(Camera));
            camGo.transform.SetParent(root.transform, false);
            cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(0, 0, -10);

            var canvasGo = new GameObject("Hud", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(root.transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); // 필드 HUD와 같은 설정
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var layer = UiKit.Stretch("SafeArea", canvasGo.transform);

            hero = Character("Hero", new Vector2(0.5f, 0.5f));
            bubble = SpeechBubble.Create(layer, hero);
            yield return null; // 화면 크기 맞춤
        }

        [TearDown]
        public void TearDown() => Object.Destroy(root);

        // 한 칸(16px) 그림의 캐릭터를 화면의 그 자리(0~1)에
        private Transform Character(string name, Vector2 viewport)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(root.transform, false);
            var texture = new Texture2D(16, 16);
            go.GetComponent<SpriteRenderer>().sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            var world = cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, 10f));
            go.transform.position = new Vector3(world.x, world.y, 0f);
            return go.transform;
        }

        private void Lay() => bubble.Tick(cam, canvas);

        private void AssertInsideScreen()
        {
            var body = bubble.BodyRect();
            var screen = bubble.LayerRect;
            Assert.GreaterOrEqual(body.xMin, screen.xMin, "몸통이 화면 왼쪽 안");
            Assert.LessOrEqual(body.xMax, screen.xMax, "몸통이 화면 오른쪽 안");
            Assert.GreaterOrEqual(body.yMin, screen.yMin, "몸통이 화면 아래 안");
            Assert.LessOrEqual(body.yMax, screen.yMax, "몸통이 화면 위 안");
        }

        [UnityTest]
        public IEnumerator TheTailStaysOnTheSpeakersHeadWhenTheBodyIsPushedIntoTheScreen()
        {
            // 화면 가장자리 가까이 (머리가 몸통의 둥근 모서리보다 안쪽인 곳까지 — 그보다 끝이면 꼬리는 모서리 앞에서 멈춤)
            foreach (float x in new[] { 0.07f, 0.93f })
            {
                var elder = Character("Elder", new Vector2(x, 0.45f));
                bubble.Speak(elder, "어르신", LongLine, 5f);
                Lay();
                Assert.AreSame(elder, bubble.Speaker);
                Assert.IsTrue(bubble.FromNpc);
                Assert.IsFalse(bubble.Flipped);
                Assert.Less(TailGap(bubble, cam), Tolerance, $"x={x}: 꼬리 끝이 말하는 사람 머리에");
                AssertInsideScreen();
                var body = bubble.BodyRect();
                Assert.Greater(Mathf.Abs(body.center.x - bubble.TailTip().x), 100f, "몸통은 화면 안으로 밀리고 꼬리만 머리에");
                Assert.LessOrEqual(body.width, SpeechBubble.MaxWidth + 0.5f, "긴 말은 줄을 바꿔 너비를 줄임");
                Object.Destroy(elder.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ALongLineWrapsIntoSeveralLinesInsideTheBody()
        {
            bubble.Speak(hero, "어르신", "짧은 말", 5f);
            Lay();
            float shortHeight = bubble.BodyRect().height;
            bubble.Speak(hero, "어르신", LongLine, 5f);
            Lay();
            Assert.Greater(bubble.BodyRect().height, shortHeight + 30f, "긴 말은 두 줄 이상 — 몸통도 그만큼 높아짐");
            var line = bubble.Root.Find("TextBubble/Body/Sub").GetComponent<Text>();
            Assert.AreEqual(LongLine, line.text);
            Assert.LessOrEqual(line.preferredHeight, line.rectTransform.rect.height + 1f, "줄을 바꾼 말이 칸 밖으로 넘치지 않음 (예전엔 한 줄 높이에 두 줄)");            yield return null;
        }

        [UnityTest]
        public IEnumerator WithNoRoomAboveTheBubbleFlipsBelowAndPointsUpAtTheFeet()
        {
            var high = Character("High", new Vector2(0.5f, 0.97f));
            bubble.Speak(high, "광부", LongLine, 5f);
            Lay();
            Assert.IsTrue(bubble.Flipped, "위에 자리가 없으면 아래로");
            Assert.Less(TailGap(bubble, cam), Tolerance, "꼬리 끝이 발에");
            Assert.Less(bubble.BodyRect().yMax, bubble.TailTip().y, "몸통은 꼬리 아래");
            AssertInsideScreen();

            // 다시 가운데로 오면 원래대로
            high.position = hero.position;
            Lay();
            Assert.IsFalse(bubble.Flipped);
            Assert.Less(TailGap(bubble, cam), Tolerance);
            Assert.Greater(bubble.BodyRect().yMin, bubble.TailTip().y, "몸통은 꼬리 위");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheHerosOwnWordsGoBackToTheHeroAndAVanishedSpeakerClosesTheBubble()
        {
            var merchant = Character("Merchant", new Vector2(0.2f, 0.5f));
            bubble.Speak(merchant, "상인", "상처약 있어요!", 5f);
            Lay();
            Assert.AreSame(merchant, bubble.Speaker);

            // 인물이 말하는 중에 주인공이 말하면 주인공 머리로 (#227)
            bubble.ShowWord("새로운 단어를 발견했다!", "overcome", "극복하다", "초원 도감 1 / 150", 5f);
            Lay();
            Assert.AreSame(hero, bubble.Speaker);
            Assert.IsFalse(bubble.FromNpc);
            Assert.Less(TailGap(bubble, cam), Tolerance);
            bubble.Say("빈 받침대다.", 5f);
            Lay();
            Assert.AreSame(hero, bubble.Speaker);

            // 인물이 화면 밖으로 나가면 닫힘 (#231 사용자: 상인이 안 보이는데 호객 말풍선만 남았음)
            bubble.Speak(merchant, "상인", "상처약 있어요!", 5f);
            merchant.position = cam.ViewportToWorldPoint(new Vector3(0.9f, 0.5f, 10f));
            Lay();
            Assert.IsTrue(bubble.IsVisible, "화면 안이면 그대로");
            merchant.position = cam.ViewportToWorldPoint(new Vector3(1.08f, 0.5f, 10f));
            Lay();
            Assert.IsFalse(bubble.IsVisible, "화면 밖으로 나가면 닫힘");
            merchant.position = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 10f));

            // 주인공 말은 카메라가 다른 곳을 비춰도 그대로 (연출)
            bubble.Say("어, 어르신?!", 5f);
            var heroHome = hero.position;
            hero.position = cam.ViewportToWorldPoint(new Vector3(0.5f, 1.2f, 10f));
            Lay();
            Assert.IsTrue(bubble.IsVisible, "주인공 말은 닫히지 않음");
            hero.position = heroHome;

            // 말하던 인물이 사라지면 말풍선도 닫힘
            bubble.Speak(merchant, "상인", "상처약 있어요!", 5f);
            Object.Destroy(merchant.gameObject);
            yield return null;
            Lay();
            Assert.IsFalse(bubble.IsVisible);
        }
    }
}
