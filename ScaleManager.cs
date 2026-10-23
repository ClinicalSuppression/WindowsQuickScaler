using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WindowsQuickScaler
{
    /// <summary>
    /// Reads and updates the DPI scale for a connected Windows display.
    /// </summary>
    public static class ScaleManager
    {
        private const uint QueryDisplayConfigOnlyActivePaths = 0x00000002;
        private const int GetSourceDeviceName = 1;
        private const int GetDpiScale = -3;
        private const int SetDpiScale = -4;
        private const int ErrorSuccess = 0;
        private const int ErrorInsufficientBuffer = 122;

        private static readonly int[] SupportedScalePercentages =
            [100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500];

        /// <summary>
        /// Sets the scaling percentage for the active display identified by its GDI device name.
        /// </summary>
        /// <param name="deviceName">A display name such as <c>\\.\DISPLAY1</c>.</param>
        /// <param name="scalePercentage">A supported Windows scale percentage.</param>
        /// <exception cref="ArgumentException">The display name is null, empty, or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The requested percentage is not supported.</exception>
        /// <exception cref="InvalidOperationException">The display is inactive or does not support the requested scale.</exception>
        /// <exception cref="Win32Exception">A Windows display configuration call fails.</exception>
        public static void SetMonitorScaling(string deviceName, int scalePercentage)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);

            int scaleIndex = Array.IndexOf(SupportedScalePercentages, scalePercentage);
            if (scaleIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scalePercentage),
                    scalePercentage,
                    $"Scale must be one of {string.Join(", ", SupportedScalePercentages)} percent.");
            }

            DisplayConfigPathInfo? targetPath = FindActiveDisplayPath(deviceName);
            if (targetPath is not DisplayConfigPathInfo path)
            {
                throw new InvalidOperationException($"Could not find an active display named '{deviceName}'.");
            }

            DisplayConfigGetDpiScale scaleInfo = new()
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = GetDpiScale,
                    Size = (uint)Marshal.SizeOf<DisplayConfigGetDpiScale>(),
                    AdapterId = path.SourceInfo.AdapterId,
                    Id = path.SourceInfo.Id
                }
            };

            int result = DisplayConfigGetDeviceInfo(ref scaleInfo);
            if (result != ErrorSuccess)
                throw new Win32Exception(result, $"Could not read the scaling range for '{deviceName}'.");

            int maxScaleIndex = Math.Abs(scaleInfo.MinScaleRelative) + scaleInfo.MaxScaleRelative;
            if (scaleInfo.MinScaleRelative > 0 ||
                scaleInfo.MaxScaleRelative < 0 ||
                maxScaleIndex < 0)
            {
                throw new InvalidOperationException($"Windows returned an invalid scaling range for '{deviceName}'.");
            }

            if (scaleIndex > maxScaleIndex)
            {
                throw new InvalidOperationException(
                    $"{scalePercentage}% scaling is not available for '{deviceName}'. " +
                    $"This display supports {SupportedScalePercentages[0]}% through " +
                    $"{SupportedScalePercentages[Math.Min(maxScaleIndex, SupportedScalePercentages.Length - 1)]}%.");
            }

            DisplayConfigSetDpiScale setScale = new()
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = SetDpiScale,
                    Size = (uint)Marshal.SizeOf<DisplayConfigSetDpiScale>(),
                    AdapterId = path.SourceInfo.AdapterId,
                    Id = path.SourceInfo.Id
                },
                ScaleRelative = scaleIndex + scaleInfo.MinScaleRelative
            };

            result = DisplayConfigSetDeviceInfo(ref setScale);
            if (result != ErrorSuccess)
                throw new Win32Exception(result, $"Could not set scaling for '{deviceName}'.");
        }

        private static DisplayConfigPathInfo? FindActiveDisplayPath(string deviceName)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int result = GetDisplayConfigBufferSizes(
                    QueryDisplayConfigOnlyActivePaths,
                    out uint pathCount,
                    out uint modeCount);
                if (result != ErrorSuccess)
                    throw new Win32Exception(result, "Could not enumerate active displays.");

                var paths = new DisplayConfigPathInfo[pathCount];
                var modes = new DisplayConfigModeInfo[modeCount];

                result = QueryDisplayConfig(
                    QueryDisplayConfigOnlyActivePaths,
                    ref pathCount,
                    paths,
                    ref modeCount,
                    modes,
                    IntPtr.Zero);

                if (result == ErrorInsufficientBuffer)
                    continue;
                if (result != ErrorSuccess)
                    throw new Win32Exception(result, "Could not enumerate active displays.");

                foreach (DisplayConfigPathInfo path in paths.Take((int)pathCount))
                {
                    var sourceName = new DisplayConfigSourceDeviceName
                    {
                        Header = new DisplayConfigDeviceInfoHeader
                        {
                            Type = GetSourceDeviceName,
                            Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                            AdapterId = path.SourceInfo.AdapterId,
                            Id = path.SourceInfo.Id
                        }
                    };

                    result = DisplayConfigGetDeviceInfo(ref sourceName);
                    if (result != ErrorSuccess)
                        throw new Win32Exception(result, "Could not identify an active display.");

                    if (string.Equals(sourceName.ViewGdiDeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                        return path;
                }

                return null;
            }

            throw new InvalidOperationException("The display configuration changed repeatedly during enumeration.");
        }

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(
            uint flags,
            out uint numPathArrayElements,
            out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            uint flags,
            ref uint numPathArrayElements,
            [Out] DisplayConfigPathInfo[] pathArray,
            ref uint numModeInfoArrayElements,
            [Out] DisplayConfigModeInfo[] modeInfoArray,
            IntPtr currentTopologyId);

        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
        private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSourceDeviceName requestPacket);

        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
        private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigGetDpiScale requestPacket);

        [DllImport("user32.dll", EntryPoint = "DisplayConfigSetDeviceInfo")]
        private static extern int DisplayConfigSetDeviceInfo(ref DisplayConfigSetDpiScale requestPacket);

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigLuid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigDeviceInfoHeader
        {
            public int Type;
            public uint Size;
            public DisplayConfigLuid AdapterId;
            public uint Id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayConfigSourceDeviceName
        {
            public DisplayConfigDeviceInfoHeader Header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string ViewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathSourceInfo
        {
            public DisplayConfigLuid AdapterId;
            public uint Id;
            public uint ModeInfoIndex;
            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigRational
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathTargetInfo
        {
            public DisplayConfigLuid AdapterId;
            public uint Id;
            public uint ModeInfoIndex;
            public uint OutputTechnology;
            public uint Rotation;
            public uint Scaling;
            public DisplayConfigRational RefreshRate;
            public uint ScanLineOrdering;
            public int TargetAvailable;
            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathInfo
        {
            public DisplayConfigPathSourceInfo SourceInfo;
            public DisplayConfigPathTargetInfo TargetInfo;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct DisplayConfigModeInfo
        {
            [FieldOffset(0)] public uint InfoType;
            [FieldOffset(4)] public uint Id;
            [FieldOffset(8)] public DisplayConfigLuid AdapterId;
            [FieldOffset(16)] public ulong ModeData0;
            [FieldOffset(24)] public ulong ModeData1;
            [FieldOffset(32)] public ulong ModeData2;
            [FieldOffset(40)] public ulong ModeData3;
            [FieldOffset(48)] public ulong ModeData4;
            [FieldOffset(56)] public ulong ModeData5;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigGetDpiScale
        {
            public DisplayConfigDeviceInfoHeader Header;
            public int MinScaleRelative;
            public int CurrentScaleRelative;
            public int MaxScaleRelative;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigSetDpiScale
        {
            public DisplayConfigDeviceInfoHeader Header;
            public int ScaleRelative;
        }
    }
}
