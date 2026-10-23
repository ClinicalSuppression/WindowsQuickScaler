using System.IO;
using System.Text.Json;

namespace WindowsQuickScaler
{
    /// <summary>
    /// A named scaling action and the displays to which it applies.
    /// </summary>
    public sealed class ScalingPreset
    {
        /// <summary>Gets or sets the display name shown in the dashboard and tray menu.</summary>
        public string Name { get; set; } = "";

        /// <summary>Gets or sets the requested scale percentage.</summary>
        public int ScalePercentage { get; set; }

        /// <summary>Gets or sets whether the preset applies to every connected display.</summary>
        public bool ApplyToAllMonitors { get; set; }

        /// <summary>Gets or sets the GDI display names targeted when not applying to all displays.</summary>
        public List<string> DeviceNames { get; set; } = [];
    }

    /// <summary>
    /// Loads and atomically saves the user's presets in their local application data directory.
    /// </summary>
    internal static class PresetStore
    {
        private static readonly string PresetFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsQuickScaler",
            "presets.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// Loads and validates saved presets, creating the default all-displays preset on first run.
        /// </summary>
        public static List<ScalingPreset> LoadOrCreateDefault()
        {
            if (!File.Exists(PresetFilePath))
            {
                List<ScalingPreset> defaults = [CreateDefaultPreset()];
                Save(defaults);
                return defaults;
            }

            try
            {
                string json = File.ReadAllText(PresetFilePath);
                List<ScalingPreset> presets = JsonSerializer.Deserialize<List<ScalingPreset>>(json, JsonOptions)
                    ?? throw new InvalidDataException("The preset file does not contain a preset list.");

                Validate(presets);
                return presets;
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    $"The saved preset file is invalid and was not changed: {PresetFilePath}",
                    ex);
            }
        }

        /// <summary>Saves a validated preset collection without partially replacing the current file.</summary>
        public static void Save(IReadOnlyCollection<ScalingPreset> presets)
        {
            Validate(presets);

            string directory = Path.GetDirectoryName(PresetFilePath)
                ?? throw new InvalidOperationException("Could not determine the preset storage directory.");
            Directory.CreateDirectory(directory);

            string temporaryPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
            try
            {
                string json = JsonSerializer.Serialize(presets, JsonOptions);
                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, PresetFilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        /// <summary>Creates the initial 100 percent preset for all connected displays.</summary>
        public static ScalingPreset CreateDefaultPreset() => new()
        {
            Name = "100% — All monitors",
            ScalePercentage = 100,
            ApplyToAllMonitors = true
        };

        private static void Validate(IReadOnlyCollection<ScalingPreset> presets)
        {
            if (presets.Any(preset =>
                string.IsNullOrWhiteSpace(preset.Name) ||
                preset.Name.Length > 120 ||
                preset.ScalePercentage is not (100 or 125 or 150 or 175 or 200 or 225 or 250 or 300 or 350 or 400 or 450 or 500) ||
                preset.DeviceNames is null ||
                (!preset.ApplyToAllMonitors && preset.DeviceNames.Count == 0) ||
                preset.DeviceNames.Any(string.IsNullOrWhiteSpace)))
            {
                throw new InvalidDataException("One or more saved presets contain invalid names, scales, or monitor targets.");
            }
        }
    }
}
