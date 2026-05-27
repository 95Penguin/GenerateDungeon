using UnityEngine;

/// <summary>
/// 全局游戏状态单例
/// 跨场景持久化，保存钥匙、计时、种子等全局信息
/// </summary>
public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    [Header("运行时状态（只读，调试用）")]
    [SerializeField] private bool _hasKey;
    [SerializeField] private bool _isGameOver;
    [SerializeField] private float _elapsedTime;
    [SerializeField] private int _currentSeed;

    public bool HasKey
    {
        get => _hasKey;
        set
        {
            _hasKey = value;
            OnKeyStateChanged?.Invoke(value);
        }
    }

    public bool IsGameOver
    {
        get => _isGameOver;
        set => _isGameOver = value;
    }

    public float ElapsedTime => _elapsedTime;
    public int CurrentSeed => _currentSeed;

    // 事件：钥匙状态变化时通知UI
    public System.Action<bool> OnKeyStateChanged;
    // 事件：通关时触发
    public System.Action<float> OnGameClear;

    void Awake()
    {
        // 单例，跨场景不销毁
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // 游戏未结束时持续计时
        if (!_isGameOver)
            _elapsedTime += Time.deltaTime;
    }

    /// <summary>
    /// 以新种子重置所有状态（重新生成地牢前调用）
    /// </summary>
    public void ResetWithNewSeed(int seed = -1)
    {
        _hasKey = false;
        _isGameOver = false;
        _elapsedTime = 0f;
        _currentSeed = seed >= 0 ? seed : Random.Range(1, 1000000);
        OnKeyStateChanged = null;
        OnGameClear = null;
    }

    /// <summary>
    /// 触发通关
    /// </summary>
    public void TriggerGameClear()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        OnGameClear?.Invoke(_elapsedTime);
    }

    /// <summary>
    /// 格式化时间为 mm:ss
    /// </summary>
    public static string FormatTime(float seconds)
    {
        int m = (int)(seconds / 60);
        int s = (int)(seconds % 60);
        return $"{m:00}:{s:00}";
    }
}
