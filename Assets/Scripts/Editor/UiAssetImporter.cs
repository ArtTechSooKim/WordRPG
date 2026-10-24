using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using WordRPG.UI;

namespace WordRPG.EditorTools
{
    // 그림 가져오기 설정
    //  - Assets/Resources/UI/Icons: Figma에서 내보낸 아이콘 → 부드러운 UI 스프라이트
    //  - Assets/Resources/Art: 도트 그림(Ninja Adventure 팩) → 16픽셀 = 1칸, 확대해도 또렷하게(Point)
    //  - Assets/Branding: 앱 아이콘 (MobileBuild가 플레이어 설정에 넣음)
    public class UiAssetImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            // 앱 아이콘(Figma에서 1024px로 내보냄): 압축 없이 원본 그대로 (빌드가 크기별로 줄인다)
            if (assetPath.StartsWith("Assets/Branding/"))
            {
                var brand = (TextureImporter)assetImporter;
                brand.textureType = TextureImporterType.Default;
                brand.mipmapEnabled = false;
                brand.alphaIsTransparency = true;
                brand.npotScale = TextureImporterNPOTScale.None;
                brand.maxTextureSize = 1024;
                brand.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }

            bool icon = assetPath.StartsWith("Assets/Resources/UI/Icons/");
            bool pixelArt = assetPath.StartsWith("Assets/Resources/Art/");
            if (!icon && !pixelArt) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (icon)
            {
                importer.spritePixelsPerUnit = 100;
                return;
            }
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        // 소리: 음악은 스트리밍(메모리 절약), 효과음은 미리 풀어 두고 모노로 (바로 재생)
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            bool music = IsMusic(assetPath);
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !music;
        }

        // 음악 = Audio/Music 폴더, 또는 유료 소리 폴더 Audio/Licensed에서 이름이 음악(Music 열거형)과 같은 것
        // (#133 사용자가 찾은 입장 영상 곡 Licensed/intro.wav — 효과음처럼 모노·통째로 메모리에 올리지 않게)
        public static bool IsMusic(string assetPath)
        {
            if (assetPath.StartsWith("Assets/Resources/Audio/Music/")) return true;
            if (!assetPath.StartsWith("Assets/Resources/Audio/Licensed/")) return false;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            foreach (Music track in Enum.GetValues(typeof(Music)))
                if (track != Music.None && string.Equals(track.ToString(), name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
