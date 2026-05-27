using UnityEngine;

/// <summary>
/// BSP树节点
/// 每个节点代表地图上的一块矩形空间
/// 叶节点内会生成一个实际的房间
/// </summary>
public class BSPNode
{
    // ── 空间数据 ──────────────────────────────────────────
    public Rect area;           // 本节点占据的总空间（世界坐标XZ平面）
    public Rect roomRect;       // 叶节点内实际房间的矩形（略小于area）

    // ── 树结构 ────────────────────────────────────────────
    public BSPNode left;
    public BSPNode right;
    public BSPNode parent;

    // ── 房间元数据 ────────────────────────────────────────
    public int roomIndex = -1;          // 对应 DungeonManager.rooms 中的索引，-1表示非叶节点
    public bool isLeaf => left == null && right == null;

    // ── 连接信息（BFS用）────────────────────────────────────
    // 叶节点之间通过走廊相连，邻接关系存在 DungeonManager 的图里

    public BSPNode(Rect area, BSPNode parent = null)
    {
        this.area = area;
        this.parent = parent;
    }

    /// <summary>
    /// 获取叶节点房间中心（世界坐标）
    /// </summary>
    public Vector3 RoomCenter => new Vector3(roomRect.center.x, 0, roomRect.center.y);
}
