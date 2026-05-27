using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BSP（二叉空间分割）地牢生成器
/// 负责将总空间递归切分为若干矩形区域，每个叶节点生成一个房间
/// </summary>
public class BSPDungeonGenerator
{
    // ── 参数 ──────────────────────────────────────────────
    private float _minNodeRatio = 0.4f;   // 切割比例最小值（防止切出极窄区域）
    private float _maxNodeRatio = 0.6f;   // 切割比例最大值
    private int _maxDepth;                // 最大递归深度（控制房间数量）
    private float _roomMargin;            // 房间相对节点区域的缩进（留出走廊空间）
    private float _minRoomSize;           // 房间最小边长

    public List<BSPNode> leafNodes = new List<BSPNode>();  // 所有叶节点（=实际房间）
    public BSPNode root;

    /// <param name="maxDepth">递归深度，约产生 2^maxDepth 个房间</param>
    /// <param name="roomMargin">节点内房间的内缩边距</param>
    /// <param name="minRoomSize">房间最小边长（防止过小房间）</param>
    public BSPDungeonGenerator(int maxDepth = 3, float roomMargin = 2f, float minRoomSize = 6f)
    {
        _maxDepth = maxDepth;
        _roomMargin = roomMargin;
        _minRoomSize = minRoomSize;
    }

    /// <summary>
    /// 在给定总空间内执行BSP分割，返回根节点
    /// </summary>
    /// <param name="totalArea">地图总空间（XZ平面矩形）</param>
    public BSPNode Generate(Rect totalArea)
    {
        leafNodes.Clear();
        root = new BSPNode(totalArea);
        SplitNode(root, 0);
        return root;
    }

    // ── 递归切割 ──────────────────────────────────────────

    private void SplitNode(BSPNode node, int depth)
    {
        if (depth >= _maxDepth)
        {
            // 到达最大深度，作为叶节点生成房间
            GenerateRoomInNode(node);
            leafNodes.Add(node);
            return;
        }

        // 决定切割方向：优先切割较长边，加一点随机性
        bool splitHorizontal = ShouldSplitHorizontal(node.area);

        float splitRatio = Random.Range(_minNodeRatio, _maxNodeRatio);

        Rect leftRect, rightRect;
        if (splitHorizontal)
        {
            // 水平切割（沿Z轴）
            float splitZ = node.area.y + node.area.height * splitRatio;
            leftRect  = new Rect(node.area.x, node.area.y, node.area.width, splitZ - node.area.y);
            rightRect = new Rect(node.area.x, splitZ,      node.area.width, node.area.yMax - splitZ);
        }
        else
        {
            // 垂直切割（沿X轴）
            float splitX = node.area.x + node.area.width * splitRatio;
            leftRect  = new Rect(node.area.x,  node.area.y, splitX - node.area.x,  node.area.height);
            rightRect = new Rect(splitX,        node.area.y, node.area.xMax - splitX, node.area.height);
        }

        // 只有子区域足够大才真正切割
        if (!IsNodeViable(leftRect) || !IsNodeViable(rightRect))
        {
            GenerateRoomInNode(node);
            leafNodes.Add(node);
            return;
        }

        node.left  = new BSPNode(leftRect,  node);
        node.right = new BSPNode(rightRect, node);

        SplitNode(node.left,  depth + 1);
        SplitNode(node.right, depth + 1);
    }

    /// <summary>
    /// 在节点区域内随机生成一个略小的房间矩形
    /// </summary>
    // private void GenerateRoomInNode(BSPNode node)
    // {
    //     float margin = _roomMargin;

    //     float minX = node.area.x + margin;
    //     float minY = node.area.y + margin;
    //     float maxW = node.area.width  - margin * 2;
    //     float maxH = node.area.height - margin * 2;

    //     // 保证房间不小于最小尺寸
    //     if (maxW < _minRoomSize) maxW = _minRoomSize;
    //     if (maxH < _minRoomSize) maxH = _minRoomSize;

    //     float roomW = Random.Range(_minRoomSize, maxW);
    //     float roomH = Random.Range(_minRoomSize, maxH);

    //     // 在节点内随机偏移房间位置
    //     float roomX = Random.Range(minX, minX + (maxW - roomW));
    //     float roomY = Random.Range(minY, minY + (maxH - roomH));

    //     node.roomRect = new Rect(roomX, roomY, roomW, roomH);
    // }

    private void GenerateRoomInNode(BSPNode node)
    {
        float margin = _roomMargin;

        // 限制最大可用宽高，使其不小于零
        float maxW = Mathf.Max(0.1f, node.area.width  - margin * 2);
        float maxH = Mathf.Max(0.1f, node.area.height - margin * 2);

        // 房间大小在 最小尺寸 与 节点实际最大尺寸 之间取值
        float minW = Mathf.Min(_minRoomSize, maxW);
        float minH = Mathf.Min(_minRoomSize, maxH);

        float roomW = Random.Range(minW, maxW);
        float roomH = Random.Range(minH, maxH);

        float roomX = Random.Range(node.area.x + margin, node.area.xMax - margin - roomW);
        float roomY = Random.Range(node.area.y + margin, node.area.yMax - margin - roomH);

        node.roomRect = new Rect(roomX, roomY, roomW, roomH);
    }

    // ── 辅助 ──────────────────────────────────────────────

    private bool ShouldSplitHorizontal(Rect area)
    {
        float ratio = area.width / area.height;
        if (ratio > 1.25f) return false; // 明显宽，竖切
        if (ratio < 0.75f) return true;  // 明显高，横切
        return Random.value > 0.5f;      // 接近正方形，随机
    }

    private bool IsNodeViable(Rect rect)
    {
        return rect.width  >= _minRoomSize + _roomMargin * 2
            && rect.height >= _minRoomSize + _roomMargin * 2;
    }

    // ── 连通图辅助：获取兄弟节点树中最近的一对叶节点 ────────

    /// <summary>
    /// 从left子树和right子树各取一个叶节点，用于连接走廊
    /// 返回两个叶节点（left侧、right侧）
    /// </summary>
    public static (BSPNode, BSPNode) GetClosestLeafPair(BSPNode left, BSPNode right)
    {
        var leftLeaves  = GetAllLeaves(left);
        var rightLeaves = GetAllLeaves(right);

        BSPNode bestL = null, bestR = null;
        float bestDist = float.MaxValue;

        foreach (var l in leftLeaves)
        foreach (var r in rightLeaves)
        {
            float d = Vector2.Distance(l.roomRect.center, r.roomRect.center);
            if (d < bestDist)
            {
                bestDist = d;
                bestL = l;
                bestR = r;
            }
        }

        return (bestL, bestR);
    }

    private static List<BSPNode> GetAllLeaves(BSPNode node)
    {
        var result = new List<BSPNode>();
        CollectLeaves(node, result);
        return result;
    }

    private static void CollectLeaves(BSPNode node, List<BSPNode> result)
    {
        if (node == null) return;
        if (node.isLeaf) { result.Add(node); return; }
        CollectLeaves(node.left,  result);
        CollectLeaves(node.right, result);
    }
}
