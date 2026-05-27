// using System.Collections.Generic;
// using UnityEngine;

// /// <summary>
// /// 走廊构建器 v5
// /// 1. 移除了走廊天花板。
// /// 2. 升级为智能 3 段 Z 型/ 2 段 L 型混合规划，确保走廊进出任何门时都绝对保持垂直，杜绝平贴外墙。
// /// 3. 对拐角侧墙进行了向后收缩处理，保证内部拐折处的物理畅通。
// /// </summary>
// public class CorridorBuilder : MonoBehaviour
// {
//     [Header("走廊尺寸")]
//     public float corridorWidth          = 3f;
//     public float corridorWallHeight     = 4f;
//     public float corridorWallThickness  = 0.3f;

//     [Header("走廊地板厚度")]
//     public float corridorFloorThickness = 0.1f;

//     [Header("材质（留空则使用默认色）")]
//     public Material corridorMaterial;
//     public Material corridorWallMaterial;

//     [HideInInspector]
//     public List<CorridorConnection> connections = new List<CorridorConnection>();

//     private GameObject _corridorRoot;

//     void Awake()
//     {
//         _corridorRoot = new GameObject("Corridors");
//         _corridorRoot.transform.SetParent(transform);
//     }

//     /// <summary>
//     /// 核心升级：根据起、终点的墙面方向，规划完美的 Z 型或 L 型通路。
//     /// </summary>
//     public void BuildSmartCorridor(Vector3 doorL, Vector3 doorR, 
//                                    GenerateDungeon.WallDirection exitDir, 
//                                    GenerateDungeon.WallDirection enterDir, 
//                                    int connectionId = -1)
//     {
//         bool exitIsHorizontal  = (exitDir == GenerateDungeon.WallDirection.East  || exitDir == GenerateDungeon.WallDirection.West);
//         bool enterIsHorizontal = (enterDir == GenerateDungeon.WallDirection.East || enterDir == GenerateDungeon.WallDirection.West);

//         if (exitIsHorizontal == enterIsHorizontal)
//         {
//             // 场景 1：同向连接（比如东墙连西墙，或南墙连北墙） -> 强制走 3 段式 Z 型走廊，确保出入口均垂直于墙面
//             if (exitIsHorizontal)
//             {
//                 // 横向同向连接：X轴延伸 -> Z轴拐弯 -> X轴延伸
//                 float midX = (doorL.x + doorR.x) / 2f;
//                 Vector3 c1 = new Vector3(midX, 0, doorL.z);
//                 Vector3 c2 = new Vector3(midX, 0, doorR.z);

//                 BuildSegment(doorL, c1);
//                 BuildSegment(c1, c2);
//                 BuildSegment(c2, doorR);
//             }
//             else
//             {
//                 // 纵向同向连接：Z轴延伸 -> X轴拐弯 -> Z轴延伸
//                 float midZ = (doorL.z + doorR.z) / 2f;
//                 Vector3 c1 = new Vector3(doorL.x, 0, midZ);
//                 Vector3 c2 = new Vector3(doorR.x, 0, midZ);

//                 BuildSegment(doorL, c1);
//                 BuildSegment(c1, c2);
//                 BuildSegment(c2, doorR);
//             }
//         }
//         else
//         {
//             // 场景 2：异向连接（比如东墙连南墙） -> 正常的 2 段式 L 型走廊，此时两端自然垂直于墙体
//             Vector3 corner = exitIsHorizontal 
//                 ? new Vector3(doorR.x, 0, doorL.z) 
//                 : new Vector3(doorL.x, 0, doorR.z);

//             BuildSegment(doorL, corner);
//             BuildSegment(corner, doorR);
//         }

//         connections.Add(new CorridorConnection
//         {
//             id   = connectionId,
//             from = doorL,
//             to   = doorR
//         });
//     }

//     private void BuildSegment(Vector3 a, Vector3 b)
//     {
//         float dist = Vector3.Distance(a, b);
//         if (dist < 0.01f) return;

//         Vector3 center = (a + b) * 0.5f;
//         Vector3 dir    = (b - a).normalized;

//         bool isAlongX = Mathf.Abs(dir.x) > 0.5f;

//         // ── 地板 ──
//         float floorSX = isAlongX ? dist : corridorWidth;
//         float floorSZ = isAlongX ? corridorWidth : dist;

//         CreateBox(
//             name:   "CorridorFloor",
//             pos:    new Vector3(center.x, -corridorFloorThickness / 2f, center.z),
//             scaleX: floorSX,
//             scaleY: corridorFloorThickness,
//             scaleZ: floorSZ,
//             mat:    corridorMaterial,
//             defaultColor: new Color(0.45f, 0.45f, 0.45f),
//             addCollider: true
//         );

//         // ── 侧墙收缩 ──
//         float wallOverlapOffset = corridorWidth / 2f;
//         float wallLength = Mathf.Max(0.1f, dist - wallOverlapOffset); 
//         Vector3 wallCenter = a + dir * (wallLength * 0.5f);

//         float wallCenterY = corridorWallHeight / 2f;
//         float wallOffset  = corridorWidth / 2f + corridorWallThickness / 2f;

//         if (isAlongX)
//         {
//             float wallSX = wallLength; 
//             float wallSZ = corridorWallThickness;

//             CreateBox(
//                 name:   "CorridorWall_Z+",
//                 pos:    new Vector3(wallCenter.x, wallCenterY, wallCenter.z + wallOffset),
//                 scaleX: wallSX,
//                 scaleY: corridorWallHeight,
//                 scaleZ: wallSZ,
//                 mat:    corridorWallMaterial ?? corridorMaterial,
//                 defaultColor: new Color(0.42f, 0.42f, 0.42f),
//                 addCollider: true
//             );
//             CreateBox(
//                 name:   "CorridorWall_Z-",
//                 pos:    new Vector3(wallCenter.x, wallCenterY, wallCenter.z - wallOffset),
//                 scaleX: wallSX,
//                 scaleY: corridorWallHeight,
//                 scaleZ: wallSZ,
//                 mat:    corridorWallMaterial ?? corridorMaterial,
//                 defaultColor: new Color(0.42f, 0.42f, 0.42f),
//                 addCollider: true
//             );
//         }
//         else
//         {
//             float wallSX = corridorWallThickness;
//             float wallSZ = wallLength; 

//             CreateBox(
//                 name:   "CorridorWall_X+",
//                 pos:    new Vector3(wallCenter.x + wallOffset, wallCenterY, wallCenter.z),
//                 scaleX: wallSX,
//                 scaleY: corridorWallHeight,
//                 scaleZ: wallSZ,
//                 mat:    corridorWallMaterial ?? corridorMaterial,
//                 defaultColor: new Color(0.42f, 0.42f, 0.42f),
//                 addCollider: true
//             );
//             CreateBox(
//                 name:   "CorridorWall_X-",
//                 pos:    new Vector3(wallCenter.x - wallOffset, wallCenterY, wallCenter.z),
//                 scaleX: wallSX,
//                 scaleY: corridorWallHeight,
//                 scaleZ: wallSZ,
//                 mat:    corridorWallMaterial ?? corridorMaterial,
//                 defaultColor: new Color(0.42f, 0.42f, 0.42f),
//                 addCollider: true
//             );
//         }
//     }

//     private void CreateBox(string name, Vector3 pos,
//                            float scaleX, float scaleY, float scaleZ,
//                            Material mat, Color defaultColor,
//                            bool addCollider)
//     {
//         var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
//         go.name = name;
//         go.transform.SetParent(_corridorRoot.transform);
//         go.transform.position   = pos;
//         go.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

//         if (mat != null)
//         {
//             go.GetComponent<Renderer>().material = mat;
//         }
//         else
//         {
//             var shader = Shader.Find("Universal Render Pipeline/Lit")
//                       ?? Shader.Find("URP/Lit")
//                       ?? Shader.Find("Standard");

//             if (shader != null)
//             {
//                 var m = new Material(shader);
//                 m.color = defaultColor;
//                 go.GetComponent<Renderer>().material = m;
//             }
//         }

//         if (!addCollider)
//             Destroy(go.GetComponent<Collider>());
//     }
// }

// [System.Serializable]
// public class CorridorConnection
// {
//     public int     id;
//     public Vector3 from;
//     public Vector3 to;
// }



using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 走廊构建器 v6
/// 1. 采用“直行段 + 拐角节点”分离生成算法，彻底解决拐弯处墙体穿插堵路、外侧漏空的问题。
/// 2. 智能拐角：自动检测连接方向，仅在无连接的盲侧生成密封墙。
/// 3. 直行缩进：直行段向后预留出拐角正方形区域，保证无缝拼接。
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

        // 1. 路径点规划
        if (exitIsHorizontal == enterIsHorizontal)
        {
            // 同向连接：3 段式 Z 型走廊
            path.Add(doorL);
            isCorner.Add(false);

            if (exitIsHorizontal)
            {
                float midX = (doorL.x + doorR.x) / 2f;
                path.Add(new Vector3(midX, 0, doorL.z)); // 拐角 1
                path.Add(new Vector3(midX, 0, doorR.z)); // 拐角 2
            }
            else
            {
                float midZ = (doorL.z + doorR.z) / 2f;
                path.Add(new Vector3(doorL.x, 0, midZ)); // 拐角 1
                path.Add(new Vector3(doorR.x, 0, midZ)); // 拐角 2
            }
            isCorner.Add(true);
            isCorner.Add(true);

            path.Add(doorR);
            isCorner.Add(false);
        }
        else
        {
            // 异向连接：2 段式 L 型走廊
            path.Add(doorL);
            isCorner.Add(false);

            Vector3 corner = exitIsHorizontal 
                ? new Vector3(doorR.x, 0, doorL.z) 
                : new Vector3(doorL.x, 0, doorR.z);

            path.Add(corner);
            isCorner.Add(true);

            path.Add(doorR);
            isCorner.Add(false);
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