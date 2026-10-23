using NUnit.Framework;
using UnityEngine;
using UnityEngine.Video;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 입장 영상 (#129): Tools/build_intro_video.py가 만든 영상 + 자막 시간표
    public class IntroCinematicTests
    {
        [Test]
        public void IntroVideoAndCaptionsExist()
        {
            var clip = Resources.Load<VideoClip>(IntroCinematicView.VideoPath);
            Assert.IsNotNull(clip, "Resources/Video/intro.mp4");
            Assert.Less(clip.width, clip.height, "세로 영상");
            var captions = IntroCaptions.Load();
            Assert.AreEqual(3, captions.captions.Count, "자막 세 줄 (스토리 프롤로그)");
            Assert.That(captions.duration, Is.EqualTo((float)clip.length).Within(0.6f), "자막표 길이 = 영상 길이");
            Assert.LessOrEqual(captions.duration, 40f, "입장 영상은 짧게");
        }

        [Test]
        public void CaptionsFollowTheTimeline()
        {
            var c = IntroCaptions.Parse("{\"duration\":26.5,\"captions\":[{\"start\":0.6,\"end\":5.4,\"text\":\"A\"},{\"start\":10.1,\"end\":14.9,\"text\":\"B\"}]}");
            Assert.AreEqual("", c.At(0.2f), "시작 직후는 비어 있음");
            Assert.AreEqual("A", c.At(1f));
            Assert.AreEqual("", c.At(7f), "자막 사이");
            Assert.AreEqual("B", c.At(12f));
            Assert.AreEqual("", IntroCaptions.Parse("").At(1f), "자막표가 없어도 멈추지 않음");
        }
    }
}
