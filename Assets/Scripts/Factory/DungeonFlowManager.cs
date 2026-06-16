using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulCraft.Core;
using SoulCraft.Enemy;
using SoulCraft.World;

namespace SoulCraft.Factory
{
    /// <summary>
    /// 여러 방을 연결하여 던전을 구성하는 시스템.
    /// 층(Floor)당 5~7개 방으로 구성되며, 방 전환은 문 통과 트리거로 처리한다.
    /// SceneBootstrapper에서 생성하여 사용한다.
    /// </summary>
    public class DungeonFlowManager : MonoBehaviour
    {
        // ================================================================
        //  Constants
        // ================================================================

        private const int MinRoomsPerFloor = 5;
        private const int MaxRoomsPerFloor = 7;

        // 방 크기 상수
        private const int CombatRoomWidth = 18;
        private const int CombatRoomHeight = 14;
        private const int TreasureRoomWidth = 12;
        private const int TreasureRoomHeight = 10;
        private const int BossRoomWidth = 26;
        private const int BossRoomHeight = 20;

        // ================================================================
        //  Stage Color Themes
        // ================================================================

        /// <summary>
        /// 스테이지별 바닥 타일 색상 테마.
        /// </summary>
        private struct StageTheme
        {
            public string themeName;
            public Color floorColor1;
            public Color floorColor2;
            public Color wallColor;
            public Color wallTopColor;
        }

        private static readonly StageTheme[] StageThemes = new StageTheme[]
        {
            // 스테이지1: 잊혀진 숲 (짙은 녹색/갈색)
            new StageTheme
            {
                themeName = "잊혀진 숲",
                floorColor1 = new Color(0.10f, 0.20f, 0.08f, 1f),
                floorColor2 = new Color(0.14f, 0.18f, 0.10f, 1f),
                wallColor   = new Color(0.25f, 0.18f, 0.10f, 1f),
                wallTopColor= new Color(0.18f, 0.28f, 0.12f, 1f)
            },
            // 스테이지2: 붉은 광산 (짙은 빨강/검정)
            new StageTheme
            {
                themeName = "붉은 광산",
                floorColor1 = new Color(0.22f, 0.08f, 0.06f, 1f),
                floorColor2 = new Color(0.18f, 0.06f, 0.04f, 1f),
                wallColor   = new Color(0.12f, 0.08f, 0.08f, 1f),
                wallTopColor= new Color(0.30f, 0.10f, 0.08f, 1f)
            },
            // 스테이지3: 얼어붙은 성채 (하늘색/흰색)
            new StageTheme
            {
                themeName = "얼어붙은 성채",
                floorColor1 = new Color(0.55f, 0.72f, 0.82f, 1f),
                floorColor2 = new Color(0.60f, 0.78f, 0.88f, 1f),
                wallColor   = new Color(0.75f, 0.80f, 0.85f, 1f),
                wallTopColor= new Color(0.85f, 0.90f, 0.95f, 1f)
            }
        };

        // ================================================================
        //  Runtime State
        // ================================================================

        private int _currentStageIndex;
        private int _currentFloorIndex;
        private int _currentRoomIndex;

        private List<GameObject> _floorRooms = new List<GameObject>();
        private List<RoomType> _roomSequence = new List<RoomType>();
        private List<List<GameObject>> _roomEnemies = new List<List<GameObject>>();
        private List<GameObject> _roomDoors = new List<GameObject>();

        private GameObject _dungeonRoot;
        private Transform _playerTransform;

        private bool _isBossDefeated;

        // ================================================================
        //  Sprite Cache
        // ================================================================

        private static Sprite _whiteSquare;

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
            _whiteSquare.name = "WhiteSquare_Dungeon";
            return _whiteSquare;
        }

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>
        /// 던전을 시작한다. 지정된 스테이지 인덱스(1-based)로 1층부터 생성.
        /// </summary>
        public void StartDungeon(int stageIndex)
        {
            _currentStageIndex = Mathf.Clamp(stageIndex, 1, StageThemes.Length) - 1;
            _currentFloorIndex = 0;
            _isBossDefeated = false;

            Debug.Log($"[DungeonFlowManager] 던전 시작: 스테이지 {stageIndex} ({GetCurrentTheme().themeName})");

            BuildFloor();
        }

        /// <summary>
        /// 플레이어 스폰 위치를 반환한다. 첫 번째 방의 중앙 하단.
        /// </summary>
        public Vector2 GetPlayerSpawnPosition()
        {
            return new Vector2(0, -CombatRoomHeight * 0.3f);
        }

        /// <summary>
        /// 현재 층의 적 스폰 위치 목록을 반환한다 (첫 번째 전투방 기준).
        /// </summary>
        public List<Vector2> GetInitialEnemySpawnPositions()
        {
            return GenerateEnemyPositions(0, CombatRoomWidth, CombatRoomHeight);
        }

        // ================================================================
        //  Floor Building
        // ================================================================

        private StageTheme GetCurrentTheme()
        {
            return StageThemes[Mathf.Clamp(_currentStageIndex, 0, StageThemes.Length - 1)];
        }

        private void BuildFloor()
        {
            ClearCurrentFloor();

            _dungeonRoot = new GameObject($"Dungeon_Stage{_currentStageIndex + 1}_Floor{_currentFloorIndex + 1}");
            _currentRoomIndex = 0;

            // 방 시퀀스 생성
            _roomSequence = GenerateRoomSequence();

            Debug.Log($"[DungeonFlowManager] 층 {_currentFloorIndex + 1} 생성: {_roomSequence.Count}개 방");

            // 모든 방 생성 (비활성 상태)
            for (int i = 0; i < _roomSequence.Count; i++)
            {
                var roomGo = CreateRoom(i, _roomSequence[i]);
                _floorRooms.Add(roomGo);
                _roomEnemies.Add(new List<GameObject>());

                // 첫 번째 방만 활성화
                roomGo.SetActive(i == 0);
            }

            // 첫 번째 방의 적 스폰 및 문 설정
            ActivateRoom(0);
        }

        /// <summary>
        /// 층당 5~7개 방의 시퀀스를 생성한다.
        /// 패턴: [전투] -> [전투] -> [보물 or 상점] -> [전투] -> [전투] -> [보스]
        /// </summary>
        private List<RoomType> GenerateRoomSequence()
        {
            var sequence = new List<RoomType>();
            int roomCount = UnityEngine.Random.Range(MinRoomsPerFloor, MaxRoomsPerFloor + 1);

            // 기본 패턴
            sequence.Add(RoomType.Combat);   // 0: 전투
            sequence.Add(RoomType.Combat);   // 1: 전투

            // 보물 or 상점
            sequence.Add(UnityEngine.Random.value > 0.5f ? RoomType.Treasure : RoomType.Shop);

            // 추가 전투방 (roomCount에 따라 1~3개)
            int extraCombatRooms = roomCount - 4; // 보스방 1개 빼고 남은 전투방 수
            for (int i = 0; i < extraCombatRooms; i++)
            {
                sequence.Add(RoomType.Combat);
            }

            // 마지막: 보스방
            sequence.Add(RoomType.Boss);

            return sequence;
        }

        // ================================================================
        //  Room Creation
        // ================================================================

        private GameObject CreateRoom(int roomIndex, RoomType type)
        {
            int width, height;
            GetRoomSize(type, out width, out height);

            StageTheme theme = GetCurrentTheme();

            var roomGo = new GameObject($"Room_{roomIndex}_{type}");
            roomGo.transform.SetParent(_dungeonRoot.transform, false);
            roomGo.transform.localPosition = Vector3.zero;

            // 바닥 타일
            var floorParent = new GameObject("Floors");
            floorParent.transform.SetParent(roomGo.transform, false);
            LayFloorTiles(floorParent.transform, width, height, theme);

            // 벽 타일
            var wallParent = new GameObject("Walls");
            wallParent.transform.SetParent(roomGo.transform, false);
            LayWallTiles(wallParent.transform, width, height, theme);

            // 장애물 배치 (전투방과 보스방)
            if (type == RoomType.Combat || type == RoomType.Boss)
            {
                PlaceObstacles(roomGo.transform, width, height);
            }

            // 문 생성
            var doorGo = CreateRoomDoor(roomGo.transform, width, height);
            _roomDoors.Add(doorGo);

            // 타입별 콘텐츠
            switch (type)
            {
                case RoomType.Treasure:
                    PlaceTreasureChest(roomGo.transform);
                    break;
                case RoomType.Shop:
                    PlaceShopNPC(roomGo.transform);
                    break;
            }

            // Room 컴포넌트 추가
            var roomComp = roomGo.AddComponent<Room>();
            SetPrivateField(roomComp, "roomType", type);
            SetPrivateField(roomComp, "lockDoorsOnEnter", type == RoomType.Combat || type == RoomType.Boss);

            return roomGo;
        }

        private void GetRoomSize(RoomType type, out int width, out int height)
        {
            switch (type)
            {
                case RoomType.Boss:
                    width = BossRoomWidth;
                    height = BossRoomHeight;
                    break;
                case RoomType.Treasure:
                case RoomType.Shop:
                    width = TreasureRoomWidth;
                    height = TreasureRoomHeight;
                    break;
                default:
                    width = CombatRoomWidth;
                    height = CombatRoomHeight;
                    break;
            }
        }

        // ================================================================
        //  Floor Tiles
        // ================================================================

        private void LayFloorTiles(Transform parent, int width, int height, StageTheme theme)
        {
            Sprite sprite = GetWhiteSquare();
            float offsetX = -width * 0.5f + 0.5f;
            float offsetY = -height * 0.5f + 0.5f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var tileGo = new GameObject($"Floor_{x}_{y}");
                    tileGo.transform.SetParent(parent, false);
                    tileGo.transform.localPosition = new Vector3(offsetX + x, offsetY + y, 0.1f);

                    var sr = tileGo.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.color = (x + y) % 2 == 0 ? theme.floorColor1 : theme.floorColor2;
                    sr.sortingOrder = -10;
                }
            }
        }

        // ================================================================
        //  Wall Tiles
        // ================================================================

        private void LayWallTiles(Transform parent, int width, int height, StageTheme theme)
        {
            Sprite sprite = GetWhiteSquare();
            float offsetX = -width * 0.5f + 0.5f;
            float offsetY = -height * 0.5f + 0.5f;

            for (int x = -1; x <= width; x++)
            {
                for (int y = -1; y <= height; y++)
                {
                    bool isEdge = (x == -1 || x == width || y == -1 || y == height);
                    if (!isEdge) continue;

                    // 상단 중앙에 문 자리 확보 (2타일)
                    bool isDoor = (x == width / 2 || x == width / 2 - 1) && y == height;
                    if (isDoor) continue;

                    var wallGo = new GameObject($"Wall_{x}_{y}");
                    wallGo.transform.SetParent(parent, false);
                    wallGo.transform.localPosition = new Vector3(offsetX + x, offsetY + y, 0f);
                    wallGo.layer = LayerMask.NameToLayer("Default");

                    var sr = wallGo.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.color = (y == height) ? theme.wallTopColor : theme.wallColor;
                    sr.sortingOrder = (y == height) ? 5 : 0;

                    var col = wallGo.AddComponent<BoxCollider2D>();
                    col.size = Vector2.one;
                }
            }
        }

        // ================================================================
        //  Obstacles
        // ================================================================

        /// <summary>
        /// 랜덤 장애물 배치 (돌기둥 2~4개, 물웅덩이 등)
        /// </summary>
        private void PlaceObstacles(Transform parent, int width, int height)
        {
            Sprite sprite = GetWhiteSquare();
            int obstacleCount = UnityEngine.Random.Range(2, 5);

            var obstacleParent = new GameObject("Obstacles");
            obstacleParent.transform.SetParent(parent, false);

            for (int i = 0; i < obstacleCount; i++)
            {
                float x = UnityEngine.Random.Range(-width * 0.3f, width * 0.3f);
                float y = UnityEngine.Random.Range(-height * 0.3f, height * 0.3f);

                // 중앙 근처 제외 (플레이어 스폰 지점)
                Vector2 pos = new Vector2(x, y);
                if (pos.magnitude < 2.5f)
                    pos = pos.normalized * 3f;

                bool isPuddle = UnityEngine.Random.value > 0.6f;

                var obstGo = new GameObject(isPuddle ? $"Puddle_{i}" : $"Pillar_{i}");
                obstGo.transform.SetParent(obstacleParent.transform, false);
                obstGo.transform.localPosition = (Vector3)pos;

                var sr = obstGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 2;

                if (isPuddle)
                {
                    // 물웅덩이: 반투명 파란색, 납작한 형태
                    sr.color = new Color(0.15f, 0.25f, 0.45f, 0.6f);
                    obstGo.transform.localScale = new Vector3(1.8f, 0.8f, 1f);
                    // 물웅덩이는 통행 차단하지 않음 (시각적 요소만)
                }
                else
                {
                    // 돌기둥: BoxCollider2D로 통행 차단
                    StageTheme theme = GetCurrentTheme();
                    sr.color = new Color(
                        theme.wallColor.r * 1.2f,
                        theme.wallColor.g * 1.2f,
                        theme.wallColor.b * 1.2f,
                        1f
                    );
                    obstGo.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

                    var col = obstGo.AddComponent<BoxCollider2D>();
                    col.size = Vector2.one;
                }
            }
        }

        // ================================================================
        //  Door
        // ================================================================

        /// <summary>
        /// 방 상단 중앙에 문을 생성한다. 문은 DoorTrigger 컴포넌트를 가진다.
        /// </summary>
        private GameObject CreateRoomDoor(Transform parent, int width, int height)
        {
            var doorGo = new GameObject("Door_North");
            doorGo.transform.SetParent(parent, false);
            doorGo.transform.localPosition = new Vector3(0, height * 0.5f - 0.5f, 0);

            var sr = doorGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSquare();
            sr.color = new Color(0.5f, 0.5f, 0.5f, 1f); // 초기 회색 (잠김)
            sr.sortingOrder = 6;
            doorGo.transform.localScale = new Vector3(2f, 1f, 1f);

            // 트리거 콜라이더
            var triggerCol = doorGo.AddComponent<BoxCollider2D>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector2(1.5f, 1.5f);

            // DoorTrigger 컴포넌트 추가
            var doorTrigger = doorGo.AddComponent<DungeonDoorTrigger>();
            doorTrigger.Initialize(this);

            // 초기 상태: 비활성 (적을 모두 처치해야 활성화)
            doorGo.SetActive(false);

            return doorGo;
        }

        // ================================================================
        //  Treasure / Shop
        // ================================================================

        private void PlaceTreasureChest(Transform parent)
        {
            var chestGo = new GameObject("TreasureChest");
            chestGo.transform.SetParent(parent, false);
            chestGo.transform.localPosition = Vector3.zero;

            var sr = chestGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSquare();
            sr.color = new Color(0.8f, 0.65f, 0.1f, 1f); // 금색
            sr.sortingOrder = 3;
            chestGo.transform.localScale = new Vector3(1.2f, 0.9f, 1f);

            var col = chestGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            // 보물상자 상호작용 컴포넌트
            var chest = chestGo.AddComponent<TreasureChestInteraction>();
        }

        private void PlaceShopNPC(Transform parent)
        {
            var npcGo = new GameObject("ShopNPC");
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
        //  Enemy Spawning
        // ================================================================

        /// <summary>
        /// 지정된 방 인덱스에 적을 스폰한다.
        /// </summary>
        private void SpawnEnemiesForRoom(int roomIndex)
        {
            RoomType type = _roomSequence[roomIndex];
            if (type != RoomType.Combat && type != RoomType.Boss) return;

            int width, height;
            GetRoomSize(type, out width, out height);

            List<Vector2> positions = GenerateEnemyPositions(roomIndex, width, height);
            var enemies = new List<GameObject>();

            if (type == RoomType.Boss)
            {
                // 보스 1마리
                var bossGo = SimpleRoom.CreateSimpleBoss(Vector2.zero + Vector2.up * (height * 0.15f));
                bossGo.transform.SetParent(_floorRooms[roomIndex].transform, false);

                // EnemyData 생성
                var bossData = ScriptableObject.CreateInstance<EnemyData>();
                bossData.enemyId = $"boss_stage{_currentStageIndex + 1}";
                bossData.enemyName = GetBossName();
                bossData.isBoss = true;
                bossData.maxHp = 300 + _currentStageIndex * 200;
                bossData.attack = 20 + _currentStageIndex * 10;
                bossData.defense = 5 + _currentStageIndex * 3;
                bossData.speed = 2f;
                bossData.detectionRange = 10f;
                bossData.attackRange = 2f;
                bossData.expReward = 100 + _currentStageIndex * 50;
                bossData.goldReward = 50 + _currentStageIndex * 30;

                var bossBase = bossGo.GetComponent<EnemyBase>();
                if (bossBase == null)
                    bossBase = bossGo.AddComponent<EnemyBase>();
                SetPrivateField(bossBase, "data", bossData);

                enemies.Add(bossGo);
            }
            else
            {
                // 전투방: 적 3~6마리 (방 번호가 높을수록 많고 강함)
                int enemyCount = Mathf.Clamp(3 + roomIndex, 3, 6);
                string[] enemyTypes = { "slime", "skeleton" };

                for (int i = 0; i < positions.Count && i < enemyCount; i++)
                {
                    // 방 인덱스가 높을수록 강한 적 비율 증가
                    string type2 = (roomIndex >= 3 || UnityEngine.Random.value > 0.5f)
                        ? "skeleton" : "slime";

                    var enemyGo = SimpleRoom.CreateSimpleEnemy(type2, positions[i]);
                    enemyGo.transform.SetParent(_floorRooms[roomIndex].transform, false);

                    // 방 인덱스에 따른 스탯 스케일링
                    float scaleFactor = 1f + roomIndex * 0.2f;

                    var enemyData = ScriptableObject.CreateInstance<EnemyData>();
                    enemyData.enemyId = $"{type2}_{roomIndex}_{i}";
                    enemyData.enemyName = type2 == "slime" ? "슬라임" : "해골 전사";
                    enemyData.isBoss = false;
                    enemyData.maxHp = Mathf.RoundToInt((type2 == "slime" ? 30 : 50) * scaleFactor);
                    enemyData.attack = Mathf.RoundToInt((type2 == "slime" ? 5 : 10) * scaleFactor);
                    enemyData.defense = Mathf.RoundToInt((type2 == "slime" ? 1 : 3) * scaleFactor);
                    enemyData.speed = type2 == "slime" ? 1.5f : 2.5f;
                    enemyData.detectionRange = 5f;
                    enemyData.attackRange = 1.2f;
                    enemyData.expReward = Mathf.RoundToInt((type2 == "slime" ? 10 : 20) * scaleFactor);
                    enemyData.goldReward = Mathf.RoundToInt((type2 == "slime" ? 5 : 12) * scaleFactor);

                    var enemyBase = enemyGo.GetComponent<EnemyBase>();
                    if (enemyBase == null)
                        enemyBase = enemyGo.AddComponent<EnemyBase>();
                    SetPrivateField(enemyBase, "data", enemyData);

                    enemies.Add(enemyGo);
                }
            }

            _roomEnemies[roomIndex] = enemies;
        }

        private List<Vector2> GenerateEnemyPositions(int roomIndex, int width, int height)
        {
            var positions = new List<Vector2>();
            int count = Mathf.Clamp(3 + roomIndex, 3, 6);

            for (int i = 0; i < count; i++)
            {
                float x = UnityEngine.Random.Range(-width * 0.3f, width * 0.3f);
                float y = UnityEngine.Random.Range(-height * 0.3f, height * 0.3f);

                Vector2 pos = new Vector2(x, y);
                if (pos.magnitude < 2f)
                    pos = pos.normalized * 3f;

                positions.Add(pos);
            }

            return positions;
        }

        private string GetBossName()
        {
            switch (_currentStageIndex)
            {
                case 0: return "숲의 수호자";
                case 1: return "불꽃 군주";
                case 2: return "서리 여왕";
                default: return "어둠의 지배자";
            }
        }

        // ================================================================
        //  Room Activation / Transition
        // ================================================================

        /// <summary>
        /// 방을 활성화하고 적을 스폰한다.
        /// </summary>
        private void ActivateRoom(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= _floorRooms.Count) return;

            _currentRoomIndex = roomIndex;

            // 현재 방 활성화
            _floorRooms[roomIndex].SetActive(true);

            // 플레이어 위치를 방 하단 중앙으로 이동
            if (_playerTransform != null && roomIndex > 0)
            {
                int width, height;
                GetRoomSize(_roomSequence[roomIndex], out width, out height);
                _playerTransform.position = new Vector3(0, -height * 0.35f, 0);
            }

            // 전투방/보스방이면 적 스폰 및 문 잠금
            RoomType type = _roomSequence[roomIndex];
            if (type == RoomType.Combat || type == RoomType.Boss)
            {
                SpawnEnemiesForRoom(roomIndex);
                SetDoorLocked(roomIndex, true);

                // 적 사망 감시 시작
                StartCoroutine(MonitorEnemies(roomIndex));
            }
            else
            {
                // 보물방/상점방: 문 바로 활성화
                SetDoorLocked(roomIndex, false);
            }

            // 카메라 바운드 설정
            UpdateCameraBounds(roomIndex);

            Debug.Log($"[DungeonFlowManager] 방 {roomIndex} 활성화: {type}");
        }

        /// <summary>
        /// 문을 통과하면 호출. 현재 방 비활성화, 다음 방 활성화.
        /// </summary>
        public void OnDoorEntered()
        {
            int nextIndex = _currentRoomIndex + 1;

            if (nextIndex >= _floorRooms.Count)
            {
                // 모든 방 클리어 - 이 경우는 보스 클리어 후이므로
                // ShowStageClear에서 이미 처리됨
                return;
            }

            // 현재 방 비활성화
            if (_currentRoomIndex >= 0 && _currentRoomIndex < _floorRooms.Count)
            {
                _floorRooms[_currentRoomIndex].SetActive(false);
            }

            // 다음 방 활성화
            ActivateRoom(nextIndex);
        }

        // ================================================================
        //  Enemy Monitoring
        // ================================================================

        /// <summary>
        /// 해당 방의 적이 모두 처치되면 문을 활성화한다.
        /// </summary>
        private IEnumerator MonitorEnemies(int roomIndex)
        {
            while (true)
            {
                yield return new WaitForSeconds(0.3f);

                if (roomIndex != _currentRoomIndex) yield break;

                var enemies = _roomEnemies[roomIndex];
                enemies.RemoveAll(e => e == null || !e.activeInHierarchy);

                if (enemies.Count == 0)
                {
                    OnAllEnemiesDefeated(roomIndex);
                    yield break;
                }
            }
        }

        private void OnAllEnemiesDefeated(int roomIndex)
        {
            Debug.Log($"[DungeonFlowManager] 방 {roomIndex} 적 전멸! 문 활성화.");

            RoomType type = _roomSequence[roomIndex];

            if (type == RoomType.Boss)
            {
                _isBossDefeated = true;
                ShowStageClear();
            }
            else
            {
                SetDoorLocked(roomIndex, false);
            }

            // 방 클리어 이벤트 발행
            GameEventSystem.Publish(new RoomClearedEvent
            {
                RoomType = type
            });
        }

        // ================================================================
        //  Door State
        // ================================================================

        private void SetDoorLocked(int roomIndex, bool locked)
        {
            if (roomIndex < 0 || roomIndex >= _roomDoors.Count) return;
            var door = _roomDoors[roomIndex];
            if (door == null) return;

            if (locked)
            {
                door.SetActive(false);
            }
            else
            {
                door.SetActive(true);
                // 색상 변경: 회색 -> 금색
                var sr = door.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = new Color(0.85f, 0.75f, 0.2f, 1f); // 금색
                }
            }
        }

        // ================================================================
        //  Stage Clear
        // ================================================================

        private void ShowStageClear()
        {
            Debug.Log("[DungeonFlowManager] 스테이지 클리어!");

            // 문 활성화 (다음 스테이지 또는 허브 복귀용)
            SetDoorLocked(_currentRoomIndex, false);

            // 스테이지 클리어 UI 표시
            StartCoroutine(ShowStageClearUI());
        }

        private IEnumerator ShowStageClearUI()
        {
            // "스테이지 클리어" 텍스트 표시
            var clearUIGo = new GameObject("StageClearUI");
            var canvas = clearUIGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var canvasScaler = clearUIGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1080, 1920);

            // 클리어 텍스트
            var textGo = new GameObject("ClearText");
            textGo.transform.SetParent(clearUIGo.transform, false);

            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            text.text = "STAGE CLEAR!";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 60;
            text.color = new Color(1f, 0.85f, 0.2f, 1f);
            text.alignment = TextAnchor.MiddleCenter;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.6f);
            textRect.anchorMax = new Vector2(0.5f, 0.6f);
            textRect.sizeDelta = new Vector2(600, 100);

            // 선택지 버튼: 다음 스테이지
            var nextBtnGo = CreateClearButton(clearUIGo.transform, "다음 스테이지",
                new Vector2(0.5f, 0.45f), () => OnNextStageSelected());

            // 선택지 버튼: 허브 복귀
            var hubBtnGo = CreateClearButton(clearUIGo.transform, "허브 복귀",
                new Vector2(0.5f, 0.35f), () => OnReturnToHubSelected());

            // 발행
            GameEventSystem.Publish(new StageCompleteEvent
            {
                StageIndex = _currentStageIndex,
                FloorIndex = _currentFloorIndex,
                ClearTime = Time.timeSinceLevelLoad
            });

            if (GameManager.Instance != null)
                GameManager.Instance.StageClear();

            yield return null;
        }

        private GameObject CreateClearButton(Transform parent, string label,
            Vector2 anchorPos, UnityEngine.Events.UnityAction onClick)
        {
            var btnGo = new GameObject($"Btn_{label}");
            btnGo.transform.SetParent(parent, false);

            var image = btnGo.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.2f, 0.2f, 0.3f, 0.9f);

            var rect = btnGo.GetComponent<RectTransform>();
            rect.anchorMin = anchorPos;
            rect.anchorMax = anchorPos;
            rect.sizeDelta = new Vector2(300, 60);

            var btn = btnGo.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = image;
            btn.onClick.AddListener(onClick);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);

            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return btnGo;
        }

        private void OnNextStageSelected()
        {
            Debug.Log("[DungeonFlowManager] 다음 스테이지 선택!");

            // 클리어 UI 제거
            var clearUI = GameObject.Find("StageClearUI");
            if (clearUI != null) Destroy(clearUI);

            // 다음 스테이지 시작
            int nextStage = _currentStageIndex + 2; // 1-based
            if (nextStage <= StageThemes.Length)
            {
                StartDungeon(nextStage);
            }
            else
            {
                Debug.Log("[DungeonFlowManager] 모든 스테이지 클리어! 허브로 복귀.");
                OnReturnToHubSelected();
            }
        }

        private void OnReturnToHubSelected()
        {
            Debug.Log("[DungeonFlowManager] 허브 복귀 선택!");

            // 클리어 UI 제거
            var clearUI = GameObject.Find("StageClearUI");
            if (clearUI != null) Destroy(clearUI);

            if (GameManager.Instance != null)
                GameManager.Instance.ReturnToHub();
        }

        // ================================================================
        //  Camera Bounds
        // ================================================================

        private void UpdateCameraBounds(int roomIndex)
        {
            int width, height;
            GetRoomSize(_roomSequence[roomIndex], out width, out height);

            if (CameraController.Instance != null)
            {
                float halfW = width * 0.5f - 2f;
                float halfH = height * 0.5f - 2f;
                CameraController.Instance.SetBounds(
                    new Vector2(-halfW, -halfH),
                    new Vector2(halfW, halfH)
                );
            }
        }

        // ================================================================
        //  Player Reference
        // ================================================================

        /// <summary>
        /// 플레이어 Transform을 등록한다. SceneBootstrapper에서 호출.
        /// </summary>
        public void SetPlayerTransform(Transform player)
        {
            _playerTransform = player;
        }

        // ================================================================
        //  Cleanup
        // ================================================================

        private void ClearCurrentFloor()
        {
            foreach (var room in _floorRooms)
            {
                if (room != null)
                    Destroy(room);
            }

            if (_dungeonRoot != null)
                Destroy(_dungeonRoot);

            _floorRooms.Clear();
            _roomSequence.Clear();
            _roomEnemies.Clear();
            _roomDoors.Clear();
        }

        private void OnDestroy()
        {
            ClearCurrentFloor();
        }

        // ================================================================
        //  Reflection Helper
        // ================================================================

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;

            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
        }
    }

    // ================================================================
    //  DungeonDoorTrigger - 문 통과 트리거 컴포넌트
    // ================================================================

    /// <summary>
    /// 플레이어가 문에 닿으면 OnTriggerEnter2D로 감지하여
    /// DungeonFlowManager에 방 전환을 요청하는 컴포넌트.
    /// </summary>
    public class DungeonDoorTrigger : MonoBehaviour
    {
        private DungeonFlowManager _manager;

        public void Initialize(DungeonFlowManager manager)
        {
            _manager = manager;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                Debug.Log("[DungeonDoorTrigger] 플레이어가 문을 통과!");
                if (_manager != null)
                {
                    _manager.OnDoorEntered();
                }
            }
        }
    }

    // ================================================================
    //  TreasureChestInteraction - 보물상자 상호작용
    // ================================================================

    /// <summary>
    /// 보물상자를 탭하면 랜덤 아이템을 드롭하는 컴포넌트.
    /// </summary>
    public class TreasureChestInteraction : MonoBehaviour
    {
        private bool _opened;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_opened) return;
            if (!other.CompareTag("Player")) return;

            OpenChest();
        }

        private void OpenChest()
        {
            _opened = true;

            Debug.Log("[TreasureChest] 보물상자 오픈!");

            // 시각적 변화: 색상 변경
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(0.5f, 0.4f, 0.1f, 0.6f); // 열린 상자 (어두운 금색)
            }

            // 랜덤 아이템 드롭 이벤트 발행
            string[] possibleItems = {
                "item_sword", "item_armor", "item_potion_hp",
                "item_potion_mp", "item_gold"
            };
            string randomItem = possibleItems[UnityEngine.Random.Range(0, possibleItems.Length)];

            GameEventSystem.Publish(new ItemDropEvent
            {
                ItemId = randomItem,
                Position = transform.position,
                Quantity = 1
            });
        }
    }
}
