using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 필드 > 맵 그리기: 글자 맵 → 타일(길·물 테두리, 출입구, 다리, 꾸밈), 칸보다 큰 그림, 월드 오브젝트(주인공·카메라·타일맵)
    public partial class FieldScreen
    {
        // 지역 타일 전체를 그린다 (연 상자·쓰러뜨린 보스·잠긴 출입구 반영). 길 열림 연출에서는 다른 지역도 그린다
        private void DrawTiles(FieldArea fieldArea, Func<Vector2Int, bool> lockedDoor)
        {
            var map = fieldArea.Map;
            tilemap.ClearAllTiles();
            foreach (var glow in exitGlows) Destroy(glow);
            exitGlows.Clear();
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var cell = new Vector2Int(x, y);
                var kind = map.Get(cell);
                bool done = CellState.IsDone(fieldArea, session, cell);
                Tile tile;
                if (kind == FieldTile.Door) tile = DoorTile(fieldArea, cell, lockedDoor(cell));
                else if (kind == FieldTile.Bridge) tile = BridgeTile(fieldArea.Theme, done, map.IsBridgeHorizontal(cell));
                else if (kind == FieldTile.Decor) tile = DecorTile(fieldArea.Theme, FieldDecor.Variant(map, cell));
                else if (FieldAutotile.IsAutotiled(kind) && AutoTile(fieldArea.Theme, kind, FieldAutotile.Mask(map, cell)) is Tile auto) tile = auto;
                else if (!done && FieldArt.VariantCount(kind, fieldArea.Theme) > 0)
                    tile = VariantTile(fieldArea.Theme, kind, FieldDecor.TileVariant(cell, FieldArt.VariantCount(kind, fieldArea.Theme)));
                else tile = TileFor(kind, done, fieldArea.Theme);
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
            cam.backgroundColor = PlaceholderArt.OutsideColor(fieldArea.Theme);
            DrawProps(fieldArea);
        }

        // 칸보다 큰 그림 (#80): 유적 제단의 잠든 석상 · 섬의 동굴 입구. 바닥 칸 아래 가운데에 맞춰 타일 위에 (주인공보다 아래)
        private void DrawProps(FieldArea fieldArea)
        {
            foreach (var prop in props) Destroy(prop);
            props.Clear();
            var map = fieldArea.Map;
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var cell = new Vector2Int(x, y);
                var sprite = map.Get(cell) == FieldTile.Decor
                    ? FieldArt.DecorProp(fieldArea.Theme, FieldDecor.Variant(map, cell))
                    : FieldArt.Prop(map.Get(cell), fieldArea.Theme, CellState.IsDone(fieldArea, session, cell));
                if (sprite == null) continue;
                var go = new GameObject("Prop_" + map.Get(cell), typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(cell.x + 0.5f, cell.y, 0f);
                var renderer = go.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 5;
                props.Add(go);
            }
        }

        public int PropCount => props.Count; // 큰 그림 수 (테스트용)

        // 칸 그림 변형 (#98): 서고 책장 3가지·바랜 종이 3가지처럼 같은 종류의 다른 그림
        private Tile VariantTile(FieldTheme theme, FieldTile kind, int variant)
        {
            string key = $"{theme}_{kind}_v{variant}";
            if (!tileCache.TryGetValue(key, out var tile))
            {
                tile = MakeTile(FieldArt.ForTileVariant(kind, theme, variant));
                tileCache[key] = tile;
            }
            return tile;
        }

        private Tile DecorTile(FieldTheme theme, string variant)
        {
            string key = $"{theme}_Decor_{variant}";
            if (!tileCache.TryGetValue(key, out var tile))
            {
                tile = MakeTile(FieldArt.ForDecor(theme, variant));
                tileCache[key] = tile;
            }
            return tile;
        }

        private Tile BridgeTile(FieldTheme theme, bool built, bool horizontal)
        {
            string key = $"{theme}_Bridge_{built}_{horizontal}";
            if (!tileCache.TryGetValue(key, out var tile))
            {
                tile = MakeTile(FieldArt.ForBridge(theme, built, horizontal));
                tileCache[key] = tile;
            }
            return tile;
        }

        private Tile TileFor(FieldTile kind, bool done, FieldTheme? theme = null)
        {
            var t = theme ?? area.Theme;
            string key = $"{t}_{kind}_{done}";
            if (!tileCache.TryGetValue(key, out var tile))
            {
                tile = MakeTile(FieldArt.ForTile(kind, t, done));
                tileCache[key] = tile;
            }
            return tile;
        }

        // 길·물가 자동 테두리 (그림이 없는 테마면 null → 한 칸 그림)
        private Tile AutoTile(FieldTheme theme, FieldTile kind, int mask)
        {
            string key = $"{theme}_{kind}_auto_{mask}";
            if (!tileCache.TryGetValue(key, out var tile))
            {
                var sprite = FieldArt.AutoTile(kind, theme, mask);
                tile = sprite != null ? MakeTile(sprite) : null;
                tileCache[key] = tile;
            }
            return tile;
        }

        // 출입구 그림: 잠김 / 지역 사이 길(초원↔숲↔정글, #120) = 옆 길과 이어진 바닥 + 바깥에서 비치는 빛 /
        // 도착 지역 테마 전용 / 기본
        private Tile DoorTile(FieldArea fieldArea, Vector2Int cell, bool locked)
        {
            var exit = fieldArea.GetExit(cell);
            FieldTheme? target = exit != null && exit.Target != null ? exit.Target.Theme : (FieldTheme?)null;
            if (!locked && FieldArt.ExitGlow(fieldArea.Theme, target) is Color glow
                && AutoTile(fieldArea.Theme, FieldTile.Floor, FieldAutotile.DoorFloorMask(fieldArea.Map, cell)) is Tile floor)
            {
                AddExitGlow(cell, FieldAutotile.Outward(fieldArea.Map, cell), glow);
                return floor;
            }
            string key = $"{fieldArea.Theme}_Door_{target}_{locked}";
            if (!tileCache.TryGetValue(key, out var tile))
            {
                tile = MakeTile(FieldArt.ForDoor(fieldArea.Theme, target, locked));
                tileCache[key] = tile;
            }
            return tile;
        }

        private readonly List<GameObject> exitGlows = new List<GameObject>(); // 지역 사이 길 출입구의 빛 (#120)
        public int ExitGlowCount => exitGlows.Count; // 테스트용

        // 출입구 칸 가운데에 빛 한 장 — 바깥(outward) 쪽이 가장 밝게 돌려 놓음. 타일 위, 큰 그림·주인공 아래
        private void AddExitGlow(Vector2Int cell, Vector2Int outward, Color color)
        {
            var go = new GameObject("ExitGlow", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            go.transform.position = CellCenter(cell);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = FieldArt.ExitGlowSprite();
            renderer.color = color;
            renderer.sortingOrder = 4;
            exitGlows.Add(go);
        }

        private void CreateWorldObjects()
        {
            var gridGo = new GameObject("FieldGrid", typeof(Grid));
            gridGo.transform.SetParent(transform, false);
            var tilemapGo = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer));
            tilemapGo.transform.SetParent(gridGo.transform, false);
            tilemap = tilemapGo.GetComponent<Tilemap>();

            var playerGo = new GameObject("Player", typeof(SpriteRenderer));
            playerGo.transform.SetParent(transform, false);
            player = playerGo.transform;
            playerRenderer = playerGo.GetComponent<SpriteRenderer>();
            playerRenderer.sortingOrder = 10;
            follower = new FieldFollower(transform);
            roamerViews = new FieldRoamerViews(transform, CellCenter);
            streams = new FieldStreams(transform);
            scatter = new FieldScatter(transform);
            fog = new FieldFog(transform);
            var raftTexture = Resources.Load<Texture2D>("Art/NinjaAdventure/Field/raft");
            if (raftTexture != null)
                raftSprite = Sprite.Create(raftTexture, new Rect(0, 0, raftTexture.width, raftTexture.height), new Vector2(0.5f, 0.5f), 16);
            var raftGo = new GameObject("Raft", typeof(SpriteRenderer));
            raftGo.transform.SetParent(transform, false);
            raftRenderer = raftGo.GetComponent<SpriteRenderer>();
            raftRenderer.sprite = raftSprite;
            raftRenderer.sortingOrder = 8; // 주인공(10)·동료(9·11) 아래
            raftGo.SetActive(false);

            cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                camGo.transform.SetParent(transform, false); // 직접 만든 카메라는 필드와 함께 정리
                cam = camGo.GetComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        private static Tile MakeTile(Sprite sprite)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            return tile;
        }
    }
}
