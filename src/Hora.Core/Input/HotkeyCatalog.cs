using System.Text.RegularExpressions;

namespace Hora.Core.Input;

/// <summary>
/// Defines the global shortcut gestures the application offers and validates
/// an arbitrary gesture before it is stored or registered.
/// </summary>
public static partial class HotkeyCatalog
{
    public const string Default = "Ctrl+Alt+H";

    public static IReadOnlyList<string> Presets { get; } = new[]
    {
        "Ctrl+Alt+H", "Ctrl+Alt+N", "Ctrl+Shift+H", "Alt+Shift+H", "Ctrl+Alt+F12",
    };

    public static bool IsValid(string? gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture) || gesture.Length > 64)
        {
            return false;
        }

        var parts = gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts.Length > 4)
        {
            return false;
        }

        var key = parts[^1];
        if (!KeyToken().IsMatch(key))
        {
            return false;
        }

        var modifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < parts.Length - 1; index += 1)
        {
            if (!ModifierToken().IsMatch(parts[index]) || !modifiers.Add(parts[index]))
            {
                return false;
            }
        }

        return modifiers.Count > 0;
    }

    [GeneratedRegex(@"^(?:[A-Z]|[0-9]|F(?:[1-9]|1[0-9]|2[0-4]))$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyToken();

    [GeneratedRegex(@"^(?:Ctrl|Control|Alt|Shift|Win)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ModifierToken();
}
