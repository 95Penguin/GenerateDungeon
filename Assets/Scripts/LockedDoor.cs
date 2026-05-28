using UnityEngine;

/// <summary>
/// 终点锁门 v4 (递归阻挡物清理优化版)
/// </summary>
public class LockedDoor : MonoBehaviour
{
    [Header("门外观（可留空，脚本会自动尝试获取子物体渲染器）")]
    public MeshRenderer doorRenderer;

    [Header("提示文字")]
    public string lockedMessage   = "Key Required!";
    public string keyPromptMessage = "Press [X] to Unlock";
    public string unlockedMessage = "Door Unlocked!";

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
        _blockCollider.isTrigger = false; 

        // 2. 动态添加一个独立的触发器碰撞体，用于检测玩家靠近
        BoxCollider triggerCol = gameObject.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector3(4f, 3f, 4f); 
        triggerCol.center = Vector3.zero;

        // 3. 自动关联网格渲染器
        if (doorRenderer == null)
        {
            doorRenderer = GetComponentInChildren<MeshRenderer>();
        }

        if (doorRenderer == null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Fallback_Door_Visual";
            cube.transform.SetParent(transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = new Vector3(3f, 3f, 0.3f);
            doorRenderer = cube.GetComponent<MeshRenderer>();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.5f, 0.25f, 0.05f); 
            doorRenderer.material = mat;

            Destroy(cube.GetComponent<Collider>());
        }
    }

    void Update()
    {
        if (_isUnlocked || !_isPlayerNear) return;

        if (GameState.Instance != null && GameState.Instance.HasKey)
        {
            OnPlayerNearDoor?.Invoke(keyPromptMessage);

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
        if (!other.CompareTag("Player") && other.GetComponentInParent<PlayerController>() == null) return;
        _isPlayerNear = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") && other.GetComponentInParent<PlayerController>() == null) return;
        _isPlayerNear = false;
        OnPlayerNearDoor?.Invoke(""); 
    }

    public void Unlock()
    {
        _isUnlocked = true;

        // ─── 核心修改：禁用大门自身以及所有子孙物体上的所有 Collider，确保无物理残留 ───
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        foreach (var col in allColliders)
        {
            col.enabled = false;
        }

        // ─── 核心修改：隐藏所有子渲染器 ───
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        foreach (var rend in allRenderers)
        {
            rend.enabled = false;
        }

        if (doorRenderer != null)
        {
            doorRenderer.gameObject.SetActive(false);
        }

        OnPlayerNearDoor?.Invoke(unlockedMessage);
        Debug.Log("[LockedDoor] 大门成功解锁。已彻底关闭所有关联碰撞体和渲染器。");
    }
}