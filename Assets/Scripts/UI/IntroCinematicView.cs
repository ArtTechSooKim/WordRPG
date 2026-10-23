using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace WordRPG.UI
{
    // 입장 영상의 자막 시간표 (#129) — Tools/build_intro_video.py가 영상과 함께 만드는 Resources/Video/intro_captions.json
    [Serializable]
    public class IntroCaptions
    {
        [Serializable]
        public class Line
        {
            public float start;
            public float end;
            public string text;
        }

        public float duration;
        public List<Line> captions = new List<Line>();

        public static IntroCaptions Parse(string json) =>
            string.IsNullOrEmpty(json) ? new IntroCaptions() : JsonUtility.FromJson<IntroCaptions>(json) ?? new IntroCaptions();

        public static IntroCaptions Load()
        {
            var asset = Resources.Load<TextAsset>(IntroCinematicView.CaptionsPath);
            return Parse(asset != null ? asset.text : null);
        }

        // 그 시각의 자막 (없으면 빈 글자)
        public string At(float seconds)
        {
            foreach (var line in captions)
                if (seconds >= line.start && seconds < line.end) return line.text;
            return "";
        }
    }

    // 입장 영상 (#129 사용자: 새 게임을 시작하면 시네마틱 — 컷은 사용자가 Google Flow로, Figma '입장 영상 재생 화면 (#129)').
    // 검정 화면에 영상을 화면 너비에 맞춰 (위아래 검정) + 아래쪽 자막 + 오른쪽 위 [건너뛰기]. 영상이 끝나거나 건너뛰면 onDone.
    // 영상을 못 열면(기기·테스트 환경) 잠깐 뒤 그냥 끝냄 — 게임이 멈추지 않게
    public class IntroCinematicView
    {
        public const string VideoPath = "Video/intro";
        public const string CaptionsPath = "Video/intro_captions";
        private const float PrepareTimeout = 4f;

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public string Caption => caption != null ? caption.text : "";
        public bool IsVideoPlaying => player != null && player.isPlaying;

        private VideoPlayer player;
        private RawImage screen;
        private AspectRatioFitter fitter;
        private Text caption;
        private RenderTexture texture;
        private IntroCaptions captions;
        private Action onDone;
        private float elapsed;
        private bool finished;

        public static IntroCinematicView Create(Transform parent)
        {
            var view = new IntroCinematicView();
            var root = UiKit.Stretch("IntroCinematicView", parent);
            view.Root = root.gameObject;
            view.Root.AddComponent<Image>().color = Color.black; // 아래 화면을 가리고 터치도 막음

            var frame = UiKit.Stretch("Video", root);
            view.screen = frame.gameObject.AddComponent<RawImage>();
            view.screen.color = Color.white;
            view.screen.raycastTarget = false;
            view.fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            view.fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            view.fitter.aspectRatio = 9f / 16f;

            view.caption = UiKit.Display(UiKit.Label("Caption", root, "", 52, new Color(0.98f, 0.97f, 0.92f), 0.05f, 0.1f, 0.95f, 0.2f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 30));
            var shadow = view.caption.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(0, -4);

            var skip = UiKit.MakeButton("IntroSkipButton", root, "건너뛰기", new Color(0f, 0f, 0f, 0.45f), 40, 0.74f, 0.925f, 0.96f, 0.965f);
            UiKit.Pill((Image)skip.targetGraphic);
            UiKit.Outline(UiKit.Panel("Edge", skip.transform, new Color(1f, 1f, 1f, 0.5f)), UiKit.RadiusLg, 3).raycastTarget = false;
            skip.onClick.AddListener(view.Skip);

            view.player = view.Root.AddComponent<VideoPlayer>();
            view.player.playOnAwake = false;
            view.player.isLooping = false;
            view.player.audioOutputMode = VideoAudioOutputMode.None;
            view.player.renderMode = VideoRenderMode.RenderTexture;
            view.player.loopPointReached += _ => view.Finish();
            view.player.errorReceived += (_, message) =>
            {
                Debug.LogWarning($"[Intro] 영상을 열 수 없음: {message}");
                view.Finish();
            };
            view.Root.SetActive(false);
            return view;
        }

        public void Play(Action done)
        {
            onDone = done;
            finished = false;
            elapsed = 0f;
            captions = IntroCaptions.Load();
            caption.text = "";
            Root.SetActive(true);
            var clip = Resources.Load<VideoClip>(VideoPath);
            if (clip == null)
            {
                Finish();
                return;
            }
            if (texture == null) texture = new RenderTexture((int)clip.width, (int)clip.height, 0);
            fitter.aspectRatio = clip.width / (float)Mathf.Max(1, (int)clip.height);
            player.clip = clip;
            player.targetTexture = texture;
            screen.texture = texture;
            player.Play();
        }

        // 자막은 영상 시각을 따르고, 영상이 아직 준비 중이면 아무것도 띄우지 않음. 오래 준비가 안 되면 끝냄
        public void Tick(float dt)
        {
            if (!IsOpen || finished) return;
            elapsed += dt;
            if (!player.isPrepared && !player.isPlaying)
            {
                if (elapsed > PrepareTimeout) Finish();
                return;
            }
            caption.text = captions.At((float)player.time);
            if (captions.duration > 0f && player.time >= captions.duration - 0.05f) Finish();
        }

        public void Skip() => Finish();

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (player != null) player.Stop();
            caption.text = "";
            Root.SetActive(false);
            var done = onDone;
            onDone = null;
            done?.Invoke();
        }
    }
}
