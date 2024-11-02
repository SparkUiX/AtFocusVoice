using NAudio.CoreAudioApi;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using static AtFoucsVoice.MainWindow;

namespace AtFoucsVoice
{
    public class FocusWatcher
    {
        private MainWindow _mainWindow; // 引用主窗口，用于获取启用静音功能的进程列表
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventTime, uint dwmsEventTime);
        private const int EVENT_SYSTEM_FOREGROUND = 3;

        private WinEventDelegate _winEventDelegate;
        private IntPtr _hook;
        private DispatcherTimer _timer; // 用于延迟判断
        private const int DelayTime = 0; // 延迟200毫秒
        private IniFileHelper _iniHelper; // 用于读取INI配置

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint dwProcessId, uint dwThreadId, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public FocusWatcher(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _winEventDelegate = new WinEventDelegate(WinEventProc);
            _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventDelegate, 0, 0, 0);

            // 初始化定时器
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DelayTime)
            };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                RecheckVolume(); // 延迟后重新检查音量
            };

            // 初始化配置读取
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtFoucsVoice");
            Directory.CreateDirectory(appDataPath); // 确保目录存在
            string filePath = Path.Combine(appDataPath, "processConfig.ini");
            _iniHelper = new IniFileHelper(filePath);
        }

        public void Stop()
        {
            UnhookWinEvent(_hook);
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventTime, uint dwmsEventTime)
        {
            GetWindowThreadProcessId(hwnd, out uint foregroundProcessId);
            Process process = Process.GetProcessById((int)foregroundProcessId);

            if (process == null) return;

            foreach (var processInfo in _mainWindow._processInfos)
            {
                try
                {
                    // 判断当前前台窗口是否为该进程，并根据INI配置文件中的启用状态调整音量
                    bool isProcessEnabled = bool.TryParse(_iniHelper.ReadValue("Processes", processInfo.ProcessName), out bool enabled) && enabled;

                    if (processInfo.ProcessName == process.ProcessName && isProcessEnabled)
                    {
                        SetProcessVolume(processInfo, 1.0f); // 启用音量
                    }
                    else if(processInfo.ProcessName != process.ProcessName && isProcessEnabled)
                    {
                        SetProcessVolume(processInfo, 0.0f); // 静音
                    }
                    else
                    {
                        SetProcessVolume(processInfo, 1.0f); // 启用音量
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error setting volume for process {processInfo.ProcessName}: {ex.Message}");
                }
            }

            _timer.Stop();
            _timer.Start();
        }

        private void RecheckVolume()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            GetWindowThreadProcessId(foregroundWindow, out uint foregroundProcessId);
            Process currentProcess = Process.GetProcessById((int)foregroundProcessId);

            if (currentProcess == null) return;

            foreach (var processInfo in _mainWindow._processInfos)
            {
                try
                {
                    // 判断当前前台窗口是否为该进程，并根据INI配置文件中的启用状态调整音量
                    bool isProcessEnabled = bool.TryParse(_iniHelper.ReadValue("Processes", processInfo.ProcessName), out bool enabled) && enabled;

                    if (processInfo.ProcessName == currentProcess.ProcessName && isProcessEnabled)
                    {
                        SetProcessVolume(processInfo, 1.0f); // 启用音量
                    }
                    else if(processInfo.ProcessName != currentProcess.ProcessName && isProcessEnabled)
                    {
                        SetProcessVolume(processInfo, 0.0f); // 静音
                    }
                    else
                    {
                        SetProcessVolume(processInfo, 1.0f); // 启用音量
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error rechecking volume for process {processInfo.ProcessName}: {ex.Message}");
                }
            }
        }

        public void SetProcessVolume(ProcessInfo processInfo, float volume)
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sessionManager = device.AudioSessionManager;
                var sessions = sessionManager.Sessions;

                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (session.GetProcessID == processInfo.ProcessID)
                    {
                        session.SimpleAudioVolume.Volume = volume; // 设置音量
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting volume for {processInfo.ProcessName}: {ex.Message}");
            }
        }

    }
}
