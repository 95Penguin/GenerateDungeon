using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
// 如果项目中没有安装 TextMeshPro，将下面这行注释掉
// 并将文件中所有 TMP_Text 替换为 UnityEngine.UI.Text
using TMPro;

/// <summary>
/// UI管理器
/// 管理：计时器、钥匙状态图标、门提示、拾取提示、通关界面
/// 
/// Canvas层级建议：
///   Canvas
///   ├── HUD
///   │   ├── TimerText          (右上角，格式 "00:00")
///   │   ├── KeyIcon            (左上角图标，有钥匙时高亮)
///   │   ├── KeyStatusText      (KeyIcon旁边文字 "×" / "✓")
///   │   ├── DoorPromptText     (屏幕中下，锁门提示)
///   │   └── PickupPromptText   (屏幕中央，"按E拾取")
///   └── WinPanel               (通关面板，默认隐藏)
///       ├── WinTimeText        ("用时 01:23")
///       └── RestartButton
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("HUD")]
    public TMP_Text timerText;
    public Image    keyIcon;            // 钥匙图标（有钥匙时变亮）
    public TMP_Text keyStatusText;      // "无钥匙" / "已持有钥匙"
    public TMP_Text doorPromptText;     // 靠近锁门时的提示
    public TMP_Text pickupPromptText;   // 准星中央的拾取提示

    [Header("通关界面")]
    public GameObject winPanel;
    public TMP_Text   winTimeText;
    public Button     restartButton;

    [Header("钥匙图标颜色")]
    public Color keyAbsentColor  = new Color(0.4f, 0.4f, 0.4f);
    public Color keyPresentColor = new Color(1f,   0.9f, 0f);

    void Start()
    {
        // 初始状态
        SetWinPanel(false);
        SetDoorPrompt("");
        SetPickupPrompt(false, "");
        UpdateKeyUI(false);

        // 订阅事件
        if (GameState.Instance != null)
        {
            GameState.Instance.OnKeyStateChanged += UpdateKeyUI;
            GameState.Instance.OnGameClear       += ShowWinScreen;
        }

        LockedDoor.OnPlayerNearDoor    += SetDoorPrompt;
        PlayerController.OnPickupPrompt += SetPickupPrompt;

        // 重新生成按钮
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);
    }

    void OnDestroy()
    {
        if (GameState.Instance != null)
        {
            GameState.Instance.OnKeyStateChanged -= UpdateKeyUI;
            GameState.Instance.OnGameClear       -= ShowWinScreen;
        }
        LockedDoor.OnPlayerNearDoor    -= SetDoorPrompt;
        PlayerController.OnPickupPrompt -= SetPickupPrompt;
    }

    void Update()
    {
        // 实时更新计时器
        if (GameState.Instance != null && timerText != null && !GameState.Instance.IsGameOver)
            timerText.text = GameState.FormatTime(GameState.Instance.ElapsedTime);
    }

    // ── UI更新方法 ────────────────────────────────────────

    private void UpdateKeyUI(bool hasKey)
    {
        if (keyIcon != null)
            keyIcon.color = hasKey ? keyPresentColor : keyAbsentColor;

        if (keyStatusText != null)
            keyStatusText.text = hasKey ? "已持有钥匙" : "未获得钥匙";
    }

    private void SetDoorPrompt(string msg)
    {
        if (doorPromptText == null) return;
        doorPromptText.text = msg;
        doorPromptText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }

    private void SetPickupPrompt(bool show, string msg)
    {
        if (pickupPromptText == null) return;
        pickupPromptText.gameObject.SetActive(show);
        pickupPromptText.text = msg;
    }

    private void ShowWinScreen(float elapsedTime)
    {
        SetWinPanel(true);

        if (winTimeText != null)
            winTimeText.text = $"用时  {GameState.FormatTime(elapsedTime)}";

        // 解锁鼠标
        Cursor.lockState = CursorLockMode.None;
    }

    private void SetWinPanel(bool active)
    {
        if (winPanel != null)
            winPanel.SetActive(active);
    }

    // ── 重新生成 ──────────────────────────────────────────

    private void OnRestartClicked()
    {
        // 生成新种子并重置状态
        if (GameState.Instance != null)
            GameState.Instance.ResetWithNewSeed(); // 内部自动随机新种子

        // 重载当前场景
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
