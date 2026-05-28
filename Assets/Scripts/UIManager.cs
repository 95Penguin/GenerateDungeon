using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// UI管理器 v5 (带 Inspector 绑定缺失警告版)
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("开始界面")]
    public GameObject startPanel;
    public Button     startGameButton;

    [Header("HUD")]
    public TMP_Text timerText;          
    public TMP_Text healthText;         
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
        
        if (GameState.Instance != null)
        {
            UpdateHealthUI(GameState.Instance.maxHealth);
        }

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

    private void UpdateHealthUI(float currentHP)
    {
        if (healthText == null) return;

        healthText.text = $"HP: {currentHP} / 100";

        if (currentHP <= 40f)
            healthText.color = Color.red;
        else
            healthText.color = Color.white;
    }

    private void UpdateKeyUI(bool hasKey)
    {
        Debug.Log($"[UIManager] UpdateKeyUI 状态切换 -> 是否持有钥匙: {hasKey}");
        
        if (keyIcon != null)
        {
            keyIcon.gameObject.SetActive(hasKey);
            keyIcon.color = hasKey ? keyPresentColor : keyAbsentColor;
        }
        else
        {
            Debug.LogWarning("[UIManager] 警告：keyIcon (钥匙图标) 未在 UIManager 的 Inspector 面板中赋值！");
        }

        if (keyStatusText != null)
        {
            keyStatusText.text = hasKey ?"Key Acquired" : "";
        }
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
        Debug.Log($"[UIManager] ShowWinScreen 触发，耗时: {elapsedTime}");
        
        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("[UIManager] 错误：winPanel (胜利界面) 未在 UIManager 的 Inspector 面板中赋值！");
        }

        if (winTimeText != null)
            winTimeText.text = $"用时  {GameState.FormatTime(elapsedTime)}";
        Cursor.lockState = CursorLockMode.None;
    }

    private void ShowLoseScreen()
    {
        if (losePanel != null) 
            losePanel.SetActive(true);
        else
            Debug.LogError("[UIManager] 错误：losePanel (失败界面) 未在 UIManager 的 Inspector 面板中赋值！");

        Cursor.lockState = CursorLockMode.None;
    }
}