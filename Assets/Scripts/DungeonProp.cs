using UnityEngine;

// 道具配置类
[System.Serializable]
public class DungeonProp
{
    public GameObject prefab; // 预制体
    public enum PositionType { Wall, Corner, Middle, Anywhere }// 放置位置类型
    public PositionType positionType; // 放置位置类型
    [Range(0, 1)] public float spawnProbability; // 生成概率（0~1）
    public int minCount; // 最小数量
    public int maxCount; // 最大数量
    public Vector3 offset; //放置偏移量
    public bool randomRotation; // 是否随机旋转
}