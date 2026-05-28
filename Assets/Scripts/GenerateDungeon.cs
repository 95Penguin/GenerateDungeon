using System.Collections.Generic;
using UnityEngine;

public class GenerateDungeon : MonoBehaviour
{
    [Header("房间尺寸(X为宽度，Y为长度)")]
    public Vector2 roomSize = new Vector2(10, 10);

    [Header("门预制体对齐微调")]
    public Vector3 doorPositionOffset = Vector3.zero;      // 位置微调 (X, Y, Z)
    public Vector3 doorRotationOffset = new Vector3(0, -90, 0); // 旋转微调，通常是 (0, 90, 0) 或 (0, -90, 0)
    public float doorWidthMultiplier = 1.0f;               // 新增：门体宽度整体比例缩放（用于消除与墙壁接缝的缝隙）

    // 记录本房间所有已启用的门点位置（世界坐标），用于避开道具生成
    [HideInInspector]
    public List<Vector3> activeDoorPositions = new List<Vector3>();

    private bool _initializedByManager = false;

    void Start()
    {
        if (_initializedByManager) return;
        BuildRoom();
    }

    public void InitializeRoom(Vector2 size)
    {
        _initializedByManager = true;
        roomSize = size;
        activeDoorPositions.Clear();
        BuildRoom();
    }

    private void BuildRoom()
    {
        CreateWalls();
        CreatePillars();
        CreateFloorTile();
        AddWallJunctionPillars();

        CreateCombinedCollider(_pillarMatrices,         pillarMesh);
        CreateCombinedCollider(_floorMatrices,          floorMesh);
        CreateCombinedCollider(_junctionPillarMatrices, junctionPillarMesh);

        // 道具生成延迟到 Manager 确定所有门位置后再进行，防止堵门
        // 这里如果是单独测试则直接生成
        if (!_initializedByManager)
        {
            PlaceProps();
        }
    }

    public void DelayedPlaceProps()
    {
        PlaceProps();
    }


    /// <summary>
    /// 遍历本房间所有拱门（matricesB），应用偏移量并生成物理门。
    /// </summary>
    public void SpawnDoors(GameObject doorPrefab)
    {
        if (doorPrefab == null) return;

        foreach (var face in _wallFaces)
        {
            foreach (var matrix in face.matricesB)
            {
                Vector3 pos = matrix.GetPosition();
                Quaternion rot = matrix.rotation;

                // 1. 获取水平缩放比例
                float wallScaleX = matrix.GetColumn(0).magnitude;

                // 2. 计算微调后的旋转与位置
                Quaternion finalRot = rot * Quaternion.Euler(doorRotationOffset);
                Vector3 finalPos = pos + (rot * doorPositionOffset);

                // 3. 判定该拱门是否在 activeDoorPositions（真门位置）中
                bool isConnected = false;
                foreach (var doorPos in activeDoorPositions)
                {
                    if (Vector3.Distance(finalPos, doorPos) < 1.5f)
                    {
                        isConnected = true;
                        break;
                    }
                }

                // 4. 实例化门
                GameObject doorInstance = Instantiate(doorPrefab, finalPos, finalRot, transform);
                doorInstance.name = isConnected ? "ConnectingDoor" : "WrongDoor";

                // ─── 5. 新增：将所有生成的门（无论是真门还是假门）都注册到避让列表中，防止桌子堵门 ───
                if (!activeDoorPositions.Contains(finalPos))
                {
                    activeDoorPositions.Add(finalPos);
                }

                // 6. 缩放
                float finalScale = wallScaleX * doorWidthMultiplier;
                doorInstance.transform.localScale = new Vector3(finalScale, 1f, finalScale);

                // 7. 获取或挂载 InteractiveDoor 并配置属性
                InteractiveDoor doorScript = doorInstance.GetComponent<InteractiveDoor>();
                if (doorScript == null) 
                    doorScript = doorInstance.AddComponent<InteractiveDoor>();

                doorScript.isConnectingDoor = isConnected; 
            }
        }
    }

    public void RebuildWallColliders()
    {
        if (_collidersParent != null)
        {
            var toDestroy = new List<Transform>();
            foreach (Transform child in _collidersParent.transform)
            {
                string wn  = wallMesh  != null ? wallMesh.name  : "$$";
                string wnb = wallMeshB != null ? wallMeshB.name : "$$";
                if (child.name.StartsWith(wn) || child.name.StartsWith(wnb))
                    toDestroy.Add(child);
            }
            foreach (var t in toDestroy) Destroy(t.gameObject);
        }

        CreateCombinedCollider(GatherWallMatrices(true),  wallMesh);
        CreateCombinedCollider(GatherWallMatrices(false), wallMeshB);
    }

    private List<Matrix4x4> GatherWallMatrices(bool typeA)
    {
        var result = new List<Matrix4x4>();
        foreach (var face in _wallFaces)
            result.AddRange(typeA ? face.matricesA : face.matricesB);
        return result;
    }

    void Update()
    {
        RenderWalls();
        RenderPillars();
        RenderFloor();
        RenderJunctionPillars();
    }

    public void SetFloorOverlay(Material mat)
    {
        if (mat == null) return;
        var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
        overlay.name = "FloorOverlay";
        overlay.transform.SetParent(transform);
        overlay.transform.position   = transform.position + new Vector3(0, 0.01f, 0);
        overlay.transform.rotation   = Quaternion.Euler(90, 0, 0);
        overlay.transform.localScale = new Vector3(roomSize.x * 0.9f, roomSize.y * 0.9f, 1);
        overlay.GetComponent<Renderer>().material = mat;
        var col = overlay.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    // ══════════════════════════════════════════════════════
    // 墙壁生成与拱门强制转换
    // ══════════════════════════════════════════════════════

    public enum WallDirection { North = 0, South = 1, East = 2, West = 3 }

    private class WallFace
    {
        public float fixedAxisValue;
        public float fixedAxisTolerance;
        public List<Matrix4x4> matricesA = new List<Matrix4x4>(); // 普通实心墙
        public List<Matrix4x4> matricesB = new List<Matrix4x4>(); // 开放拱门
    }

    private WallFace[] _wallFaces = new WallFace[4];

    #region 墙壁生成
    [Header("墙壁生成相关")]
    public Mesh     wallMesh;   // 普通墙壁
    public Mesh     wallMeshB;  // 开放式拱门
    public Material wallMaterial;

    private List<Matrix4x4> _renderN  = new List<Matrix4x4>();
    private List<Matrix4x4> _renderNB = new List<Matrix4x4>();

    void CreateWalls()
    {
        for (int i = 0; i < 4; i++) _wallFaces[i] = new WallFace();

        CreateWallEdge(WallDirection.North, Vector3.forward, roomSize.y / 2f, false);
        CreateWallEdge(WallDirection.South, Vector3.back,    roomSize.y / 2f, false);
        CreateWallEdge(WallDirection.East,  Vector3.right,   roomSize.x / 2f, true);
        CreateWallEdge(WallDirection.West,  Vector3.left,    roomSize.x / 2f, true);
    }

    void CreateWallEdge(WallDirection dir, Vector3 direction, float offset, bool isVertical)
    {
        float spanLength    = isVertical ? roomSize.y : roomSize.x;
        float rotationAngle = isVertical ? 90f : 0f;
        Vector3 axis        = isVertical ? Vector3.up : Vector3.zero;

        int   wallCount = Mathf.Max(1, (int)(spanLength / wallMesh.bounds.size.x));
        float scale     = (spanLength / wallCount) / wallMesh.bounds.size.x;

        Vector3 wallCenterOffset = direction * offset;
        float   tolerance        = wallMesh.bounds.extents.z * scale + 0.1f;

        var face = _wallFaces[(int)dir];
        bool isNS = (dir == WallDirection.North || dir == WallDirection.South);
        Vector3 worldFaceCenter = transform.position + wallCenterOffset;
        face.fixedAxisValue     = isNS ? worldFaceCenter.z : worldFaceCenter.x;
        face.fixedAxisTolerance = tolerance;

        for (int i = 0; i < wallCount; i++)
        {
            Vector3 position = transform.position + wallCenterOffset;
            float along = -spanLength / 2f
                        + wallMesh.bounds.size.x * scale / 2f
                        + i * scale * wallMesh.bounds.size.x;

            position += isVertical ? Vector3.forward * along : Vector3.right * along;

            Quaternion rotation = transform.rotation * Quaternion.AngleAxis(rotationAngle, axis);
            Matrix4x4  matrix   = Matrix4x4.TRS(position, rotation, new Vector3(scale, 1f, 1f));

            // 初始化：默认 85% 是普通墙体，15% 是天然开放拱门
            if (Random.value < 0.85f) face.matricesA.Add(matrix);
            else                      face.matricesB.Add(matrix);
        }
    }

    /// <summary>
    /// 将指定墙面上最接近 targetCoord 的某块墙强制转为开放拱门（Type B）。
    /// 返回该拱门精确的世界排列轴坐标。
    /// </summary>
    public float ConvertWallToArchway(WallDirection dir, float targetCoord)
    {
        var face = _wallFaces[(int)dir];
        bool isNS = (dir == WallDirection.North || dir == WallDirection.South);

        Matrix4x4 bestMatrix = default;
        bool foundInA = false;
        float bestCoord = targetCoord;
        float bestDist = float.MaxValue;

        // 在实心墙中查找
        foreach (var m in face.matricesA)
        {
            Vector3 pos = m.GetPosition();
            float coord = isNS ? pos.x : pos.z;
            float dist = Mathf.Abs(coord - targetCoord);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCoord = coord;
                bestMatrix = m;
                foundInA = true;
            }
        }

        // 在已有拱门中查找
        foreach (var m in face.matricesB)
        {
            Vector3 pos = m.GetPosition();
            float coord = isNS ? pos.x : pos.z;
            float dist = Mathf.Abs(coord - targetCoord);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCoord = coord;
                bestMatrix = m;
                foundInA = false;
            }
        }

        // 如果最接近的点原本是实心墙，将其替换为拱门
        if (foundInA && bestDist < float.MaxValue)
        {
            face.matricesA.Remove(bestMatrix);
            face.matricesB.Add(bestMatrix);
        }

        // 记录该门的位置，用于之后生成道具时避开
        Vector3 doorWorldPos = new Vector3(
            isNS ? bestCoord : face.fixedAxisValue,
            0f,
            isNS ? face.fixedAxisValue : bestCoord
        );
        activeDoorPositions.Add(doorWorldPos);

        return bestCoord;
    }

    void RenderWalls()
    {
        _renderN.Clear();
        _renderNB.Clear();
        foreach (var face in _wallFaces)
        {
            _renderN.AddRange(face.matricesA);
            _renderNB.AddRange(face.matricesB);
        }
        if (_renderN.Count  > 0) Graphics.DrawMeshInstanced(wallMesh,  0, wallMaterial, _renderN.ToArray(),  _renderN.Count);
        if (_renderNB.Count > 0) Graphics.DrawMeshInstanced(wallMeshB, 0, wallMaterial, _renderNB.ToArray(), _renderNB.Count);
    }
    #endregion

    // ══════════════════════════════════════════════════════
    // 支柱、地板、接缝支柱与碰撞体生成 (保持正常)
    // ══════════════════════════════════════════════════════

    #region 支柱
    [Header("支柱生成相关")]
    public Mesh     pillarMesh;
    public Material pillarMaterial;
    List<Matrix4x4> _pillarMatrices;

    void CreatePillars()
    {
        _pillarMatrices = new List<Matrix4x4>();
        Vector3[] corners = {
            new Vector3(-roomSize.x / 2f, 0, -roomSize.y / 2f),
            new Vector3(-roomSize.x / 2f, 0,  roomSize.y / 2f),
            new Vector3( roomSize.x / 2f, 0, -roomSize.y / 2f),
            new Vector3( roomSize.x / 2f, 0,  roomSize.y / 2f)
        };
        foreach (var c in corners)
            _pillarMatrices.Add(Matrix4x4.TRS(transform.position + c, transform.rotation, Vector3.one));
    }

    void RenderPillars()
    {
        if (_pillarMatrices != null && _pillarMatrices.Count > 0)
            Graphics.DrawMeshInstanced(pillarMesh, 0, pillarMaterial, _pillarMatrices.ToArray(), _pillarMatrices.Count);
    }
    #endregion

    #region 地板
    [Header("地板生成相关")]
    public Mesh     floorMesh;
    public Material floorMaterial;
    List<Matrix4x4> _floorMatrices;

    void CreateFloorTile()
    {
        _floorMatrices = new List<Matrix4x4>();
        float fw = floorMesh.bounds.size.x;
        float fl = floorMesh.bounds.size.z;
        int   cx = Mathf.Max(1, Mathf.CeilToInt(roomSize.x / fw));
        int   cz = Mathf.Max(1, Mathf.CeilToInt(roomSize.y / fl));
        float sx = (roomSize.x / cx) / fw;
        float sz = (roomSize.y / cz) / fl;
        Vector3 start = transform.position + new Vector3(-roomSize.x / 2f + fw * sx / 2f, 0, -roomSize.y / 2f + fl * sz / 2f);
        for (int x = 0; x < cx; x++)
        for (int z = 0; z < cz; z++)
        {
            Vector3 pos = start + new Vector3(x * fw * sx, 0, z * fl * sz);
            _floorMatrices.Add(Matrix4x4.TRS(pos, transform.rotation, new Vector3(sx, 1f, sz)));
        }
    }

    void RenderFloor()
    {
        if (_floorMatrices != null && _floorMatrices.Count > 0)
            Graphics.DrawMeshInstanced(floorMesh, 0, floorMaterial, _floorMatrices.ToArray(), _floorMatrices.Count);
    }
    #endregion

    #region 接缝支柱
    [Header("墙壁接缝支柱")]
    public Mesh     junctionPillarMesh;
    public Material junctionPillarMaterial;
    List<Matrix4x4> _junctionPillarMatrices;

    void AddWallJunctionPillars()
    {
        if (junctionPillarMesh == null) return;
        _junctionPillarMatrices = new List<Matrix4x4>();

        float wallWidth = wallMesh.bounds.size.x;
        int   hCount    = Mathf.Max(1, (int)(roomSize.x / wallWidth));
        int   vCount    = Mathf.Max(1, (int)(roomSize.y / wallWidth));
        float hScale    = (roomSize.x / hCount) / wallWidth;
        float vScale    = (roomSize.y / vCount) / wallWidth;

        for (int i = 1; i < hCount; i++)
        {
            float xPos = -roomSize.x / 2f + i * wallWidth * hScale;
            AddPillar(transform.position + new Vector3(xPos, 0,  roomSize.y / 2f));
            AddPillar(transform.position + new Vector3(xPos, 0, -roomSize.y / 2f));
        }
        for (int i = 1; i < vCount; i++)
        {
            float zPos = -roomSize.y / 2f + i * wallWidth * vScale;
            AddPillar(transform.position + new Vector3( roomSize.x / 2f, 0, zPos));
            AddPillar(transform.position + new Vector3(-roomSize.x / 2f, 0, zPos));
        }
    }

    void AddPillar(Vector3 position, float scaleMultiplier = 1f)
    {
        _junctionPillarMatrices.Add(Matrix4x4.TRS(position, transform.rotation, Vector3.one * scaleMultiplier));
    }

    void RenderJunctionPillars()
    {
        if (_junctionPillarMatrices != null && _junctionPillarMatrices.Count > 0)
            Graphics.DrawMeshInstanced(junctionPillarMesh, 0, junctionPillarMaterial, _junctionPillarMatrices.ToArray(), _junctionPillarMatrices.Count);
    }
    #endregion

    #region 碰撞体
    private GameObject _collidersParent;

    void CreateCombinedCollider(List<Matrix4x4> matrices, Mesh mesh)
    {
        if (mesh == null || matrices == null || matrices.Count == 0) return;

        if (_collidersParent == null)
        {
            _collidersParent = new GameObject(gameObject.name + "_CollidersParent");
            _collidersParent.transform.SetParent(transform);
        }

        var combines = new CombineInstance[matrices.Count];
        for (int i = 0; i < matrices.Count; i++)
            combines[i] = new CombineInstance { mesh = mesh, transform = matrices[i] };

        var combined = new Mesh();
        combined.CombineMeshes(combines);

        var go = new GameObject(mesh.name + "Colliders");
        go.transform.SetParent(_collidersParent.transform);
        go.AddComponent<MeshCollider>().sharedMesh = combined;
    }
    #endregion

    // ══════════════════════════════════════════════════════
    // 道具放置系统 (智能避开所有已启用的门点)
    // ══════════════════════════════════════════════════════
    
    #region 道具放置
    [Header("道具放置系统")]
    public DungeonProp[] dungeonProps;
    private float      _wallThickness;
    private GameObject _propsParent;



    /// <summary>
    /// 获取指定墙面上单块墙体（以及拱门）的实际物理宽度
    /// </summary>
    public float GetWallSegmentWidth(WallDirection dir)
    {
        if (wallMesh == null) return roomSize.x;
        bool isVertical = (dir == WallDirection.East || dir == WallDirection.West);
        float spanLength = isVertical ? roomSize.y : roomSize.x;
        int wallCount = Mathf.Max(1, (int)(spanLength / wallMesh.bounds.size.x));
        return spanLength / wallCount;
    }


    void PlaceProps()
    {
        if (dungeonProps == null || dungeonProps.Length == 0) return;

        _wallThickness = wallMesh.bounds.extents.z;
        float uw = roomSize.x - 2f * _wallThickness;
        float uh = roomSize.y - 2f * _wallThickness;
        float minX = transform.position.x - uw / 2f;
        float maxX = transform.position.x + uw / 2f;
        float minZ = transform.position.z - uh / 2f;
        float maxZ = transform.position.z + uh / 2f;

        if (_propsParent == null)
        {
            _propsParent = new GameObject("PropsParent");
            _propsParent.transform.SetParent(transform);
        }

        var placedOBBs = new List<OBB>();

        foreach (var prop in dungeonProps)
        {
            int count = Random.Range(prop.minCount, prop.maxCount + 1);
            for (int i = 0; i < count; i++)
            {
                if (Random.value > prop.spawnProbability) continue;

                Vector3    pos  = Vector3.zero;
                Quaternion rot  = Quaternion.identity;
                Vector3    half = Vector3.zero;

                bool ok = prop.positionType switch
                {
                    DungeonProp.PositionType.Wall    => TryFindWallPosition  (prop.prefab, minX, maxX, minZ, maxZ, out pos, out rot, out half),
                    DungeonProp.PositionType.Corner  => TryFindCornerPosition(prop.prefab, minX, maxX, minZ, maxZ, out pos, out rot, out half),
                    DungeonProp.PositionType.Middle  => TryFindMiddlePosition(prop.prefab, minX, maxX, minZ, maxZ, out pos, out rot, out half),
                    DungeonProp.PositionType.Anywhere=> TryFindAnyPosition   (prop.prefab, minX, maxX, minZ, maxZ, out pos, out rot, out half),
                    _ => false
                };
                if (!ok) continue;

                // 核心过滤：检查道具是否太靠近任何已经激活的拱门出口
                bool blocksDoor = false;
                foreach (var doorPos in activeDoorPositions)
                {
                    // 如果道具距离门小于 3.2 米，则跳过生成
                    if (Vector3.Distance(pos, doorPos) < 2.5f)
                    {
                        blocksDoor = true;
                        break;
                    }
                }
                if (blocksDoor) continue;

                if (prop.randomRotation) rot = Quaternion.Euler(0, Random.Range(0, 360), 0);
                if (!CheckOverlap(pos, half, rot, placedOBBs))
                {
                    Instantiate(prop.prefab, pos + prop.offset, rot).transform.SetParent(_propsParent.transform);
                    placedOBBs.Add(new OBB { center = pos, extents = half, rotation = rot });
                }
            }
        }
    }

    bool TryFindWallPosition(GameObject prefab, float minX, float maxX, float minZ, float maxZ,
                             out Vector3 pos, out Quaternion rot, out Vector3 half)
    {
        pos  = Vector3.zero; rot = Quaternion.identity; half = Vector3.zero;
        Bounds b = GetPrefabBounds(prefab);
        if (b.size == Vector3.zero) return false;
        half = b.extents;
        switch (Random.Range(0, 4))
        {
            case 0: pos = new Vector3(Random.Range(minX+half.x, maxX-half.x), 0, maxZ-half.z); rot = Quaternion.Euler(0,180,0); break;
            case 1: pos = new Vector3(Random.Range(minX+half.x, maxX-half.x), 0, minZ+half.z); rot = Quaternion.identity;       break;
            case 2: pos = new Vector3(maxX-half.z, 0, Random.Range(minZ+half.x, maxZ-half.x)); rot = Quaternion.Euler(0,270,0); break;
            case 3: pos = new Vector3(minX+half.z, 0, Random.Range(minZ+half.x, maxZ-half.x)); rot = Quaternion.Euler(0, 90,0); break;
        }
        return true;
    }

    bool TryFindCornerPosition(GameObject prefab, float minX, float maxX, float minZ, float maxZ,
                               out Vector3 pos, out Quaternion rot, out Vector3 half)
    {
        pos  = Vector3.zero; rot = Quaternion.identity; half = Vector3.zero;
        Bounds b = GetPrefabBounds(prefab);
        if (b.size == Vector3.zero) return false;
        half = b.extents;
        float off = _wallThickness + Mathf.Max(half.x, half.z);
        switch (Random.Range(0, 4))
        {
            case 0: pos = new Vector3(minX+off, 0, maxZ-off); rot = Quaternion.Euler(0,225,0); break;
            case 1: pos = new Vector3(maxX-off, 0, maxZ-off); rot = Quaternion.Euler(0,315,0); break;
            case 2: pos = new Vector3(minX+off, 0, minZ+off); rot = Quaternion.Euler(0,135,0); break;
            case 3: pos = new Vector3(maxX-off, 0, minZ+off); rot = Quaternion.Euler(0, 45,0); break;
        }
        return true;
    }

    bool TryFindMiddlePosition(GameObject prefab, float minX, float maxX, float minZ, float maxZ,
                               out Vector3 pos, out Quaternion rot, out Vector3 half)
    {
        pos  = Vector3.zero; rot = Quaternion.identity; half = Vector3.zero;
        Bounds b = GetPrefabBounds(prefab);
        if (b.size == Vector3.zero) return false;
        half = b.extents;
        float m = Mathf.Max(half.x, half.z) * 2f;
        pos = new Vector3(Random.Range(minX+m, maxX-m), 0, Random.Range(minZ+m, maxZ-m));
        return true;
    }

    bool TryFindAnyPosition(GameObject prefab, float minX, float maxX, float minZ, float maxZ,
                            out Vector3 pos, out Quaternion rot, out Vector3 half)
    {
        float r = Random.value;
        if (r < 0.5f)  return TryFindWallPosition  (prefab, minX, maxX, minZ, maxZ, out pos, out rot, out half);
        if (r < 0.75f) return TryFindCornerPosition(prefab, minX, maxX, minZ, maxZ, out pos, out rot, out half);
                       return TryFindMiddlePosition (prefab, minX, maxX, minZ, maxZ, out pos, out rot, out half);
    }

    Bounds GetPrefabBounds(GameObject prefab)
    {
        var r = prefab.GetComponentInChildren<Renderer>();
        if (r != null) return r.bounds;
        var c = prefab.GetComponentInChildren<Collider>();
        if (c != null) return c.bounds;
        return new Bounds(Vector3.zero, Vector3.one * 0.5f);
    }

    bool CheckOverlap(Vector3 pos, Vector3 half, Quaternion rot, List<OBB> existing)
    {
        var newOBB = new OBB { center = pos, extents = half, rotation = rot };
        foreach (var obb in existing)
            if (newOBB.Intersects(obb)) return true;
        return false;
    }
    #endregion
}