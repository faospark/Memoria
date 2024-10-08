using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Memoria.Launcher
{
    public sealed class SettingsGrid_Vanilla : UiGrid, INotifyPropertyChanged
    {
        public SettingsGrid_Vanilla()
        {
            SetCols(8);

            Width = 260;
            Margin = new Thickness(0);
            DataContext = this;

            Thickness rowMargin = new Thickness(0, 2, 0, 2);

            CreateTextbloc(Lang.Settings.ActiveMonitor, false, Lang.Settings.ActiveMonitor_Tooltip);
            String[] comboboxchoices = GetAvailableMonitors();
            CreateCombobox("ActiveMonitor", comboboxchoices, 4, "", true);

            CreateTextbloc(Lang.Settings.WindowMode, false, Lang.Settings.WindowMode_Tooltip);
            comboboxchoices = new String[]
            {
                Lang.Settings.Window,
                Lang.Settings.ExclusiveFullscreen,
                Lang.Settings.BorderlessFullscreen
            };
            CreateCombobox("WindowMode", comboboxchoices, 4);

            CreateTextbloc(Lang.Settings.Resolution, false, Lang.Settings.Resolution_Tooltip);
            comboboxchoices = EnumerateDisplaySettings(true).OrderByDescending(x => Convert.ToInt32(x.Split('x')[0])).ToArray();
            CreateCombobox("ScreenResolution", comboboxchoices, 4, "", true);

            CreateCheckbox("IsX64", "x64", Lang.Settings.Xsixfour_Tooltip, 0, "IsX64Enabled");
            CreateCheckbox("IsDebugMode", Lang.Settings.Debuggable, Lang.Settings.Debuggable_Tooltip, 4);
            CreateCheckbox("CheckUpdates", Lang.Settings.CheckUpdates, Lang.Settings.CheckUpdates_Tooltip);

            String OSversion = $"{Environment.OSVersion}";
            if (OSversion.Contains("Windows") && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINELOADER")))
                CreateCheckbox("SteamOverlayFix", Lang.SteamOverlay.OptionLabel, Lang.Settings.SteamOverlayFix_Tooltip);

            try
            {
                LoadSettings();
            }
            catch (Exception ex)
            {
                UiHelper.ShowError(Application.Current.MainWindow, ex);
            }
        }

        #region Properties

        public String ScreenResolution
        {
            get { return _resolution; }
            set
            {
                if (_resolution != value)
                {
                    _resolution = addRatio(value);
                    OnPropertyChanged();
                }
            }
        }

        public Int16 WindowMode
        {
            get { return _windowMode; }
            set
            {
                if (_windowMode != value)
                {
                    _windowMode = value;
                    OnPropertyChanged();
                }
            }
        }

        public String ActiveMonitor
        {
            get { return _activeMonitor; }
            set
            {
                if (_activeMonitor != value)
                {
                    _activeMonitor = value;
                    OnPropertyChanged();
                }
            }
        }

        public Boolean IsX64
        {
            get { return _isX64; }
            set
            {
                if (_isX64 != value)
                {
                    _isX64 = value;
                    OnPropertyChanged();
                }
            }
        }

        public Boolean IsX64Enabled
        {
            get { return _isX64Enabled; }
            set
            {
                if (_isX64Enabled != value)
                {
                    _isX64Enabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public Boolean IsDebugMode
        {
            get { return _isDebugMode; }
            set
            {
                if (_isDebugMode != value)
                {
                    _isDebugMode = value;
                    OnPropertyChanged();
                }
            }
        }

        public String[] DownloadMirrors
        {
            get { return _downloadMirrors; }
            set
            {
                if (_downloadMirrors != value)
                {
                    _downloadMirrors = value;
                    OnPropertyChanged();
                }
            }
        }

        public Boolean CheckUpdates
        {
            get => _checkUpdates;
            set
            {
                if (_checkUpdates != value)
                {
                    _checkUpdates = value;
                    OnPropertyChanged();
                }
            }
        }

        public Boolean SteamOverlayFix
        {
            get => IsSteamOverlayFixed();
            set
            {
                MessageBoxResult ShowMessage(String message, MessageBoxButton button, MessageBoxImage image)
                {
                    return MessageBox.Show((System.Windows.Window)this.GetRootElement(), message, Lang.SteamOverlay.Caption, button, image);
                }

                if (IsSteamOverlayFixed() == value)
                    return;

                if (value)
                {
                    if (ShowMessage(Lang.SteamOverlay.FixAreYouSure, MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => OnPropertyChanged()), DispatcherPriority.ContextIdle, null);
                        return;
                    }

                    String currentLauncherPath = Process.GetCurrentProcess().MainModule.FileName;

                    Process process = Process.Start(new ProcessStartInfo("Memoria.SteamFix.exe", @$" ""{currentLauncherPath}"" ") { Verb = "runas" });
                    process.WaitForExit();
                }
                else
                {
                    if (ShowMessage(Lang.SteamOverlay.RollbackAreYouSure, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => OnPropertyChanged()), DispatcherPriority.ContextIdle, null);
                        return;
                    }

                    Process process = Process.Start(new ProcessStartInfo("Memoria.SteamFix.exe") { Verb = "runas" });
                    process.WaitForExit();
                }

                Application.Current.Dispatcher.BeginInvoke(new Action(() => OnPropertyChanged()), DispatcherPriority.ContextIdle, null);
            }
        }

        public Boolean AutoRunGame { get; private set; }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private async void OnPropertyChanged([CallerMemberName] String propertyName = null)
        {
            try
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

                IniFile iniFile = new IniFile(IniPath);
                switch (propertyName)
                {
                    case nameof(ScreenResolution):
                        iniFile.WriteValue("Settings", propertyName, ScreenResolution.Split('|')[0].Trim(' ') ?? "1280x960");
                        break;
                    case nameof(ActiveMonitor):
                        iniFile.WriteValue("Settings", propertyName, ActiveMonitor ?? String.Empty);
                        break;
                    case nameof(WindowMode):
                        iniFile.WriteValue("Settings", propertyName, WindowMode.ToString());
                        break;
                    case nameof(IsDebugMode):
                        iniFile.WriteValue("Memoria", propertyName, IsDebugMode.ToString());
                        break;
                    case nameof(IsX64):
                        iniFile.WriteValue("Memoria", propertyName, IsX64.ToString());
                        break;
                    case nameof(CheckUpdates):
                    {
                        iniFile.WriteValue("Memoria", propertyName, CheckUpdates.ToString());
                        if (CheckUpdates)
                        {
                            using (ManualResetEvent evt = new ManualResetEvent(false))
                            {
                                System.Windows.Window root = this.GetRootElement() as System.Windows.Window;
                                if (root != null)
                                    await UiLauncherPlayButton.CheckUpdates(root, evt, this);
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                UiHelper.ShowError(Application.Current.MainWindow, ex);
            }
        }

        #endregion

        public static readonly String IniPath = AppDomain.CurrentDomain.BaseDirectory + "\\Settings.ini";

        private String _resolution = "";
        private String _activeMonitor = "";
        private Int16 _windowMode;
        private Boolean _isX64 = true;
        private Boolean _isX64Enabled = true;
        private Boolean _isDebugMode;
        private Boolean _checkUpdates = true;
        private String[] _downloadMirrors;

        private void LoadSettings()
        {
            try
            {
                IniFile iniFile = new IniFile(IniPath);

                String value = iniFile.ReadValue("Settings", nameof(ScreenResolution)).Split('|')[0].Trim(' ');
                //if res in settings.ini exists AND corresponds to something in the res list
                if ((!String.IsNullOrEmpty(value)) && EnumerateDisplaySettings(false).ToArray().Any(value.Contains))
                    _resolution = addRatio(value);
                //else we choose the largest available one
                else
                    _resolution = EnumerateDisplaySettings(false).OrderByDescending(x => Convert.ToInt32(x.Split('x')[0])).ToArray()[0];

                value = iniFile.ReadValue("Settings", nameof(ActiveMonitor));
                if (!String.IsNullOrEmpty(value))
                    _activeMonitor = value;

                value = iniFile.ReadValue("Settings", nameof(WindowMode));
                if (!String.IsNullOrEmpty(value))
                {
                    if (value == Lang.Settings.Window) value = "0";
                    if (value == Lang.Settings.ExclusiveFullscreen) value = "1";
                    if (value == Lang.Settings.BorderlessFullscreen) value = "2";
                }
                if (!Int16.TryParse(value, out _windowMode))
                    _windowMode = 0;

                value = iniFile.ReadValue("Memoria", nameof(IsX64));
                if (String.IsNullOrEmpty(value))
                    value = "true";
                if (!Boolean.TryParse(value, out _isX64))
                    _isX64 = true;
                if (!Environment.Is64BitOperatingSystem || !Directory.Exists("x64"))
                {
                    _isX64 = false;
                    _isX64Enabled = false;
                }
                else if (!Directory.Exists("x86"))
                {
                    _isX64 = true;
                    _isX64Enabled = false;
                }

                value = iniFile.ReadValue("Memoria", nameof(IsDebugMode));
                if (String.IsNullOrEmpty(value))
                    value = "false";
                if (!Boolean.TryParse(value, out _isDebugMode))
                    _isDebugMode = false;

                value = iniFile.ReadValue("Memoria", nameof(CheckUpdates));
                if (String.IsNullOrEmpty(value))
                    value = "true";
                if (!Boolean.TryParse(value, out _checkUpdates))
                    _checkUpdates = true;

                value = iniFile.ReadValue("Memoria", nameof(AutoRunGame));
                if (String.IsNullOrEmpty(value))
                    value = "false";
                AutoRunGame = App.AutoRunGame || (Boolean.TryParse(value, out var autoRunGame) && autoRunGame);

                value = iniFile.ReadValue("Memoria", nameof(DownloadMirrors));
                if (String.IsNullOrEmpty(value))
                {
                    _downloadMirrors = ["https://github.com/Albeoris/Memoria/releases/latest/download/Memoria.Patcher.exe"];
                    //if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "ru") _downloadMirrors = ["https://ff9.ffrtt.ru/rus/FF9RU.exe", "https://ff9.ffrtt.ru/rus/Memoria.Patcher.exe"];
                }
                else
                {
                    _downloadMirrors = value.Split(',');
                }

                OnPropertyChanged(nameof(ScreenResolution));
                OnPropertyChanged(nameof(ActiveMonitor));
                OnPropertyChanged(nameof(WindowMode));
                OnPropertyChanged(nameof(IsX64));
                OnPropertyChanged(nameof(IsX64Enabled));
                OnPropertyChanged(nameof(IsDebugMode));
                OnPropertyChanged(nameof(CheckUpdates));
                OnPropertyChanged(nameof(DownloadMirrors));
            }
            catch (Exception ex)
            {
                UiHelper.ShowError(Application.Current.MainWindow, ex);
            }
        }

        private Boolean IsSteamOverlayFixed()
        {
            try
            {
                using (RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                {
                    using (var subKey = registryKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\FF9_Launcher.exe"))
                    {
                        if (subKey?.GetValue("Debugger") == null)
                            return false;
                    }
                }

                var bak = new FileInfo("FF9_Launcher.bak");
                var exe = new FileInfo("FF9_Launcher.exe");

                // Patch again if FF9_Launcher.exe was rewrited
                if (bak.Exists && exe.Exists && bak.Length != exe.Length)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll")]
        private static extern Boolean EnumDisplaySettings(String deviceName, Int32 modeNum, ref DevMode devMode);

        private IEnumerable<String> EnumerateDisplaySettings(Boolean displayRatio)
        {
            HashSet<String> set = new HashSet<String>();
            DevMode devMode = new DevMode();
            Int32 modeNum = 0;
            while (EnumDisplaySettings(null, modeNum++, ref devMode))
            {
                if (devMode.dmPelsWidth >= 640 && devMode.dmPelsHeight >= 480)
                {
                    String resolution = $"{devMode.dmPelsWidth.ToString(CultureInfo.InvariantCulture)}x{devMode.dmPelsHeight.ToString(CultureInfo.InvariantCulture)}";

                    if (displayRatio)
                        resolution = addRatio(resolution);

                    if (set.Add(resolution))
                        yield return resolution;
                }
            }
        }

        private String addRatio(String resolution)
        {
            if (!resolution.Contains("|") && resolution.Contains("x"))
            {
                String ratio = "";
                Int32 x = Int32.Parse(resolution.Split('x')[0]);
                Int32 y = Int32.Parse(resolution.Split('x')[1]);

                if ((x / 16) == (y / 9)) ratio = " | 16:9";
                else if ((x / 8) == (y / 5)) ratio = " | 16:10";
                else if ((x / 4) == (y / 3)) ratio = " | 4:3";
                else if ((x / 14) == (y / 9)) ratio = " | 14:9";
                else if ((x / 32) == (y / 9)) ratio = " | 32:9";
                else if ((x / 64) == (y / 27)) ratio = " | 64:27";
                else if ((x / 3) == (y / 2)) ratio = " | 3:2";
                else if ((x / 5) == (y / 4)) ratio = " | 5:4";
                else if ((x / 256) == (y / 135)) ratio = " | 256:135";
                else if ((x / 25) == (y / 16)) ratio = " | 25:16";
                else if ((x) == (y)) ratio = " | 1:1";
                resolution += ratio;
            }
            return resolution;
        }

        private String[] GetAvailableMonitors()
        {
            Screen[] allScreens = Screen.AllScreens;
            Dictionary<Int32, String> friendlyNames = ScreenInterrogatory.GetAllMonitorFriendlyNamesSafe();
            String[] result = new String[allScreens.Length];
            for (Int32 index = 0; index < allScreens.Length; index++)
            {
                Screen screen = allScreens[index];
                StringBuilder sb = new StringBuilder();
                sb.Append(index);
                sb.Append(" - ");

                String name;
                if (!friendlyNames.TryGetValue(index, out name))
                    name = screen.DeviceName;
                sb.Append(name);

                if (screen.Primary)
                    sb.Append(Lang.Settings.PrimaryMonitor);

                result[index] = sb.ToString();

                if (screen.Primary)
                    _activeMonitor = result[index];
            }
            return result;
        }

        private struct DevMode
        {
            private const Int32 CCHDEVICENAME = 32;
            private const Int32 CCHFORMNAME = 32;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public String dmDeviceName;
            public Int16 dmSpecVersion;
            public Int16 dmDriverVersion;
            public Int16 dmSize;
            public Int16 dmDriverExtra;
            public Int32 dmFields;
            public Int32 dmPositionX;
            public Int32 dmPositionY;
            public ScreenOrientation dmDisplayOrientation;
            public Int32 dmDisplayFixedOutput;
            public Int16 dmColor;
            public Int16 dmDuplex;
            public Int16 dmYResolution;
            public Int16 dmTTOption;
            public Int16 dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public String dmFormName;
            public Int16 dmLogPixels;
            public Int32 dmBitsPerPel;
            public Int32 dmPelsWidth;
            public Int32 dmPelsHeight;
            public Int32 dmDisplayFlags;
            public Int32 dmDisplayFrequency;
            public Int32 dmICMMethod;
            public Int32 dmICMIntent;
            public Int32 dmMediaType;
            public Int32 dmDitherType;
            public Int32 dmReserved1;
            public Int32 dmReserved2;
            public Int32 dmPanningWidth;
            public Int32 dmPanningHeight;
        }
    }
}
