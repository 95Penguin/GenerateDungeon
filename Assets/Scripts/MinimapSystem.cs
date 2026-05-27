using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 小地图系统
/// 方案：俯视正交摄像机 → RenderTexture → UI RawImage
/// 
/// 场景搭建：
///   1. 在 Canvas 下创建一个 RawImage，命名 "MinimapImage"，右下角锚点
///   2. 创建一个 Camera 命名 "MinimapCamera"，挂本脚本或在此初始化
///   3. 将 DungeonManager 引用拖入
/// </summary>
public class MinimapSystem : MonoBehaviour
{
    [Header("引用")]
    public DungeonManager dungeonManager;
    public RawImage minimapImage;       // Canvas上的RawImage
    public Transform playerTransform;   // 运行时动态查找

    [Header("小地图摄像机参数")]
    public int renderTextureSize = 512;
    public float cameraHeight = 80f;        // 俯视高度
    public float cameraOrthoSize = 70f;     // 正交视野大小
    public LayerMask minimapLayers = ~0;    // 渲染所有层

    [Header("跟随玩家")]
    public bool followPlayer = true;

    // 标记点（起/终点图标）
    private List<GameObject> _markers = new List<GameObject>();

    private Camera _minimapCam;
    private RenderTexture _rt;

    void Start()
    {
        SetupCamera();
        // 延迟一帧，等DungeonManager.Generate()完成后再设置标记点和查找玩家
        Invoke(nameof(LateSetup), 0.1f);
    }

    private void LateSetup()
    {
        SetupMarkers();
        FindPlayer();
    }

    void LateUpdate()
    {
        if (!followPlayer || _minimapCam == null) return;

        // 小地图摄像机跟随玩家XZ，Y固定
        Vector3 target = playerTransform != null
            ? new Vector3(playerTransform.position.x, cameraHeight, playerTransform.position.z)
            : new Vector3(0, cameraHeight, 0);

        _minimapCam.transform.position = target;
    }

    // ── 初始化 ────────────────────────────────────────────

    private void SetupCamera()
    {
        // 创建RenderTexture
        _rt = new RenderTexture(renderTextureSize, renderTextureSize, 16);
        _rt.name = "MinimapRT";

        // 创建俯视摄像机
        var camGO = new GameObject("MinimapCamera");
        camGO.transform.SetParent(transform);
        camGO.transform.position = new Vector3(0, cameraHeight, 0);
        camGO.transform.rotation = Quaternion.Euler(90, 0, 0); // 正朝下

        _minimapCam = camGO.AddComponent<Camera>();
        _minimapCam.orthographic = true;
        _minimapCam.orthographicSize = cameraOrthoSize;
        _minimapCam.targetTexture = _rt;
        _minimapCam.cullingMask = minimapLayers;
        _minimapCam.clearFlags = CameraClearFlags.SolidColor;
        _minimapCam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        _minimapCam.depth = -1; // 先于主摄像机渲染

        // 绑定到UI
        if (minimapImage != null)
            minimapImage.texture = _rt;
    }

    /// <summary>
    /// 在起点（绿）和终点（金）位置放彩色地面Quad作为图标
    /// 俯视摄像机自然能看到
    /// </summary>
    private void SetupMarkers()
    {
        if (dungeonManager == null) return;

        var rooms = dungeonManager.Rooms;
        int startIdx = dungeonManager.StartRoomIndex;
        int goalIdx  = dungeonManager.GoalRoomIndex;

        if (startIdx >= 0 && startIdx < rooms.Count)
            CreateMarker(rooms[startIdx].transform.position, Color.green,  "StartMarker");

        if (goalIdx >= 0 && goalIdx < rooms.Count)
            CreateMarker(rooms[goalIdx].transform.position,  new Color(1f, 0.8f, 0f), "GoalMarker");
    }

    private void CreateMarker(Vector3 worldPos, Color color, string markerName)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = markerName;
        quad.transform.position = new Vector3(worldPos.x, 0.05f, worldPos.z);
        quad.transform.rotation = Quaternion.Euler(90, 0, 0);
        quad.transform.localScale = new Vector3(4f, 4f, 1f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = color;
        quad.GetComponent<Renderer>().material = mat;
        Destroy(quad.GetComponent<Collider>());

        _markers.Add(quad);
    }

    private void FindPlayer()
    {
        var pc = FindObjectOfType<PlayerController>();
        if (pc != null)
            playerTransform = pc.transform;
    }
}
