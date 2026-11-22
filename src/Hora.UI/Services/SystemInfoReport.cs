using System.Globalization;
using System.Text;

namespace Hora.UI.Services;

/// <summary>Builds the redacted system report used by the copy and export actions.</summary>
public static class SystemInfoReport
{
    public static string Build(AppContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var builder = new StringBuilder();
        builder.AppendLine("Hora " + (typeof(SystemInfoReport).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"));
        builder.Append(T(context, "sysOs")).Append(": ").AppendLine(Environment.OSVersion.VersionString);
        builder.Append(T(context, "sysRuntime")).Append(": ").AppendLine(Environment.Version.ToString());
        builder.Append(T(context, "sysArchitecture")).Append(": ")
            .AppendLine(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString());
        builder.Append(T(context, "sysCulture")).Append(": ").AppendLine(CultureInfo.CurrentUICulture.Name);
        builder.Append(T(context, "sysLanguage")).Append(": ").AppendLine(context.Settings.Language);
        builder.Append(T(context, "sysTheme")).Append(": ")
            .AppendLine(context.Settings.Theme + " / " + context.Settings.ThemeMode);
        builder.Append(T(context, "sysBackupProvider")).Append(": ")
            .AppendLine(context.T(AppContext.ProviderLabelKey(context.Settings.BackupProvider)));
        builder.Append(T(context, "sysBackupConnected")).Append(": ")
            .AppendLine(context.T(context.Settings.BackupConnected ? "commonYes" : "commonNo"));
        builder.Append(T(context, "sysDataFolder")).Append(": ")
            .AppendLine(Core.Logging.Redact.Message(Hora.Storage.Paths.HoraPaths.AppDir));
        builder.Append(T(context, "sysBackupFolder")).Append(": ")
            .AppendLine(Core.Logging.Redact.Message(context.BackupFolder));
        builder.Append(T(context, "sysScreen")).Append(": ").AppendLine(SystemParametersInfo(context));
        return builder.ToString();
    }

    private static string T(AppContext context, string key) => context.T(key);

    private static string SystemParametersInfo(AppContext context)
    {
        try
        {
            // WPF's SystemParameters values are DPI-relative and report odd
            // numbers on scaled displays; the real pixel bounds come from WinForms.
            var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds;
            if (bounds is null || bounds.Value.Width <= 0)
            {
                return context.T("commonUnknown");
            }

            return $"{bounds.Value.Width}x{bounds.Value.Height}";
        }
        catch (InvalidOperationException)
        {
            return context.T("commonUnknown");
        }
    }
}
