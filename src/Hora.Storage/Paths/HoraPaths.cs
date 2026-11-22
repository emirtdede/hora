using Microsoft.Data.Sqlite;

namespace Hora.Storage.Paths;

/// <summary>Resolves every on-disk location used by Hora.</summary>
public static class HoraPaths
{
    public static string AppDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hora");

    /// <summary>Location used by 0.x builds. Kept only for a one-time migration.</summary>
    public static string LegacyAppDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hora");

    public static string DbPath => Path.Combine(AppDir, "usage.db");

    public static string SettingsPath => Path.Combine(AppDir, "settings.json");

    public static string LogDir => Path.Combine(AppDir, "logs");

    public static string LogPath => Path.Combine(
        LogDir,
        $"hora-{DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture)}.log");

    public static string LocalBackupDir => Path.Combine(AppDir, "backup");

    public static string IconDir => Path.Combine(AppDir, "icons");

    public static string ReportDir => Path.Combine(AppDir, "reports");

    /// <summary>Request written by a second launch and read by the running instance.</summary>
    public static string ActivationPath => Path.Combine(AppDir, "activation.request");

    public static void EnsureCreated()
    {
        MigrateLegacyData();
        Directory.CreateDirectory(AppDir);
        Directory.CreateDirectory(LogDir);
        Directory.CreateDirectory(IconDir);
    }

    /// <summary>
    /// Moves a 0.x profile from the roaming folder into the local folder. The
    /// database and logs belong on the machine, not in a roaming profile. A
    /// completion marker keeps deleted data from being resurrected later.
    /// </summary>
    public static void MigrateLegacyData()
    {
        try
        {
            if (!Directory.Exists(LegacyAppDir))
            {
                return;
            }

            Directory.CreateDirectory(AppDir);
            var marker = Path.Combine(AppDir, "migration.complete");
            if (File.Exists(marker))
            {
                return;
            }

            var complete = true;
            complete &= TryCopyFile(Path.Combine(LegacyAppDir, "settings.json"), SettingsPath);
            complete &= TryCopyDatabase(Path.Combine(LegacyAppDir, "usage.db"), DbPath);

            foreach (var folder in new[] { "icons", "reports" })
            {
                var source = Path.Combine(LegacyAppDir, folder);
                if (!Directory.Exists(source))
                {
                    continue;
                }

                var target = Path.Combine(AppDir, folder);
                Directory.CreateDirectory(target);
                foreach (var file in Directory.EnumerateFiles(source))
                {
                    complete &= TryCopyFile(file, Path.Combine(target, Path.GetFileName(file)));
                }
            }

            if (complete)
            {
                File.WriteAllText(marker, "Hora 0.x profile migrated to LocalAppData.");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A failed migration must never block startup; the next launch retries.
        }
    }

    private static bool TryCopyFile(string source, string target)
    {
        try
        {
            if (!File.Exists(source) || File.Exists(target))
            {
                return true;
            }

            File.Copy(source, target, overwrite: false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Copies the database through SQLite so committed WAL pages are included and
    /// the destination is a consistent standalone file.
    /// </summary>
    private static bool TryCopyDatabase(string source, string target)
    {
        if (!File.Exists(source) || File.Exists(target))
        {
            return true;
        }

        try
        {
            var sourceBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = source,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
            };
            var targetBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = target,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            };

            using var sourceConnection = new SqliteConnection(sourceBuilder.ToString());
            sourceConnection.Open();
            using var targetConnection = new SqliteConnection(targetBuilder.ToString());
            targetConnection.Open();
            sourceConnection.BackupDatabase(targetConnection);
            return true;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            // Never leave a half copied database behind that would block a retry.
            try
            {
                if (File.Exists(target))
                {
                    File.Delete(target);
                }
            }
            catch (Exception deleteException) when (deleteException is IOException or UnauthorizedAccessException)
            {
                // The next attempt reports the failure again.
            }

            return false;
        }
    }
}
