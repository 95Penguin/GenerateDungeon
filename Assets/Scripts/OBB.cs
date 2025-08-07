using UnityEngine;

/// <summary>
/// 有向包围盒(Oriented Bounding Box)结构体
/// </summary>
public struct OBB
{
    public Vector3 center;    // 包围盒中心点
    public Vector3 extents;   // 包围盒半尺寸（从中心到各边的距离）
    public Quaternion rotation; // 包围盒旋转
    
    /// <summary>
    /// 检测两个OBB是否相交
    /// </summary>
    /// <param name="other">另一个OBB</param>
    /// <returns>true表示相交，false表示不相交</returns>
    public bool Intersects(OBB other)
    {
        // 使用分离轴定理(SAT)进行相交检测
        return SATIntersection(this, other);
    }
    
    /// <summary>
    /// 分离轴定理(SAT)实现
    /// </summary>
    private static bool SATIntersection(OBB a, OBB b)
    {
        // 获取两个OBB的旋转矩阵
        Matrix4x4 aRot = Matrix4x4.Rotate(a.rotation);
        Matrix4x4 bRot = Matrix4x4.Rotate(b.rotation);

        // 需要测试的15条分离轴：
        // 6条来自两个OBB的局部坐标轴
        // 9条来自两两轴的叉积
        Vector3[] axes = new Vector3[15];

        // A的3个局部轴（X/Y/Z）
        axes[0] = aRot.GetColumn(0).normalized; // A的X轴
        axes[1] = aRot.GetColumn(1).normalized; // A的Y轴
        axes[2] = aRot.GetColumn(2).normalized; // A的Z轴

        // B的3个局部轴（X/Y/Z）
        axes[3] = bRot.GetColumn(0).normalized; // B的X轴
        axes[4] = bRot.GetColumn(1).normalized; // B的Y轴
        axes[5] = bRot.GetColumn(2).normalized; // B的Z轴

        // 计算A和B各轴的叉积（9条轴）
        // 这些轴是两个OBB各边平面法线的方向
        axes[6] = Vector3.Cross(axes[0], axes[3]).normalized;  // A.X × B.X
        axes[7] = Vector3.Cross(axes[0], axes[4]).normalized;  // A.X × B.Y
        axes[8] = Vector3.Cross(axes[0], axes[5]).normalized;  // A.X × B.Z
        axes[9] = Vector3.Cross(axes[1], axes[3]).normalized;  // A.Y × B.X
        axes[10] = Vector3.Cross(axes[1], axes[4]).normalized; // A.Y × B.Y
        axes[11] = Vector3.Cross(axes[1], axes[5]).normalized; // A.Y × B.Z
        axes[12] = Vector3.Cross(axes[2], axes[3]).normalized; // A.Z × B.X
        axes[13] = Vector3.Cross(axes[2], axes[4]).normalized; // A.Z × B.Y
        axes[14] = Vector3.Cross(axes[2], axes[5]).normalized; // A.Z × B.Z

        // 在每条分离轴上做投影测试
        foreach (var axis in axes)
        {
            // 忽略长度接近0的无效轴（叉积结果可能是零向量）
            if (axis.sqrMagnitude < 0.001f) continue;

            // 如果在当前轴上投影不重叠，则存在分离轴，OBB不相交
            if (!OverlapOnAxis(a, b, axis))
                return false;
        }
        
        // 所有轴上都重叠，则OBB相交
        return true;
    }

    /// <summary>
    /// 检测两个OBB在指定轴上的投影是否重叠
    /// </summary>
    private static bool OverlapOnAxis(OBB a, OBB b, Vector3 axis)
    {
        // 计算两个OBB在当前轴上的投影半径
        float aProj = GetProjectionRadius(a, axis);
        float bProj = GetProjectionRadius(b, axis);
        
        // 计算两个中心点在当前轴上的距离
        float centerDistance = Mathf.Abs(Vector3.Dot(axis, b.center - a.center));
        
        // 如果中心距离小于投影半径之和，则投影重叠
        return centerDistance <= (aProj + bProj);
    }

    /// <summary>
    /// 计算OBB在指定轴上的投影半径
    /// </summary>
    private static float GetProjectionRadius(OBB obb, Vector3 axis)
    {
        // 获取OBB的旋转矩阵
        Matrix4x4 rot = Matrix4x4.Rotate(obb.rotation);
        
        // 计算OBB三个轴向的向量（考虑半尺寸）
        Vector3 xAxis = rot.GetColumn(0) * obb.extents.x;
        Vector3 yAxis = rot.GetColumn(1) * obb.extents.y;
        Vector3 zAxis = rot.GetColumn(2) * obb.extents.z;

        // 投影半径 = 各轴在测试轴上投影长度的绝对值之和
        return Mathf.Abs(Vector3.Dot(xAxis, axis))
             + Mathf.Abs(Vector3.Dot(yAxis, axis))
             + Mathf.Abs(Vector3.Dot(zAxis, axis));
    }
}