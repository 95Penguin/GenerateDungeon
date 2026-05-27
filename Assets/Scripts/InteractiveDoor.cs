using UnityEngine;

/// <summary>
/// 可交互门脚本
/// 挂载在门预制体上，支持平滑旋转开关门
/// </summary>
public class InteractiveDoor : MonoBehaviour
{
    [Header("开门旋转角度")]
    public float openAngle = -90f; // 根据预制体轴向，可以是 90 或 -90
    public float openSpeed = 5f;

    private bool _isOpen = false;
    private Quaternion _closedRotation;
    private Quaternion _targetRotation;

    void Start()
    {
        // 记录初始关闭状态的旋转
        _closedRotation = transform.localRotation;
        _targetRotation = _closedRotation;
    }

    void Update()
    {
        // 平滑旋转到目标角度
        transform.localRotation = Quaternion.Slerp(transform.localRotation, _targetRotation, Time.deltaTime * openSpeed);
    }

    /// <summary>
    /// 被玩家交互时调用
    /// </summary>
    public void Interact()
    {
        _isOpen = !_isOpen;

        if (_isOpen)
        {
            // 沿 Y 轴旋转 openAngle 度
            _targetRotation = _closedRotation * Quaternion.Euler(0, openAngle, 0);
            Debug.Log("[InteractiveDoor] 门已打开");
        }
        else
        {
            _targetRotation = _closedRotation;
            Debug.Log("[InteractiveDoor] 门已关闭");
        }
    }
}