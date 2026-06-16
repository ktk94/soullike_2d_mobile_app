using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.World;
using SoulCraft.Enemy;

namespace SoulCraft.Factory
{
    /// <summary>
    /// 프리팹 없이 코드로 방(Room)을 생성하는 간단한 방 생성기.
    /// SpriteRenderer 그리드로 바닥/벽 타일을 배치하고, RoomType에 따라 적/보물/보스를 배치한다.
    /// </summary>
    public static class SimpleRoom
    {
        // ================================================================
        //  Tile Colors
        // ================================================================

        private static readonly Color ColFloor1   = new(0.18f, 0.16f, 0.14f, 1f);
        private static readonly Color ColFloor2   = new(0.20f, 0.18f, 0.16f, 1f);
        private static readonly Color ColWall     = new(0.35f, 0.30f, 0.25f, 1f);
        private static readonly Color ColWallTop  = new(0.45f, 0.38f, 0.30f, 1f);
        private static readonly Color ColDoor     = new(0.6f, 0.5f, 0.2f, 1f);
        private static readonly Color ColPillar   = new(0.4f, 0.35f, 0.3f, 1f);

        // ================================================================
        //  Sprite Cache
        // ================================================================

        private static Sprite _whiteSquare;

        /// <summary>
        /// 1x1 흰색 스프라이트를 가져오거나 생성한다.
        /// </summary>
        private static Sprite GetWhiteSquare()
        {
            if (_whiteSquare != null) return _whiteSquare;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            _whiteSquare = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            _whiteSquare.name = "WhiteSquare";

            return _whiteSquare;
        }

        // ================================================================
        //  GenerateRoom
        // ================================================================

        /// <summary>
        /// 지정 크기와 타입으로 방을 생성한다.
        /// </summary>
        /// <param name="width">방의 가로 타일 수</param>
        /// <param name="height">방의 세로 타일 수</param>
        /// <param name="type">방 타입</param>
        /// <param name="centerPosition">방 중앙의 월드 좌표</param>
        /// <returns>생성된 방의 루트 GameObject</returns>
        public static GameObject GenerateRoom(int width, int height, RoomType type,
            Vector2 centerPosition = default)
        {
            var roomGo = new GameObject($"Room_{type}_{width}x{height}");
            roomGo.transform.position = (Vector3)centerPosition;

            // 오프셋 계산 (좌하단 기준)
            float offsetX = -width * 0.5f + 0.5f;
            float offsetY = -height * 0.5f + 0.5f;

            // 바닥 타일
            var floorParent = new GameObject("Floors");
            floorParent.transform.SetParent(roomGo.transform, false);
            LayFloorTiles(floorParent.transform, width, height, offsetX, offsetY);

            // 벽 타일
            var wallParent = new GameObject("Walls");
            wallParent.transform.SetParent(roomGo.transform, false);
            var wallColliders = LayWallTiles(wallParent.transform, width, height, offsetX, offsetY);

            // 문 배치
            var doors = new List<GameObject>();
            doors.Add(CreateDoor(roomGo.transform, new Vector2(0, height * 0.5f), DoorDirection.North));
            doors.Add(CreateDoor(roomGo.transform, new Vector2(width * 0.5f, 0), DoorDirection.East));

            // Room 컴포넌트 추가 (선택적)
            var roomComp = roomGo.AddComponent<Room>();
            // Room 필드 설정
            SetPrivateField(roomComp, "roomType", type);
            SetPrivateField(roomComp, "lockDoorsOnEnter", type == RoomType.Combat || type == RoomType.Boss);
            SetPrivateField(roomComp, "doors", doors.ToArray());

            // 방 트리거 콜라이더 (플레이어 진입 감지)
            var triggerGo = new GameObject("RoomTrigger");
            triggerGo.transform.SetParent(roomGo.transform, false);
            var triggerCol = triggerGo.AddComponent<BoxCollider2D>();
            triggerCol.size = new Vector2(width - 2, height - 2);
            triggerCol.isTrigger = true;

            // 타입별 내부 배치
            switch (type)
            {
                case RoomType.Combat:
                    PlaceCombatEnemies(roomGo.transform, width, height, 3, 5);
                    break;
                case RoomType.Boss:
                    PlaceBossContent(roomGo.transform, width, height);
                    break;
                case RoomType.Treasure:
                    PlaceTreasure(roomGo.transform);
                    break;
                case RoomType.Shop:
                    PlaceShopNPC(roomGo.transform);
                    break;
            }

            return roomGo;
        }

        // ================================================================
        //  GenerateBossRoom
        // ================================================================

        /// <summary>
        /// 보스방을 생성한다. 더 큰 방 + 기둥 장애물 + 보스 1마리.
        /// </summary>
        public static GameObject GenerateBossRoom(Vector2 centerPosition = default)
        {
            int width = 26;
            int height = 20;

            var roomGo = GenerateRoom(width, height, RoomType.Boss, centerPosition);

            // 기둥 장애물 배치
            PlacePillars(roomGo.transform, width, height);

            return roomGo;
        }

        // ================================================================
        //  Test Stage (SceneBootstrapper용)
        // ================================================================

        /// <summary>
        /// 간단한 테스트 스테이지를 생성한다.
        /// 바닥 + 벽 + 문 + 적 배치.
        /// </summary>
        /// <param name="playerSpawnPos">플레이어 스폰 위치 (out)</param>
        /// <param name="enemySpawnPositions">적 스폰 위치 리스트 (out)</param>
        /// <param name="doorPositions">문 위치 리스트 (out)</param>
        /// <returns>스테이지 루트 GameObject</returns>
        public static GameObject GenerateTestStage(
            out Vector2 playerSpawnPos,
            out List<Vector2> enemySpawnPositions,
            out List<Vector2> doorPositions)
        {
            int width = 20;
            int height = 15;

            var stageGo = new GameObject("TestStage");
            stageGo.transform.position = Vector3.zero;

            float offsetX = -width * 0.5f + 0.5f;
            float offsetY = -height * 0.5f + 0.5f;

            // 바닥
            var floorParent = new GameObject("Floors");
            floorParent.transform.SetParent(stageGo.transform, false);
            LayFloorTiles(floorParent.transform, width, height, offsetX, offsetY);

            // 벽
            var wallParent = new GameObject("Walls");
            wallParent.transform.SetParent(stageGo.transform, false);
            LayWallTiles(wallParent.transform, width, height, offsetX, offsetY);

            // 문
            doorPositions = new List<Vector2>();
            var doorN = CreateDoor(stageGo.transform,
                new Vector2(0, height * 0.5f - 0.5f), DoorDirection.North);
            doorPositions.Add(doorN.transform.localPosition);

            var doorE = CreateDoor(stageGo.transform,
                new Vector2(width * 0.5f - 0.5f, 0), DoorDirection.East);
            doorPositions.Add(doorE.transform.localPosition);

            // 플레이어 스폰 위치 (중앙)
            playerSpawnPos = Vector2.zero;

            // 적 스폰 위치 (랜덤)
            enemySpawnPositions = new List<Vector2>();
            int enemyCount = Random.Range(3, 6);
            for (int i = 0; i < enemyCount; i++)
            {
                float ex = Random.Range(-width * 0.3f, width * 0.3f);
                float ey = Random.Range(-height * 0.3f, height * 0.3f);

                // 플레이어 스폰 근처 제외
                Vector2 pos = new Vector2(ex, ey);
                if (pos.magnitude < 2f)
                    pos = pos.normalized * 3f;

                enemySpawnPositions.Add(pos);
            }

            return stageGo;
        }

        // ================================================================
        //  Floor Tiles
        // ================================================================

        private static void LayFloorTiles(Transform parent, int width, int height,
            float offsetX, float offsetY)
        {
            Sprite sprite = GetWhiteSquare();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var tileGo = new GameObject($"Floor_{x}_{y}");
                    tileGo.transform.SetParent(parent, false);
                    tileGo.transform.localPosition = new Vector3(offsetX + x, offsetY + y, 0.1f);

                    var sr = tileGo.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.color = (x + y) % 2 == 0 ? ColFloor1 : ColFloor2;
                    sr.sortingOrder = -10;
                }
            }
        }

        // ================================================================
        //  Wall Tiles
        // ================================================================

        private static List<GameObject> LayWallTiles(Transform parent, int width, int height,
            float offsetX, float offsetY)
        {
            Sprite sprite = GetWhiteSquare();
            var walls = new List<GameObject>();

            for (int x = -1; x <= width; x++)
            {
                for (int y = -1; y <= height; y++)
                {
                    // 내부는 스킵
                    bool isEdge = (x == -1 || x == width || y == -1 || y == height);
                    if (!isEdge) continue;

                    // 문 위치는 스킵 (상단 중앙, 우측 중앙)
                    bool isDoorN = (x == width / 2 || x == width / 2 - 1) && y == height;
                    bool isDoorE = (y == height / 2 || y == height / 2 - 1) && x == width;
                    if (isDoorN || isDoorE) continue;

                    var wallGo = new GameObject($"Wall_{x}_{y}");
                    wallGo.transform.SetParent(parent, false);
                    wallGo.transform.localPosition = new Vector3(offsetX + x, offsetY + y, 0f);
                    wallGo.layer = LayerMask.NameToLayer("Default");

                    var sr = wallGo.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.color = (y == height) ? ColWallTop : ColWall;
                    sr.sortingOrder = (y == height) ? 5 : 0;

                    var col = wallGo.AddComponent<BoxCollider2D>();
                    col.size = Vector2.one;

                    walls.Add(wallGo);
                }
            }

            return walls;
        }

        // ================================================================
        //  Doors
        // ================================================================

        private enum DoorDirection { North, South, East, West }

        private static GameObject CreateDoor(Transform parent, Vector2 localPos,
            DoorDirection direction)
        {
            var doorGo = new GameObject($"Door_{direction}");
            doorGo.transform.SetParent(parent, false);
            doorGo.transform.localPosition = (Vector3)localPos;

            var sr = doorGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSquare();
            sr.color = ColDoor;
            sr.sortingOrder = 1;

            // 문 크기 (방향에 따라)
            bool horizontal = (direction == DoorDirection.North || direction == DoorDirection.South);
            doorGo.transform.localScale = horizontal
                ? new Vector3(2f, 1f, 1f)
                : new Vector3(1f, 2f, 1f);

            // 문 트리거 (진입 감지)
            var triggerCol = doorGo.AddComponent<BoxCollider2D>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector2(1.5f, 1.5f);

            return doorGo;
        }

        // ================================================================
        //  Combat Room Contents
        // ================================================================

        private static void PlaceCombatEnemies(Transform parent, int width, int height,
            int minCount, int maxCount)
        {
            var spawnParent = new GameObject("EnemySpawnPoints");
            spawnParent.transform.SetParent(parent, false);

            int count = Random.Range(minCount, maxCount + 1);
            for (int i = 0; i < count; i++)
            {
                var spawnPoint = new GameObject($"SpawnPoint_{i}");
                spawnPoint.transform.SetParent(spawnParent.transform, false);

                float x = Random.Range(-width * 0.35f, width * 0.35f);
                float y = Random.Range(-height * 0.35f, height * 0.35f);

                // 중앙 근처 제외
                Vector2 pos = new Vector2(x, y);
                if (pos.magnitude < 2f)
                    pos = pos.normalized * 2.5f;

                spawnPoint.transform.localPosition = (Vector3)pos;
            }
        }

        private static void PlaceBossContent(Transform parent, int width, int height)
        {
            // 보스 스폰 포인트 (방 중앙 약간 위)
            var bossSpawn = new GameObject("BossSpawnPoint");
            bossSpawn.transform.SetParent(parent, false);
            bossSpawn.transform.localPosition = new Vector3(0, height * 0.15f, 0);
        }

        // ================================================================
        //  Boss Room Pillars
        // ================================================================

        private static void PlacePillars(Transform parent, int width, int height)
        {
            Sprite sprite = GetWhiteSquare();

            // 4개의 기둥을 대칭으로 배치
            float pillarOffsetX = width * 0.25f;
            float pillarOffsetY = height * 0.25f;

            Vector2[] pillarPositions = new Vector2[]
            {
                new(-pillarOffsetX, pillarOffsetY),
                new(pillarOffsetX, pillarOffsetY),
                new(-pillarOffsetX, -pillarOffsetY),
                new(pillarOffsetX, -pillarOffsetY)
            };

            var pillarParent = new GameObject("Pillars");
            pillarParent.transform.SetParent(parent, false);

            for (int i = 0; i < pillarPositions.Length; i++)
            {
                var pillarGo = new GameObject($"Pillar_{i}");
                pillarGo.transform.SetParent(pillarParent.transform, false);
                pillarGo.transform.localPosition = (Vector3)pillarPositions[i];
                pillarGo.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

                var sr = pillarGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = ColPillar;
                sr.sortingOrder = 2;

                var col = pillarGo.AddComponent<BoxCollider2D>();
                col.size = Vector2.one;
            }
        }

        // ================================================================
        //  Treasure / Shop
        // ================================================================

        private static void PlaceTreasure(Transform parent)
        {
            var chestGo = new GameObject("TreasureChest_Placeholder");
            chestGo.transform.SetParent(parent, false);
            chestGo.transform.localPosition = Vector3.zero;

            var sr = chestGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSquare();
            sr.color = new Color(0.8f, 0.65f, 0.1f, 1f); // 금색
            sr.sortingOrder = 3;
            chestGo.transform.localScale = new Vector3(1.2f, 0.9f, 1f);

            var col = chestGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        private static void PlaceShopNPC(Transform parent)
        {
            var npcGo = new GameObject("ShopNPC_Placeholder");
            npcGo.transform.SetParent(parent, false);
            npcGo.transform.localPosition = new Vector3(0, 1f, 0);

            var sr = npcGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSquare();
            sr.color = new Color(0.3f, 0.7f, 0.9f, 1f); // 파란색
            sr.sortingOrder = 3;
            npcGo.transform.localScale = new Vector3(0.8f, 1.2f, 1f);

            var col = npcGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        // ================================================================
        //  Create Enemy GameObject (프리팹 없이)
        // ================================================================

        /// <summary>
        /// EnemyData 없이 간단한 적 오브젝트를 생성한다.
        /// </summary>
        /// <param name="enemyType">"slime" 또는 "skeleton"</param>
        /// <param name="position">월드 위치</param>
        /// <returns>생성된 적 GameObject</returns>
        public static GameObject CreateSimpleEnemy(string enemyType, Vector2 position)
        {
            var go = new GameObject($"Enemy_{enemyType}");
            go.transform.position = (Vector3)position;
            go.tag = "Enemy";
            go.layer = LayerMask.NameToLayer("Enemy") >= 0 ? LayerMask.NameToLayer("Enemy") : 0;

            var sr = go.AddComponent<SpriteRenderer>();
            string spriteKey = $"enemy_{enemyType.ToLower()}";
            var sprite = SpriteFactory.GetSprite(spriteKey);
            sr.sprite = sprite != null ? sprite : GetWhiteSquare();
            sr.sortingOrder = 5;
            sr.sortingLayerName = "Enemy";

            if (sprite == null)
            {
                switch (enemyType.ToLower())
                {
                    case "slime":
                        sr.color = new Color(0.2f, 0.8f, 0.3f, 1f);
                        go.transform.localScale = new Vector3(0.8f, 0.6f, 1f);
                        break;
                    case "skeleton":
                        sr.color = new Color(0.85f, 0.85f, 0.8f, 1f);
                        go.transform.localScale = new Vector3(0.7f, 1.1f, 1f);
                        break;
                    default:
                        sr.color = new Color(0.6f, 0.2f, 0.2f, 1f);
                        break;
                }
            }

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.35f;

            go.AddComponent<SoulCraft.Enemy.EnemyHPBar>();
            go.AddComponent<SoulCraft.Enemy.EnemyHitReaction>();

            return go;
        }

        /// <summary>
        /// 간단한 보스 오브젝트를 생성한다.
        /// </summary>
        public static GameObject CreateSimpleBoss(Vector2 position)
        {
            var go = new GameObject("Boss_Placeholder");
            go.transform.position = (Vector3)position;
            go.tag = "Enemy";
            go.layer = LayerMask.NameToLayer("Enemy") >= 0 ? LayerMask.NameToLayer("Enemy") : 0;

            var sr = go.AddComponent<SpriteRenderer>();
            var sprite = SpriteFactory.GetSprite("boss_elder_grove");
            sr.sprite = sprite != null ? sprite : GetWhiteSquare();
            if (sprite == null) sr.color = new Color(0.7f, 0.1f, 0.1f, 1f);
            sr.sortingOrder = 5;
            sr.sortingLayerName = "Enemy";
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.mass = 5f;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.8f;

            return go;
        }

        // ================================================================
        //  Player Creation (프리팹 없이)
        // ================================================================

        /// <summary>
        /// 프리팹 없이 간단한 플레이어 오브젝트를 생성한다.
        /// </summary>
        public static GameObject CreateSimplePlayer(Vector2 position)
        {
            var go = new GameObject("Player");
            go.transform.position = (Vector3)position;
            go.tag = "Player";
            go.layer = LayerMask.NameToLayer("Player") >= 0 ? LayerMask.NameToLayer("Player") : 0;

            // SpriteFactory 픽셀아트 사용
            var sr = go.AddComponent<SpriteRenderer>();
            var sprite = SpriteFactory.GetSprite("player_idle");
            sr.sprite = sprite != null ? sprite : GetWhiteSquare();
            if (sprite == null) sr.color = new Color(0.3f, 0.6f, 1f, 1f);
            sr.sortingOrder = 10;
            sr.sortingLayerName = "Player";

            // Rigidbody2D
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            // Collider
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.5f, 0.7f);

            // Animator (PlayerAnimator가 RequireComponent로 요구)
            go.AddComponent<Animator>();

            // 필수 컴포넌트 추가
            go.AddComponent<SoulCraft.Player.PlayerStats>();
            go.AddComponent<SoulCraft.Player.PlayerCombat>();
            go.AddComponent<SoulCraft.Player.PlayerAnimator>();
            go.AddComponent<SoulCraft.Player.PlayerController>();

            return go;
        }

        // ================================================================
        //  Reflection Helper
        // ================================================================

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;

            var type = target.GetType();
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);

            if (field != null)
            {
                field.SetValue(target, value);
            }
            else
            {
                var baseType = type.BaseType;
                while (baseType != null)
                {
                    field = baseType.GetField(fieldName,
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public);
                    if (field != null)
                    {
                        field.SetValue(target, value);
                        return;
                    }
                    baseType = baseType.BaseType;
                }
            }
        }
    }
}
