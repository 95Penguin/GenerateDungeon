using UnityEngine;

/// <summary>
/// 钥匙道具
/// 挂在钥匙预制体上，处理被玩家拾取后的逻辑
/// 预制体需要：
///   - 一个 Collider（isTrigger = true，用于自动检测）
///   - 或者依赖 PlayerController 的 Raycast（两种方式任选）
/// 
/// 本脚本采用 Trigger 方式：玩家走近自动拾取
/// 若想改为 "按E拾取"，参见 PlayerController 中的 TryPickup() 方法
/// </summary>
[RequireComponent(typeof(Collider))]
public class KeyItem : MonoBehaviour
{
    [Header("拾取提示（可选）")]
    public string pickupMessage = "Key Picked Up!";

    [Header("旋转动画")]
    public float rotateSpeed = 90f;
    public float bobSpeed    = 2f;
    public float bobHeight   = 0.2f;

    private Vector3 _startPos;

    void Start()
    {
        _startPos = transform.position;

        // 确保是触发器
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        // 确保有 "Key" Tag（方便PlayerController的Raycast识别）
        gameObject.tag = "Key";
    }

    void Update()
    {
        // 上下浮动 + 旋转动画
        float y = _startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(_startPos.x, y, _startPos.z);
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // 检查是否是玩家
        if (!other.CompareTag("Player")) return;
        Pickup();
    }

    /// <summary>由 PlayerController 的 Raycast 方式调用，或 Trigger 自动调用</summary>
    public void Pickup()
    {
        if (GameState.Instance == null) return;

        GameState.Instance.HasKey = true;
        Debug.Log($"[KeyItem] {pickupMessage}");

        // 播放拾取特效（如有）
        // ParticleSystem ps = GetComponentInChildren<ParticleSystem>();
        // if (ps) { ps.transform.SetParent(null); ps.Play(); }

        Destroy(gameObject);
    }
}
