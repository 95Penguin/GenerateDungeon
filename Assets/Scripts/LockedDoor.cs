// using UnityEngine;

// /// <summary>
// /// 终点锁门 v2
// /// 靠近时检测钥匙：
// ///   - 无钥匙：提示“需要钥匙才能通过！”
// ///   - 有钥匙：提示“按 [X] 键解锁大门”
// /// 玩家按下 X 键后大门解锁并消失。
// /// </summary>
// public class LockedDoor : MonoBehaviour
// {
//     [Header("门外观（可留空，脚本自动创建占位体）")]
//     public MeshRenderer doorRenderer;

//     [Header("提示文字")]
//     public string lockedMessage   = "需要钥匙才能通过！";
//     public string keyPromptMessage = "持有钥匙，按 [X] 键解锁大门";
//     public string unlockedMessage = "门已解锁！";

//     // 事件：通知 UIManager 显示提示
//     public static System.Action<string> OnPlayerNearDoor;

//     private Collider _blockCollider;
//     private bool _isUnlocked = false;
//     private bool _isPlayerNear = false;

//     void Awake()
//     {
//         // 1. 获取物理阻挡碰撞体
//         _blockCollider = GetComponent<Collider>();
//         if (_blockCollider == null)
//         {
//             _blockCollider = gameObject.AddComponent<BoxCollider>();
//             (_blockCollider as BoxCollider).size = new Vector3(3f, 3f, 0.3f);
//         }
//         _blockCollider.isTrigger = false; // 物理实体阻挡

//         // 2. 动态添加一个独立的触发器碰撞体，用于检测玩家靠近
//         BoxCollider triggerCol = gameObject.AddComponent<BoxCollider>();
//         triggerCol.isTrigger = true;
//         triggerCol.size = new Vector3(4f, 3f, 4f); // 触发器探测区域
//         triggerCol.center = Vector3.zero;

//         // 3. 若没有外观，创建默认门板
//         if (doorRenderer == null)
//         {
//             var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
//             cube.transform.SetParent(transform);
//             cube.transform.localPosition = Vector3.zero;
//             cube.transform.localScale = new Vector3(3f, 3f, 0.3f);
//             doorRenderer = cube.GetComponent<MeshRenderer>();

//             var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
//             mat.color = new Color(0.5f, 0.25f, 0.05f); // 棕木色
//             doorRenderer.material = mat;

//             Destroy(cube.GetComponent<Collider>());
//         }
//     }

//     void Update()
//     {
//         if (_isUnlocked || !_isPlayerNear) return;

//         // 实时检测玩家是否持有钥匙
//         if (GameState.Instance != null && GameState.Instance.HasKey)
//         {
//             OnPlayerNearDoor?.Invoke(keyPromptMessage);

//             // 按下 X 键解锁
//             if (Input.GetKeyDown(KeyCode.X))
//             {
//                 Unlock();
//             }
//         }
//         else
//         {
//             OnPlayerNearDoor?.Invoke(lockedMessage);
//         }
//     }

//     void OnTriggerEnter(Collider other)
//     {
//         if (!other.CompareTag("Player")) return;
//         _isPlayerNear = true;
//     }

//     void OnTriggerExit(Collider other)
//     {
//         if (!other.CompareTag("Player")) return;
//         _isPlayerNear = false;
//         OnPlayerNearDoor?.Invoke(""); // 清空提示
//     }

//     public void Unlock()
//     {
//         _isUnlocked = true;

//         // 禁用阻挡，允许玩家走过去
//         if (_blockCollider != null)
//             _blockCollider.enabled = false;

//         // 让大门完全隐藏
//         if (doorRenderer != null)
//         {
//             doorRenderer.gameObject.SetActive(false);
//         }

//         OnPlayerNearDoor?.Invoke(unlockedMessage);
//         Debug.Log("[LockedDoor] 玩家按下 [X] 成功解锁了大门");
//     }
// }





using UnityEngine;

/// <summary>
/// 终点锁门 v3 (自动关联渲染器优化版)
/// </summary>
public class LockedDoor : MonoBehaviour
{
    [Header("门外观（可留空，脚本会自动尝试获取子物体渲染器）")]
    public MeshRenderer doorRenderer;

    [Header("提示文字")]
    public string lockedMessage   = "需要钥匙才能通过！";
    public string keyPromptMessage = "持有钥匙，按 [X] 键解锁大门";
    public string unlockedMessage = "门已解锁！";

    // 事件：通知 UIManager 显示提示
    public static System.Action<string> OnPlayerNearDoor;

    private Collider _blockCollider;
    private bool _isUnlocked = false;
    private bool _isPlayerNear = false;

    void Awake()
    {
        // 1. 获取或创建物理阻挡碰撞体
        _blockCollider = GetComponent<Collider>();
        if (_blockCollider == null)
        {
            _blockCollider = gameObject.AddComponent<BoxCollider>();
            (_blockCollider as BoxCollider).size = new Vector3(3f, 3f, 0.3f);
        }
        _blockCollider.isTrigger = false; // 物理实体阻挡

        // 2. 动态添加一个独立的触发器碰撞体，用于检测玩家靠近
        BoxCollider triggerCol = gameObject.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector3(4f, 3f, 4f); // 触发器探测区域
        triggerCol.center = Vector3.zero;

        // 3. ─── 优化：优先自动获取预制体子物体自带的渲染器，防止生成多余的红/褐色方块 ───
        if (doorRenderer == null)
        {
            doorRenderer = GetComponentInChildren<MeshRenderer>();
        }

        // 如果预制体里确实没有任何渲染器，才创建默认的方块作为备用外观
        if (doorRenderer == null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Fallback_Door_Visual";
            cube.transform.SetParent(transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = new Vector3(3f, 3f, 0.3f);
            doorRenderer = cube.GetComponent<MeshRenderer>();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.5f, 0.25f, 0.05f); // 棕木色
            doorRenderer.material = mat;

            Destroy(cube.GetComponent<Collider>());
        }
    }

    void Update()
    {
        if (_isUnlocked || !_isPlayerNear) return;

        // 实时检测玩家是否持有钥匙
        if (GameState.Instance != null && GameState.Instance.HasKey)
        {
            OnPlayerNearDoor?.Invoke(keyPromptMessage);

            // 按下 X 键解锁
            if (Input.GetKeyDown(KeyCode.X))
            {
                Unlock();
            }
        }
        else
        {
            OnPlayerNearDoor?.Invoke(lockedMessage);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerNear = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerNear = false;
        OnPlayerNearDoor?.Invoke(""); // 清空提示
    }

    public void Unlock()
    {
        _isUnlocked = true;

        // 禁用实体阻挡，允许玩家走过去
        if (_blockCollider != null)
            _blockCollider.enabled = false;

        // 让大门完全隐藏
        if (doorRenderer != null)
        {
            doorRenderer.gameObject.SetActive(false);
        }

        OnPlayerNearDoor?.Invoke(unlockedMessage);
        Debug.Log("[LockedDoor] 玩家按下 [X] 成功解锁了大门");
    }
}