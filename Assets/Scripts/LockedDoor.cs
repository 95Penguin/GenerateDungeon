using UnityEngine;

/// <summary>
/// 锁门
/// 挂在终点入口的门预制体上
/// 门有两个 Collider：
///   1. _blockCollider（非触发器）：物理阻挡，有钥匙后禁用
///   2. _triggerCollider（触发器）：检测玩家靠近，显示提示
/// 
/// 如果预制体只有一个Collider，本脚本会自动创建第二个
/// </summary>
public class LockedDoor : MonoBehaviour
{
    [Header("门外观（可留空，脚本自动创建占位体）")]
    public MeshRenderer doorRenderer;

    [Header("提示文字（由UIManager订阅显示）")]
    public string lockedMessage   = "需要钥匙才能通过！";
    public string unlockedMessage = "门已解锁！";

    // 事件：玩家在门口时触发（UIManager监听）
    public static System.Action<string> OnPlayerNearDoor;

    private Collider _blockCollider;
    private bool _isUnlocked = false;

    void Awake()
    {
        // 如果预制体没有碰撞体，自动创建一个门形BoxCollider
        _blockCollider = GetComponent<Collider>();
        if (_blockCollider == null)
        {
            _blockCollider = gameObject.AddComponent<BoxCollider>();
            (_blockCollider as BoxCollider).size = new Vector3(3f, 3f, 0.3f);
        }
        _blockCollider.isTrigger = false; // 物理阻挡

        // 如果没有外观，自动创建一个颜色块
        if (doorRenderer == null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = new Vector3(3f, 3f, 0.3f);
            doorRenderer = cube.GetComponent<MeshRenderer>();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.6f, 0.3f, 0.1f); // 棕色门
            doorRenderer.material = mat;

            // 子物体碰撞体与父物体重叠，移除子物体的
            Destroy(cube.GetComponent<Collider>());
        }

        // 订阅钥匙状态变化
        if (GameState.Instance != null)
            GameState.Instance.OnKeyStateChanged += OnKeyStateChanged;
    }

    void OnDestroy()
    {
        if (GameState.Instance != null)
            GameState.Instance.OnKeyStateChanged -= OnKeyStateChanged;
    }

    // 当玩家进入门附近触发器
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (!_isUnlocked)
            OnPlayerNearDoor?.Invoke(lockedMessage);
        else
            OnPlayerNearDoor?.Invoke(unlockedMessage);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        OnPlayerNearDoor?.Invoke(""); // 清空提示
    }

    private void OnKeyStateChanged(bool hasKey)
    {
        if (!hasKey || _isUnlocked) return;
        Unlock();
    }

    public void Unlock()
    {
        _isUnlocked = true;

        // 禁用物理阻挡
        if (_blockCollider != null)
            _blockCollider.enabled = false;

        // 视觉上让门变透明/消失（简单处理：直接Destroy或改颜色）
        if (doorRenderer != null)
        {
            var mat = doorRenderer.material;
            mat.color = new Color(mat.color.r, mat.color.g, mat.color.b, 0.2f);
            // 若需要真实透明，需要设置渲染模式为Transparent
        }

        OnPlayerNearDoor?.Invoke(unlockedMessage);
        Debug.Log("[LockedDoor] 门已解锁");
    }
}
