using NAudio.CoreAudioApi;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AtFocusVoice
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<ProcessInfo> ProcessInfos { get; } = new();

        private FocusWatcher? _focusWatcher;
        private IniFileHelper? _iniHelper;

        public MainWindow()
        {
            InitializeComponent();
            ProcessListView.ItemsSource = ProcessInfos;
        }

        public void InitializeBackend()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtFocusVoice");
            Directory.CreateDirectory(appDataPath);
            string filePath = Path.Combine(appDataPath, "processConfig.ini");

            _iniHelper = new IniFileHelper(filePath);
            LoadProcesses();

            _focusWatcher = new FocusWatcher(this);
            _focusWatcher.ApplyVolumesForCurrentForeground();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.F5)
            {
                LoadProcesses();
                _focusWatcher?.ApplyVolumesForCurrentForeground();
            }
        }

        private void LoadProcesses()
        {
            if (_iniHelper == null)
            {
                return;
            }

            ProcessInfos.Clear();
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                try
                {
                    string volume = GetProcessVolume(process);
                    Debug.WriteLine($"Process: {process.ProcessName}, Volume: {volume}");

                    if (volume == "N/A")
                    {
                        continue;
                    }

                    var processInfo = new ProcessInfo(_iniHelper, ApplyConfiguredVolumeForProcess)
                    {
                        ProcessName = process.ProcessName,
                        Volume = volume,
                        ProcessID = process.Id,
                        IsUsed = ReadBool("Processes", process.ProcessName, false),
                        ForegroundVolumePercent = ReadPercent("ForegroundVolumes", process.ProcessName, 100),
                        BackgroundVolumePercent = ReadPercent("BackgroundVolumes", process.ProcessName, 0)
                    };

                    try
                    {
                        processInfo.Icon = GetProcessIcon(process);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting icon for process {process.ProcessName}: {ex.Message}");
                    }

                    try
                    {
                        processInfo.FileName = process.MainModule?.FileName ?? string.Empty;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error accessing file name for process {process.ProcessName}: {ex.Message}");
                    }

                    ProcessInfos.Add(processInfo);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing process {process.ProcessName}: {ex.Message}");
                }
            }

            ProcessListView.Items.Refresh();
        }

        private bool ReadBool(string section, string key, bool defaultValue)
        {
            string value = _iniHelper?.ReadValue(section, key, defaultValue.ToString()) ?? defaultValue.ToString();
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        private double ReadPercent(string section, string key, double defaultValue)
        {
            string value = _iniHelper?.ReadValue(section, key, defaultValue.ToString("0")) ?? defaultValue.ToString("0");
            return double.TryParse(value, out double result) ? Math.Clamp(result, 0, 100) : defaultValue;
        }

        private ImageSource? GetProcessIcon(Process process)
        {
            try
            {
                string? filePath = process.MainModule?.FileName;
                if (filePath == null)
                {
                    return null;
                }

                using Icon? icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
                if (icon != null)
                {
                    using MemoryStream iconStream = new();
                    using Bitmap bitmap = icon.ToBitmap();
                    bitmap.Save(iconStream, System.Drawing.Imaging.ImageFormat.Png);
                    iconStream.Seek(0, SeekOrigin.Begin);

                    BitmapImage bitmapImage = new();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = iconStream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();

                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting icon for process {process.ProcessName}: {ex.Message}");
            }

            return null;
        }

        private string GetProcessVolume(Process process)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sessions = device.AudioSessionManager.Sessions;

                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (session.GetProcessID == process.Id)
                    {
                        return (session.SimpleAudioVolume.Volume * 100).ToString("0.00") + "%";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error accessing process volume: {ex.Message}");
            }

            return "N/A";
        }

        private void ApplyConfiguredVolumeForProcess(ProcessInfo processInfo)
        {
            if (_focusWatcher == null)
            {
                return;
            }

            float volume = 1.0f;
            if (processInfo.IsUsed)
            {
                volume = _focusWatcher.IsProcessForeground(processInfo)
                    ? processInfo.ForegroundVolumeLevel
                    : processInfo.BackgroundVolumeLevel;
            }

            _focusWatcher.SetProcessVolume(processInfo, volume);
        }

        protected override void OnClosed(EventArgs e)
        {
            DisposeBackend();
            base.OnClosed(e);
        }

        public void DisposeBackend()
        {
            _focusWatcher?.Stop();
            _focusWatcher = null;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (Application.Current is App app && !app._isExit)
            {
                e.Cancel = true;
                Hide();
            }
        }

        public class ProcessInfo : INotifyPropertyChanged
        {
            private readonly IniFileHelper _iniHelper;
            private readonly Action<ProcessInfo> _applyVolume;
            private bool _isUsed;
            private double _foregroundVolumePercent = 100;
            private double _backgroundVolumePercent;

            public ProcessInfo(IniFileHelper iniHelper, Action<ProcessInfo> applyVolume)
            {
                _iniHelper = iniHelper;
                _applyVolume = applyVolume;
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public ImageSource? Icon { get; set; }
            public string ProcessName { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string Volume { get; set; } = string.Empty;
            public int ProcessID { get; set; }

            public bool IsUsed
            {
                get => _isUsed;
                set
                {
                    if (_isUsed == value)
                    {
                        return;
                    }

                    _isUsed = value;
                    _iniHelper.WriteValue("Processes", ProcessName, _isUsed.ToString());
                    OnPropertyChanged(nameof(IsUsed));
                    _applyVolume(this);
                }
            }

            public double ForegroundVolumePercent
            {
                get => _foregroundVolumePercent;
                set
                {
                    double clampedValue = Math.Clamp(Math.Round(value), 0, 100);
                    if (Math.Abs(_foregroundVolumePercent - clampedValue) < 0.001)
                    {
                        return;
                    }

                    _foregroundVolumePercent = clampedValue;
                    _iniHelper.WriteValue("ForegroundVolumes", ProcessName, _foregroundVolumePercent.ToString("0"));
                    OnPropertyChanged(nameof(ForegroundVolumePercent));
                    OnPropertyChanged(nameof(ForegroundVolumeText));
                    _applyVolume(this);
                }
            }

            public double BackgroundVolumePercent
            {
                get => _backgroundVolumePercent;
                set
                {
                    double clampedValue = Math.Clamp(Math.Round(value), 0, 100);
                    if (Math.Abs(_backgroundVolumePercent - clampedValue) < 0.001)
                    {
                        return;
                    }

                    _backgroundVolumePercent = clampedValue;
                    _iniHelper.WriteValue("BackgroundVolumes", ProcessName, _backgroundVolumePercent.ToString("0"));
                    OnPropertyChanged(nameof(BackgroundVolumePercent));
                    OnPropertyChanged(nameof(BackgroundVolumeText));
                    _applyVolume(this);
                }
            }

            public string ForegroundVolumeText => $"{ForegroundVolumePercent:0}%";
            public string BackgroundVolumeText => $"{BackgroundVolumePercent:0}%";
            public float ForegroundVolumeLevel => (float)(ForegroundVolumePercent / 100.0);
            public float BackgroundVolumeLevel => (float)(BackgroundVolumePercent / 100.0);

            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
