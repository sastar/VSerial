using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace VSerial.UI.Localization;

/// <summary>
/// 现代化 WPF 多语言本地化管理引擎
/// 支持热切换、动态 XAML 资源替换、与 Inno Setup 安装语言自动同步，并具备高度的可扩展性。
/// </summary>
public static class LocalizationManager
{
    public const string DefaultLanguageCode = "en-US";
    private const string ConfigFileName = "config.json";
    private const string LanguageResourceDictionaryKey = "CurrentLanguageDictionary";

    private static readonly Dictionary<string, ResourceDictionary> LoadedDictionaries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 支持的所有语言列表（易扩展，只需添加一条记录并在同目录提供 Strings.{Code}.xaml）
    /// </summary>
    public static IReadOnlyList<LanguageInfo> SupportedLanguages { get; } = new List<LanguageInfo>
    {
        new("en-US", "English", "English", "English (US)"),
        new("zh-CN", "简体中文", "Chinese (Simplified)", "简体中文 (Chinese)")
    };

    /// <summary>
    /// 当前生效的语言代码 (如 "en-US", "zh-CN")
    /// </summary>
    public static string CurrentLanguageCode { get; private set; } = DefaultLanguageCode;

    /// <summary>
    /// 语言切换全局通知事件
    /// </summary>
    public static event Action? LanguageChanged;

    /// <summary>
    /// 初始化多语言系统（在 App.OnStartup 中调用）
    /// </summary>
    public static void Initialize()
    {
        string targetLanguage = DetermineInitialLanguage();
        ApplyLanguage(targetLanguage, savePreference: false);
    }

    /// <summary>
    /// 应用并热切换语言
    /// </summary>
    /// <param name="languageCode">标准语言代码 (如 "en-US" 或 "zh-CN")</param>
    /// <param name="savePreference">是否将此偏好持久化到用户配置文件中</param>
    public static void ApplyLanguage(string languageCode, bool savePreference = true)
    {
        var matched = SupportedLanguages.FirstOrDefault(l =>
            string.Equals(l.Code, languageCode, StringComparison.OrdinalIgnoreCase));

        string resolvedCode = matched?.Code ?? DefaultLanguageCode;

        try
        {
            var dict = GetOrCreateDictionary(resolvedCode);

            var app = Application.Current;
            if (app != null)
            {
                // 查找并替换现有的语言资源字典（支持标记检测与 Source 路径检测）
                var merged = app.Resources.MergedDictionaries;
                var existingDict = merged.FirstOrDefault(d =>
                    d.Contains(LanguageResourceDictionaryKey) ||
                    (d.Source != null && d.Source.OriginalString.IndexOf("Strings.", StringComparison.OrdinalIgnoreCase) >= 0));

                if (existingDict != null)
                {
                    int index = merged.IndexOf(existingDict);
                    merged[index] = dict;
                }
                else
                {
                    merged.Add(dict);
                }
            }

            // 同步当前线程的区域文化
            var culture = new CultureInfo(resolvedCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            CurrentLanguageCode = resolvedCode;

            if (savePreference)
            {
                SaveUserPreference(resolvedCode);
            }

            // 触发事件通知订阅者
            LanguageChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalizationManager] Failed to apply language '{languageCode}': {ex.Message}");
        }
    }

    /// <summary>
    /// 从当前激活语言资源中安全获取字符串
    /// </summary>
    public static string GetString(string key, string? defaultValue = null)
    {
        try
        {
            // 1. 优先从当前语言字典读取
            var currentDict = GetOrCreateDictionary(CurrentLanguageCode);
            if (currentDict.Contains(key) && currentDict[key] is string currentVal)
            {
                return currentVal;
            }

            // 2. 尝试从 Application 全局资源树读取
            if (Application.Current != null && Application.Current.Resources.Contains(key))
            {
                var val = Application.Current.Resources[key];
                if (val is string str)
                {
                    return str;
                }
            }

            // 3. 回退机制：若当前字典找不到，尝试从默认 en-US 字典查找
            var fallbackDict = GetOrCreateDictionary(DefaultLanguageCode);
            if (fallbackDict.Contains(key) && fallbackDict[key] is string fallbackStr)
            {
                return fallbackStr;
            }
        }
        catch
        {
            // 忽略回退加载异常
        }

        return defaultValue ?? key;
    }

    /// <summary>
    /// 格式化本地化文本
    /// </summary>
    public static string Format(string key, params object[] args)
    {
        string pattern = GetString(key);
        try
        {
            return string.Format(pattern, args);
        }
        catch
        {
            return pattern;
        }
    }

    /// <summary>
    /// 核心算法：依据 3 级优先级仲裁首次启动展示语言：
    /// 1. 用户历史偏好配置 (%APPDATA%\VSerial\config.json)
    /// 2. Inno Setup 安装包写入的语言注册表记录 (HKLM / HKCU \ Software \ VSerial \ InstallLanguage)
    /// 3. 当前系统 UI 区域环境探测 (CultureInfo.CurrentUICulture)
    /// 4. 默认托底 (en-US)
    /// </summary>
    public static string DetermineInitialLanguage()
    {
        // 优先级 1：用户曾经在 GUI 界面主动配置过的语言
        string? savedPref = LoadUserPreference();
        if (!string.IsNullOrEmpty(savedPref) && IsSupported(savedPref))
        {
            return savedPref;
        }

        // 优先级 2：读取 Inno Setup 安装选择的语言
        string? installerLang = ReadInstallerLanguageFromRegistry();
        if (!string.IsNullOrEmpty(installerLang))
        {
            string mapped = MapInstallerLanguageCode(installerLang);
            if (IsSupported(mapped))
            {
                return mapped;
            }
        }

        // 优先级 3：根据当前操作系统的 UI 语言进行自动探测
        try
        {
            string systemCulture = CultureInfo.CurrentUICulture.Name;
            if (systemCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-CN";
            }
        }
        catch
        {
            // 探测失败使用默认值
        }

        // 优先级 4：默认托底英文
        return DefaultLanguageCode;
    }

    private static bool IsSupported(string code)
    {
        return SupportedLanguages.Any(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    private static ResourceDictionary GetOrCreateDictionary(string code)
    {
        if (LoadedDictionaries.TryGetValue(code, out var cached))
        {
            return cached;
        }

        string asmName = typeof(LocalizationManager).Assembly.GetName().Name ?? "vserial-gui";
        var uri = new Uri($"/{asmName};component/Localization/Strings.{code}.xaml", UriKind.Relative);
        var dict = (ResourceDictionary)Application.LoadComponent(uri);
        dict[LanguageResourceDictionaryKey] = code;

        LoadedDictionaries[code] = dict;
        return dict;
    }

    private static string MapInstallerLanguageCode(string rawInstallerLang)
    {
        string lower = rawInstallerLang.Trim().ToLowerInvariant();
        if (lower.Contains("chinese") || lower.Contains("zh") || lower.Contains("chs"))
        {
            return "zh-CN";
        }

        if (lower.Contains("english") || lower.Contains("en"))
        {
            return "en-US";
        }

        return lower;
    }

    private static string? ReadInstallerLanguageFromRegistry()
    {
        string[] subKeys = { @"Software\VSerial" };
        RegistryView[] views = { RegistryView.Registry64, RegistryView.Registry32 };

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in views)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    foreach (var subKey in subKeys)
                    {
                        using var key = baseKey.OpenSubKey(subKey);
                        if (key != null)
                        {
                            var val = key.GetValue("InstallLanguage");
                            if (val is string str && !string.IsNullOrWhiteSpace(str))
                            {
                                return str;
                            }
                        }
                    }
                }
                catch
                {
                    // 注册表读取安全降级
                }
            }
        }

        return null;
    }

    private static string GetConfigFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string vserialFolder = Path.Combine(appData, "VSerial");
        if (!Directory.Exists(vserialFolder))
        {
            Directory.CreateDirectory(vserialFolder);
        }
        return Path.Combine(vserialFolder, ConfigFileName);
    }

    private static string? LoadUserPreference()
    {
        try
        {
            string path = GetConfigFilePath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Language", out var langProp))
                {
                    return langProp.GetString();
                }
            }
        }
        catch
        {
            // 读取异常忽略
        }

        return null;
    }

    private static void SaveUserPreference(string languageCode)
    {
        try
        {
            string path = GetConfigFilePath();
            var data = new Dictionary<string, object>
            {
                ["Language"] = languageCode,
                ["LastUpdated"] = DateTime.UtcNow.ToString("o")
            };

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // 写入异常不阻断运行
        }
    }
}
