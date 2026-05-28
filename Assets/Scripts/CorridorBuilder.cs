// using System.Collections.Generic;
// using UnityEngine;

// /// <summary>
// /// 走廊构建器 v8 (动态宽度适配版)
// /// </summary>
// public class CorridorBuilder : MonoBehaviour
// {
//     [Header("默认走廊尺寸（作为备用）")]
//     public float corridorWidth          = 3f;
//     public float corridorWallHeight     = 4f;
//     public float corridorWallThickness  = 0.3f;

//     [Header("走廊地板厚度")]
//     public float corridorFloorThickness = 0.1f;

//     [Header("材质（留空则使用默认色）")]
//     public Material corridorMaterial;
//     public Material corridorWallMaterial;

//     [Header("走廊火炬自动生成（可选）")]
//     public GameObject torchPrefab;                           
//     [Range(0, 1)] public float torchSpawnProbability = 0.25f; 
//     public float torchHeight = 2.8f;                         

//     [HideInInspector]
//     public List<CorridorConnection> connections = new List<CorridorConnection>();

//     private GameObject _corridorRoot;

//     void Awake()
//     {
//         _corridorRoot = new GameObject("Corridors");
//         _corridorRoot.transform.SetParent(transform);
//     }

//     /// <summary>
//     /// 智能规划 Z 型或 L 型通路，并采用节点分离算法渲染（使用动态宽度）
//     /// </summary>
//     public void BuildSmartCorridor(Vector3 doorL, Vector3 doorR, 
//                                    GenerateDungeon.WallDirection exitDir, 
//                                    GenerateDungeon.WallDirection enterDir, 
//                                    float width, // 新增：动态传入的走廊内宽
//                                    int connectionId = -1)
//     {
//         bool exitIsHorizontal  = (exitDir == GenerateDungeon.WallDirection.East  || exitDir == GenerateDungeon.WallDirection.West);
//         bool enterIsHorizontal = (enterDir == GenerateDungeon.WallDirection.East || enterDir == GenerateDungeon.WallDirection.West);

//         List<Vector3> path = new List<Vector3>();
//         List<bool> isCorner = new List<bool>();

//         // 1. 路径点规划
//         if (exitIsHorizontal == enterIsHorizontal)
//         {
//             if (exitIsHorizontal)
//             {
//                 if (Mathf.Abs(doorL.z - doorR.z) < width)
//                 {
//                     path.Add(doorL); isCorner.Add(false);
//                     path.Add(doorR); isCorner.Add(false);
//                 }
//                 else
//                 {
//                     path.Add(doorL); isCorner.Add(false);
//                     float midX = (doorL.x + doorR.x) / 2f;
//                     path.Add(new Vector3(midX, 0, doorL.z)); isCorner.Add(true);
//                     path.Add(new Vector3(midX, 0, doorR.z)); isCorner.Add(true);
//                     path.Add(doorR); isCorner.Add(false);
//                 }
//             }
//             else
//             {
//                 if (Mathf.Abs(doorL.x - doorR.x) < width)
//                 {
//                     path.Add(doorL); isCorner.Add(false);
//                     path.Add(doorR); isCorner.Add(false);
//                 }
//                 else
//                 {
//                     path.Add(doorL); isCorner.Add(false);
//                     float midZ = (doorL.z + doorR.z) / 2f;
//                     path.Add(new Vector3(doorL.x, 0, midZ)); isCorner.Add(true);
//                     path.Add(new Vector3(doorR.x, 0, midZ)); isCorner.Add(true);
//                     path.Add(doorR); isCorner.Add(false);
//                 }
//             }
//         }

//         // 2. 第一阶段：生成所有拐角节点 (Corners)
//         for (int i = 0; i < path.Count; i++)
//         {
//             if (isCorner[i])
//             {
//                 Vector3 dirPrev = (path[i - 1] - path[i]).normalized;
//                 Vector3 dirNext = (path[i + 1] - path[i]).normalized;
//                 BuildCornerNode(path[i], dirPrev, dirNext, width);
//             }
//         }

//         // 3. 第二阶段：生成所有缩进直行段 (Straight Segments)
//         for (int i = 0; i < path.Count - 1; i++)
//         {
//             BuildStraightSegment(path[i], path[i + 1], isCorner[i], isCorner[i + 1], width);
//         }

//         connections.Add(new CorridorConnection
//         {
//             id   = connectionId,
//             from = doorL,
//             to   = doorR
//         });
//     }

//     /// <summary>
//     /// 生成一个独立的拐角节点，自动密封盲区侧墙
//     /// </summary>
//     private void BuildCornerNode(Vector3 pos, Vector3 dir1, Vector3 dir2, float width)
//     {
//         // 生成拐角正方形地板
//         CreateBox(
//             name:   "CorridorCornerFloor",
//             pos:    new Vector3(pos.x, -corridorFloorThickness / 2f, pos.z),
//             scaleX: width,
//             scaleY: corridorFloorThickness,
//             scaleZ: width,
//             mat:    corridorMaterial,
//             defaultColor: new Color(0.45f, 0.45f, 0.45f),
//             addCollider: true
//         );

//         HashSet<Vector3> openDirections = new HashSet<Vector3>
//         {
//             GetNearestCardinal(dir1),
//             GetNearestCardinal(dir2)
//         };

//         Vector3[] cardinals = {
//             Vector3.forward, 
//             Vector3.back,    
//             Vector3.right,   
//             Vector3.left     
//         };

//         float wallCenterY = corridorWallHeight / 2f;

//         foreach (var card in cardinals)
//         {
//             if (openDirections.Contains(card)) continue; 

//             Vector3 wallPos = pos + card * (width / 2f + corridorWallThickness / 2f);
//             wallPos.y = wallCenterY;

//             bool isNSWall = (card == Vector3.forward || card == Vector3.back);
//             float scaleX = isNSWall ? (width + corridorWallThickness * 2f) : corridorWallThickness;
//             float scaleZ = isNSWall ? corridorWallThickness : (width + corridorWallThickness * 2f);

//             CreateBox(
//                 name:   "CorridorCornerWall",
//                 pos:    wallPos,
//                 scaleX: scaleX,
//                 scaleY: corridorWallHeight,
//                 scaleZ: scaleZ,
//                 mat:    corridorWallMaterial ?? corridorMaterial,
//                 defaultColor: new Color(0.42f, 0.42f, 0.42f),
//                 addCollider: true
//             );
//         }
//     }

//     /// <summary>
//     /// 生成缩进式的直行段
//     /// </summary>
//     private void BuildStraightSegment(Vector3 a, Vector3 b, bool aIsCorner, bool bIsCorner, float width)
//     {
//         Vector3 dir = (b - a).normalized;
//         Vector3 start = a;
//         Vector3 end = b;

//         if (aIsCorner) start += dir * (width / 2f);
//         if (bIsCorner) end   -= dir * (width / 2f);

//         float segmentLength = Vector3.Distance(start, end);
//         if (segmentLength < 0.01f) return;

//         Vector3 center = (start + end) * 0.5f;
//         bool isAlongX = Mathf.Abs(dir.x) > 0.5f;

//         // 地板
//         float floorSX = isAlongX ? segmentLength : width;
//         float floorSZ = isAlongX ? width : segmentLength;

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

//         // 侧墙
//         float wallCenterY = corridorWallHeight / 2f;
//         float wallOffset  = width / 2f + corridorWallThickness / 2f;

//         float wallSX = isAlongX ? segmentLength : corridorWallThickness;
//         float wallSZ = isAlongX ? corridorWallThickness : segmentLength;

//         if (isAlongX)
//         {
//             CreateBox(
//                 name:   "CorridorWall_Z+",
//                 pos:    new Vector3(center.x, wallCenterY, center.z + wallOffset),
//                 scaleX: wallSX,
//                 scaleY: corridorWallHeight,
//                 scaleZ: wallSZ,
//                 mat:    corridorWallMaterial ?? corridorMaterial,
//                 defaultColor: new Color(0.42f, 0.42f, 0.42f),
//                 addCollider: true
//             );
//             CreateBox(
//                 name:   "CorridorWall_Z-",
//                 pos:    new Vector3(center.x, wallCenterY, center.z - wallOffset),
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
//             CreateBox(
//                 name:   "CorridorWall_X+",
//                 pos:    new Vector3(center.x + wallOffset, wallCenterY, center.z),
//                 scaleX: wallSX,
//                 scaleY: corridorWallHeight,
//                 scaleZ: wallSZ,
//                 mat:    corridorWallMaterial ?? corridorMaterial,
//                 defaultColor: new Color(0.42f, 0.42f, 0.42f),
//                 addCollider: true
//             );
//             CreateBox(
//                 name:   "CorridorWall_X-",
//                 pos:    new Vector3(center.x - wallOffset, wallCenterY, center.z),
//                 scaleX: wallSX,
//                 scaleY: corridorWallHeight,
//                 scaleZ: wallSZ,
//                 mat:    corridorWallMaterial ?? corridorMaterial,
//                 defaultColor: new Color(0.42f, 0.42f, 0.42f),
//                 addCollider: true
//             );
//         }

//         // ─── 修改：在直行段的走廊两侧，分别独立概率生成火炬 ───
//         if (torchPrefab != null && segmentLength > width)
//         {
//             float torchSpacing = 5f; // 每隔 5 米尝试生成一次火炬
//             float startOffset = width / 2f + 1f;
//             float endOffset = segmentLength - width / 2f - 1f;

//             for (float tOffset = startOffset; tOffset <= endOffset; tOffset += torchSpacing)
//             {
//                 Vector3 basePos = start + dir * tOffset;

//                 if (isAlongX)
//                 {
//                     // 1. 尝试在 Z+ 侧墙面生成火炬
//                     if (Random.value < torchSpawnProbability)
//                     {
//                         Vector3 torchPos = basePos;
//                         torchPos.y = torchHeight;
//                         torchPos.z += (width / 2f - 0.05f); 
//                         Quaternion torchRot = Quaternion.Euler(0, 180, 0); // 面向南方
//                         GameObject torch = Instantiate(torchPrefab, torchPos, torchRot, _corridorRoot.transform);
//                         torch.name = "CorridorTorch_Z+";
//                     }

//                     // 2. 尝试在 Z- 侧墙面生成火炬
//                     if (Random.value < torchSpawnProbability)
//                     {
//                         Vector3 torchPos = basePos;
//                         torchPos.y = torchHeight;
//                         torchPos.z -= (width / 2f - 0.05f); 
//                         Quaternion torchRot = Quaternion.Euler(0, 0, 0); // 面向北方
//                         GameObject torch = Instantiate(torchPrefab, torchPos, torchRot, _corridorRoot.transform);
//                         torch.name = "CorridorTorch_Z-";
//                     }
//                 }
//                 else
//                 {
//                     // 3. 尝试在 X+ 侧墙面生成火炬
//                     if (Random.value < torchSpawnProbability)
//                     {
//                         Vector3 torchPos = basePos;
//                         torchPos.y = torchHeight;
//                         torchPos.x += (width / 2f - 0.05f); 
//                         Quaternion torchRot = Quaternion.Euler(0, 270, 0); // 面向西方
//                         GameObject torch = Instantiate(torchPrefab, torchPos, torchRot, _corridorRoot.transform);
//                         torch.name = "CorridorTorch_X+";
//                     }

//                     // 4. 尝试在 X- 侧墙面生成火炬
//                     if (Random.value < torchSpawnProbability)
//                     {
//                         Vector3 torchPos = basePos;
//                         torchPos.y = torchHeight;
//                         torchPos.x -= (width / 2f - 0.05f); 
//                         Quaternion torchRot = Quaternion.Euler(0, 90, 0); // 面向东方
//                         GameObject torch = Instantiate(torchPrefab, torchPos, torchRot, _corridorRoot.transform);
//                         torch.name = "CorridorTorch_X-";
//                     }
//                 }
//             }
//         }
//     }

//     private Vector3 GetNearestCardinal(Vector3 dir)
//     {
//         float absX = Mathf.Abs(dir.x);
//         float absZ = Mathf.Abs(dir.z);
//         return absX > absZ ? (dir.x > 0 ? Vector3.right : Vector3.left) : (dir.z > 0 ? Vector3.forward : Vector3.back);
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

//         if (mat != null) go.GetComponent<Renderer>().material = mat;
//         else
//         {
//             var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("URP/Lit") ?? Shader.Find("Standard");
//             if (shader != null)
//             {
//                 var m = new Material(shader);
//                 m.color = defaultColor;
//                 go.GetComponent<Renderer>().material = m;
//             }
//         }

//         if (!addCollider) Destroy(go.GetComponent<Collider>());
//     }
// }

// // === 在这里添加遗漏的类定义 ===
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
/// 走廊构建器 v10 (自适应预制体包围盒对齐版)
/// </summary>
public class CorridorBuilder : MonoBehaviour
{
    [Header("默认走廊尺寸（作为备用）")]
    public float corridorWidth          = 3f;
    public float corridorWallHeight     = 4f;
    public float corridorWallThickness  = 0.3f;

    [Header("走廊地板厚度")]
    public float corridorFloorThickness = 0.1f;

    [Header("材质（留空则使用默认色）")]
    public Material corridorMaterial;
    public Material corridorWallMaterial;

    [Header("走廊火炬自动生成（可选）")]
    public GameObject torchPrefab;                           
    [Range(0, 1)] public float torchSpawnProbability = 0.5f; 
    public float torchHeight = 2.8f;                         

    [Header("火炬位置微调配置")]
    [Tooltip("火炬贴墙额外微调：正值向走廊内移动，负值向墙体深处嵌入（通常设为 0 即可自动贴合）")]
    public float torchWallOffset = 0.0f;

    [Tooltip("火炬旋转微调（角度），用于修正模型预制体本身默认的方向偏角")]
    public Vector3 torchRotationOffset = Vector3.zero;

    [Tooltip("避免在靠近门口或拐角的区域生成火炬的安全距离")]
    public float torchDoorSafetyDistance = 2.5f;

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
                                   float width, 
                                   int connectionId = -1)
    {
        bool exitIsHorizontal  = (exitDir == GenerateDungeon.WallDirection.East  || exitDir == GenerateDungeon.WallDirection.West);
        bool enterIsHorizontal = (enterDir == GenerateDungeon.WallDirection.East || enterDir == GenerateDungeon.WallDirection.West);

        List<Vector3> path = new List<Vector3>();
        List<bool> isCorner = new List<bool>();

        if (exitIsHorizontal == enterIsHorizontal)
        {
            if (exitIsHorizontal)
            {
                if (Mathf.Abs(doorL.z - doorR.z) < width)
                {
                    path.Add(doorL); isCorner.Add(false);
                    path.Add(doorR); isCorner.Add(false);
                }
                else
                {
                    path.Add(doorL); isCorner.Add(false);
                    float midX = (doorL.x + doorR.x) / 2f;
                    path.Add(new Vector3(midX, 0, doorL.z)); isCorner.Add(true);
                    path.Add(new Vector3(midX, 0, doorR.z)); isCorner.Add(true);
                    path.Add(doorR); isCorner.Add(false);
                }
            }
            else
            {
                if (Mathf.Abs(doorL.x - doorR.x) < width)
                {
                    path.Add(doorL); isCorner.Add(false);
                    path.Add(doorR); isCorner.Add(false);
                }
                else
                {
                    path.Add(doorL); isCorner.Add(false);
                    float midZ = (doorL.z + doorR.z) / 2f;
                    path.Add(new Vector3(doorL.x, 0, midZ)); isCorner.Add(true);
                    path.Add(new Vector3(doorR.x, 0, midZ)); isCorner.Add(true);
                    path.Add(doorR); isCorner.Add(false);
                }
            }
        }

        for (int i = 0; i < path.Count; i++)
        {
            if (isCorner[i])
            {
                Vector3 dirPrev = (path[i - 1] - path[i]).normalized;
                Vector3 dirNext = (path[i + 1] - path[i]).normalized;
                BuildCornerNode(path[i], dirPrev, dirNext, width);
            }
        }

        for (int i = 0; i < path.Count - 1; i++)
        {
            BuildStraightSegment(path[i], path[i + 1], isCorner[i], isCorner[i + 1], width);
        }

        connections.Add(new CorridorConnection
        {
            id   = connectionId,
            from = doorL,
            to   = doorR
        });
    }

    private void BuildCornerNode(Vector3 pos, Vector3 dir1, Vector3 dir2, float width)
    {
        CreateBox(
            name:   "CorridorCornerFloor",
            pos:    new Vector3(pos.x, -corridorFloorThickness / 2f, pos.z),
            scaleX: width,
            scaleY: corridorFloorThickness,
            scaleZ: width,
            mat:    corridorMaterial,
            defaultColor: new Color(0.45f, 0.45f, 0.45f),
            addCollider: true
        );

        HashSet<Vector3> openDirections = new HashSet<Vector3>
        {
            GetNearestCardinal(dir1),
            GetNearestCardinal(dir2)
        };

        Vector3[] cardinals = {
            Vector3.forward, 
            Vector3.back,    
            Vector3.right,   
            Vector3.left     
        };

        float wallCenterY = corridorWallHeight / 2f;

        foreach (var card in cardinals)
        {
            if (openDirections.Contains(card)) continue; 

            Vector3 wallPos = pos + card * (width / 2f + corridorWallThickness / 2f);
            wallPos.y = wallCenterY;

            bool isNSWall = (card == Vector3.forward || card == Vector3.back);
            float scaleX = isNSWall ? (width + corridorWallThickness * 2f) : corridorWallThickness;
            float scaleZ = isNSWall ? corridorWallThickness : (width + corridorWallThickness * 2f);

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

    private void BuildStraightSegment(Vector3 a, Vector3 b, bool aIsCorner, bool bIsCorner, float width)
    {
        Vector3 dir = (b - a).normalized;
        Vector3 start = a;
        Vector3 end = b;

        if (aIsCorner) start += dir * (width / 2f);
        if (bIsCorner) end   -= dir * (width / 2f);

        float segmentLength = Vector3.Distance(start, end);
        if (segmentLength < 0.01f) return;

        Vector3 center = (start + end) * 0.5f;
        bool isAlongX = Mathf.Abs(dir.x) > 0.5f;

        // 地板
        float floorSX = isAlongX ? segmentLength : width;
        float floorSZ = isAlongX ? width : segmentLength;

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

        // 侧墙
        float wallCenterY = corridorWallHeight / 2f;
        float wallOffset  = width / 2f + corridorWallThickness / 2f;

        float wallSX = isAlongX ? segmentLength : corridorWallThickness;
        float wallSZ = isAlongX ? corridorWallThickness : segmentLength;

        if (isAlongX)
        {
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

    
        // ─── 核心修改：使用直接且可控的偏移参数，避开粒子系统包围盒干扰 ───
        if (torchPrefab != null && segmentLength > (torchDoorSafetyDistance * 2f))
        {
            float torchSpacing = 5f; 
            float startOffset = torchDoorSafetyDistance;
            float endOffset = segmentLength - torchDoorSafetyDistance;

            for (float tOffset = startOffset; tOffset <= endOffset; tOffset += torchSpacing)
            {
                Vector3 basePos = start + dir * tOffset;

                if (isAlongX)
                {
                    // Z+ 侧墙面（朝南）
                    if (Random.value < torchSpawnProbability)
                    {
                        Vector3 surfacePos = new Vector3(basePos.x, torchHeight, basePos.z + width / 2f);
                        Vector3 wallNormal = Vector3.back; // 指向通道中央的法向
                        
                        // 直接根据墙面坐标加上直观的 Wall Offset 偏移
                        Vector3 torchPos = surfacePos + wallNormal * torchWallOffset;
                        Quaternion torchRot = Quaternion.Euler(0, 180, 0) * Quaternion.Euler(torchRotationOffset);
                        
                        GameObject torch = Instantiate(torchPrefab, torchPos, torchRot, _corridorRoot.transform);
                        torch.name = "CorridorTorch_Z+";
                    }

                    // Z- 侧墙面（朝北）
                    if (Random.value < torchSpawnProbability)
                    {
                        Vector3 surfacePos = new Vector3(basePos.x, torchHeight, basePos.z - width / 2f);
                        Vector3 wallNormal = Vector3.forward;
                        
                        Vector3 torchPos = surfacePos + wallNormal * torchWallOffset;
                        Quaternion torchRot = Quaternion.Euler(0, 0, 0) * Quaternion.Euler(torchRotationOffset);
                        
                        GameObject torch = Instantiate(torchPrefab, torchPos, torchRot, _corridorRoot.transform);
                        torch.name = "CorridorTorch_Z-";
                    }
                }
                else
                {
                    // X+ 侧墙面（朝西）
                    if (Random.value < torchSpawnProbability)
                    {
                        Vector3 surfacePos = new Vector3(basePos.x + width / 2f, torchHeight, basePos.z);
                        Vector3 wallNormal = Vector3.left;
                        
                        Vector3 torchPos = surfacePos + wallNormal * torchWallOffset;
                        Quaternion torchRot = Quaternion.Euler(0, 270, 0) * Quaternion.Euler(torchRotationOffset);
                        
                        GameObject torch = Instantiate(torchPrefab, torchPos, torchRot, _corridorRoot.transform);
                        torch.name = "CorridorTorch_X+";
                    }

                    // X- 侧墙面（朝东）
                    if (Random.value < torchSpawnProbability)
                    {
                        Vector3 surfacePos = new Vector3(basePos.x - width / 2f, torchHeight, basePos.z);
                        Vector3 wallNormal = Vector3.right;
                        
                        Vector3 torchPos = surfacePos + wallNormal * torchWallOffset;
                        Quaternion torchRot = Quaternion.Euler(0, 90, 0) * Quaternion.Euler(torchRotationOffset);
                        
                        GameObject torch = Instantiate(torchPrefab, torchPos, torchRot, _corridorRoot.transform);
                        torch.name = "CorridorTorch_X-";
                    }
                }
            }
        }
    }

    /// <summary>
    /// 获取预制体的实际网格边界
    /// </summary>
    private Bounds GetPrefabBounds(GameObject prefab)
    {
        var r = prefab.GetComponentInChildren<Renderer>();
        if (r != null) return r.bounds;
        var c = prefab.GetComponentInChildren<Collider>();
        if (c != null) return c.bounds;
        return new Bounds(Vector3.zero, Vector3.one * 0.5f);
    }

    private Vector3 GetNearestCardinal(Vector3 dir)
    {
        float absX = Mathf.Abs(dir.x);
        float absZ = Mathf.Abs(dir.z);
        return absX > absZ ? (dir.x > 0 ? Vector3.right : Vector3.left) : (dir.z > 0 ? Vector3.forward : Vector3.back);
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

        if (mat != null) go.GetComponent<Renderer>().material = mat;
        else
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("URP/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                var m = new Material(shader);
                m.color = defaultColor;
                go.GetComponent<Renderer>().material = m;
            }
        }

        if (!addCollider) Destroy(go.GetComponent<Collider>());
    }
}

// === 在这里添加遗漏的类定义 ===
[System.Serializable]
public class CorridorConnection
{
    public int     id;
    public Vector3 from;
    public Vector3 to;
}