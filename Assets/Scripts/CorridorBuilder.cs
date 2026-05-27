using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 走廊构建器 v7
/// 1. 采用“直行段 + 拐角节点”分离生成算法，彻底解决拐弯处墙体穿插堵路、外侧漏空的问题。
/// 2. 智能拐角：自动检测连接方向，仅在无连接的盲侧生成密封墙。
/// 3. 直行缩进：直行段向后预留出拐角正方形区域，保证无缝拼接。
/// 4. 走廊灯光：在直行段的墙面上，自动、随机、对齐地挂载点燃版火炬。
/// </summary>
public class CorridorBuilder : MonoBehaviour
{
    [Header("走廊尺寸")]
    public float corridorWidth          = 3f;
    public float corridorWallHeight     = 4f;
    public float corridorWallThickness  = 0.3f;

    [Header("走廊地板厚度")]
    public float corridorFloorThickness = 0.1f;

    [Header("材质（留空则使用默认色）")]
    public Material corridorMaterial;
    public Material corridorWallMaterial;

    [Header("走廊火炬自动生成（可选）")]
    public GameObject torchPrefab;                           // 拖入您的 Lit_Torch 预制体
    [Range(0, 1)] public float torchSpawnProbability = 0.25f; // 生成火炬的概率（25% 概率）
    public float torchHeight = 2.8f;                         // 火炬挂墙高度

    [HideInInspector]
    public List<CorridorConnection> connections = new List<CorridorConnection>();

    private GameObject _corridorRoot;

    void Awake()
    {
        _corridorRoot = new GameObject("Corridors");
        _corridorRoot.transform.SetParent(transform);
    }

    /// <summary>
    /// 智能规划 Z 型或 L 型通路，并采用节点分离算法渲染
    /// </summary>
    public void BuildSmartCorridor(Vector3 doorL, Vector3 doorR, 
                                   GenerateDungeon.WallDirection exitDir, 
                                   GenerateDungeon.WallDirection enterDir, 
                                   int connectionId = -1)
    {
        bool exitIsHorizontal  = (exitDir == GenerateDungeon.WallDirection.East  || exitDir == GenerateDungeon.WallDirection.West);
        bool enterIsHorizontal = (enterDir == GenerateDungeon.WallDirection.East || enterDir == GenerateDungeon.WallDirection.West);

        List<Vector3> path = new List<Vector3>();
        List<bool> isCorner = new List<bool>();

        // // 1. 路径点规划
        // if (exitIsHorizontal == enterIsHorizontal)
        // {
        //     // 同向连接：3 段式 Z 型走廊
        //     path.Add(doorL);
        //     isCorner.Add(false);

        //     if (exitIsHorizontal)
        //     {
        //         float midX = (doorL.x + doorR.x) / 2f;
        //         path.Add(new Vector3(midX, 0, doorL.z)); // 拐角 1
        //         path.Add(new Vector3(midX, 0, doorR.z)); // 拐角 2
        //     }
        //     else
        //     {
        //         float midZ = (doorL.z + doorR.z) / 2f;
        //         path.Add(new Vector3(doorL.x, 0, midZ)); // 拐角 1
        //         path.Add(new Vector3(doorR.x, 0, midZ)); // 拐角 2
        //     }
        //     isCorner.Add(true);
        //     isCorner.Add(true);

        //     path.Add(doorR);
        //     isCorner.Add(false);
        // }
        // else
        // {
        //     // 异向连接：2 段式 L 型走廊
        //     path.Add(doorL);
        //     isCorner.Add(false);

        //     Vector3 corner = exitIsHorizontal 
        //         ? new Vector3(doorR.x, 0, doorL.z) 
        //         : new Vector3(doorL.x, 0, doorR.z);

        //     path.Add(corner);
        //     isCorner.Add(true);

        //     path.Add(doorR);
        //     isCorner.Add(false);
        // }

        // 1. 路径点规划
        if (exitIsHorizontal == enterIsHorizontal)
        {
            // 同向连接
            if (exitIsHorizontal)
            {
                // ── 新增：如果 Z 轴已经完美对齐，直接生成单段直行走廊 ──
                if (Mathf.Abs(doorL.z - doorR.z) < 0.01f)
                {
                    path.Add(doorL); isCorner.Add(false);
                    path.Add(doorR); isCorner.Add(false);
                }
                else
                {
                    // 3 段式 Z 型走廊
                    path.Add(doorL); isCorner.Add(false);
                    float midX = (doorL.x + doorR.x) / 2f;
                    path.Add(new Vector3(midX, 0, doorL.z)); isCorner.Add(true);
                    path.Add(new Vector3(midX, 0, doorR.z)); isCorner.Add(true);
                    path.Add(doorR); isCorner.Add(false);
                }
            }
            else
            {
                // ── 新增：如果 X 轴已经完美对齐，直接生成单段直行走廊 ──
                if (Mathf.Abs(doorL.x - doorR.x) < 0.01f)
                {
                    path.Add(doorL); isCorner.Add(false);
                    path.Add(doorR); isCorner.Add(false);
                }
                else
                {
                    // 3 段式 Z 型走廊
                    path.Add(doorL); isCorner.Add(false);
                    float midZ = (doorL.z + doorR.z) / 2f;
                    path.Add(new Vector3(doorL.x, 0, midZ)); isCorner.Add(true);
                    path.Add(new Vector3(doorR.x, 0, midZ)); isCorner.Add(true);
                    path.Add(doorR); isCorner.Add(false);
                }
            }
        }

        // 2. 第一阶段：生成所有拐角节点 (Corners)
        for (int i = 0; i < path.Count; i++)
        {
            if (isCorner[i])
            {
                Vector3 dirPrev = (path[i - 1] - path[i]).normalized;
                Vector3 dirNext = (path[i + 1] - path[i]).normalized;
                BuildCornerNode(path[i], dirPrev, dirNext);
            }
        }

        // 3. 第二阶段：生成所有缩进直行段 (Straight Segments)
        for (int i = 0; i < path.Count - 1; i++)
        {
            BuildStraightSegment(path[i], path[i + 1], isCorner[i], isCorner[i + 1]);
        }

        connections.Add(new CorridorConnection
        {
            id   = connectionId,
            from = doorL,
            to   = doorR
        });
    }

    /// <summary>
    /// 生成一个独立的拐角节点，自动密封盲区侧墙
    /// </summary>
    private void BuildCornerNode(Vector3 pos, Vector3 dir1, Vector3 dir2)
    {
        // 生成拐角正方形地板
        CreateBox(
            name:   "CorridorCornerFloor",
            pos:    new Vector3(pos.x, -corridorFloorThickness / 2f, pos.z),
            scaleX: corridorWidth,
            scaleY: corridorFloorThickness,
            scaleZ: corridorWidth,
            mat:    corridorMaterial,
            defaultColor: new Color(0.45f, 0.45f, 0.45f),
            addCollider: true
        );

        // 检测开放方向
        HashSet<Vector3> openDirections = new HashSet<Vector3>
        {
            GetNearestCardinal(dir1),
            GetNearestCardinal(dir2)
        };

        // 四个基准方向
        Vector3[] cardinals = {
            Vector3.forward, // 北
            Vector3.back,    // 南
            Vector3.right,   // 东
            Vector3.left     // 西
        };

        float wallCenterY = corridorWallHeight / 2f;

        // 在未连接通道的盲端，生成密封挡墙
        foreach (var card in cardinals)
        {
            if (openDirections.Contains(card)) continue; // 开放端，不生成墙体

            Vector3 wallPos = pos + card * (corridorWidth / 2f + corridorWallThickness / 2f);
            wallPos.y = wallCenterY;

            bool isNSWall = (card == Vector3.forward || card == Vector3.back);
            // 拐角墙稍微加宽两个墙厚度，以在 90 度折角处无缝拼接
            float scaleX = isNSWall ? (corridorWidth + corridorWallThickness * 2f) : corridorWallThickness;
            float scaleZ = isNSWall ? corridorWallThickness : (corridorWidth + corridorWallThickness * 2f);

            CreateBox(
                name:   "CorridorCornerWall",
                pos:    wallPos,
                scaleX: scaleX,
                scaleY: corridorWallHeight,
                scaleZ: scaleZ,
                mat:    corridorWallMaterial ?? corridorMaterial,
                defaultColor: new Color(0.42f, 0.42f, 0.42f),
                addCollider: true
            );
        }
    }

    /// <summary>
    /// 生成缩进式的直行段
    /// </summary>
    private void BuildStraightSegment(Vector3 a, Vector3 b, bool aIsCorner, bool bIsCorner)
    {
        Vector3 dir = (b - a).normalized;
        Vector3 start = a;
        Vector3 end = b;

        // 如果端点是拐角，直行段起点/终点向后缩进半个通道宽，为正方形拐角留出空间
        if (aIsCorner) start += dir * (corridorWidth / 2f);
        if (bIsCorner) end   -= dir * (corridorWidth / 2f);

        float segmentLength = Vector3.Distance(start, end);
        if (segmentLength < 0.01f) return;

        Vector3 center = (start + end) * 0.5f;
        bool isAlongX = Mathf.Abs(dir.x) > 0.5f;

        // ── 地板 ──
        float floorSX = isAlongX ? segmentLength : corridorWidth;
        float floorSZ = isAlongX ? corridorWidth : segmentLength;

        CreateBox(
            name:   "CorridorFloor",
            pos:    new Vector3(center.x, -corridorFloorThickness / 2f, center.z),
            scaleX: floorSX,
            scaleY: corridorFloorThickness,
            scaleZ: floorSZ,
            mat:    corridorMaterial,
            defaultColor: new Color(0.45f, 0.45f, 0.45f),
            addCollider: true
        );

        // ── 侧墙（因为已经精确缩减，所以墙体直接生成，无需额外收缩） ──
        float wallCenterY = corridorWallHeight / 2f;
        float wallOffset  = corridorWidth / 2f + corridorWallThickness / 2f;

        float wallSX = isAlongX ? segmentLength : corridorWallThickness;
        float wallSZ = isAlongX ? corridorWallThickness : segmentLength;

        if (isAlongX)
        {
            // 正 Z 侧墙
            CreateBox(
                name:   "CorridorWall_Z+",
                pos:    new Vector3(center.x, wallCenterY, center.z + wallOffset),
                scaleX: wallSX,
                scaleY: corridorWallHeight,
                scaleZ: wallSZ,
                mat:    corridorWallMaterial ?? corridorMaterial,
                defaultColor: new Color(0.42f, 0.42f, 0.42f),
                addCollider: true
            );
            // 负 Z 侧墙
            CreateBox(
                name:   "CorridorWall_Z-",
                pos:    new Vector3(center.x, wallCenterY, center.z - wallOffset),
                scaleX: wallSX,
                scaleY: corridorWallHeight,
                scaleZ: wallSZ,
                mat:    corridorWallMaterial ?? corridorMaterial,
                defaultColor: new Color(0.42f, 0.42f, 0.42f),
                addCollider: true
            );
        }
        else
        {
            // 正 X 侧墙
            CreateBox(
                name:   "CorridorWall_X+",
                pos:    new Vector3(center.x + wallOffset, wallCenterY, center.z),
                scaleX: wallSX,
                scaleY: corridorWallHeight,
                scaleZ: wallSZ,
                mat:    corridorWallMaterial ?? corridorMaterial,
                defaultColor: new Color(0.42f, 0.42f, 0.42f),
                addCollider: true
            );
            // 负 X 侧墙
            CreateBox(
                name:   "CorridorWall_X-",
                pos:    new Vector3(center.x - wallOffset, wallCenterY, center.z),
                scaleX: wallSX,
                scaleY: corridorWallHeight,
                scaleZ: wallSZ,
                mat:    corridorWallMaterial ?? corridorMaterial,
                defaultColor: new Color(0.42f, 0.42f, 0.42f),
                addCollider: true
            );
        }

        // ─── 新增：在直行段的侧墙上自动随机生成走廊火炬 ───
        if (torchPrefab != null && segmentLength > corridorWidth  && Random.value < torchSpawnProbability)
        {
            // 随机选择直行段长度上的一个位置点（避开拐角）
            float tOffset = Random.Range(corridorWidth / 2f, segmentLength - corridorWidth / 2f);
            Vector3 torchPos = start + dir * tOffset;
            torchPos.y = torchHeight;

            Quaternion torchRot = Quaternion.identity;
            if (isAlongX)
            {
                // 走廊沿 X 轴：火炬随机挂在 Z+ 侧墙 或 Z- 侧墙内壁
                if (Random.value < 0.5f)
                {
                    torchPos.z += (corridorWidth / 2f - 0.05f); // 贴在 Z+ 墙面
                    torchRot = Quaternion.Euler(0, 180, 0);    // 面向南方
                }
                else
                {
                    torchPos.z -= (corridorWidth / 2f - 0.05f); // 贴在 Z- 墙面
                    torchRot = Quaternion.Euler(0, 0, 0);      // 面向北方
                }
            }
            else
            {
                // 走廊沿 Z 轴：火炬随机挂在 X+ 侧墙 或 X- 侧墙内壁
                if (Random.value < 0.5f)
                {
                    torchPos.x += (corridorWidth / 2f - 0.05f); // 贴在 X+ 墙面
                    torchRot = Quaternion.Euler(0, 270, 0);    // 面向西方
                }
                else
                {
                    torchPos.x -= (corridorWidth / 2f - 0.05f); // 贴在 X- 墙面
                    torchRot = Quaternion.Euler(0, 90, 0);     // 面向东方
                }
            }

            // 实例化走廊火炬并挂入走廊根节点下
            GameObject cTorch = Instantiate(torchPrefab, torchPos, torchRot, _corridorRoot.transform);
            cTorch.name = "CorridorTorch";
        }
    }

    private Vector3 GetNearestCardinal(Vector3 dir)
    {
        float absX = Mathf.Abs(dir.x);
        float absZ = Mathf.Abs(dir.z);
        if (absX > absZ)
        {
            return dir.x > 0 ? Vector3.right : Vector3.left;
        }
        else
        {
            return dir.z > 0 ? Vector3.forward : Vector3.back;
        }
    }

    private void CreateBox(string name, Vector3 pos,
                           float scaleX, float scaleY, float scaleZ,
                           Material mat, Color defaultColor,
                           bool addCollider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(_corridorRoot.transform);
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

        if (mat != null)
        {
            go.GetComponent<Renderer>().material = mat;
        }
        else
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("URP/Lit")
                      ?? Shader.Find("Standard");

            if (shader != null)
            {
                var m = new Material(shader);
                m.color = defaultColor;
                go.GetComponent<Renderer>().material = m;
            }
        }

        if (!addCollider)
            Destroy(go.GetComponent<Collider>());
    }
}

[System.Serializable]
public class CorridorConnection
{
    public int     id;
    public Vector3 from;
    public Vector3 to;
}