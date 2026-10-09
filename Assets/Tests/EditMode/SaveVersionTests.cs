using System;
using System.IO;
using NUnit.Framework;
using WordRPG.Save;

namespace WordRPG.Tests
{
    // 더 새 버전 앱의 세이브 (#48 코드 검토): 깨진 세이브와 구분해서, 치우거나 덮어쓰지 않는다
    public class SaveVersionTests
    {
        private string dir;

        [SetUp]
        public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "WordRPG_Version_" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [Test]
        public void NewerVersionSaveIsReportedAsTooNewNotCorrupt()
        {
            var system = new SaveSystem(dir);
            Directory.CreateDirectory(dir);
            File.WriteAllText(system.MainPath, "{\"version\": 99}");

            var result = system.Load();
            Assert.IsNull(result.Data);
            Assert.IsTrue(result.TooNew, "새 버전 세이브");
            StringAssert.Contains("업데이트", result.Error);
            Assert.IsTrue(File.Exists(system.MainPath), "읽기만 하고 파일은 그대로");
        }

        [Test]
        public void BrokenSaveIsNotTooNew()
        {
            var system = new SaveSystem(dir);
            Directory.CreateDirectory(dir);
            File.WriteAllText(system.MainPath, "{ 깨진 파일");
            var result = system.Load();
            Assert.IsNull(result.Data);
            Assert.IsFalse(result.TooNew);
            Assert.IsNotNull(result.Error);
        }
    }
}
