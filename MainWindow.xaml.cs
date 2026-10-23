using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfMedia = System.Windows.Media;
using Forms = System.Windows.Forms;
using Draw = System.Drawing;
using Microsoft.Win32;

namespace WindowsQuickScaler
{
    /// <summary>
    /// Dashboard for selecting displays, managing scaling presets, and applying display scaling.
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private Forms.NotifyIcon trayIcon = null!;
        private Draw.Icon trayAppIcon = null!;
        private readonly List<MonitorDisplayInfo> detectedMonitors = new();
        private readonly ObservableCollection<ScalingPreset> presets = new();
        private Forms.ContextMenuStrip trayContextMenu = null!;
        private bool isExiting;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            MonitorCanvas.SizeChanged += (_, _) => LayoutMonitorCards();
            LoadConnectedMonitors();
            LoadPresets();
            PresetsListBox.ItemsSource = presets;
            PresetsListBox.DisplayMemberPath = nameof(ScalingPreset.Name);
            RefreshTrayPresetMenu();
        }

        private void InitializeTrayIcon()
        {
            string executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("The application executable path could not be determined.");
            trayAppIcon = Draw.Icon.ExtractAssociatedIcon(executablePath)
                ?? throw new InvalidOperationException($"The application icon could not be loaded from '{executablePath}'.");

            trayIcon = new Forms.NotifyIcon
            {
                Icon = trayAppIcon,
                Visible = true,
                Text = "Windows Quick Scaler"
            };

            trayContextMenu = new Forms.ContextMenuStrip();
            trayContextMenu.Items.Add("Open Dashboard", null, (s, e) => { this.Show(); this.WindowState = WindowState.Normal; });
            trayContextMenu.Items.Add(new Forms.ToolStripSeparator());
            trayContextMenu.Items.Add("Exit App", null, (s, e) =>
            {
                isExiting = true;
                System.Windows.Application.Current.Shutdown();
            });

            trayIcon.ContextMenuStrip = trayContextMenu;
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = WindowState.Normal; };
            Closed += (_, _) =>
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayAppIcon.Dispose();
            };
        }

        private void LoadConnectedMonitors()
        {
            MonitorCanvas.Children.Clear();
            detectedMonitors.Clear();

            foreach (var screen in Forms.Screen.AllScreens)
            {
                var info = new MonitorDisplayInfo
                {
                    DeviceName = screen.DeviceName,
                    FriendlyName = screen.DeviceName.Replace("\\\\.\\DISPLAY", "Monitor "),
                    Bounds = screen.Bounds,
                    IsSelected = true
                };
                detectedMonitors.Add(info);

                var border = new Border
                {
                    Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0, 122, 204)),
                    BorderBrush = WpfMedia.Brushes.White,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(5),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = info,
                    ToolTip = "Click to toggle this display"
                };
                info.Card = border;

                var textBlock = new TextBlock
                {
                    Text = $"{info.FriendlyName}\n({screen.Bounds.Width}x{screen.Bounds.Height})",
                    Foreground = WpfMedia.Brushes.White,
                    FontSize = 9,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                border.Child = textBlock;

                border.MouseLeftButtonUp += (s, e) => {
                    info.IsSelected = !info.IsSelected;
                    UpdateMonitorCard(info);
                };

                MonitorCanvas.Children.Add(border);
                UpdateMonitorCard(info);
            }

            LayoutMonitorCards();
        }

        private void LoadPresets()
        {
            try
            {
                presets.Clear();
                foreach (ScalingPreset preset in PresetStore.LoadOrCreateDefault())
                    presets.Add(preset);
            }
            catch (Exception ex)
            {
                presets.Clear();
                presets.Add(PresetStore.CreateDefaultPreset());
                System.Windows.MessageBox.Show(
                    $"Saved presets could not be loaded. The original preset file was left unchanged.\n\n{ex.Message}",
                    "Preset loading error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RefreshTrayPresetMenu()
        {
            while (trayContextMenu.Items.Count > 2)
                trayContextMenu.Items.RemoveAt(1);

            if (presets.Count == 0)
            {
                trayContextMenu.Items.Insert(1, new Forms.ToolStripMenuItem("No saved presets")
                {
                    Enabled = false
                });
            }
            else
            {
                foreach (ScalingPreset preset in presets)
                {
                    var menuItem = new Forms.ToolStripMenuItem(preset.Name);
                    menuItem.Click += (_, _) => ApplyPreset(preset);
                    trayContextMenu.Items.Insert(trayContextMenu.Items.Count - 1, menuItem);
                }
            }

            trayContextMenu.Items.Insert(trayContextMenu.Items.Count - 1, new Forms.ToolStripSeparator());
        }

        private void LayoutMonitorCards()
        {
            if (detectedMonitors.Count == 0)
                return;

            double minX = detectedMonitors.Min(monitor => monitor.Bounds.X);
            double minY = detectedMonitors.Min(monitor => monitor.Bounds.Y);
            double maxX = detectedMonitors.Max(monitor => monitor.Bounds.Right);
            double maxY = detectedMonitors.Max(monitor => monitor.Bounds.Bottom);
            double totalWidth = maxX - minX;
            double totalHeight = maxY - minY;
            double availableWidth = Math.Max(1, MonitorCanvas.ActualWidth - 30);
            double availableHeight = Math.Max(1, MonitorCanvas.ActualHeight - 30);
            double scaleFactor = Math.Min(
                availableWidth / Math.Max(1, totalWidth),
                availableHeight / Math.Max(1, totalHeight));

            foreach (MonitorDisplayInfo monitor in detectedMonitors)
            {
                if (monitor.Card is not Border card)
                    continue;

                card.Width = Math.Max(70, monitor.Bounds.Width * scaleFactor);
                card.Height = Math.Max(50, monitor.Bounds.Height * scaleFactor);
                Canvas.SetLeft(card, (monitor.Bounds.X - minX) * scaleFactor + 15);
                Canvas.SetTop(card, (monitor.Bounds.Y - minY) * scaleFactor + 15);
            }
        }

        private static void UpdateMonitorCard(MonitorDisplayInfo monitor)
        {
            if (monitor.Card is not Border card)
                return;

            card.Background = monitor.IsSelected
                ? new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0, 122, 204))
                : new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(180, 180, 180));
            card.ToolTip = $"{monitor.FriendlyName} — {(monitor.IsSelected ? "selected" : "not selected")}";
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!isExiting && ChkMinimizeToTray.IsChecked == true)
            {
                e.Cancel = true;
                this.Hide();
                trayIcon.ShowBalloonTip(2000, "Windows Quick Scaler", "App minimized to tray. Right-click the tray icon for options.", Forms.ToolTipIcon.Info);
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (PresetsListBox.SelectedItem is ScalingPreset selectedPreset)
            {
                ApplyPreset(selectedPreset);
                return;
            }

            if (TryGetSelectedScale(out int scalePercent))
            {
                ExecuteScaleChange(scalePercent, false);
            }
        }

        private bool TryGetSelectedScale(out int scalePercent)
        {
            string? selectedItem = (CmbScaleTarget.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Replace("%", "");
            if (int.TryParse(selectedItem, out scalePercent))
                return true;

            System.Windows.MessageBox.Show(
                "Select a valid scaling percentage.",
                "Invalid scale",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        private void ExecuteScaleChange(int percentage, bool applyToAll)
        {
            List<MonitorDisplayInfo> targets = detectedMonitors
                .Where(monitor => applyToAll || monitor.IsSelected)
                .ToList();

            ExecuteScaleChange(percentage, targets, []);
        }

        private void ApplyPreset(ScalingPreset preset)
        {
            List<MonitorDisplayInfo> targets;
            List<string> missingMonitors;

            if (preset.ApplyToAllMonitors)
            {
                targets = detectedMonitors.ToList();
                missingMonitors = [];
            }
            else
            {
                var monitorsByName = detectedMonitors.ToDictionary(
                    monitor => monitor.DeviceName,
                    StringComparer.OrdinalIgnoreCase);
                targets = preset.DeviceNames
                    .Where(monitorsByName.ContainsKey)
                    .Select(deviceName => monitorsByName[deviceName])
                    .ToList();
                missingMonitors = preset.DeviceNames
                    .Where(deviceName => !monitorsByName.ContainsKey(deviceName))
                    .ToList();
            }

            ExecuteScaleChange(preset.ScalePercentage, targets, missingMonitors);
        }

        private void ExecuteScaleChange(
            int percentage,
            List<MonitorDisplayInfo> targets,
            List<string> missingMonitors)
        {
            if (targets.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    missingMonitors.Count == 0
                        ? "Select at least one display before applying scaling."
                        : $"The preset targets displays that are not currently connected:\n{string.Join("\n", missingMonitors)}",
                    "No available target displays",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var failures = new List<string>();
            int appliedCount = 0;
            foreach (MonitorDisplayInfo monitor in targets)
            {
                try
                {
                    ScaleManager.SetMonitorScaling(monitor.DeviceName, percentage);
                    appliedCount++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{monitor.FriendlyName}: {ex.Message}");
                }
            }

            if (failures.Count == 0)
            {
                string message = $"Saved {percentage}% scaling for {appliedCount} display(s).";
                if (missingMonitors.Count > 0)
                    message += $" Not connected: {string.Join(", ", missingMonitors)}.";
                message += " Sign out and back in if Windows does not apply it immediately.";

                trayIcon.ShowBalloonTip(2500, "Scaling saved", message, Forms.ToolTipIcon.Info);
                return;
            }

            System.Windows.MessageBox.Show(
                $"Saved scaling for {appliedCount} of {targets.Count} connected display(s)." +
                (missingMonitors.Count > 0 ? $"\n\nNot connected: {string.Join(", ", missingMonitors)}" : "") +
                $"\n\n{string.Join("\n", failures)}",
                "Some displays could not be updated",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (MonitorDisplayInfo monitor in detectedMonitors)
            {
                monitor.IsSelected = true;
                UpdateMonitorCard(monitor);
            }
        }

        private void BtnAddPreset_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedScale(out int scalePercent))
                return;

            List<MonitorDisplayInfo> selectedMonitors = detectedMonitors
                .Where(monitor => monitor.IsSelected)
                .ToList();

            if (selectedMonitors.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "Select one or more monitor cards before creating a preset.",
                    "No monitors selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            bool applyToAll = selectedMonitors.Count == detectedMonitors.Count;
            var preset = new ScalingPreset
            {
                Name = CreatePresetName(scalePercent, selectedMonitors, applyToAll),
                ScalePercentage = scalePercent,
                ApplyToAllMonitors = applyToAll,
                DeviceNames = applyToAll
                    ? []
                    : selectedMonitors.Select(monitor => monitor.DeviceName).ToList()
            };

            List<ScalingPreset> updatedPresets = presets.Append(preset).ToList();
            if (!TrySavePresets(updatedPresets))
                return;

            presets.Add(preset);
            PresetsListBox.SelectedItem = preset;
            RefreshTrayPresetMenu();
        }

        private void BtnDeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (PresetsListBox.SelectedItem is not ScalingPreset selectedPreset)
                return;

            List<ScalingPreset> updatedPresets = presets
                .Where(preset => !ReferenceEquals(preset, selectedPreset))
                .ToList();
            if (!TrySavePresets(updatedPresets))
                return;

            presets.Remove(selectedPreset);
            RefreshTrayPresetMenu();
        }

        private static string CreatePresetName(
            int scalePercent,
            IReadOnlyCollection<MonitorDisplayInfo> selectedMonitors,
            bool applyToAll)
        {
            string targets = applyToAll
                ? "All monitors"
                : string.Join(", ", selectedMonitors.Select(monitor => monitor.FriendlyName));
            return $"{scalePercent}% — {targets}";
        }

        private bool TrySavePresets(List<ScalingPreset> updatedPresets)
        {
            try
            {
                PresetStore.Save(updatedPresets);
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"The preset change could not be saved. Your current presets were not changed.\n\n{ex.Message}",
                    "Preset save error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        private void ChkStartup_Changed(object sender, RoutedEventArgs e)
        {
            string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true)
                    ?? throw new InvalidOperationException("The Windows startup settings could not be opened.");

                if (ChkStartup.IsChecked == true)
                {
                    string executablePath = Environment.ProcessPath
                        ?? throw new InvalidOperationException("The application executable path could not be determined.");
                    key.SetValue("WindowsQuickScaler", $"\"{executablePath}\"");
                }
                else
                    key.DeleteValue("WindowsQuickScaler", false);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"The startup setting could not be changed.\n\n{ex.Message}",
                    "Startup setting error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// A connected display and its dashboard selection state.
    /// </summary>
    public class MonitorDisplayInfo
    {
        /// <summary>Gets or sets the Windows GDI device name used to address this display.</summary>
        public string DeviceName { get; set; } = "";

        /// <summary>Gets or sets the friendly name shown in the dashboard.</summary>
        public string FriendlyName { get; set; } = "";

        /// <summary>Gets or sets the display bounds in virtual-screen coordinates.</summary>
        public Draw.Rectangle Bounds { get; set; }

        /// <summary>Gets or sets whether this display is selected as an operation target.</summary>
        public bool IsSelected { get; set; }

        /// <summary>Gets or sets the dashboard card representing this display.</summary>
        public Border? Card { get; set; }
    }
}