using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地牢总控制器 v7 (支持捷径与环路生成版)
/// 1. 取消了 OpenWallGap 暴力删墙，改为调用房间的 ConvertWallToArchway。
/// 2. 升级走廊生成调用，将 exitDir 和 enterDir 传入以生成完美对齐的 Z/L 型通道。
/// 3. 调整道具放置时机为：走廊规划完毕后延迟生成，从而精确避开门点区域。
/// 4. 增加 AddRandomExtraCorridors 方法，支持生成不封死的环路与捷径。
/// </summary>
public class DungeonManager : MonoBehaviour
{
    [Header("BSP参数")]
    public int     bspDepth      = 3;
    public Vector2 totalMapSize  = new Vector2(120, 120);
    public float   roomMargin    = 3f;
    public float   minRoomSize   = 8f;

    [Header("房间预制体")]
    public GameObject roomPrefab;

    [Header("走廊")]
    public CorridorBuilder corridorBuilder;

    [Header("捷径与环路配置")]
    [Tooltip("生成额外捷径的概率（0~1）。设为 0 代表传统的单条主通路，设为 0.15 代表有 15% 的临近墙面会被打通成捷径")]
    [Range(0, 1)] public float extraConnectionProbability = 0.15f; 
    [Tooltip("判定两个房间可以打通捷径的最大物理中心距离（单位：米）")]
    public float maxShortcutDistance = 35f;

    [Header("特殊预制体")]
    public GameObject keyPrefab;
    public GameObject lockedDoorPrefab;
    public GameObject goalTriggerPrefab;
    public GameObject interactiveDoorPrefab;

    [Header("地板覆盖材质")]
    public Material startRoomMaterial;
    public Material goalRoomMaterial;

    [Header("玩家")]
    public GameObject playerPrefab;

    private List<GenerateDungeon> _rooms     = new List<GenerateDungeon>();
    private List<List<int>>       _adjacency = new List<List<int>>();
    private int _startRoomIndex = -1;
    private int _goalRoomIndex  = -1;
    private int _keyRoomIndex   = -1;

    void Start()
    {
        int seed = (GameState.Instance != null)
            ? GameState.Instance.CurrentSeed
            : Random.Range(1, 1000000);
        Random.InitState(seed);
        Debug.Log($"[DungeonManager] Seed = {seed}");
        Generate();
    }


    public void Generate()
    {
        // 1. BSP 切割
        var  bsp  = new BSPDungeonGenerator(bspDepth, roomMargin, minRoomSize);
        Rect rect = new Rect(-totalMapSize.x / 2f, -totalMapSize.y / 2f, totalMapSize.x, totalMapSize.y);
        BSPNode root = bsp.Generate(rect);

        // 2. 实例化房间几何体
        _rooms.Clear();
        _adjacency.Clear();
        foreach (var leaf in bsp.leafNodes)
        {
            int idx = _rooms.Count;
            leaf.roomIndex = idx;

            var go  = Instantiate(roomPrefab, leaf.RoomCenter, Quaternion.identity, transform);
            go.name = $"Room_{idx}";
            var gen = go.GetComponent<GenerateDungeon>();
            gen.InitializeRoom(new Vector2(leaf.roomRect.width, leaf.roomRect.height));

            _rooms.Add(gen);
            _adjacency.Add(new List<int>());
        }

        // 3. 连接基础走廊
        ConnectRoomsViaBSP(root);

        // 4. 随机打通邻近墙体，产生捷径与环路 
        AddRandomExtraCorridors();

        // 5. 将“寻找起终点”的调用提前
        FindStartAndGoal();

        // ─── 核心修改 1：提前计算出钥匙房的索引，以便道具生成时避让 ───
        _keyRoomIndex = FindMiddleRoom(_startRoomIndex, _goalRoomIndex);

        // 6. 确定锁门位置并排除
        Vector3 lockedDoorPos = Vector3.zero;
        if (_goalRoomIndex >= 0 && _rooms[_goalRoomIndex].activeDoorPositions.Count > 0)
        {
            lockedDoorPos = _rooms[_goalRoomIndex].activeDoorPositions[0];
        }

        // 7. 生成门与碰撞体
        foreach (var room in _rooms)
        {
            room.SpawnDoors(interactiveDoorPrefab);
        }

        foreach (var room in _rooms)
            room.RebuildWallColliders();

        // ─── 核心修改 2：在放置道具时，传入正确的 isKeyRoom 标记 ───
        for (int i = 0; i < _rooms.Count; i++)
        {
            bool isKey = (i == _keyRoomIndex); // 判定当前房间是否是钥匙房
            _rooms[i].DelayedPlaceProps(isKey); // 传入标记，钥匙房中心 2.5 米内将不生成任何道具
        }

        // 9. 标记起终点地板颜色
        if (_startRoomIndex >= 0) _rooms[_startRoomIndex].SetFloorOverlay(startRoomMaterial);
        if (_goalRoomIndex  >= 0) _rooms[_goalRoomIndex ].SetFloorOverlay(goalRoomMaterial);

        // 10. 放置特殊道具（修改：钥匙房索引已提前计算，这里直接摆放即可）
        PlaceKeyInMiddleRoom();
        PlaceLockedDoor();
        PlaceGoalTrigger();
        SpawnPlayer();
    }

    private void ConnectRoomsViaBSP(BSPNode node)
    {
        if (node == null || node.isLeaf) return;
        ConnectRoomsViaBSP(node.left);
        ConnectRoomsViaBSP(node.right);

        var (leftLeaf, rightLeaf) = BSPDungeonGenerator.GetClosestLeafPair(node.left, node.right);
        if (leftLeaf == null || rightLeaf == null) return;

        int lIdx = leftLeaf.roomIndex;
        int rIdx = rightLeaf.roomIndex;

        GenerateDungeon roomL = _rooms[lIdx];
        GenerateDungeon roomR = _rooms[rIdx];

        Vector3 from = roomL.transform.position;
        Vector3 to   = roomR.transform.position;

        // 1. 判断两个相邻房间的连接墙体朝向
        Vector3 diff = to - from;
        GenerateDungeon.WallDirection exitDir;
        GenerateDungeon.WallDirection enterDir;

        if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z))
        {
            if (diff.x > 0)
            {
                exitDir  = GenerateDungeon.WallDirection.East;
                enterDir = GenerateDungeon.WallDirection.West;
            }
            else
            {
                exitDir  = GenerateDungeon.WallDirection.West;
                enterDir = GenerateDungeon.WallDirection.East;
            }
        }
        else
        {
            if (diff.z > 0)
            {
                exitDir  = GenerateDungeon.WallDirection.North;
                enterDir = GenerateDungeon.WallDirection.South;
            }
            else
            {
                exitDir  = GenerateDungeon.WallDirection.South;
                enterDir = GenerateDungeon.WallDirection.North;
            }
        }

        // 获取该通道的动态匹配内宽
        float customCorridorWidth = roomL.GetWallSegmentWidth(exitDir);

        // 2. 找到最契合连接线的墙砖，并将其强制转换为开放式拱门模型
        Vector3 doorL = GetDoorPosition(roomL, exitDir, (exitDir == GenerateDungeon.WallDirection.East || exitDir == GenerateDungeon.WallDirection.West) ? from.z : from.x);
        Vector3 doorR = GetDoorPosition(roomR, enterDir, (enterDir == GenerateDungeon.WallDirection.East || enterDir == GenerateDungeon.WallDirection.West) ? to.z : to.x);
        
        // 3. 消除极小错位导致的走廊自我堵塞（使用动态宽度判断）
        if (exitDir == GenerateDungeon.WallDirection.North || exitDir == GenerateDungeon.WallDirection.South)
        {
            if (Mathf.Abs(doorL.x - doorR.x) < customCorridorWidth)
            {
                doorR = GetDoorPosition(roomR, enterDir, doorL.x);
            }
        }
        else
        {
            if (Mathf.Abs(doorL.z - doorR.z) < customCorridorWidth)
            {
                doorR = GetDoorPosition(roomR, enterDir, doorL.z);
            }
        }

        // 4. 构建智能走廊规划
        corridorBuilder.BuildSmartCorridor(doorL, doorR, exitDir, enterDir, customCorridorWidth, lIdx * 100 + rIdx);

        // 5. 登记图的邻接关系
        if (!_adjacency[lIdx].Contains(rIdx)) _adjacency[lIdx].Add(rIdx);
        if (!_adjacency[rIdx].Contains(lIdx)) _adjacency[rIdx].Add(lIdx);
    }

    /// <summary>
    /// 新增：随机打通不相邻但物理上靠得很近的隔壁房间，产生环路与捷径
    /// </summary>
    private void AddRandomExtraCorridors()
    {
        if (extraConnectionProbability <= 0f) return;

        for (int i = 0; i < _rooms.Count; i++)
        {
            for (int j = i + 1; j < _rooms.Count; j++)
            {
                // 如果在基础 BSP 生成中已经连通了，跳过
                if (_adjacency[i].Contains(j)) continue;

                // 计算两个房间物理中心点的距离
                float distance = Vector3.Distance(_rooms[i].transform.position, _rooms[j].transform.position);

                // 如果两个房间挨得很近，说明它们是紧邻的“邻居”
                if (distance < maxShortcutDistance)
                {
                    // 按照概率随机决定是否打通这条捷径
                    if (Random.value < extraConnectionProbability)
                    {
                        GenerateDungeon roomA = _rooms[i];
                        GenerateDungeon roomB = _rooms[j];

                        Vector3 diff = roomB.transform.position - roomA.transform.position;
                        GenerateDungeon.WallDirection exitDir;
                        GenerateDungeon.WallDirection enterDir;

                        if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z))
                        {
                            exitDir  = diff.x > 0 ? GenerateDungeon.WallDirection.East : GenerateDungeon.WallDirection.West;
                            enterDir = diff.x > 0 ? GenerateDungeon.WallDirection.West : GenerateDungeon.WallDirection.East;
                        }
                        else
                        {
                            exitDir  = diff.z > 0 ? GenerateDungeon.WallDirection.North : GenerateDungeon.WallDirection.South;
                            enterDir = diff.z > 0 ? GenerateDungeon.WallDirection.South : GenerateDungeon.WallDirection.North;
                        }

                        float width = roomA.GetWallSegmentWidth(exitDir);
                        
                        // 计算门口坐标（会自动调用 ConvertWallToArchway，将门口位置加入 activeDoorPositions 列表）
                        Vector3 doorA = GetDoorPosition(roomA, exitDir, (exitDir == GenerateDungeon.WallDirection.East || exitDir == GenerateDungeon.WallDirection.West) ? roomA.transform.position.z : roomA.transform.position.x);
                        Vector3 doorB = GetDoorPosition(roomB, enterDir, (enterDir == GenerateDungeon.WallDirection.East || enterDir == GenerateDungeon.WallDirection.West) ? roomB.transform.position.z : roomB.transform.position.x);

                        // 建造捷径走廊
                        corridorBuilder.BuildSmartCorridor(doorA, doorB, exitDir, enterDir, width, i * 100 + j);

                        // 在邻接矩阵中登记连通关系，保证路径分析正常
                        _adjacency[i].Add(j);
                        _adjacency[j].Add(i);

                        Debug.Log($"[DungeonManager] 随机打通捷径环路：成功连通 Room_{i} 和 Room_{j}。");
                    }
                }
            }
        }
    }

    private Vector3 GetDoorPosition(GenerateDungeon room, GenerateDungeon.WallDirection dir, float targetCoord)
    {
        Vector3 center = room.transform.position;
        Vector2 size   = room.roomSize;
        float x = center.x;
        float z = center.z;

        // 调用 ConvertWallToArchway，在指定墙面最接近位置自动生成并替换一个拱门
        float snappedCoord = room.ConvertWallToArchway(dir, targetCoord);

        switch (dir)
        {
            case GenerateDungeon.WallDirection.North:
                z += size.y / 2f;
                x = snappedCoord;
                break;
            case GenerateDungeon.WallDirection.South:
                z -= size.y / 2f;
                x = snappedCoord;
                break;
            case GenerateDungeon.WallDirection.East:
                x += size.x / 2f;
                z = snappedCoord;
                break;
            case GenerateDungeon.WallDirection.West:
                x -= size.x / 2f;
                z = snappedCoord;
                break;
        }
        return new Vector3(x, 0, z);
    }

    private void FindStartAndGoal()
    {
        if (_rooms.Count == 0) return;
        int a = BFSFarthest(0);
        int b = BFSFarthest(a);
        _startRoomIndex = a;
        _goalRoomIndex  = b;
        Debug.Log($"[DungeonManager] Start={a}, Goal={b}");
    }

    private int BFSFarthest(int src)
    {
        int n = _rooms.Count;
        int[] dist = new int[n];
        for (int i = 0; i < n; i++) dist[i] = -1;
        var q = new Queue<int>();
        dist[src] = 0; q.Enqueue(src);
        int far = src;
        while (q.Count > 0)
        {
            int cur = q.Dequeue();
            foreach (int nb in _adjacency[cur])
                if (dist[nb] == -1)
                {
                    dist[nb] = dist[cur] + 1; q.Enqueue(nb);
                    if (dist[nb] > dist[far]) far = nb;
                }
        }
        return far;
    }

    // private void PlaceKeyInMiddleRoom()
    // {
    //     if (keyPrefab == null) return;
    //     _keyRoomIndex = FindMiddleRoom(_startRoomIndex, _goalRoomIndex);
    //     if (_keyRoomIndex < 0) return;

    //     var keyGO = Instantiate(keyPrefab,
    //         _rooms[_keyRoomIndex].transform.position + new Vector3(0, 0.5f, 0),
    //         Quaternion.identity);
    //     keyGO.name = "Key";
    //     if (keyGO.GetComponent<KeyItem>() == null) keyGO.AddComponent<KeyItem>();
    //     Debug.Log($"[DungeonManager] Key in Room_{_keyRoomIndex}");
    // }

    private void PlaceKeyInMiddleRoom()
    {
        if (keyPrefab == null || _keyRoomIndex < 0) return;

        // 直接在提前算好的 _keyRoomIndex 房间中心实例化钥匙
        var keyGO = Instantiate(keyPrefab,
            _rooms[_keyRoomIndex].transform.position + new Vector3(0, 0.5f, 0),
            Quaternion.identity);
        keyGO.name = "Key";
        if (keyGO.GetComponent<KeyItem>() == null) keyGO.AddComponent<KeyItem>();
        Debug.Log($"[DungeonManager] 成功在避让空旷的 Room_{_keyRoomIndex} 中心放置钥匙。");
    }

    private int FindMiddleRoom(int start, int goal)
    {
        if (start < 0 || goal < 0) return -1;
        int n = _rooms.Count;
        int[] prev = new int[n], dist = new int[n];
        for (int i = 0; i < n; i++) { prev[i] = -1; dist[i] = -1; }
        var q = new Queue<int>(); dist[start] = 0; q.Enqueue(start);
        while (q.Count > 0)
        {
            int cur = q.Dequeue();
            if (cur == goal) break;
            foreach (int nb in _adjacency[cur])
                if (dist[nb] == -1) { dist[nb] = dist[cur]+1; prev[nb] = cur; q.Enqueue(nb); }
        }
        var path = new List<int>(); int node = goal;
        while (node != -1) { path.Add(node); node = prev[node]; }
        path.Reverse();
        return path.Count < 3 ? -1 : path[Random.Range(1, path.Count - 1)];
    }

    private void PlaceLockedDoor()
    {
        if (_goalRoomIndex < 0) return;

        GenerateDungeon goalRoom = _rooms[_goalRoomIndex];
        if (goalRoom.activeDoorPositions.Count == 0) return;

        // 终点门口的物理世界坐标
        Vector3 doorPos = goalRoom.activeDoorPositions[0];

        int lockCount = 0;
        
        // 获取场景中所有的普通门组件
        InteractiveDoor[] allDoors = FindObjectsByType<InteractiveDoor>(FindObjectsSortMode.None);
        
        foreach (var door in allDoors)
        {
            if (door != null)
            {
                float distance = Vector3.Distance(door.transform.position, doorPos);
                Debug.Log($"[DungeonManager] 检索到门板: {door.gameObject.name}, 距离终点定位点为: {distance:F2} 米");

                // 将判定阈值扩大到 4.0 米，绝对安全地锁住终点房的双扇大门
                if (distance < 4.0f)
                {
                    door.requiresKeyToOpen = true;
                    lockCount++;
                }
            }
        }

        Debug.Log($"[DungeonManager] 成功将终点房门口已有的双扇普通门（共锁定 {lockCount} 个门板对象）设为限制锁定状态！");
    }

    private void PlaceGoalTrigger()
    {
        if (_goalRoomIndex < 0) return;
        Vector3 center = _rooms[_goalRoomIndex].transform.position;

        if (goalTriggerPrefab != null)
        {
            Instantiate(goalTriggerPrefab, center, Quaternion.identity).name = "GoalTrigger";
        }
        else
        {
            var go  = new GameObject("GoalTrigger");
            go.transform.position = center;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size      = new Vector3(_rooms[_goalRoomIndex].roomSize.x * 0.8f, 3f,
                                        _rooms[_goalRoomIndex].roomSize.y * 0.8f);
            go.AddComponent<GoalTrigger>();
        }
    }

    private void SpawnPlayer()
    {
        if (_startRoomIndex < 0 || playerPrefab == null) return;
        Instantiate(playerPrefab,
            _rooms[_startRoomIndex].transform.position + new Vector3(0, 1f, 0),
            Quaternion.identity);
    }

    public List<GenerateDungeon> Rooms          => _rooms;
    public int                   StartRoomIndex => _startRoomIndex;
    public int                   GoalRoomIndex  => _goalRoomIndex;
    public int                   KeyRoomIndex   => _keyRoomIndex;
}