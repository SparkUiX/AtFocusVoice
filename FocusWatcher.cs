using NAudio.CoreAudioApi;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using static AtFocusVoice.MainWindow;

namespace AtFocusVoice
{
    public class FocusWatcher
    {
        private readonly MainWindow _mainWindow;
        private readonly WinEventDelegate _winEventDelegate;
        private readonly DispatcherTimer _timer;
        private IntPtr _hook;

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventTime, uint dwmsEventTime);
        private const int EVENT_SYSTEM_FOREGROUND = 3;
        private const int DelayTime = 0;

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
            _winEventDelegate = WinEventProc;
            _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventDelegate, 0, 0, 0);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DelayTime)
            };
            _timer.Tick += (_, _) =>
            {
                _timer.Stop();
                ApplyVolumesForCurrentForeground();
            };
        }

        public void Stop()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
            }
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventTime, uint dwmsEventTime)
        {
            ApplyVolumes(hwnd);
            _timer.Stop();
            _timer.Start();
        }

        public void ApplyVolumesForCurrentForeground()
        {
            ApplyVolumes(GetForegroundWindow());
        }

        public bool IsProcessForeground(ProcessInfo processInfo)
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            GetWindowThreadProcessId(foregroundWindow, out uint foregroundProcessId);
            return processInfo.ProcessID == foregroundProcessId;
        }

        private void ApplyVolumes(IntPtr foregroundWindow)
        {
            GetWindowThreadProcessId(foregroundWindow, out uint foregroundProcessId);
            Process? foregroundProcess = null;

            try
            {
                foregroundProcess = Process.GetProcessById((int)foregroundProcessId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resolving foreground process: {ex.Message}");
            }

            foreach (var processInfo in _mainWindow.ProcessInfos)
            {
                try
                {
                    float volume = 1.0f;
                    if (processInfo.IsUsed)
                    {
                        bool isForeground = processInfo.ProcessID == foregroundProcessId
                            || foregroundProcess?.ProcessName == processInfo.ProcessName;
                        volume = isForeground ? processInfo.ForegroundVolumeLevel : processInfo.BackgroundVolumeLevel;
                    }

                    SetProcessVolume(processInfo, volume);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error setting volume for process {processInfo.ProcessName}: {ex.Message}");
                }
            }
        }

        public void SetProcessVolume(ProcessInfo processInfo, float volume)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sessions = device.AudioSessionManager.Sessions;

                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (session.GetProcessID == processInfo.ProcessID)
                    {
                        session.SimpleAudioVolume.Volume = Math.Clamp(volume, 0.0f, 1.0f);
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
