// using UnityEngine;

// /// <summary>
// /// 全局游戏状态单例
// /// 跨场景持久化，保存钥匙、计时、种子等全局信息
// /// </summary>
// public class GameState : MonoBehaviour
// {
//     public static GameState Instance { get; private set; }

//     [Header("运行时状态（只读，调试用）")]
//     [SerializeField] private bool _hasKey;
//     [SerializeField] private bool _isGameOver;
//     [SerializeField] private float _elapsedTime;
//     [SerializeField] private int _currentSeed;

//     public bool HasKey
//     {
//         get => _hasKey;
//         set
//         {
//             _hasKey = value;
//             OnKeyStateChanged?.Invoke(value);
//         }
//     }

//     public bool IsGameOver
//     {
//         get => _isGameOver;
//         set => _isGameOver = value;
//     }

//     public float ElapsedTime => _elapsedTime;
//     public int CurrentSeed => _currentSeed;

//     // 事件：钥匙状态变化时通知UI
//     public System.Action<bool> OnKeyStateChanged;
//     // 事件：通关时触发
//     public System.Action<float> OnGameClear;

//     void Awake()
//     {
//         // 单例，跨场景不销毁
//         if (Instance != null && Instance != this)
//         {
//             Destroy(gameObject);
//             return;
//         }
//         Instance = this;
//         DontDestroyOnLoad(gameObject);
//     }

//     void Update()
//     {
//         // 游戏未结束时持续计时
//         if (!_isGameOver)
//             _elapsedTime += Time.deltaTime;
//     }

//     /// <summary>
//     /// 以新种子重置所有状态（重新生成地牢前调用）
//     /// </summary>
//     public void ResetWithNewSeed(int seed = -1)
//     {
//         _hasKey = false;
//         _isGameOver = false;
//         _elapsedTime = 0f;
//         _currentSeed = seed >= 0 ? seed : Random.Range(1, 1000000);
//         OnKeyStateChanged = null;
//         OnGameClear = null;
//     }

//     /// <summary>
//     /// 触发通关
//     /// </summary>
//     public void TriggerGameClear()
//     {
//         if (_isGameOver) return;
//         _isGameOver = true;
//         OnGameClear?.Invoke(_elapsedTime);
//     }

//     /// <summary>
//     /// 格式化时间为 mm:ss
//     /// </summary>
//     public static string FormatTime(float seconds)
//     {
//         int m = (int)(seconds / 60);
//         int s = (int)(seconds % 60);
//         return $"{m:00}:{s:00}";
//     }
// }



using UnityEngine;

/// <summary>
/// 全局游戏状态单例 v3
/// 管理正向累计计时、最大生命值、扣血与失败判定
/// </summary>
public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    [Header("生命值配置")]
    public float maxHealth = 100f;

    [Header("运行时状态（只读，调试用）")]
    [SerializeField] private bool _isGameStarted = false;
    [SerializeField] private bool _hasKey;
    [SerializeField] private bool _isGameOver;
    [SerializeField] private bool _isGameWon;
    [SerializeField] private float _elapsedTime; // 正向计时累加
    [SerializeField] private float _currentHealth;
    [SerializeField] private int _currentSeed;

    public bool IsGameStarted
    {
        get => _isGameStarted;
        set => _isGameStarted = value;
    }

    public bool HasKey
    {
        get => _hasKey;
        set
        {
            _hasKey = value;
            OnKeyStateChanged?.Invoke(value);
        }
    }

    public bool IsGameOver => _isGameOver;
    public bool IsGameWon => _isGameWon;
    public float ElapsedTime => _elapsedTime;
    public float CurrentHealth => _currentHealth;
    public int CurrentSeed => _currentSeed;

    // 事件系统
    public System.Action<bool> OnKeyStateChanged;
    public System.Action<float> OnHealthChanged;   // 生命值变化事件
    public System.Action<float> OnGameClear;       // 胜利事件
    public System.Action OnGameOver;               // 失败事件

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResetWithNewSeed();
    }

    void Update()
    {
        // 游戏正式开始、且未结束时，正向累计时间
        if (_isGameStarted && !_isGameOver)
        {
            _elapsedTime += Time.deltaTime;
        }
    }

    public void StartGame()
    {
        _isGameStarted = true;
        _isGameOver = false;
    }

    /// <summary>
    /// 扣除生命值（开错假门时由大门触发）
    /// </summary>
    public void DeductHealth(float amount)
    {
        if (_isGameOver) return;

        _currentHealth -= amount;
        if (_currentHealth <= 0f)
        {
            _currentHealth = 0f;
            OnHealthChanged?.Invoke(_currentHealth);
            TriggerGameOver(); // 生命值归零，触发游戏结束
        }
        else
        {
            OnHealthChanged?.Invoke(_currentHealth);
        }
    }

    public void ResetWithNewSeed(int seed = -1)
    {
        _isGameStarted = false;
        _hasKey = false;
        _isGameOver = false;
        _isGameWon = false;
        _elapsedTime = 0f;
        _currentHealth = maxHealth;
        _currentSeed = seed >= 0 ? seed : Random.Range(1, 1000000);
        
        OnKeyStateChanged = null;
        OnHealthChanged = null;
        OnGameClear = null;
        OnGameOver = null;
    }

    public void TriggerGameClear()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        _isGameWon = true;
        OnGameClear?.Invoke(_elapsedTime);
    }

    public void TriggerGameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        _isGameWon = false;
        OnGameOver?.Invoke();
    }

    public static string FormatTime(float seconds)
    {
        int m = (int)(seconds / 60);
        int s = (int)(seconds % 60);
        return $"{m:00}:{s:00}";
    }
}