namespace PixShell.Store
{
    /// <summary>让纯持久化测试无需加载 WPF 应用及用户的实际数据目录。</summary>
    internal static class HostStore
    {
        public static string AppDir => Path.GetTempPath();
    }
}

namespace PixShell.Logging
{
    /// <summary>测试中不写应用日志；生产实现仍由主项目提供。</summary>
    internal static class Log
    {
        public static void Info(string message, string category) { }
        public static void Warn(string message, string category) { }
        public static void Error(string message, string category) { }
    }
}

namespace PixShell
{
    /// <summary>供 SemanticHighlight 纯逻辑单元测试读取默认空覆盖色，不加载 WPF 颜色画刷。</summary>
    internal static class HighlightColors
    {
        public static string HighlightHex => "";
        public static string PlainHex => "";
    }
}
