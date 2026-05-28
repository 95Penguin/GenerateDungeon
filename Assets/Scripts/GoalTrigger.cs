// using UnityEngine;

// /// <summary>
// /// 终点触发器 (已修复 Physics 刚体触发判定问题)
// /// </summary>
// [RequireComponent(typeof(Collider))]
// public class GoalTrigger : MonoBehaviour
// {
//     void Start()
//     {
//         var col = GetComponent<Collider>();
//         col.isTrigger = true;

//         // ─── 核心修改：确保生成 Kinematic 刚体，否则 CharacterController 无法触发事件 ───
//         var rb = GetComponent<Rigidbody>();
//         if (rb == null)
//         {
//             rb = gameObject.AddComponent<Rigidbody>();
//         }
//         rb.isKinematic = true;
//         rb.useGravity = false;
//     }

//     void OnTriggerEnter(Collider other)
//     {
//         // 支持 Tag 或者挂载了 PlayerController 脚本的物体，防止因未加标签导致失效
//         if (!other.CompareTag("Player") && other.GetComponent<PlayerController>() == null) return;

//         // 必须持有钥匙
//         if (GameState.Instance == null || !GameState.Instance.HasKey)
//         {
//             Debug.Log("[GoalTrigger] 玩家到达终点，但尚未拾取钥匙");
//             return;
//         }

//         GameState.Instance.TriggerGameClear();
//     }
// }



using UnityEngine;

/// <summary>
/// 终点触发器 v4 (带调试控制台提示输出版)
/// </summary>
[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    void Start()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        // 确保生成 Kinematic 刚体，保证触发器稳定工作
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[GoalTrigger] 检测到物体碰触终点区域: {other.name} (Tag: {other.tag})");

        // 兼容性检测：只要有 PlayerTag、或者组件上挂有 PlayerController，都认定为玩家
        bool isPlayer = other.CompareTag("Player") 
                        || other.GetComponent<PlayerController>() != null 
                        || other.GetComponentInParent<PlayerController>() != null;

        if (!isPlayer) return;

        if (GameState.Instance == null)
        {
            Debug.LogError("[GoalTrigger] 错误：场景中未找到 GameState 实例，无法执行通关！");
            return;
        }

        // 验证钥匙状态
        if (!GameState.Instance.HasKey)
        {
            Debug.LogWarning("[GoalTrigger] 玩家已进入终点，但由于 HasKey = false，无法通关。");
            return;
        }

        Debug.Log("[GoalTrigger] 通关判定达成！正在触发全局 Game Clear。");
        GameState.Instance.TriggerGameClear();
    }
}