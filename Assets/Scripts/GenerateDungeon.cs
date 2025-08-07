using System.Collections.Generic;
using UnityEngine;

//生成地牢函数
public class GenerateDungeon : MonoBehaviour
{
    [Header("房间尺寸(X为宽度，Y为长度)")]
    public Vector2 roomSize = new Vector2(10, 10);
    [Header("随机种子")]
    [SerializeField] private int _seed; // 随机种子
    [SerializeField] private bool _useSeed; // 是否使用指定种子
    void Start()
    {
        // 初始化随机种子
        if (_useSeed)
        {
            Random.InitState(_seed); // 使用指定的种子
        }
        else
        {
            int randomSeed = Random.Range(1, 1000000); // 生成随机种子
            Random.InitState(randomSeed);
            Debug.Log(randomSeed); // 输出种子以便重现
        }

        // 初始化地牢各部分
        CreateWalls();// 创建墙壁
        CreatePillars();// 创建支柱
        CreateFloorTile();// 创建地板
        AddWallJunctionPillars();// 添加墙壁接缝处的支柱

        // 为各部分添加碰撞体
        CreateCombinedCollider(_wallMatricesN, wallMesh);
        CreateCombinedCollider(_wallMatricesNB, wallMeshB);
        CreateCombinedCollider(_pillarMatrices, pillarMesh);
        CreateCombinedCollider(_floorMatrices, floorMesh);
        CreateCombinedCollider(_junctionPillarMatrices, junctionPillarMesh);

        // CreateWallsWithColliders();//为每个实例生成GameObject

        PlaceProps(); // 放置随机物品
    }

    void Update()
    {
        // 每帧渲染地牢各部分
        RenderWalls();
        RenderPillars();
        RenderFloor();
        RenderJunctionPillars();
    }

    #region 墙壁生成相关
    [Header("墙壁生成相关")]
    public Mesh wallMesh; // 普通墙壁网格
    public Mesh wallMeshB; // 特殊墙壁网格
    public Material wallMaterial; // 墙壁材质
    List<Matrix4x4> _wallMatricesN; // 普通墙壁变换矩阵列表
    List<Matrix4x4> _wallMatricesNB; // 特殊墙壁变换矩阵列表

    // 创建所有墙壁
    void CreateWalls()
    {
        _wallMatricesN = new List<Matrix4x4>();
        _wallMatricesNB = new List<Matrix4x4>();

        // 生成四条边的墙体
        CreateWallEdge(Vector3.forward, roomSize.y / 2, false);    // 北墙
        CreateWallEdge(Vector3.back, roomSize.y / 2, false);       // 南墙
        CreateWallEdge(Vector3.right, roomSize.x / 2, true);      // 东墙
        CreateWallEdge(Vector3.left, roomSize.x / 2, true);       // 西墙
    }

    // 创建单边墙体
    void CreateWallEdge(Vector3 direction, float offset, bool isVertical)
    {
        // 确定墙体方向
        Vector3 axis = isVertical ? Vector3.up : Vector3.zero;
        float rotationAngle = isVertical ? 90f : 0f;
        Vector2 size = isVertical ? new Vector2(roomSize.y, 0) : new Vector2(roomSize.x, 0);

        // 计算墙体数量和缩放比例
        int wallCount = Mathf.Max(1, (int)(size.x / wallMesh.bounds.size.x));
        float scale = (size.x / wallCount) / wallMesh.bounds.size.x;

        // 创建该边的所有墙体
        for (int i = 0; i < wallCount; i++)
        {
            // 计算墙体位置
            Vector3 position = transform.position + direction * offset;
            if (isVertical)
            {
                position += Vector3.forward * (-size.x / 2 + wallMesh.bounds.size.x * scale / 2 + i * scale * wallMesh.bounds.size.x);
            }
            else
            {
                position += Vector3.right * (-size.x / 2 + wallMesh.bounds.size.x * scale / 2 + i * scale * wallMesh.bounds.size.x);
            }

            // 设置旋转和缩放
            Quaternion rotation = transform.rotation * Quaternion.AngleAxis(rotationAngle, axis);
            Vector3 scaleVec = new Vector3(scale, 1, 1);

            // 创建变换矩阵
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scaleVec);

            // 随机分配墙体类型
            if (Random.Range(0, 2) == 0)
                _wallMatricesN.Add(matrix);
            else
                _wallMatricesNB.Add(matrix);
        }
    }

    // 渲染墙面物体的函数
    void RenderWalls()
    {
        // 渲染普通墙体
        if (_wallMatricesN != null && _wallMatricesN.Count > 0)
        {
            Graphics.DrawMeshInstanced(
                wallMesh,
                0,
                wallMaterial,
                _wallMatricesN.ToArray(),
                _wallMatricesN.Count
            );
        }

        // 渲染B类型墙体
        if (_wallMatricesNB != null && _wallMatricesNB.Count > 0)
        {
            Graphics.DrawMeshInstanced(
                wallMeshB,
                0,
                wallMaterial,
                _wallMatricesNB.ToArray(),
                _wallMatricesNB.Count
            );
        }
    }
    #endregion

    #region 支柱生成相关
    [Header("支柱生成相关")]
    public Mesh pillarMesh; // 支柱网格
    public Material pillarMaterial; // 支柱材质
    List<Matrix4x4> _pillarMatrices; // 支柱变换矩阵列表

    //在四个角落添加支柱
    void CreatePillars()
    {
        _pillarMatrices = new List<Matrix4x4>();

        // 定义房间四个角落的位置
        Vector3[] corners = new Vector3[]
        {
            new Vector3(-roomSize.x/2, 0, -roomSize.y/2), // 西南角
            new Vector3(-roomSize.x/2, 0, roomSize.y/2),  // 西北角
            new Vector3(roomSize.x/2, 0, -roomSize.y/2),  // 东南角
            new Vector3(roomSize.x/2, 0, roomSize.y/2)    // 东北角
        };

        // 为每个角落创建支柱
        foreach (Vector3 corner in corners)
        {
            Vector3 position = transform.position + corner;
            Quaternion rotation = transform.rotation;
            Vector3 scale = Vector3.one; // 支柱默认缩放

            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
            _pillarMatrices.Add(matrix);
        }
    }

    // 渲染支柱
    void RenderPillars()
    {
        if (_pillarMatrices != null && _pillarMatrices.Count > 0)
        {
            Graphics.DrawMeshInstanced(
                pillarMesh,
                0,
                pillarMaterial,
                _pillarMatrices.ToArray(),
                _pillarMatrices.Count
            );
        }
    }

    #endregion

    #region  地板生成相关
    [Header("地板生成相关")]
    public Mesh floorMesh; // 地板网格
    public Material floorMaterial; // 地板材质
    List<Matrix4x4> _floorMatrices; // 地板变换矩阵列表

    // 创建地板（缩放方式）
    void createFloorScale()
    {
        _floorMatrices = new List<Matrix4x4>();

        // 计算地板位置（房间中心）
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;

        // 根据房间尺寸缩放地板
        Vector3 scale = new Vector3(roomSize.x, 1, roomSize.y);

        Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
        _floorMatrices.Add(matrix);
    }

    // 创建地板（平铺方式）
    void CreateFloorTile()
    {
        _floorMatrices = new List<Matrix4x4>();

        // 获取地板网格的原始尺寸
        float floorWidth = floorMesh.bounds.size.x;
        float floorLength = floorMesh.bounds.size.z;

        // 计算X轴和Z轴需要的地板数量
        int floorCountX = Mathf.Max(1, Mathf.CeilToInt(roomSize.x / floorWidth));
        int floorCountZ = Mathf.Max(1, Mathf.CeilToInt(roomSize.y / floorLength)); // 注意：RoomSize.y对应的是Z轴长度

        // 计算实际缩放比例（使地板正好铺满房间）
        float scaleX = (roomSize.x / floorCountX) / floorWidth;
        float scaleZ = (roomSize.y / floorCountZ) / floorLength;

        // 起始位置（左下角）
        Vector3 startPos = transform.position + new Vector3(-roomSize.x / 2 + floorWidth * scaleX / 2, 0, -roomSize.y / 2 + floorLength * scaleZ / 2);

        // 平铺地板
        for (int x = 0; x < floorCountX; x++)
        {
            for (int z = 0; z < floorCountZ; z++)
            {
                // 计算每块地板的位置
                Vector3 position = startPos + new Vector3(
                    x * floorWidth * scaleX,
                    0,
                    z * floorLength * scaleZ
                );

                // 保持默认旋转
                Quaternion rotation = transform.rotation;

                // 设置缩放（保持Y轴不变）
                Vector3 scale = new Vector3(scaleX, 1, scaleZ);

                // 创建变换矩阵
                Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
                _floorMatrices.Add(matrix);
            }
        }
    }

    // 渲染地板
    void RenderFloor()
    {
        if (_floorMatrices != null && _floorMatrices.Count > 0)
        {
            Graphics.DrawMeshInstanced(
                floorMesh,
                0,
                floorMaterial,
                _floorMatrices.ToArray(),
                _floorMatrices.Count
            );
        }
    }
    #endregion

    #region 墙壁接缝支柱
    [Header("墙壁接缝支柱")]
    public Mesh junctionPillarMesh; // 接缝支柱网格
    public Material junctionPillarMaterial; // 接缝支柱材质
    List<Matrix4x4> _junctionPillarMatrices; // 接缝支柱变换矩阵

    void AddWallJunctionPillars()
    {
        if (junctionPillarMesh == null) return;

        _junctionPillarMatrices = new List<Matrix4x4>();

        // 获取墙体网格尺寸（假设所有墙体网格尺寸相同）
        float wallWidth = wallMesh.bounds.size.x;

        // 计算水平和垂直墙体的数量
        int horizontalWallCount = Mathf.Max(1, (int)(roomSize.x / wallWidth));
        int verticalWallCount = Mathf.Max(1, (int)(roomSize.y / wallWidth));

        // 计算墙体缩放比例
        float hScale = (roomSize.x / horizontalWallCount) / wallWidth;
        float vScale = (roomSize.y / verticalWallCount) / wallWidth;

        // 添加南北墙的接缝支柱（东西走向的墙）
        for (int i = 1; i < horizontalWallCount; i++)
        {
            // 北墙接缝
            float xPos = -roomSize.x / 2 + i * wallWidth * hScale;
            AddPillar(transform.position + new Vector3(xPos, 0, roomSize.y / 2));

            // 南墙接缝
            AddPillar(transform.position + new Vector3(xPos, 0, -roomSize.y / 2));
        }

        // 添加东西墙的接缝支柱（南北走向的墙）
        for (int i = 1; i < verticalWallCount; i++)
        {
            // 东墙接缝
            float zPos = -roomSize.y / 2 + i * wallWidth * vScale;
            AddPillar(transform.position + new Vector3(roomSize.x / 2, 0, zPos));

            // 西墙接缝
            AddPillar(transform.position + new Vector3(-roomSize.x / 2, 0, zPos));
        }
    }

    // 在指定位置添加支柱
    void AddPillar(Vector3 position, float scaleMultiplier = 1f)
    {
        Quaternion rotation = transform.rotation;
        Vector3 scale = Vector3.one * scaleMultiplier;
        _junctionPillarMatrices.Add(Matrix4x4.TRS(position, rotation, scale));
    }

    // 渲染接缝支柱
    void RenderJunctionPillars()
    {
        if (_junctionPillarMatrices != null && _junctionPillarMatrices.Count > 0)
        {
            Graphics.DrawMeshInstanced(
                junctionPillarMesh,
                0,
                junctionPillarMaterial,
                _junctionPillarMatrices.ToArray(),
                _junctionPillarMatrices.Count
            );
        }
    }
    #endregion

    #region 碰撞体生成
    private GameObject _collidersParent;

    void CreateCombinedCollider(List<Matrix4x4> matrices, Mesh mesh)
    {
        if (mesh == null) return;

        if (_collidersParent == null)
        {
            _collidersParent = new GameObject("Colliders");
            _collidersParent.transform.SetParent(transform);
        }

        // 合并所有网格
        CombineInstance[] combines = new CombineInstance[matrices.Count];
        for (int i = 0; i < matrices.Count; i++)
        {
            combines[i].mesh = mesh;
            combines[i].transform = matrices[i];
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combines);

        // 创建碰撞体父物体
        GameObject colliderGameObject = new GameObject(mesh.name + "Colliders");
        colliderGameObject.transform.SetParent(_collidersParent.transform);
        MeshCollider collider = colliderGameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = combinedMesh;
    }
    #endregion

    #region 为每个实例生成GameObject
    // 修改后的创建方法示例（以墙体为例）
    List<GameObject> _wallInstances = new List<GameObject>();

    void CreateWallsWithColliders()
    {
        // 销毁旧实例（如果存在）
        foreach (var wall in _wallInstances) Destroy(wall);
        _wallInstances.Clear();

        // 生成带碰撞体的墙体
        foreach (var matrix in _wallMatricesN)
        {
            GameObject wall = new GameObject("WallInstance");
            wall.transform.SetPositionAndRotation(matrix.GetPosition(), matrix.rotation);
            wall.transform.localScale = matrix.lossyScale;

            // 添加网格组件
            MeshFilter filter = wall.AddComponent<MeshFilter>();
            filter.mesh = wallMesh;
            MeshRenderer renderer = wall.AddComponent<MeshRenderer>();
            renderer.material = wallMaterial;

            // 添加碰撞体（根据需求选择类型）
            MeshCollider collider = wall.AddComponent<MeshCollider>();
            collider.sharedMesh = wallMesh;
            collider.convex = false; // 对于静态墙体设为false

            _wallInstances.Add(wall);
        }
    }
    #endregion

    #region 道具放置系统
    [Header("道具放置系统")]
    public DungeonProp[] dungeonProps; // 可放置的道具配置
    private float _wallThickness; // 墙体一半厚度
    private GameObject _propsParent; //父物体

    // 放置道具
    void PlaceProps()
    {
        if (dungeonProps == null || dungeonProps.Length == 0) return;

        _wallThickness = wallMesh.bounds.extents.z;

        // 计算实际可用区域（考虑墙体厚度）
        float usableWidth = roomSize.x - 2 * _wallThickness;
        float usableHeight = roomSize.y - 2 * _wallThickness;

        // 计算房间边界（考虑墙体厚度）
        float minX = transform.position.x - usableWidth / 2;
        float maxX = transform.position.x + usableWidth / 2;
        float minZ = transform.position.z - usableHeight / 2;
        float maxZ = transform.position.z + usableHeight / 2;

        // 创建道具父物体
        if (_propsParent == null)
        {
            _propsParent = new GameObject("PropsParent");
            _propsParent.transform.SetParent(transform);
        } 

        // 存储已放置物体的OBB信息
        List<OBB> placedOBBs = new List<OBB>();

        foreach (var prop in dungeonProps)
        {
            // 计算实际要放置的数量
            int count = Random.Range(prop.minCount, prop.maxCount + 1);

            for (int i = 0; i < count; i++)
            {
                // 根据概率决定是否生成
                if (Random.value > prop.spawnProbability) continue;

                Vector3 position = Vector3.zero;
                Quaternion rotation = Quaternion.identity;
                Vector3 halfExtents = Vector3.zero;

                // 根据位置类型确定位置和朝向
                switch (prop.positionType)
                {
                    case DungeonProp.PositionType.Wall:
                        if (!TryFindWallPosition(prop.prefab, minX, maxX, minZ, maxZ,
                                               out position, out rotation, out halfExtents))
                            continue;
                        break;

                    case DungeonProp.PositionType.Corner:
                        if (!TryFindCornerPosition(prop.prefab, minX, maxX, minZ, maxZ,
                                                out position, out rotation, out halfExtents))
                            continue;
                        break;

                    case DungeonProp.PositionType.Middle:
                        if (!TryFindMiddlePosition(prop.prefab, minX, maxX, minZ, maxZ,
                                                 out position, out rotation, out halfExtents))
                            continue;
                        break;

                    case DungeonProp.PositionType.Anywhere:
                        if (!TryFindAnyPosition(prop.prefab, minX, maxX, minZ, maxZ,
                                              out position, out rotation, out halfExtents))
                            continue;
                        break;
                }

                // 应用随机旋转
                if (prop.randomRotation)
                {
                    rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                }

                // 检查碰撞并放置道具
                if (!CheckOverlap(position, halfExtents, rotation, placedOBBs))
                {
                    GameObject propInstance = Instantiate(prop.prefab, position + prop.offset, rotation);

                    propInstance.transform.SetParent(_propsParent.transform);

                    placedOBBs.Add(new OBB()
                    {
                        center = position,
                        extents = halfExtents,
                        rotation = rotation
                    });
                }
            }
        }
    }

    // 尝试在墙边放置道具
    bool TryFindWallPosition(GameObject prefab, float minX, float maxX, float minZ, float maxZ,
                            out Vector3 position, out Quaternion rotation, out Vector3 halfExtents)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        halfExtents = Vector3.zero;

        // 获取预制体大小
        Bounds bounds = GetPrefabBounds(prefab);
        if (bounds.size == Vector3.zero) return false;

        //获取包围盒（Bounds）的半尺寸
        halfExtents = bounds.extents;

        // 随机选择一面墙
        int wallIndex = Random.Range(0, 4);

        switch (wallIndex)
        {
            case 0: // 北墙 (z最大)
                position = new Vector3(
                    Random.Range(minX + halfExtents.x, maxX - halfExtents.x),
                    0,
                    maxZ - halfExtents.z
                );
                rotation = Quaternion.Euler(0, 180, 0); // 面朝南
                break;

            case 1: // 南墙 (z最小)
                position = new Vector3(
                    Random.Range(minX + halfExtents.x, maxX - halfExtents.x),
                    0,
                    minZ + halfExtents.z
                );
                rotation = Quaternion.identity; // 面朝北
                break;

            case 2: // 东墙 (x最大)
                position = new Vector3(
                    maxX - halfExtents.z,
                    0,
                    Random.Range(minZ + halfExtents.x, maxZ - halfExtents.x)
                );
                rotation = Quaternion.Euler(0, 270, 0); // 面朝西
                break;

            case 3: // 西墙 (x最小)
                position = new Vector3(
                    minX + halfExtents.z,
                    0,
                    Random.Range(minZ + halfExtents.x, maxZ - halfExtents.x)
                );
                rotation = Quaternion.Euler(0, 90, 0); // 面朝东
                break;
        }

        return true;
    }

    // 尝试在角落放置道具
    bool TryFindCornerPosition(GameObject prefab, float minX, float maxX, float minZ, float maxZ,
                             out Vector3 position, out Quaternion rotation, out Vector3 halfExtents)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        halfExtents = Vector3.zero;

        // 获取预制体大小
        Bounds bounds = GetPrefabBounds(prefab);
        if (bounds.size == Vector3.zero) return false;

        //获取包围盒（Bounds）的半尺寸
        halfExtents = bounds.extents;

        // 随机选择一个墙角
        int cornerIndex = Random.Range(0, 4);
        float cornerOffset = _wallThickness + Mathf.Max(halfExtents.x, halfExtents.z);

        switch (cornerIndex)
        {
            case 0: // 西北角
                position = new Vector3(
                    minX + cornerOffset,
                    0,
                    maxZ - cornerOffset
                );
                rotation = Quaternion.Euler(0, 225, 0); // 面向东南
                break;

            case 1: // 东北角
                position = new Vector3(
                    maxX - cornerOffset,
                    0,
                    maxZ - cornerOffset
                );
                rotation = Quaternion.Euler(0, 315, 0); // 面向西南
                break;

            case 2: // 西南角
                position = new Vector3(
                    minX + cornerOffset,
                    0,
                    minZ + cornerOffset
                );
                rotation = Quaternion.Euler(0, 135, 0); // 面向东北
                break;

            case 3: // 东南角
                position = new Vector3(
                    maxX - cornerOffset,
                    0,
                    minZ + cornerOffset
                );
                rotation = Quaternion.Euler(0, 45, 0); // 面向西北
                break;
        }

        return true;
    }

    // 尝试在中间区域放置道具
    bool TryFindMiddlePosition(GameObject prefab, float minX, float maxX, float minZ, float maxZ,
                             out Vector3 position, out Quaternion rotation, out Vector3 halfExtents)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        halfExtents = Vector3.zero;

        // 获取预制体大小
        Bounds bounds = GetPrefabBounds(prefab);
        if (bounds.size == Vector3.zero) return false;

        //获取包围盒（Bounds）的半尺寸
        halfExtents = bounds.extents;

        // 中间区域（避开墙边）
        float safeMargin = Mathf.Max(halfExtents.x, halfExtents.z) * 2;

        position = new Vector3(
            Random.Range(minX + safeMargin, maxX - safeMargin),
            0,
            Random.Range(minZ + safeMargin, maxZ - safeMargin)
        );
        return true;
    }

    // 尝试在任意位置放置道具
    bool TryFindAnyPosition(GameObject prefab, float minX, float maxX, float minZ, float maxZ,
                          out Vector3 position, out Quaternion rotation, out Vector3 halfExtents)
    {
        // 50%概率放在墙边，25%概率放在墙角，25%概率放在中间
        float rand = Random.value;

        if (rand < 0.5f)
            return TryFindWallPosition(prefab, minX, maxX, minZ, maxZ, out position, out rotation, out halfExtents);
        else if (rand < 0.75f)
            return TryFindCornerPosition(prefab, minX, maxX, minZ, maxZ, out position, out rotation, out halfExtents);
        else
            return TryFindMiddlePosition(prefab, minX, maxX, minZ, maxZ, out position, out rotation, out halfExtents);
    }

    // 获取预制体的包围盒
    Bounds GetPrefabBounds(GameObject prefab)
    {
        Renderer renderer = prefab.GetComponentInChildren<Renderer>();
        if (renderer != null) return renderer.bounds;

        // 如果预制体没有渲染器，尝试使用碰撞器
        Collider collider = prefab.GetComponentInChildren<Collider>();
        if (collider != null) return collider.bounds;

        // 默认大小
        return new Bounds(Vector3.zero, Vector3.one * 0.5f);
    }

    /// <summary>
    /// 检查新物体是否与已放置物体发生OBB重叠
    /// </summary>
    /// <param name="position">新物体的中心位置</param>
    /// <param name="halfExtents">新物体的半尺寸</param>
    /// <param name="rotation">新物体的旋转</param>
    /// <param name="existingOBBs">已放置物体的OBB列表</param>
    /// <returns>true表示有重叠，false表示无重叠</returns>
    bool CheckOverlap(Vector3 position, Vector3 halfExtents, Quaternion rotation, List<OBB> existingOBBs)
    {
        // 创建新物体的OBB
        OBB newOBB = new OBB()
        {
            center = position,    // 设置中心点
            extents = halfExtents, // 设置半尺寸
            rotation = rotation   // 设置旋转
        };

        // 遍历所有已放置物体的OBB
        foreach (var obb in existingOBBs)
        {
            // 如果与任一已放置物体相交，返回true
            if (newOBB.Intersects(obb))
                return true;
        }

        // 没有发现重叠
        return false;
    }
    #endregion
}
