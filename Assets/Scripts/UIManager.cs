// using UnityEngine;
// using UnityEngine.UI;
// using UnityEngine.SceneManagement;
// // 如果项目中没有安装 TextMeshPro，将下面这行注释掉
// // 并将文件中所有 TMP_Text 替换为 UnityEngine.UI.Text
// using TMPro;

// /// <summary>
// /// UI管理器
// /// 管理：计时器、钥匙状态图标、门提示、拾取提示、通关界面
// /// 
// /// Canvas层级建议：
// ///   Canvas
// ///   ├── HUD
// ///   │   ├── TimerText          (右上角，格式 "00:00")
// ///   │   ├── KeyIcon            (左上角图标，有钥匙时高亮)
// ///   │   ├── KeyStatusText      (KeyIcon旁边文字 "×" / "✓")
// ///   │   ├── DoorPromptText     (屏幕中下，锁门提示)
// ///   │   └── PickupPromptText   (屏幕中央，"按E拾取")
// ///   └── WinPanel               (通关面板，默认隐藏)
// ///       ├── WinTimeText        ("用时 01:23")
// ///       └── RestartButton
// /// </summary>
// public class UIManager : MonoBehaviour
// {
//     [Header("HUD")]
//     public TMP_Text timerText;
//     public Image    keyIcon;            // 钥匙图标（有钥匙时变亮）
//     public TMP_Text keyStatusText;      // "无钥匙" / "已持有钥匙"
//     public TMP_Text doorPromptText;     // 靠近锁门时的提示
//     public TMP_Text pickupPromptText;   // 准星中央的拾取提示

//     [Header("通关界面")]
//     public GameObject winPanel;
//     public TMP_Text   winTimeText;
//     public Button     restartButton;

//     [Header("钥匙图标颜色")]
//     public Color keyAbsentColor  = new Color(0.4f, 0.4f, 0.4f);
//     public Color keyPresentColor = new Color(1f,   0.9f, 0f);

//     void Start()
//     {
//         // 初始状态
//         SetWinPanel(false);
//         SetDoorPrompt("");
//         SetPickupPrompt(false, "");
//         UpdateKeyUI(false);

//         // 订阅事件
//         if (GameState.Instance != null)
//         {
//             GameState.Instance.OnKeyStateChanged += UpdateKeyUI;
//             GameState.Instance.OnGameClear       += ShowWinScreen;
//         }

//         LockedDoor.OnPlayerNearDoor    += SetDoorPrompt;
//         PlayerController.OnPickupPrompt += SetPickupPrompt;

//         // 重新生成按钮
//         if (restartButton != null)
//             restartButton.onClick.AddListener(OnRestartClicked);
//     }

//     void OnDestroy()
//     {
//         if (GameState.Instance != null)
//         {
//             GameState.Instance.OnKeyStateChanged -= UpdateKeyUI;
//             GameState.Instance.OnGameClear       -= ShowWinScreen;
//         }
//         LockedDoor.OnPlayerNearDoor    -= SetDoorPrompt;
//         PlayerController.OnPickupPrompt -= SetPickupPrompt;
//     }

//     void Update()
//     {
//         // 实时更新计时器
//         if (GameState.Instance != null && timerText != null && !GameState.Instance.IsGameOver)
//             timerText.text = GameState.FormatTime(GameState.Instance.ElapsedTime);
//     }

//     // ── UI更新方法 ────────────────────────────────────────

//     private void UpdateKeyUI(bool hasKey)
//     {
//         if (keyIcon != null)
//             keyIcon.color = hasKey ? keyPresentColor : keyAbsentColor;

//         if (keyStatusText != null)
//             keyStatusText.text = hasKey ? "已持有钥匙" : "未获得钥匙";
//     }

//     private void SetDoorPrompt(string msg)
//     {
//         if (doorPromptText == null) return;
//         doorPromptText.text = msg;
//         doorPromptText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
//     }

//     private void SetPickupPrompt(bool show, string msg)
//     {
//         if (pickupPromptText == null) return;
//         pickupPromptText.gameObject.SetActive(show);
//         pickupPromptText.text = msg;
//     }

//     private void ShowWinScreen(float elapsedTime)
//     {
//         SetWinPanel(true);

//         if (winTimeText != null)
//             winTimeText.text = $"用时  {GameState.FormatTime(elapsedTime)}";

//         // 解锁鼠标
//         Cursor.lockState = CursorLockMode.None;
//     }

//     private void SetWinPanel(bool active)
//     {
//         if (winPanel != null)
//             winPanel.SetActive(active);
//     }

//     // ── 重新生成 ──────────────────────────────────────────

//     private void OnRestartClicked()
//     {
//         // 生成新种子并重置状态
//         if (GameState.Instance != null)
//             GameState.Instance.ResetWithNewSeed(); // 内部自动随机新种子

//         // 重载当前场景
//         SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
//     }
// }




using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// UI管理器 v3
/// 管理：开始面板、胜利面板、失败面板、血量UI展示、正向累计时间显示
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("开始界面")]
    public GameObject startPanel;
    public Button     startGameButton;

    [Header("HUD")]
    public TMP_Text timerText;          // 显示累计流逝时间
    public TMP_Text healthText;         // 新增：显示生命值
    public Image    keyIcon;            
    public TMP_Text keyStatusText;      
    public TMP_Text doorPromptText;     
    public TMP_Text pickupPromptText;   

    [Header("通关(胜利)界面")]
    public GameObject winPanel;
    public TMP_Text   winTimeText;
    public Button     winRestartButton;

    [Header("失败(游戏结束)界面")]
    public GameObject losePanel;
    public Button     loseRestartButton;

    [Header("配置")]
    public Color keyAbsentColor  = new Color(0.4f, 0.4f, 0.4f);
    public Color keyPresentColor = new Color(1f,   0.9f, 0f);

    void Start()
    {
        if (startPanel != null) startPanel.SetActive(true);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        SetDoorPrompt("");
        SetPickupPrompt(false, "");
        UpdateKeyUI(false);
        
        // 初始化血量 UI 数值
        if (GameState.Instance != null)
        {
            UpdateHealthUI(GameState.Instance.maxHealth);
        }

        // 订阅事件
        if (GameState.Instance != null)
        {
            GameState.Instance.OnKeyStateChanged += UpdateKeyUI;
            GameState.Instance.OnHealthChanged   += UpdateHealthUI;
            GameState.Instance.OnGameClear       += ShowWinScreen;
            GameState.Instance.OnGameOver        += ShowLoseScreen;
        }

        LockedDoor.OnPlayerNearDoor    += SetDoorPrompt;
        PlayerController.OnPickupPrompt += SetPickupPrompt;

        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);

        if (winRestartButton != null)
            winRestartButton.onClick.AddListener(OnRestartClicked);

        if (loseRestartButton != null)
            loseRestartButton.onClick.AddListener(OnRestartClicked);
    }

    void OnDestroy()
    {
        if (GameState.Instance != null)
        {
            GameState.Instance.OnKeyStateChanged -= UpdateKeyUI;
            GameState.Instance.OnHealthChanged   -= UpdateHealthUI;
            GameState.Instance.OnGameClear       -= ShowWinScreen;
            GameState.Instance.OnGameOver        -= ShowLoseScreen;
        }
        LockedDoor.OnPlayerNearDoor    -= SetDoorPrompt;
        PlayerController.OnPickupPrompt -= SetPickupPrompt;
    }

    void Update()
    {
        // 累计计时器：在游戏运行时持续增加
        if (GameState.Instance != null && timerText != null && GameState.Instance.IsGameStarted && !GameState.Instance.IsGameOver)
        {
            timerText.text = GameState.FormatTime(GameState.Instance.ElapsedTime);
        }
    }

    private void OnStartGameClicked()
    {
        if (GameState.Instance == null) return;
        if (startPanel != null) startPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        GameState.Instance.StartGame();
    }

    private void OnRestartClicked()
    {
        if (GameState.Instance != null)
            GameState.Instance.ResetWithNewSeed();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ── HUD更新方法 ───────────────────────────────────────

    private void UpdateHealthUI(float currentHP)
    {
        if (healthText == null) return;

        healthText.text = $"HP: {currentHP} / 100";

        // 血量低于或等于 40% 时，血量文本变成红色预警
        if (currentHP <= 40f)
            healthText.color = Color.red;
        else
            healthText.color = Color.white;
    }

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
        if (winPanel != null) winPanel.SetActive(true);
        if (winTimeText != null)
            winTimeText.text = $"用时  {GameState.FormatTime(elapsedTime)}";
        Cursor.lockState = CursorLockMode.None;
    }

    private void ShowLoseScreen()
    {
        if (losePanel != null) losePanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
    }
}
