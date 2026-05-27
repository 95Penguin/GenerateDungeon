using UnityEngine;

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

    private CharacterController controller;
    private Camera playerCamera;
    private float verticalRotation = 0f;
    private float verticalVelocity = 0f; 


    // 事件：通知UIManager显示拾取提示
    public static System.Action<bool, string> OnPickupPrompt; // (显示?, 文字)

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Cursor.lockState = CursorLockMode.None;

        HandleMouseLook();
        HandleMovement();
        HandlePickup();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 水平旋转：绕世界Y轴旋转玩家物体
        transform.Rotate(Vector3.up, mouseX);

        // 垂直旋转：绕局部X轴旋转摄像机（限制角度）
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);
        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    // void HandleMovement()
    // {
    //     float horizontal = Input.GetAxis("Horizontal");
    //     float vertical   = Input.GetAxis("Vertical");

    //     Vector3 move = transform.right * horizontal + transform.forward * vertical;
    //     controller.Move(move * walkSpeed * Time.deltaTime);
    // }
    void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical   = Input.GetAxis("Vertical");

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        move *= walkSpeed;

        // 简易重力逻辑
        if (controller.isGrounded)
        {
            verticalVelocity = -2f; // 保持贴地
        }
        else
        {
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
        }

        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    // /// <summary>
    // /// 每帧向前发射射线检测钥匙，显示拾取提示；
    // /// 按下拾取键时执行拾取
    // /// </summary>
    // void HandlePickup()
    // {
    //     Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

    //     if (Physics.Raycast(ray, out RaycastHit hit, pickupRange))
    //     {
    //         var key = hit.collider.GetComponent<KeyItem>();
    //         if (key != null)
    //         {
    //             // 显示"按E拾取钥匙"提示
    //             OnPickupPrompt?.Invoke(true, $"按 [{pickupKey}] 拾取钥匙");

    //             if (Input.GetKeyDown(pickupKey))
    //                 key.Pickup();

    //             return;
    //         }
    //     }

    //     // 没有可拾取物，隐藏提示
    //     OnPickupPrompt?.Invoke(false, "");
    // }

    /// <summary>
    /// 每帧向前发射射线检测钥匙或门
    /// </summary>
    void HandlePickup()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange))
        {
            // 1. 检测钥匙
            var key = hit.collider.GetComponent<KeyItem>();
            if (key != null)
            {
                OnPickupPrompt?.Invoke(true, $"按 [{pickupKey}] 拾取钥匙");
                if (Input.GetKeyDown(pickupKey))
                    key.Pickup();
                return;
            }

            // 2. 检测物理门
            var door = hit.collider.GetComponentInParent<InteractiveDoor>() 
                    ?? hit.collider.GetComponent<InteractiveDoor>();
            if (door != null)
            {
                OnPickupPrompt?.Invoke(true, $"按 [{pickupKey}] 开关门");
                if (Input.GetKeyDown(pickupKey))
                    door.Interact();
                return;
            }
        }

        // 没有可交互物，隐藏提示
        OnPickupPrompt?.Invoke(false, "");
    }
}
