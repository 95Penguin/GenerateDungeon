using UnityEngine;

/// <summary>
/// 玩家控制器 v3
/// 支持开局输入锁定、通关锁定、ESC 暂停解锁鼠标以及掉落死亡检测
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("移动")]
    public float walkSpeed = 5f;
    public float mouseSensitivity = 2f;

    [Header("拾取")]
    [Tooltip("拾取射线距离")]
    public float pickupRange = 2.5f;
    [Tooltip("拾取按键")]
    public KeyCode pickupKey = KeyCode.E;

    [Header("掉落死亡判定")]
    [Tooltip("当玩家的 Y 坐标低于该值时，判定为坠落死亡并触发 Game Over")]
    public float fallThreshold = -10f;

    private CharacterController controller;
    private Camera playerCamera;
    private float verticalRotation = 0f;
    private float verticalVelocity = 0f; 

    // 事件：通知UIManager显示拾取提示
    public static System.Action<bool, string> OnPickupPrompt; 

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        
        // 初始时不锁定指针，等点击“开始游戏”再锁定
        Cursor.lockState = CursorLockMode.None;
    }

    void Update()
    {
        // 1. 实时检测是否掉出地图
        if (GameState.Instance != null && GameState.Instance.IsGameStarted && !GameState.Instance.IsGameOver)
        {
            if (transform.position.y < fallThreshold)
            {
                Debug.LogWarning($"[PlayerController] 玩家跌落深渊 (Y={transform.position.y:F2})，触发游戏结束！");
                GameState.Instance.TriggerGameOver();
                return;
            }
        }

        // 2. 如果游戏还没开始，或者已经结束，不接收键盘鼠标输入
        if (GameState.Instance == null || !GameState.Instance.IsGameStarted || GameState.Instance.IsGameOver)
        {
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        // 3. 按 ESC 键可以临时解锁鼠标（进入暂停状态，由 UIManager 接管显示）
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
        }

        // 4. 只有鼠标锁定时，才允许操控角色（防止点击UI时人物跟着转）
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            HandleMouseLook();
            HandleMovement();
            HandlePickup();
        }
        else
        {
            // 如果鼠标被释放了，玩家再次点击屏幕空白处时，自动重新锁定鼠标
            if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up, mouseX);

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);
        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical   = Input.GetAxis("Vertical");

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        move *= walkSpeed;

        if (controller.isGrounded)
        {
            verticalVelocity = -2f; 
        }
        else
        {
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
        }

        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    void HandlePickup()
    {
        Vector3 rayOrigin = playerCamera.transform.position + playerCamera.transform.forward * 3.0f;
        Ray ray = new Ray(rayOrigin, playerCamera.transform.forward);
        // 在 Scene 窗口绘制一条红色的调试射线，方便您实时观察射线的长度和落点
        Debug.DrawRay(rayOrigin, playerCamera.transform.forward * pickupRange, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange))
        {
            var key = hit.collider.GetComponent<KeyItem>();
            if (key != null)
            {
                OnPickupPrompt?.Invoke(true, $"Press [{pickupKey}] to Pick Up Key");
                if (Input.GetKeyDown(pickupKey))
                    key.Pickup();
                return;
            }

            var door = hit.collider.GetComponentInParent<InteractiveDoor>() 
                    ?? hit.collider.GetComponent<InteractiveDoor>();
            if (door != null)
            {
                OnPickupPrompt?.Invoke(true, $"Press [{pickupKey}] to Open/Close");
                if (Input.GetKeyDown(pickupKey))
                    door.Interact();
                return;
            }
        }

        OnPickupPrompt?.Invoke(false, "");
    }
}



