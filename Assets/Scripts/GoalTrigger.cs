using UnityEngine;

/// <summary>
/// 终点触发器
/// 放置在终点房间，玩家持有钥匙进入后触发通关
/// </summary>
[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    void Start()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // 必须持有钥匙
        if (GameState.Instance == null || !GameState.Instance.HasKey)
        {
            Debug.Log("[GoalTrigger] 玩家到达终点，但尚未拾取钥匙");
            return;
        }

        GameState.Instance.TriggerGameClear();
    }
}
