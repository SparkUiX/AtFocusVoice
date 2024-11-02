using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Collections.Generic;
using NAudio.CoreAudioApi;
using System.Drawing;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AtFoucsVoice
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<ProcessInfo> _processInfos;
        private FocusWatcher _focusWatcher;
        private IniFileHelper _iniHelper;

        public MainWindow()
        {
            //string filePath = "processConfig.ini";
            InitializeComponent();
            string appDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtFoucsVoice");
            Directory.CreateDirectory(appDataPath); // 确保目录存在
            string filePath = System.IO.Path.Combine(appDataPath, "processConfig.ini");
            _iniHelper = new IniFileHelper(filePath);

            // 初始化 ObservableCollection 并绑定到 ListView
            _processInfos = new ObservableCollection<ProcessInfo>();
            ProcessListView.ItemsSource = _processInfos;
            LoadProcesses();
            // 初始化并启动 FocusWatcher
            _focusWatcher = new FocusWatcher(this);
            //_focusWatcher.Start();
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.F5)
            {
                LoadProcesses(); // 刷新进程列表
            }
        }
        private void LoadProcesses()
        {
            _processInfos.Clear();
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                try
                {
                    // 首先获取该进程的音量
                    string volume = GetProcessVolume(process);
                    Debug.WriteLine($"Process: {process.ProcessName}, Volume: {volume}");

                    // 只有当音量不是 "N/A" 时才添加到列表中
                    if (volume != "N/A")
                    {
                        bool isUsed = bool.Parse(_iniHelper.ReadValue("Processes", process.ProcessName, "false"));

                        // 创建一个新的 ProcessInfo 对象
                        var processInfo = new ProcessInfo(_iniHelper)
                        {
                            ProcessName = process.ProcessName,
                            Volume = volume,
                            ProcessID = process.Id,
                            IsUsed = isUsed
                        };

                        // 分开获取其他属性，避免访问失败
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
                            processInfo.FileName = process.MainModule?.FileName;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error accessing file name for process {process.ProcessName}: {ex.Message}");
                        }

                        // 无论是否成功获取所有属性，始终添加到列表
                        _processInfos.Add(processInfo);

                        // 调试时使用 MessageBox 查看信息
                        // MessageBox.Show($"正在加载进程:\n名称: {process.ProcessName}\n音量: {volume}\n是否启用: {isUsed}\n进程ID: {process.Id}", "进程加载提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing process {process.ProcessName}: {ex.Message}");
                }
            }

            ProcessListView.ItemsSource = _processInfos;
            ProcessListView.Items.Refresh();
        }



        private ImageSource GetProcessIcon(Process process)
        {
            try
            {
                // 获取进程的主模块文件路径
                string filePath = process.MainModule?.FileName;
                if (filePath == null)
                    return null;

                // 从文件路径中提取图标
                Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
              
                if (icon != null)
                {
                    // 转换 Icon 为 ImageSource
                    using (MemoryStream iconStream = new MemoryStream())
                    {
                        icon.ToBitmap().Save(iconStream, System.Drawing.Imaging.ImageFormat.Png);
                        iconStream.Seek(0, SeekOrigin.Begin);

                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = iconStream;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();

                        return bitmapImage;
                    }
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
                // 获取默认音频渲染设备
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                // 获取此设备的音频会话管理器
                var sessionManager = device.AudioSessionManager;
                var sessions = sessionManager.Sessions;

                // 使用索引器遍历音频会话
                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];

                    // 检查音频会话的进程 ID 是否与指定进程相符
                    if (session.GetProcessID == process.Id)
                    {
                        // 获取音量，转换为百分比格式
                        return (session.SimpleAudioVolume.Volume * 100).ToString("0.00") + "%";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error accessing process volume: {ex.Message}");
            }

            return "N/A"; // 如果未找到进程的音量会话，返回 "N/A"
        }
        public class ProcessInfo : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private bool _isUsed;
            private readonly IniFileHelper _iniHelper;


            public ProcessInfo(IniFileHelper iniHelper)
            {
                _iniHelper = iniHelper;
            }

            public ImageSource Icon { get; set; }
            public string ProcessName { get; set; }
            public string FileName { get; set; }
            public string Volume { get; set; }
            public int ProcessID { get; set; }
            public bool IsUsed
            {
                get
                {
                    Console.WriteLine("IsUsed我确实读取了:::: " + ProcessName + " " + _isUsed);
                    return _isUsed;
                }//=> _isUsed;
                set
                {
                    Debug.WriteLine("IsUsed我确实设置了:::: " + ProcessName + " " + value);
                    if (_isUsed != value)
                    {
                        _isUsed = value;
                        OnPropertyChanged(nameof(IsUsed));
                        Debug.WriteLine("IsUsed我确实变化了:::: " + ProcessName + " " + _isUsed);

                        // 在 IsUsed 变化时写入 ini 文件
                       
                        _iniHelper.WriteValue("Processes", ProcessName, _isUsed.ToString());
                        // 获取 MainWindow 实例，并调整音量
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        if (mainWindow != null)
                        {
                            float volumeLevel = _isUsed ? 0.0f : 1.0f; // 启用为0%，禁用为100%
                            mainWindow.AdjustProcessVolume(ProcessID, volumeLevel);
                        }
                    }
                }
            }

            
            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            _focusWatcher.Stop();
            base.OnClosed(e);
        }
        public void AdjustProcessVolume(int processId, float volumeLevel)
        {
            try
            {
                // 获取默认音频渲染设备
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                // 获取音频会话管理器
                var sessionManager = device.AudioSessionManager;
                var sessions = sessionManager.Sessions;

                // 遍历音频会话，找到匹配进程 ID 的会话并设置音量
                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];

                    // 检查会话的进程 ID 是否与指定的进程 ID 相同
                    if (session.GetProcessID == processId)
                    {
                        session.SimpleAudioVolume.Volume = volumeLevel; // 设置音量
                        Debug.WriteLine($"Set volume for process ID {processId} to {volumeLevel * 100}%");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting volume for process ID {processId}: {ex.Message}");
            }
        }
    }
}
    