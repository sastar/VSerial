namespace VSerial.UI.Localization;

/// <summary>
/// 多语言定义模型，便于未来无缝扩展更多语言
/// </summary>
public record LanguageInfo(
    string Code,
    string NativeName,
    string EnglishName,
    string DisplayName
);
