// App.xaml.cs
using AtFocusVoice;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace AtFocusVoice
{
    public partial class App : Application
    {
        private NotifyIcon? _notifyIcon;
        private Mutex? _singleInstanceMutex;
        private bool _ownsSingleInstanceMutex;
        public bool _isExit;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _singleInstanceMutex = new Mutex(true, @"Local\AtFocusVoice.SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Shutdown();
                return;
            }
            _ownsSingleInstanceMutex = true;

            CreateMainWindow();
            SetupNotifyIcon();
        }

        private void CreateMainWindow()
        {
            _mainWindow = new MainWindow();
            MainWindow = _mainWindow;
            _mainWindow.InitializeBackend();
        }

        private void SetupNotifyIcon()
        {
            using Stream iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/Icon.ico"))?.Stream
                ?? throw new InvalidOperationException("无法加载内置托盘图标。");

            _notifyIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(iconStream),
                Visible = true,
                Text = "AtFocusVoice"
            };

            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("打开主界面", null, OpenMainWindow);
            _notifyIcon.ContextMenuStrip.Items.Add("退出", null, ExitApplication);

            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        }

        private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowMainWindow();
            }
        }

        private void OpenMainWindow(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                CreateMainWindow();
            }

            try
            {
                if (_mainWindow!.IsVisible)
                {
                    _mainWindow.Activate();
                }
                else
                {
                    _mainWindow.Show();
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Activate();
                }
            }
            catch (ArgumentException)
            {
                _mainWindow?.DisposeBackend();
                CreateMainWindow();
                _mainWindow!.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        }

        private void ExitApplication(object? sender, EventArgs e)
        {
            _isExit = true;
            _mainWindow?.Close();
            _notifyIcon?.Dispose();
            _notifyIcon = null;
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            if (_ownsSingleInstanceMutex)
            {
                _singleInstanceMutex?.ReleaseMutex();
            }
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
