// using UnityEngine;

// /// <summary>
// /// 可交互门脚本 v4 (解决卡人问题版)
// /// 支持平滑开关门、真假门识别、开假门扣血，并能根据玩家站位自动向远离玩家的方向开门
// /// </summary>
// public class InteractiveDoor : MonoBehaviour
// {
//     [Header("大门属性")]
//     [Tooltip("如果是 false，代表后面没路，开门会扣血")]
//     public bool isConnectingDoor = true;
//     [Tooltip("开错门扣除的血量")]
//     public float wrongDoorDamage = 20f;

//     [Header("开门旋转角度")]
//     [Tooltip("基础旋转角度绝对值。开门时会根据玩家位置自动转换为正值或负值")]
//     public float openAngle = 110f; 
//     public float openSpeed = 5f;

//     private bool _isOpen = false;
//     private bool _hasDamaged = false; 
//     private Quaternion _closedRotation;
//     private Quaternion _targetRotation;

//     void Start()
//     {
//         _closedRotation = transform.localRotation;
//         _targetRotation = _closedRotation;
        
//         string doorType = isConnectingDoor ? "【真门（通往走廊）】" : "【假门（死胡同）】";
//         Debug.Log($"[InteractiveDoor] 门 {gameObject.name} 初始化完成，判定为: {doorType}");
//     }

//     void Update()
//     {
//         transform.localRotation = Quaternion.Slerp(transform.localRotation, _targetRotation, Time.deltaTime * openSpeed);
//     }

//     public void Interact()
//     {
//         _isOpen = !_isOpen;

//         if (_isOpen)
//         {
//             // ─── 新增：动态计算开门方向，防止夹住玩家 ───
//             float finalOpenAngle = openAngle;
//             var player = FindFirstObjectByType<PlayerController>();
            
//             if (player != null)
//             {
//                 // 计算从门指向玩家的向量
//                 Vector3 toPlayer = player.transform.position - transform.position;
                
//                 // 将该向量与门的前向向量进行点积计算
//                 // dot > 0 说明玩家在门的前方，dot < 0 说明玩家在门的后方
//                 float dot = Vector3.Dot(transform.forward, toPlayer.normalized);
                
//                 float angleMagnitude = Mathf.Abs(openAngle);
                
//                 // 如果玩家在门前，门向后开（负角度）；如果玩家在门后，门向前开（正角度）
//                 finalOpenAngle = (dot > 0) ? -angleMagnitude : angleMagnitude;
//             }

//             _targetRotation = _closedRotation * Quaternion.Euler(0, finalOpenAngle, 0);
            
//             Debug.Log($"[InteractiveDoor] 玩家尝试打开大门 {gameObject.name}，该门属性 isConnectingDoor = {isConnectingDoor}");

//             // 如果是假门，且从未扣过血，则触发血量扣除
//             if (!isConnectingDoor && !_hasDamaged)
//             {
//                 _hasDamaged = true;
//                 if (GameState.Instance != null)
//                 {
//                     GameState.Instance.DeductHealth(wrongDoorDamage);
//                     Debug.LogWarning($"[InteractiveDoor] 扣血成功！当前玩家血量已扣除 {wrongDoorDamage}，剩余: {GameState.Instance.CurrentHealth}");
//                 }
//                 else
//                 {
//                     Debug.LogError("[InteractiveDoor] 扣血失败：场景中未找到 GameState.Instance 单例！");
//                 }
//             }
//         }
//         else
//         {
//             _targetRotation = _closedRotation;
//         }
//     }
// }



using UnityEngine;

/// <summary>
/// 可交互门脚本 v5 (支持锁定与同步解锁功能)
/// </summary>
public class InteractiveDoor : MonoBehaviour
{
    [Header("大门属性")]
    [Tooltip("如果是 false，代表后面没路，开门会扣血")]
    public bool isConnectingDoor = true;
    [Tooltip("开错门扣除的血量")]
    public float wrongDoorDamage = 20f;

    [Header("开门旋转角度")]
    public float openAngle = 110f; 
    public float openSpeed = 5f;

    [Header("锁门限制")]
    public bool requiresKeyToOpen = false; // 是否需要钥匙解锁
    private bool _isUnlocked = false;      // 是否已解锁
    public bool IsUnlocked => _isUnlocked;

    private bool _isOpen = false;
    private bool _hasDamaged = false; 
    private Quaternion _closedRotation;
    private Quaternion _targetRotation;

    void Start()
    {
        _closedRotation = transform.localRotation;
        _targetRotation = _closedRotation;
        
        string doorType = isConnectingDoor ? "【真门】" : "【假门】";
        Debug.Log($"[InteractiveDoor] 门 {gameObject.name} 初始化完成，判定为: {doorType}");
    }

    void Update()
    {
        transform.localRotation = Quaternion.Slerp(transform.localRotation, _targetRotation, Time.deltaTime * openSpeed);
    }

    /// <summary>
    /// 解锁大门，并同步解锁拼在一起的另一半门板
    /// </summary>
    public void Unlock()
    {
        _isUnlocked = true;
        Debug.Log($"[InteractiveDoor] 门 {gameObject.name} 已成功解锁！");
        
        // 自动寻找附近 1.5 米内的另一半门并同步解锁（双扇门结构）
        Collider[] partners = Physics.OverlapSphere(transform.position, 1.5f);
        foreach (var col in partners)
        {
            var otherDoor = col.GetComponentInParent<InteractiveDoor>() ?? col.GetComponent<InteractiveDoor>();
            if (otherDoor != null && otherDoor != this && !otherDoor.IsUnlocked)
            {
                otherDoor._isUnlocked = true;
                Debug.Log($"[InteractiveDoor] 邻近的另一半门 {otherDoor.gameObject.name} 已同步解锁。");
            }
        }
    }

    public void Interact()
    {
        // 如果门被锁住且未解开，禁止直接互动开门
        if (requiresKeyToOpen && !_isUnlocked)
        {
            Debug.LogWarning("[InteractiveDoor] 大门已被锁定，必须先用钥匙解锁！");
            return;
        }

        _isOpen = !_isOpen;

        if (_isOpen)
        {
            float finalOpenAngle = openAngle;
            var player = FindFirstObjectByType<PlayerController>();
            
            if (player != null)
            {
                Vector3 toPlayer = player.transform.position - transform.position;
                float dot = Vector3.Dot(transform.forward, toPlayer.normalized);
                float angleMagnitude = Mathf.Abs(openAngle);
                finalOpenAngle = (dot > 0) ? -angleMagnitude : angleMagnitude;
            }

            _targetRotation = _closedRotation * Quaternion.Euler(0, finalOpenAngle, 0);
            
            if (!isConnectingDoor && !_hasDamaged)
            {
                _hasDamaged = true;
                if (GameState.Instance != null)
                {
                    GameState.Instance.DeductHealth(wrongDoorDamage);
                }
            }
        }
        else
        {
            _targetRotation = _closedRotation;
        }
    }
}