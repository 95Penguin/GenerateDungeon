using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地牢总控制器 v6
/// 1. 取消了 OpenWallGap 暴力删墙，改为调用房间的 ConvertWallToArchway。
/// 2. 升级走廊生成调用，将 exitDir 和 enterDir 传入以生成完美对齐的 Z/L 型通道。
/// 3. 调整道具放置时机为：走廊规划完毕后延迟生成，从而精确避开门点区域。
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

        // 3. 连接走廊 + 自动在对应墙面“强制替换”出拱门
        ConnectRoomsViaBSP(root);

        // ─── 核心修改：将“寻找起终点”的调用提前到生成门之前 ───
        FindStartAndGoal();

        // ─── 此时 _goalRoomIndex 已经确定，可以精准获取锁门位置进行排除了 ───
        Vector3 lockedDoorPos = Vector3.zero;
        if (_goalRoomIndex >= 0 && _rooms[_goalRoomIndex].activeDoorPositions.Count > 0)
        {
            lockedDoorPos = _rooms[_goalRoomIndex].activeDoorPositions[0];
        }

        // 4. 在所有的拱门位置填入物理门（传入锁门位置进行排除）
        foreach (var room in _rooms)
        {
            room.SpawnDoors(interactiveDoorPrefab, lockedDoorPos);
        }

        // 5. 重建带拱门的房间物理碰撞体
        foreach (var room in _rooms)
            room.RebuildWallColliders();

        // 6. 延迟放置道具
        foreach (var room in _rooms)
            room.DelayedPlaceProps();

        // 7. 标记起终点地板颜色
        if (_startRoomIndex >= 0) _rooms[_startRoomIndex].SetFloorOverlay(startRoomMaterial);
        if (_goalRoomIndex  >= 0) _rooms[_goalRoomIndex ].SetFloorOverlay(goalRoomMaterial);

        // 8. 放置特殊道具、锁门与玩家
        PlaceKeyInMiddleRoom();
        PlaceLockedDoor();
        PlaceGoalTrigger();
        SpawnPlayer();
    }


    // 请将 DungeonManager.cs 中的 ConnectRoomsViaBSP 方法替换为以下内容：

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

        // 4. 构建智能走廊规划（传入动态计算出的宽度，防止侧墙穿插拱门）
        corridorBuilder.BuildSmartCorridor(doorL, doorR, exitDir, enterDir, customCorridorWidth, lIdx * 100 + rIdx);

        // 5. 登记图的邻接关系
        if (!_adjacency[lIdx].Contains(rIdx)) _adjacency[lIdx].Add(rIdx);
        if (!_adjacency[rIdx].Contains(lIdx)) _adjacency[rIdx].Add(lIdx);
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

    // ── 后续逻辑 ───────────────────────────────────────────

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

    private void PlaceKeyInMiddleRoom()
    {
        if (keyPrefab == null) return;
        _keyRoomIndex = FindMiddleRoom(_startRoomIndex, _goalRoomIndex);
        if (_keyRoomIndex < 0) return;

        var keyGO = Instantiate(keyPrefab,
            _rooms[_keyRoomIndex].transform.position + new Vector3(0, 0.5f, 0),
            Quaternion.identity);
        keyGO.name = "Key";
        if (keyGO.GetComponent<KeyItem>() == null) keyGO.AddComponent<KeyItem>();
        Debug.Log($"[DungeonManager] Key in Room_{_keyRoomIndex}");
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
        if (lockedDoorPrefab == null || _goalRoomIndex < 0) return;

        GenerateDungeon goalRoom = _rooms[_goalRoomIndex];
        if (goalRoom.activeDoorPositions.Count == 0)
        {
            Debug.LogWarning("[DungeonManager] 终点房间没有检测到激活的门口，无法放置终点大门！");
            return;
        }

        Vector3 doorPos = goalRoom.activeDoorPositions[0]; 
        Vector3 center = goalRoom.transform.position;
        Vector3 diff = doorPos - center;
        
        Quaternion doorRot = Quaternion.identity;
        GenerateDungeon.WallDirection dir;

        if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z))
        {
            dir = diff.x > 0 ? GenerateDungeon.WallDirection.East : GenerateDungeon.WallDirection.West;
            doorRot = Quaternion.Euler(0f, diff.x > 0 ? 90f : 270f, 0f);
        }
        else
        {
            dir = diff.z > 0 ? GenerateDungeon.WallDirection.North : GenerateDungeon.WallDirection.South;
            doorRot = Quaternion.Euler(0f, diff.z > 0 ? 0f : 180f, 0f);
        }

        Vector3 finalPos = new Vector3(doorPos.x, 0f, doorPos.z);
        var door = Instantiate(lockedDoorPrefab, finalPos, doorRot);
        door.name = "LockedDoor";

        // ─── 核心修改：动态计算大门所需的缩放比例，使其完美填满拱门宽度 ───
        if (goalRoom.wallMesh != null)
        {
            float spanLength = (dir == GenerateDungeon.WallDirection.East || dir == GenerateDungeon.WallDirection.West) 
                ? goalRoom.roomSize.y 
                : goalRoom.roomSize.x;
            
            int wallCount = Mathf.Max(1, (int)(spanLength / goalRoom.wallMesh.bounds.size.x));
            float wallScaleX = (spanLength / wallCount) / goalRoom.wallMesh.bounds.size.x;
            
            // float finalScale = wallScaleX * goalRoom.doorWidthMultiplier;
            // ─── 核心修改：将宽度拉伸 2.0 倍，使单扇门完美盖住整个双宽拱门通道 ───
            float finalScaleWidth = wallScaleX * goalRoom.doorWidthMultiplier * 2.0f;
            float finalScaleThickness = wallScaleX * goalRoom.doorWidthMultiplier; // 保持厚度正常
            door.transform.localScale = new Vector3(finalScaleWidth, 1f, finalScaleThickness);
            Debug.Log($"[DungeonManager] 已为终点大门应用缩放宽度: {finalScaleWidth}");
        }

        if (door.GetComponent<LockedDoor>() == null) 
            door.AddComponent<LockedDoor>();
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