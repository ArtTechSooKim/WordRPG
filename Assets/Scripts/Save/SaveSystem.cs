using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WordRPG.Save
{
    public class SaveLoadResult
    {
        public SaveData Data { get; }
        public bool UsedBackup { get; }
        public string Error { get; } // 파일은 있었는데 못 읽었을 때의 이유
        public bool TooNew { get; }  // 더 새 버전 앱의 세이브라서 못 읽음 — 깨진 것이 아님 (치우지 말 것)

        public SaveLoadResult(SaveData data, bool usedBackup, string error, bool tooNew = false)
        {
            Data = data;
            UsedBackup = usedBackup;
            Error = error;
            TooNew = tooNew;
        }
    }

    // save.json 읽기/쓰기. 쓰는 도중 앱이 꺼져도 세이브가 날아가지 않도록:
    //  1) save.json.tmp 에 먼저 쓰고  2) 기존 save.json 은 save.json.bak 으로 복사한 뒤  3) tmp를 save.json 으로 바꾼다.
    // 읽을 때 save.json 이 깨져 있으면 백업에서 읽는다
    public class SaveSystem
    {
        public const string FileName = "save.json";

        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public string Directory { get; }
        public string MainPath => Path.Combine(Directory, FileName);
        public string BackupPath => MainPath + ".bak";
        private string TempPath => MainPath + ".tmp";

        public SaveSystem(string directory)
        {
            Directory = directory;
        }

        public bool HasSave => File.Exists(MainPath) || File.Exists(BackupPath);

        public void Save(SaveData data)
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(TempPath, data.ToJson(), Utf8);

            if (File.Exists(MainPath))
            {
                File.Copy(MainPath, BackupPath, true);
                File.Delete(MainPath);
            }
            File.Move(TempPath, MainPath);
        }

        public SaveLoadResult Load()
        {
            var errors = new List<string>();
            bool tooNew = false;
            foreach (var path in new[] { MainPath, BackupPath })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var data = SaveData.FromJson(File.ReadAllText(path, Utf8));
                    return new SaveLoadResult(data, path == BackupPath, null);
                }
                catch (SaveTooNewException e)
                {
                    tooNew = true;
                    errors.Add($"{Path.GetFileName(path)}: {e.Message}");
                }
                catch (Exception e)
                {
                    errors.Add($"{Path.GetFileName(path)}: {e.Message}");
                }
            }
            return new SaveLoadResult(null, false, errors.Count > 0 ? string.Join(" / ", errors) : null, tooNew);
        }

        // 읽을 수 없는 세이브를 지우지 않고 옆으로 치워 둔다 (원인 조사용). 새 게임 저장이 덮어쓰지 않게
        public void QuarantineCorrupt()
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            foreach (var path in new[] { MainPath, BackupPath })
            {
                if (File.Exists(path)) File.Move(path, $"{path}.corrupt_{stamp}");
            }
        }

        public void Delete()
        {
            foreach (var path in new[] { MainPath, BackupPath, TempPath })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
