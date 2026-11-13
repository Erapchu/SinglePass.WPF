using AsyncKeyedLock;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Hotkeys;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SinglePass.WPF.Settings
{
    public class AppSettingsService : IAppSettings
    {
        private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
        private readonly ILogger<AppSettingsService> _logger;

        private readonly AppSettings _settings;
        public AppSettings Settings => _settings;

        public AppTheme ThemeMode
        {
            get => Settings.ThemeMode;
            set => Settings.ThemeMode = value;
        }

        public string Language
        {
            get => Settings.Language;
            set => Settings.Language = value;
        }

        public bool GoogleDriveEnabled
        {
            get => Settings.GoogleDriveEnabled;
            set => Settings.GoogleDriveEnabled = value;
        }

        public Hotkey ShowPopupHotkey
        {
            get => Settings.ShowPopupHotkey;
            set => Settings.ShowPopupHotkey = value;
        }

        public WindowSettings MainWindowSettings
        {
            get => Settings.MainWindowSettings;
            set => Settings.MainWindowSettings = value;
        }

        public SortType Sort
        {
            get => Settings.Sort;
            set => Settings.Sort = value;
        }

        public OrderType Order
        {
            get => Settings.Order;
            set => Settings.Order = value;
        }

        public double DetailsPaneRatio
        {
            get => Settings.DetailsPaneRatio;
            set => Settings.DetailsPaneRatio = value;
        }

        public AppSettingsService(AppSettings settings, AsyncKeyedLocker<string> asyncKeyedLocker, ILogger<AppSettingsService> logger)
        {
            _settings = settings;
            _asyncKeyedLocker = asyncKeyedLocker;
            _logger = logger;
        }

        internal static AppSettings Load(out Exception readError)
        {
            readError = null;
            var path = Constants.CommonSettingsFilePath;

            if (!File.Exists(path))
                return new AppSettings();

            // Read existing
            try
            {
                using var fileStream = File.OpenText(path);
                var serializer = JsonSerializer.Create(new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    Culture = CultureInfo.InvariantCulture
                });
                var loaded = serializer.Deserialize(fileStream, typeof(AppSettings)) as AppSettings;
                return loaded ?? new AppSettings();
            }
            catch (Exception ex)
            {
                readError = ex;
                try
                {
                    File.Move(path, path + ".corrupt", overwrite: true);
                }
                catch { /* best effort */ }
                return new AppSettings();
            }
        }

        public async Task Save()
        {
            var path = Constants.CommonSettingsFilePath;

            var tmp = path + ".tmp";
            var bak = path + ".bak";

            using (await _asyncKeyedLocker.LockAsync(path).ConfigureAwait(false))
            {
                try
                {
                    // Запись атомарно: tmp -> replace/move
                    await using (var fs = new FileStream(
                        tmp, FileMode.Create, FileAccess.Write, FileShare.None,
                        4096, FileOptions.WriteThrough | FileOptions.SequentialScan))
                    {
                        using var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                        var serializer = JsonSerializer.Create(new JsonSerializerSettings
                        {
                            Formatting = Formatting.Indented,
                            TypeNameHandling = TypeNameHandling.None,
                            Culture = CultureInfo.InvariantCulture,
                            // Converters = { new HotkeyJsonConverter() } // если нужен кастом
                        });
                        serializer.Serialize(sw, Settings);
                        await sw.FlushAsync().ConfigureAwait(false);
                        await fs.FlushAsync().ConfigureAwait(false);
                    }

                    if (File.Exists(path))
                    {
                        // делаем резервную копию старого
                        try { File.Copy(path, bak, overwrite: true); } catch { /* best effort */ }
                    }

                    // На современных ФС можно File.Replace, иначе — Move с overwrite
                    if (OperatingSystem.IsWindows() && File.Exists(path))
                        File.Replace(tmp, path, bak, ignoreMetadataErrors: true);
                    else
                        File.Move(tmp, path, overwrite: true);

                    _logger.LogInformation("Settings saved to {Path}", path);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save settings");
                    // если tmp остался — пробуем убрать
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                    throw;
                }
            }
        }
    }
}
