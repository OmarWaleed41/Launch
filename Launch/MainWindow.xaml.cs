using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows.Shapes;
using Path = System.IO.Path;
using System.Collections;
using System.Xml.Linq;
using static System.Formats.Asn1.AsnWriter;
using Microsoft.Win32;

namespace Launch
{
    public partial class MainWindow : Window
    {
        #region Win32 API Imports
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint WM_SPAWN_WORKER = 0x052C;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        #endregion

        #region Constants
        private const double DragThreshold = 1;
        private const double CanvasPadding = 30;
        private const int ImageCornerRadius = 8;
        private const double TaskbarHeight = 50;

        private const double FolderExpandedX = 3.6;
        private const double FolderExpandedY = 3.6;
        #endregion

        #region Fields - Paths
        private readonly string _baseDirectory;
        private readonly string _mainFolder;
        private readonly string _settingsPath;
        private readonly string _jsonFilePath;
        private readonly string _widgetPath;
        private readonly string _widgetsFolder;
        private readonly string _imgPath;
        private readonly string _folderPath;
        #endregion

        #region Fields - State
        private double _buttonSize;
        private bool _snapToGrid;
        private bool _showGrid;
        private double _gridSizeX;
        private double _gridSizeY;

        private bool _isDragging;
        private bool _isColliding;
        private Point _clickPosition;
        private Point _senderPosition;
        private UIElement _draggedElement;

        private Dictionary<string, Folder> _folders = new Dictionary<string, Folder>();

        private static CoreWebView2Environment _sharedEnvironment;
        #endregion

        #region Constructor and Initialization
        public MainWindow()
        {
            // TEMPORARY: Comment out DPI awareness to test
            // SetProcessDPIAware();

            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _mainFolder = Path.Combine(_baseDirectory, "src");
            _settingsPath = Path.Combine(_mainFolder, "settings.json");
            _jsonFilePath = Path.Combine(_mainFolder, "path.json");
            _widgetPath = Path.Combine(_mainFolder, "widgets.json");
            _widgetsFolder = Path.Combine(_mainFolder, "Widgets");
            _imgPath = Path.Combine(_mainFolder, "imgs");
            _folderPath = Path.Combine(_mainFolder, "folders.json");

            Properties.Settings.Default.PropertyChanged += OnSettingsChanged;

            InitializeComponent();

            // Ensure canvas doesn't clip content
            MainCanvas.ClipToBounds = false;
            GridCanvas.ClipToBounds = false;

            // Set render options for better quality across DPI
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

            LoadSettings();
            LoadApplications();
            LoadWidgets();
            LoadFolders();
            ConfigureWindowSize();


            Loaded += OnWindowLoaded;
            SizeChanged += OnWindowSizeChanged;
        }

        private void ConfigureWindowSize()
        {
            // enumerate all monitors
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                {
                    MONITORINFO mi = new MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(mi);

                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        var bounds = mi.rcMonitor;
                        int width = bounds.Right - bounds.Left;
                        int height = bounds.Bottom - bounds.Top;
                        Debug.WriteLine($"Monitor: Left={bounds.Left}, Top={bounds.Top}, Right={bounds.Right}, Bottom={bounds.Bottom} (Size: {width}x{height})");

                        minX = Math.Min(minX, bounds.Left);
                        minY = Math.Min(minY, bounds.Top);
                        maxX = Math.Max(maxX, bounds.Right);
                        maxY = Math.Max(maxY, bounds.Bottom);

                        Debug.WriteLine($"  Current bounds - minX={minX}, minY={minY}, maxX={maxX}, maxY={maxY}");
                    }

                    return true;
                }, IntPtr.Zero);

            double totalWidth = maxX - minX;
            double totalHeight = maxY - minY;

            Debug.WriteLine($"Final calculated bounds - Left: {minX}, Top: {minY}, Width: {totalWidth}, Height: {totalHeight}");
            Debug.WriteLine($"  This covers from ({minX}, {minY}) to ({maxX}, {maxY})");

            // Get DPI information
            var source = PresentationSource.FromVisual(this);
            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;

            if (source != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                Debug.WriteLine($"DPI Scale - X: {dpiScaleX}, Y: {dpiScaleY}");
            }

            // Convert from physical pixels to WPF units
            Left = minX / dpiScaleX;
            Top = minY / dpiScaleY;
            Width = totalWidth / dpiScaleX;
            Height = totalHeight / dpiScaleY;

            Debug.WriteLine($"Window positioned at: ({Left}, {Top}) with size: {Width}x{Height}");

            MainCanvas.Margin = new Thickness(0);
            GridCanvas.Margin = new Thickness(0);

            // Ensure canvas covers the entire area
            MainCanvas.Width = Width;
            MainCanvas.Height = Height;
            GridCanvas.Width = Width;
            GridCanvas.Height = Height;

            Debug.WriteLine($"Canvas size set to: {MainCanvas.Width}x{MainCanvas.Height}");

            // Force layout update
            UpdateLayout();
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Recalculate if window size changes (e.g., monitor config changes)
            Debug.WriteLine($"Window size changed: {e.NewSize.Width}x{e.NewSize.Height}");
            Debug.WriteLine($"Window actual position: Left={Left}, Top={Top}");
            Debug.WriteLine($"Canvas ActualWidth: {MainCanvas.ActualWidth}, ActualHeight: {MainCanvas.ActualHeight}");

            MainCanvas.Width = e.NewSize.Width;
            MainCanvas.Height = e.NewSize.Height;
            GridCanvas.Width = e.NewSize.Width;
            GridCanvas.Height = e.NewSize.Height;
            ConfigureWindowSize();
            UpdateLayout();
        }

        private void LoadSettings()
        {
            _buttonSize = Properties.Settings.Default.ButtonSize;
            _snapToGrid = Properties.Settings.Default.SnapToGrid;
            _gridSizeX = Properties.Settings.Default.GridSizeX;
            _gridSizeY = Properties.Settings.Default.GridSizeY;
            _showGrid = Properties.Settings.Default.ShowGrid;
        }
        #endregion

        #region Application Loading
        private void LoadApplications()
        {
            if (!File.Exists(_jsonFilePath))
            {
                MessageBox.Show("JSON file not found, Creating File.");
                File.Create(_jsonFilePath).Close();
                return;
            }

            try
            {
                var json = File.ReadAllText(_jsonFilePath);
                var apps = JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (apps == null) return;

                foreach (var app in apps)
                {
                    Debug.WriteLine($"Loading app: {app.Key}");
                    CreateAppButton(app.Key, app.Value.Path, new Point(app.Value.Position.X, app.Value.Position.Y), app.Value.ParentFolderId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading JSON: {ex.Message}");
            }
        }

        private void SaveApplicationPositions()
        {
            try
            {
                var json = File.ReadAllText(_jsonFilePath);
                var apps = JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (apps == null) return;

                foreach (UIElement element in MainCanvas.Children)
                {
                    if (element is Button button && button.Tag is ElementTag tag)
                    {
                        string path = tag.Path;
                        string parentFolderId = tag.ParentFolderId;

                        var app = apps.FirstOrDefault(a => a.Value.Path == path);

                        if (!app.Equals(default(KeyValuePair<string, AppInfo>)))
                        {
                            var (x, y) = GetElementPosition(button);
                            app.Value.Position.X = x;
                            app.Value.Position.Y = y;

                            app.Value.ParentFolderId = parentFolderId;
                        }
                    }
                }

                var updatedJson = JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_jsonFilePath, updatedJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error writing JSON: {ex.Message}");
            }
        }

        private (double X, double Y) GetElementPosition(UIElement element)
        {
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);

            // Handle NaN values
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            if (_snapToGrid)
            {
                left = Math.Round(left / _gridSizeX) * _gridSizeX;
                top = Math.Round(top / _gridSizeY) * _gridSizeY;
            }

            return (left, top);
        }
        #endregion

        #region Widget Loading
        private void LoadWidgets()
        {
            if (!File.Exists(_widgetPath))
            {
                MessageBox.Show("Widgets JSON file not found, Creating File.");
                File.Create(_widgetPath).Close();
                return;
            }

            try
            {
                var json = File.ReadAllText(_widgetPath);
                var widgets = JsonSerializer.Deserialize<Dictionary<string, Widget>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (widgets == null) return;

                foreach (var widget in widgets)
                {
                    if (widget.Value.Status)
                    {
                        InitWebView(widget.Value, widget.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading Widgets JSON: {ex.Message}");
            }
        }
        public async void InitWebView(Widget widget, string widgetName)
        {
            Debug.WriteLine($"Initializing widget: {widgetName}");

            var webView = CreateWebView(widget);
            var container = CreateWidgetContainer(widget, widgetName, webView);

            MainCanvas.Children.Add(container);
            Canvas.SetLeft(container, widget.Position.X);
            Canvas.SetTop(container, widget.Position.Y);

            await ConfigureWebView(webView, widgetName, container);
        }

        private WebView2 CreateWebView(Widget widget)
        {
            return new WebView2
            {
                Width = widget.Size.Width,
                Height = widget.Size.Height,
                DefaultBackgroundColor = System.Drawing.Color.Transparent,
                IsHitTestVisible = false
            };
        }

        private Border CreateWidgetContainer(Widget widget, string widgetName, WebView2 webView)
        {
            return new Border
            {
                Width = widget.Size.Width,
                Height = widget.Size.Height,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(4),
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                Cursor = Cursors.Hand,
                Child = webView,
                Tag = new { ChildType = "widget", Name = widgetName }
            };
        }

        private async Task ConfigureWebView(WebView2 webView, string widgetName, Border container)
        {
            try
            {
                await webView.EnsureCoreWebView2Async(_sharedEnvironment);

                ConfigureWebViewSettings(webView);
                SetupVirtualHostMapping(webView);
                NavigateToWidget(webView, widgetName);
                AttachWebMessageHandler(webView, container);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebView2 initialization failed for {widgetName}: {ex.Message}");
            }
        }

        private void ConfigureWebViewSettings(WebView2 webView)
        {
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
        }

        private void SetupVirtualHostMapping(WebView2 webView)
        {
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app", _mainFolder, CoreWebView2HostResourceAccessKind.Allow);
        }

        private void NavigateToWidget(WebView2 webView, string widgetName)
        {
            webView.CoreWebView2.Navigate($"http://app/Widgets/{widgetName}/{widgetName}.html");
        }

        private void AttachWebMessageHandler(WebView2 webView, Border container)
        {
            webView.WebMessageReceived += (s, e) => HandleWebMessage(e, container);
        }

        private void HandleWebMessage(CoreWebView2WebMessageReceivedEventArgs e, Border container)
        {
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                var messageType = message.GetProperty("type").GetString();

                switch (messageType)
                {
                    case "drag":
                        HandleWidgetDrag(message, container);
                        SaveWidgetPositions();
                        break;
                    case "drag_done":
                        if (_snapToGrid)
                        {
                            SnapElementToGrid(container);
                            SaveWidgetPositions();
                        }
                        break;
                    case "openBrowser":
                        HandleOpenBrowser(message);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing message from widget: {ex.Message}");
            }
        }

        private void HandleWidgetDrag(JsonElement message, Border container)
        {
            var dx = message.GetProperty("dx").GetInt32();
            var dy = message.GetProperty("dy").GetInt32();

            double left = Canvas.GetLeft(container) + dx;
            double top = Canvas.GetTop(container) + dy;

            Canvas.SetLeft(container, left);
            Canvas.SetTop(container, top);
        }

        private void HandleOpenBrowser(JsonElement message)
        {
            string url = message.GetProperty("url").GetString();
            LaunchProcess(url, "");
        }

        private void SaveWidgetPositions()
        {
            try
            {
                var json = File.ReadAllText(_widgetPath);
                var widgets = JsonSerializer.Deserialize<Dictionary<string, Widget>>(json);

                if (widgets == null) return;

                foreach (UIElement element in MainCanvas.Children)
                {
                    if (element is Border container && container.Child is WebView2 && container.Tag != null)
                    {
                        dynamic tag = container.Tag;
                        string widgetName = tag.Name;

                        if (widgets.ContainsKey(widgetName))
                        {
                            double left = Canvas.GetLeft(container);
                            double top = Canvas.GetTop(container);

                            // Handle NaN values
                            if (double.IsNaN(left)) left = 0;
                            if (double.IsNaN(top)) top = 0;

                            widgets[widgetName].Position.X = left;
                            widgets[widgetName].Position.Y = top;

                            Debug.WriteLine($"Saved widget {widgetName} at ({left}, {top})");
                        }
                    }
                }

                var updatedJson = JsonSerializer.Serialize(widgets, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_widgetPath, updatedJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating widget positions: {ex.Message}");
            }
        }

        private KeyValuePair<string, Widget>? FindWidgetBySize(Dictionary<string, Widget> widgets, Border container)
        {
            return widgets.FirstOrDefault(w =>
                Math.Abs(w.Value.Size.Width - container.Width) < 0.1 &&
                Math.Abs(w.Value.Size.Height - container.Height) < 0.1);
        }
        #endregion
        private void LoadFolders()
        {
            if (!File.Exists(_folderPath))
            {
                MessageBox.Show("Folders JSON file not found, Creating File.");
                File.Create(_folderPath).Close();
                return;
            }
            try
            {
                var json = File.ReadAllText(_folderPath);
                _folders = JsonSerializer.Deserialize<Dictionary<string, Folder>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                //MessageBox.Show($"Loading Folders: {json}");

                foreach (var folder in _folders.Values)
                {
                    if (folder.ContainedApps is not null && folder.ContainedApps.Count > 1)
                    {
                        CreateFolder(folder.ID, folder.Position);

                        foreach (var appName in folder.ContainedApps)
                        {
                            HandleAppInFolder(appName, folder);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading folders: {ex.Message}");
            }
        }
        private void SaveFolders()
        {
            try
            {
                var json = JsonSerializer.Serialize(_folders,
                    new JsonSerializerOptions { WriteIndented = true });

                foreach (var folder in _folders)
                {
                    if (folder.Value.ContainedApps.Count < 2)
                    {
                        _folders.Remove(folder.Value.ID);
                        UIElement gridToRemove = null;
                        foreach (UIElement element in MainCanvas.Children)
                        {
                            if (element is Grid grid && grid.Tag is FolderTag folderTag && folderTag.FolderId == folder.Value.ID)
                            {
                                gridToRemove = element;
                                break;
                            }
                        }

                        if (gridToRemove != null)
                        {
                            MainCanvas.Children.Remove(gridToRemove);
                        }
                    }
                }

                File.WriteAllText(_folderPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving folders: {ex.Message}");
            }
        }
        private void HandleAppInFolder(string appName, Folder folder)
        {
            int indx = 1;
            foreach (UIElement element in MainCanvas.Children)
            {
                if (element is Button button && button.Tag is ElementTag tag)
                {
                    if (tag.Name == appName)
                    {
                        if (indx < 5)
                        {
                            button.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            button.Visibility = Visibility.Collapsed;
                        }
                        button.Width = 30;
                        Canvas.SetLeft(button, folder.Position.X + (10 * indx));
                        Canvas.SetTop(button, folder.Position.Y);
                        break;
                    }
                }
                indx++;
            }
        }
        #region App Button Creation
        public void CreateAppButton(string appName, string appPath, Point position, string parentFolderId)
        {
            var button = CreateButton(appName, appPath, parentFolderId);
            var image = LoadAppImage(appName);
            var contentPanel = CreateButtonContent(image);

            button.Content = contentPanel;
            ApplyButtonTemplate(button);
            PositionButton(button, position);
            AttachButtonEventHandlers(button);
            ApplyButtonAnimations(button);

            MainCanvas.Children.Add(button);

            Panel.SetZIndex(button, 2);
        }

        private Button CreateButton(string appName, string appPath, string parentFolderId)
        {
            return new Button
            {
                Content = appName,
                Width = _buttonSize,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                BorderBrush = Brushes.Transparent,
                Padding = new Thickness(0),
                FocusVisualStyle = null,
                Tag = new ElementTag
                { 
                    ChildType = "button",
                    Name = appName,
                    Path = appPath,
                    ParentFolderId = parentFolderId
                }
            };
        }

        private Image LoadAppImage(string appName)
        {
            var image = new Image
            {
                Stretch = Stretch.Uniform
            };

            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            try
            {
                var imagePath = Path.Combine(_imgPath, $"{appName}.png");
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                image.Source = bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load image for {appName}: {ex.Message}");
            }

            ApplyRoundedCorners(image);
            return image;
        }

        private void ApplyRoundedCorners(Image image)
        {
            void UpdateClip()
            {
                image.Clip = new RectangleGeometry(
                    new Rect(0, 0, image.ActualWidth, image.ActualHeight),
                    ImageCornerRadius, ImageCornerRadius);
            }

            image.Loaded += (s, e) => UpdateClip();
            image.SizeChanged += (s, e) => UpdateClip();
        }

        private StackPanel CreateButtonContent(Image image)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(image);
            return panel;
        }

        private void ApplyButtonTemplate(Button button)
        {
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetBinding(Border.BackgroundProperty,
                new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });

            var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentPresenterFactory);

            button.Template = new ControlTemplate(typeof(Button))
            {
                VisualTree = borderFactory
            };
            button.OverridesDefaultStyle = true;
        }

        private void PositionButton(Button button, Point position)
        {
            Canvas.SetLeft(button, position.X);
            Canvas.SetTop(button, position.Y);
        }

        private void AttachButtonEventHandlers(Button button)
        {
            button.PreviewMouseLeftButtonDown += OnButtonMouseLeftButtonDown;
            button.PreviewMouseMove += OnButtonMouseMove;
            button.PreviewMouseLeftButtonUp += OnButtonMouseLeftButtonUp;
            button.MouseRightButtonDown += OnButtonRightClick;
        }

        private void ApplyButtonAnimations(Button button)
        {
            button.RenderTransformOrigin = new Point(0.5, 0.5);
            button.RenderTransform = new ScaleTransform(1, 1);

            button.MouseEnter += (s, e) => AnimateButtonScale(button, 1.1);
            button.MouseLeave += (s, e) => AnimateButtonScale(button, 1.0);
        }

        private void AnimateButtonScale(Button button, double scale)
        {
            var animation = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(100));
            var transform = (ScaleTransform)button.RenderTransform;
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
        #endregion

        #region Drag and Drop
        private void OnButtonMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _draggedElement = sender as UIElement;
            _clickPosition = e.GetPosition(MainCanvas);
            _senderPosition = new Point(Canvas.GetLeft((Button)sender), Canvas.GetTop((Button)sender));
            _isDragging = false;
            _draggedElement?.CaptureMouse();

            Panel.SetZIndex((Button)sender, 3);

            ClearButtonAnimations(_draggedElement as Button);
        }

        private void OnButtonMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedElement == null || !_draggedElement.IsMouseCaptured)
                return;

            _isColliding = false;

            Point currentPosition = e.GetPosition(MainCanvas);
            Vector diff = currentPosition - _clickPosition;

            if (!_isDragging && (Math.Abs(diff.X) > DragThreshold || Math.Abs(diff.Y) > DragThreshold))
            {
                _isDragging = true;
                //Debug.WriteLine("Started dragging");
            }

            if (_isDragging)
            {
                double oldLeft = Canvas.GetLeft(_draggedElement);
                double oldTop = Canvas.GetTop(_draggedElement);
                if (double.IsNaN(oldLeft)) oldLeft = 0;
                if (double.IsNaN(oldTop)) oldTop = 0;

                MoveElement(_draggedElement, diff);
                _clickPosition = currentPosition;

                double newLeft = Canvas.GetLeft(_draggedElement);
                double newTop = Canvas.GetTop(_draggedElement);

                var elements = MainCanvas.Children.OfType<FrameworkElement>();
                CollisionResult hit = CheckActiveCollision((FrameworkElement)sender, elements);


                ClearCollisionVisuals(elements);

                if (hit != null)
                {
                    _isColliding = true;

                    if(hit.Type == CollisionType.OuterHit)
                    {
                        MarkCollidingOuter((Button)sender, hit.Target);
                    }
                    else if (hit.Type == CollisionType.InnerHit)
                    {
                        MarkCollidingInner((Button)sender, hit.Target);
                    }
                }

                // Force immediate render to reduce glitching
                _draggedElement.InvalidateVisual();

                //Debug.WriteLine(_isColliding);
            }
        }
        private void OnButtonMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedElement == null || !_draggedElement.IsMouseCaptured)
                return;

            _draggedElement.ReleaseMouseCapture();

            if (!_isDragging)
            {
                LaunchApplication(sender as Button);
            }
            // change this logic entirly in case user is not in snap mode to be able to add the button to a folder
            else
            {
                if (sender is Button button && button.Tag is ElementTag tag && !string.IsNullOrEmpty(tag.ParentFolderId))
                {
                    // Check if button is still inside the folder
                    if (!IsButtonInsideFolder(button, tag.ParentFolderId))
                    {
                        RemoveAppFromFolder(button, tag.Name, tag.ParentFolderId);
                    }
                }

                if (_isColliding)
                {
                    var elements = MainCanvas.Children.OfType<FrameworkElement>();
                    CollisionResult hit = CheckActiveCollision((FrameworkElement)sender, elements);

                    //MessageBox.Show(hit.ToString());

                    if (hit.Type == CollisionType.OuterHit)
                    {
                        MarkCollidingOuter((Button)sender, hit.Target);

                        if (hit.Target is Grid folderGrid && folderGrid.Tag is FolderTag folderTag)
                        {
                            //MessageBox.Show("adding to folder");
                            AddAppToFolder(folderTag.FolderId, (Button)sender);

                            // maybe move this inside the add method
                            var curbButton = (Button)sender;
                            curbButton.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            var (senderLeft, senderTop) = GetElementPosition((Button)sender);
                            var (hitLeft, hitTop) = GetElementPosition(hit.Target);

                            Canvas.SetLeft((Button)sender, hitLeft);
                            Canvas.SetTop((Button)sender, hitTop);
                            Canvas.SetLeft(hit.Target, _senderPosition.X);
                            Canvas.SetTop(hit.Target, _senderPosition.Y);
                        }
                    }
                    else if (hit.Type == CollisionType.InnerHit)
                    {
                        MarkCollidingInner((Button)sender, hit.Target);

                        Position hitPos = new Position { X = GetElementPosition(hit.Target).X , Y = GetElementPosition(hit.Target).Y};
                        CreateFolder(position: hitPos, b1:(Button)sender, b2: hit.Target);
                    }

                    ClearCollisionVisuals(elements);
                }

                if (_snapToGrid)
                    SnapElementToGrid(_draggedElement);
            }

            StoreOriginalButtonProperties(_draggedElement as Button);

            _isDragging = false;
            _draggedElement = null;
            _isColliding = false;

            SaveApplicationPositions();

            Panel.SetZIndex((Button)sender, 2);
        }
        private bool IsButtonInsideFolder(Button button, string folderId)
        {
            if (!_folders.ContainsKey(folderId))
                return false;

            var folder = _folders[folderId];

            // Find the folder grid
            foreach (UIElement element in MainCanvas.Children)
            {
                if (element is Grid grid && grid.Tag is FolderTag folderTag && folderTag.FolderId == folderId)
                {
                    Rect folderRect = getMainRect(grid);

                    // If folder is expanded, use expanded bounds
                    if (folder.IsExpanded)
                    {
                        double expandedWidth = grid.Width * FolderExpandedX;
                        double expandedHeight = grid.Height * FolderExpandedY;
                        double centerX = folder.Position.X + (grid.Width / 2);
                        double centerY = folder.Position.Y + (grid.Height / 2);

                        folderRect = new Rect(
                            centerX - (expandedWidth / 2),
                            centerY - (expandedHeight / 2),
                            expandedWidth,
                            expandedHeight
                        );
                    }

                    Rect buttonRect = getMainRect(button);
                    return folderRect.Contains(new Point(buttonRect.X + buttonRect.Width / 2, buttonRect.Y + buttonRect.Height / 2));
                }
            }

            return false;
        }
        private void MoveElement(UIElement element, Vector offset)
        {
            double currentLeft = Canvas.GetLeft(element);
            double currentTop = Canvas.GetTop(element);

            // Handle NaN values (elements that haven't been positioned yet)
            if (double.IsNaN(currentLeft)) currentLeft = 0;
            if (double.IsNaN(currentTop)) currentTop = 0;

            double newLeft = currentLeft + offset.X;
            double newTop = currentTop + offset.Y;

            // Snap to pixel boundaries to reduce sub-pixel rendering issues
            newLeft = Math.Round(newLeft);
            newTop = Math.Round(newTop);

            Canvas.SetLeft(element, newLeft);
            Canvas.SetTop(element, newTop);
        }
        private void ClearButtonAnimations(Button button)
        {
            if (button == null) return;

            button.BeginAnimation(Button.WidthProperty, null);
            button.BeginAnimation(Canvas.LeftProperty, null);
            button.BeginAnimation(Canvas.TopProperty, null);
        }
        private void StoreOriginalButtonProperties(Button button)
        {
            if (button == null) return;

            button.Resources["OriginalWidth"] = button.Width;
            button.Resources["OriginalLeft"] = Canvas.GetLeft(button);
            button.Resources["OriginalTop"] = Canvas.GetTop(button);
        }
        private void LaunchApplication(Button button)
        {
            if (button.Tag == null) return;

            ElementTag tag = (ElementTag)button.Tag;
            string fullCommand = tag.Path;
            var (exePath, arguments) = ParseCommandLine(fullCommand);
            LaunchProcess(exePath, arguments);
        }
        private (string ExePath, string Arguments) ParseCommandLine(string fullCommand)
        {
            var pattern = "^\"([^\"]+)\"\\s*(.*)";
            var match = Regex.Match(fullCommand, pattern);

            if (match.Success)
            {
                return (match.Groups[1].Value, match.Groups[2].Value);
            }

            return (fullCommand, "");
        }
        private void LaunchProcess(string path, string arguments)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = arguments,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch: {ex.Message}");
            }
        }
        Rect getMainRect(FrameworkElement element)
        {
            double x = Canvas.GetLeft(element);
            double y = Canvas.GetTop(element);

            // Handle NaN values
            if (double.IsNaN(x)) x = 0;
            if (double.IsNaN(y)) y = 0;

            return new Rect(
                x,
                y,
                element.ActualWidth,
                element.ActualHeight
            );
        }
        Rect getInnerRect(FrameworkElement element)
        {
            double threshhold = 25;
            double x = Canvas.GetLeft(element);
            double y = Canvas.GetTop(element);

            // Handle NaN values
            if (double.IsNaN(x)) x = 0;
            if (double.IsNaN(y)) y = 0;

            // Ensure we have valid dimensions
            double width = element.ActualWidth;
            double height = element.ActualHeight;

            if (width <= 0 || height <= 0)
                return Rect.Empty;

            // Ensure threshold doesn't exceed half the element size
            double adjustedThreshold = Math.Min(threshhold, Math.Min(width / 2, height / 2));

            return new Rect(
                x + adjustedThreshold,
                y + adjustedThreshold,
                Math.Max(0, width - (2 * adjustedThreshold)),
                Math.Max(0, height - (2 * adjustedThreshold))
            );
        }
        private CollisionResult CheckActiveCollision(
            FrameworkElement activeElement,
            IEnumerable<FrameworkElement> allElements)
        {
            Rect activeRect = getMainRect(activeElement);

            foreach (var elem in allElements)
            {

                if (elem == activeElement)
                    continue;

                if (activeRect.IntersectsWith(getMainRect(elem)))
                {
                    if(elem is Control)
                    {
                        if (activeRect.IntersectsWith(getInnerRect(elem)))
                        {
                            return new CollisionResult(elem, CollisionType.InnerHit);
                        }
                        else
                        {
                            return new CollisionResult(elem, CollisionType.OuterHit);
                        }
                    }
                    else if(elem is Shape || elem is Grid)
                    {
                        return new CollisionResult(elem, CollisionType.OuterHit);
                    }
                }
            }

            return null;
        }
        void StoreOriginalControlVisuals(Control control)
        {
            if (!control.Resources.Contains("OriginalControlVisuals"))
            {
                control.Resources["OriginalControlVisuals"] = new
                {
                    control.BorderBrush,
                    control.BorderThickness,
                    control.Background
                };
            }
        }
        void StoreOriginalShapeVisuals(Shape shape)
        {
            if (!shape.Resources.Contains("OriginalShapeVisuals"))
            {
                shape.Resources["OriginalShapeVisuals"] = new
                {
                    shape.Stroke,
                    shape.StrokeThickness
                };
            }
        }
        private void ApplyOuterControlCollision(Control control)
        {
            StoreOriginalControlVisuals(control);

            var red = new SolidColorBrush(Color.FromRgb(229, 57, 53));
            var redTransparent = new SolidColorBrush(Color.FromArgb(68, 229, 57, 80));

            control.BorderBrush = red;
            control.BorderThickness = new Thickness(3);
            control.Background = redTransparent;
        }
        private void ApplyInnerControlCollision(Control control)
        {
            StoreOriginalControlVisuals(control);

            var blue = new SolidColorBrush(Color.FromRgb(51, 153, 204));
            var blueTransparent = new SolidColorBrush(Color.FromArgb(51, 153, 204, 80));

            control.BorderBrush = blue;
            control.BorderThickness = new Thickness(3);
            control.Background = blueTransparent;
        }
        private void ApplyShapeCollision(Shape shape)
        {
            StoreOriginalShapeVisuals(shape);

            var red = new SolidColorBrush(Color.FromRgb(229, 57, 53));

            shape.Stroke = red;
            shape.StrokeThickness = 3;
        }

        private void ApplyOuterCollisionVisuals(FrameworkElement element)
        {
            switch (element)
            {
                case Control control:
                    ApplyOuterControlCollision(control);
                    break;
                case Shape shape:
                    ApplyShapeCollision(shape);
                    break;
            }
        }
        private void ApplyInnerCollisionVisuals(FrameworkElement element)
        {
            switch (element)
            {
                case Control control:
                    ApplyInnerControlCollision(control);
                    break;
                case Shape shape:
                    ApplyShapeCollision(shape);
                    break;
            }
        }
        private void MarkCollidingOuter(FrameworkElement a, FrameworkElement b)
        {
            ApplyOuterCollisionVisuals(a);
            ApplyOuterCollisionVisuals(b);
        }
        private void MarkCollidingInner(FrameworkElement a, FrameworkElement b)
        {
            ApplyInnerCollisionVisuals(a);
            ApplyInnerCollisionVisuals(b);
        }
        private void ClearCollisionVisuals(IEnumerable<FrameworkElement> Elements)
        {
            foreach (FrameworkElement element in Elements)
            {
                switch (element)
                {
                    case Control c when c.Resources.Contains("OriginalControlVisuals"):
                        dynamic cv = c.Resources["OriginalControlVisuals"];
                        c.BorderBrush = cv.BorderBrush;
                        c.BorderThickness = cv.BorderThickness;
                        c.Background = cv.Background;
                        break;
                    case Shape s when s.Resources.Contains("OriginalShapeVisuals"):
                        dynamic sv = s.Resources["OriginalShapeVisuals"];
                        s.Stroke = sv.Stroke;
                        s.StrokeThickness = sv.StrokeThickness;
                        break;
                }
            }
        }

        #endregion

        #region Context Menu
        private void OnButtonRightClick(object sender, EventArgs e)
        {
            if (sender is not Button button || button.Tag == null)
                return;

            ElementTag tag = (ElementTag)button.Tag;
            string appName = tag.Name;
            var contextMenu = CreateContextMenu(button, appName);
            button.ContextMenu = contextMenu;
            contextMenu.IsOpen = true;
        }

        private ContextMenu CreateContextMenu(Button button, string appName)
        {
            var contextMenu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(34, 40, 49)),
                BorderBrush = Brushes.Transparent,
                Foreground = Brushes.White
            };

            var removeItem = CreateRemoveMenuItem(appName);
            removeItem.Click += (s, args) => RemoveApplication(button, appName);
            contextMenu.Items.Add(removeItem);

            return contextMenu;
        }

        private MenuItem CreateRemoveMenuItem(string appName)
        {
            var menuItemStyle = CreateMenuItemStyle();
            return new MenuItem
            {
                Header = $"Remove {appName}",
                Style = menuItemStyle
            };
        }
        private Style CreateMenuItemStyle()
        {
            var style = new Style(typeof(MenuItem));
            style.Setters.Add(new Setter(MenuItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(34, 40, 49))));
            style.Setters.Add(new Setter(MenuItem.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(MenuItem.PaddingProperty, new Thickness(10, 5, 10, 5)));
            style.Setters.Add(new Setter(MenuItem.BorderThicknessProperty, new Thickness(0)));

            var template = CreateMenuItemTemplate();
            style.Setters.Add(new Setter(MenuItem.TemplateProperty, template));

            return style;
        }
        private ControlTemplate CreateMenuItemTemplate()
        {
            var template = new ControlTemplate(typeof(MenuItem));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Border";
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(34, 40, 49)));

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            content.SetValue(ContentPresenter.MarginProperty, new Thickness(5, 2, 5, 2));
            border.AppendChild(content);

            template.VisualTree = border;

            var highlightTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
            highlightTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
                new SolidColorBrush(Color.FromRgb(57, 62, 70)), "Border"));
            template.Triggers.Add(highlightTrigger);

            return template;
        }
        private void RemoveApplication(Button button, string appName)
        {
            try
            {
                var json = File.ReadAllText(_jsonFilePath);
                var apps = JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (apps != null && apps.Remove(appName))
                {
                    var updatedJson = JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_jsonFilePath, updatedJson);

                    DeleteAppImage(appName);
                }

                MainCanvas.Children.Remove(button);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing application: {ex.Message}");
            }
        }
        private void DeleteAppImage(string appName)
        {
            try
            {
                var imagePath = Path.Combine(_imgPath, $"{appName}.png");
                if (File.Exists(imagePath))
                {
                    File.Delete(imagePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete image: {ex.Message}");
            }
        }
        #endregion

        #region Grid Management
        private (double X, double Y) SnapElementToGrid(UIElement element)
        {
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);

            // Handle NaN values
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            double snappedLeft = Math.Round(left / _gridSizeX) * _gridSizeX;
            double snappedTop = Math.Round(top / _gridSizeY) * _gridSizeY;

            Canvas.SetLeft(element, snappedLeft);
            Canvas.SetTop(element, snappedTop);

            return (snappedLeft, snappedTop);
        }
        private void DrawGridLines()
        {
            double width = MainCanvas.ActualWidth;
            double height = MainCanvas.ActualHeight;

            double gridX = Math.Max(1, Properties.Settings.Default.GridSizeX);
            double gridY = Math.Max(1, Properties.Settings.Default.GridSizeY);

            DrawVerticalGridLines(width, height, gridX);
            DrawHorizontalGridLines(width, height, gridY);
        }
        private void DrawVerticalGridLines(double width, double height, double spacing)
        {
            for (double x = 0; x < width; x += spacing)
            {
                GridCanvas.Children.Add(CreateGridLine(x, 0, x, height));
            }
        }
        private void DrawHorizontalGridLines(double width, double height, double spacing)
        {
            for (double y = 0; y < height; y += spacing)
            {
                GridCanvas.Children.Add(CreateGridLine(0, y, width, y));
            }
        }
        private Line CreateGridLine(double x1, double y1, double x2, double y2)
        {
            return new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = Brushes.Blue,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
        }
        public void UpdateGrid()
        {
            GridCanvas.Children.Clear();

            if (Properties.Settings.Default.ShowGrid)
            {
                DrawGridLines();
            }

            _gridSizeX = Properties.Settings.Default.GridSizeX;
            _gridSizeY = Properties.Settings.Default.GridSizeY;
        }
        #endregion

        #region Window Events
        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            AttachToDesktop();
            await InitializeSharedWebViewEnvironment();
        }
        private void AttachToDesktop()
        {
            IntPtr progman = FindWindow("Progman", null);
            IntPtr result = IntPtr.Zero;

            // Send message to Progman to spawn a WorkerW window behind the desktop icons
            SendMessage(progman, WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero);

            // Find the WorkerW window that was created
            IntPtr workerW = IntPtr.Zero;
            EnumWindows((topHandle, topParamHandle) =>
            {
                IntPtr shelldll = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);

                if (shelldll != IntPtr.Zero)
                {
                    // Get the next WorkerW window after the one containing SHELLDLL_DefView
                    workerW = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                }

                return true;
            }, IntPtr.Zero);

            // If we found the WorkerW, attach our window to it
            if (workerW != IntPtr.Zero)
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                SetParent(hwnd, workerW);
            }
            else
            {
                // Fallback to the old method if the new method fails
                IntPtr shellViewWin = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellViewWin != IntPtr.Zero)
                {
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    SetParent(hwnd, shellViewWin);
                }
            }
        }

        private async Task InitializeSharedWebViewEnvironment()
        {
            if (_sharedEnvironment != null) return;

            var userDataPath = Path.Combine(_mainFolder, "WebViewData");
            var options = new CoreWebView2EnvironmentOptions("--disable-gpu --disable-software-rasterizer");
            _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, userDataPath, options);
        }
        #endregion

        #region Settings Events
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new Settings(this)
            {
                Owner = this
            };

            settingsWindow.UpdateRequested += OnSettingsUpdateRequested;
            settingsWindow.Show();
        }

        private void OnSettingsUpdateRequested(object sender, string action)
        {
            if (action.StartsWith("remove:"))
            {
                var parts = action.Split(':');
                if (parts.Length == 3)
                {
                    RemoveElementByName(parts[1], parts[2]);
                }
            }
        }

        private void OnSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Properties.Settings.Default.ButtonSize):
                    RefreshApplications();
                    break;
                case nameof(Properties.Settings.Default.SnapToGrid):
                    _snapToGrid = Properties.Settings.Default.SnapToGrid;
                    break;
                case nameof(Properties.Settings.Default.GridSizeX):
                case nameof(Properties.Settings.Default.GridSizeY):
                case nameof(Properties.Settings.Default.ShowGrid):
                    UpdateGrid();
                    break;
            }
        }
        #endregion

        #region UI Actions
        private void Refresh_Page(object sender, RoutedEventArgs e)
        {
            MainCanvas.Children.Clear();
            LoadApplications();
            LoadWidgets();

            if (Properties.Settings.Default.ShowGrid)
            {
                DrawGridLines();
            }
        }

        private void RefreshApplications()
        {
            MainCanvas.Children.Clear();
            LoadApplications();
        }

        private void RemoveElementByName(string name, string type = null)
        {
            UIElement elementToRemove = null;

            foreach (UIElement element in MainCanvas.Children)
            {
                if (element is FrameworkElement fe && fe.Tag is ElementTag tag)
                {
                    if (tag.Name == name && (type == null || tag.ChildType == type))
                    {
                        elementToRemove = element;
                        break;
                    }
                }
            }

            if (elementToRemove != null)
            {
                MainCanvas.Children.Remove(elementToRemove);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        #endregion
        Rect Folder()
        {
            double x = 100;
            double y = 100;
            return new Rect(
                x,
                y,
                70,
                70
            );
        }
        private void CreateFolder(string ID = null, Position position = null, object b1 = null, object b2 = null)
        {
            if (ID == null)
                ID = Guid.NewGuid().ToString();

            if (position == null)
                position = new Position { X = 100, Y = 100 };

            Folder folder;

            if ((b1 is Button button1 && button1.Tag is ElementTag tag1) && (b2 is Button button2 && button2.Tag is ElementTag tag2))
            {
                folder = new Folder
                {
                    ID = ID,
                    Position = position,
                    IsExpanded = false,
                    ContainedApps = new List<string>([tag1.Name, tag2.Name])
                };
                tag1.ParentFolderId = ID;
                tag2.ParentFolderId = ID;
            }
            else
            {
                folder = new Folder
                {
                    ID = ID,
                    Position = position,
                    IsExpanded = false,
                    ContainedApps = new List<string>(_folders[ID].ContainedApps)
                };
            }

            _folders[ID] = folder;

            var folderGrid = new Grid
            {
                Width = 70,
                Height = 70,
                Tag = new FolderTag { FolderId = ID }
            };

            // Folder background
            var folderRect = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(120, 50, 50, 50)),
                Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                StrokeThickness = 2,
                RadiusX = 7,
                RadiusY = 7,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1)
            };

            var scale = new ScaleTransform(1, 1);
            folderRect.RenderTransform = scale;

            folderGrid.Children.Add(folderRect);

            Canvas.SetLeft(folderGrid, position.X);
            Canvas.SetTop(folderGrid, position.Y);

            folderGrid.PreviewMouseLeftButtonDown += HandleFolderLeftMouseDown;
            folderGrid.PreviewMouseMove += HandleFolderMouseMove;
            folderGrid.PreviewMouseLeftButtonUp += HandleFolderLeftMouseUp;

            MainCanvas.Children.Add(folderGrid);
            Panel.SetZIndex(folderGrid, 1);
        }
        private void HandleFolderLeftMouseDown(object sender, MouseButtonEventArgs e)
        {
           
            _draggedElement = sender as UIElement;
            _clickPosition = e.GetPosition(MainCanvas);
            _senderPosition = new Point(Canvas.GetLeft((Grid)sender), Canvas.GetTop((Grid)sender));
            _isDragging = false;
            _draggedElement?.CaptureMouse();

            Panel.SetZIndex((Grid)sender, 3);
        }
        private void HandleFolderMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedElement == null || !_draggedElement.IsMouseCaptured)
                return;

            _isColliding = false;

            Point currentPosition = e.GetPosition(MainCanvas);
            Vector diff = currentPosition - _clickPosition;

            if (!_isDragging && (Math.Abs(diff.X) > DragThreshold || Math.Abs(diff.Y) > DragThreshold))
            {
                _isDragging = true;
            }
            if (_isDragging)
            {
                double oldLeft = Canvas.GetLeft(_draggedElement);
                double oldTop = Canvas.GetTop(_draggedElement);
                if (double.IsNaN(oldLeft)) oldLeft = 0;
                if (double.IsNaN(oldTop)) oldTop = 0;

                MoveElement(_draggedElement, diff);
                _clickPosition = currentPosition;

                double newLeft = Canvas.GetLeft(_draggedElement);
                double newTop = Canvas.GetTop(_draggedElement);

                var elements = MainCanvas.Children.OfType<FrameworkElement>();
                CollisionResult hit = CheckActiveCollision((FrameworkElement)sender, elements);


                ClearCollisionVisuals(elements);

                if (hit != null)
                {
                    _isColliding = true;
                }

                // Force immediate render to reduce glitching
                _draggedElement.InvalidateVisual();

            }
        }
        private void HandleFolderLeftMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedElement == null || !_draggedElement.IsMouseCaptured)
                return;

            _draggedElement.ReleaseMouseCapture();

            Grid folderGrid = sender as Grid ?? (sender as Rectangle)?.Parent as Grid;
            if (folderGrid?.Tag is not FolderTag folderTag)
                return;

            var folder = _folders[folderTag.FolderId];

            if (!_isDragging)
            {
                if (!folder.IsExpanded)
                {
                    ExpandFolder(folderTag.FolderId, folderGrid);
                }
                else
                {
                    CollapseFolder(folderTag.FolderId, folderGrid);
                }
            }
            else
            {
                if (_isColliding)
                {
                    var elements = MainCanvas.Children.OfType<FrameworkElement>();
                    CollisionResult hit = CheckActiveCollision((FrameworkElement)sender, elements);

                    //MessageBox.Show(hit.ToString());

                    if (hit != null)
                    {
                        var (senderLeft, senderTop) = GetElementPosition((Grid)sender);
                        var (hitLeft, hitTop) = GetElementPosition(hit.Target);

                        Canvas.SetLeft((Grid)sender, hitLeft);
                        Canvas.SetTop((Grid)sender, hitTop);
                        Canvas.SetLeft(hit.Target, _senderPosition.X);
                        Canvas.SetTop(hit.Target, _senderPosition.Y);
                    }

                    ClearCollisionVisuals(elements);
                }

                var (folderPosX, folderPosY) = GetElementPosition((Grid)sender);
                folder.Position.X = folderPosX;
                folder.Position.Y = folderPosY;

                if (_snapToGrid)
                    SnapElementToGrid(_draggedElement);
            }

            _isDragging = false;
            _draggedElement = null;
            _isColliding = false;

            SaveApplicationPositions();
            SaveFolders();

            Panel.SetZIndex((Grid)sender, 1);
        }
        private void ExpandFolder(string folderId, Grid folderGrid)
        {
            var rect = folderGrid.Children.OfType<Rectangle>().FirstOrDefault();
            var folder = _folders[folderId];
            folder.IsExpanded = true;

            // Calculate expanded size
            double expandedWidth = folderGrid.Width * FolderExpandedX;
            double expandedHeight = folderGrid.Height * FolderExpandedY;

            var animX = new DoubleAnimation
            {
                From = 1,
                To = FolderExpandedX,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var animY = new DoubleAnimation
            {
                From = 1,
                To = FolderExpandedY,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var transform = (ScaleTransform)rect.RenderTransform;
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
            rect.StrokeThickness = 1;

            int appsPerRow = 3;
            double padding = 10;
            double spacing = 5;

            int totalApps = folder.ContainedApps.Count;
            int rows = (int)Math.Ceiling(totalApps / (double)appsPerRow);

            double availableWidth = expandedWidth - (2 * padding);
            double availableHeight = expandedHeight - (2 * padding);

            double cellWidth = _buttonSize;
            double cellHeight = _buttonSize;

            double folderCenterX = folder.Position.X + (folderGrid.Width / 2);
            double folderCenterY = folder.Position.Y + (folderGrid.Height / 2);

            double startX = folderCenterX - (expandedWidth / 2) + padding;
            double startY = folderCenterY - (expandedHeight / 2) + padding;

            int index = 0;
            foreach (var appName in folder.ContainedApps)
            {
                foreach (UIElement element in MainCanvas.Children)
                {
                    if (element is Button button && button.Tag is ElementTag tag)
                    {
                        if (tag.Name == appName)
                        {
                            button.Visibility = Visibility.Visible;

                            // Calculate grid position
                            int row = index / appsPerRow;
                            int col = index % appsPerRow;

                            // Calculate position with spacing
                            double offsetX = col * (cellWidth + spacing);
                            double offsetY = row * (cellHeight + spacing);

                            double finalX = startX + offsetX;
                            double finalY = startY + offsetY;

                            Canvas.SetLeft(button, finalX);
                            Canvas.SetTop(button, finalY);

                            // Optional: Resize buttons to fit better
                            button.Width = cellWidth;
                            button.Height = cellHeight;

                            index++;
                            break;
                        }
                    }
                }
            }

            SaveFolders();
        }

        private void CollapseFolder(string folderId, Grid folderGrid)
        {
            var rect = folderGrid.Children.OfType<Rectangle>().FirstOrDefault();

            var folder = _folders[folderId];
            folder.IsExpanded = false;

            var animX = new DoubleAnimation
            {
                From = FolderExpandedX,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var animY = new DoubleAnimation
            {
                From = FolderExpandedY,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(120)
            };
            var transform = (ScaleTransform)rect.RenderTransform;
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, animY);

            // Hide all apps in this folder
            foreach (var appName in folder.ContainedApps)
            {
                
                foreach (UIElement element in MainCanvas.Children)
                {
                    if (element is Button button && button.Tag is ElementTag tag)
                    {
                        if (tag.Name == appName)
                        {
                            button.Visibility = Visibility.Collapsed;
                            break;
                        }
                    }
                }
            }

            SaveFolders();
        }
        private void AddAppToFolder(string folderID, FrameworkElement activeElement)
        {
            ElementTag tag = (ElementTag)activeElement.Tag;

            string appname = tag.Name;
            string appPath = tag.Path;

            tag.ParentFolderId = folderID;

            _folders[folderID].ContainedApps.Add(appname);

            SaveFolders();
            SaveApplicationPositions();

        }
        private void RemoveAppFromFolder(Button button, string appName, string parentFolderId)
        {
            if (!_folders.ContainsKey(parentFolderId))
                return;

            _folders[parentFolderId].ContainedApps.Remove(appName);

            if (button.Tag is ElementTag tag)
            {
                tag.ParentFolderId = String.Empty;
            }

            button.Visibility = Visibility.Visible;
            button.Width = _buttonSize;

            SaveFolders();
            SaveApplicationPositions();

        }
    }
    
    #region Data Models
    public class AppInfo
    {
        public string Path { get; set; }
        public Position Position { get; set; }
        public string? ParentFolderId { get; set; }
    }

    public class Widget
    {
        public Size Size { get; set; }
        public Position Position { get; set; }
        public bool Status { get; set; }
    }
    public class Folder
    {
        public string ID { get; set; }
        public Position Position { get; set; }
        public List<string> ContainedApps { get; set; } = new();
        public bool IsExpanded { get; set; }
    }
    public class FolderTag
    {
        public string FolderId { get; set; }
    }
    public class CollisionResult
    {
        public FrameworkElement Target { get; }
        public CollisionType Type { get; }

        public CollisionResult(FrameworkElement target, CollisionType type)
        {
            Target = target;
            Type = type;
        }
    }
    public class ElementTag
    {
        public string ChildType { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string? ParentFolderId { get; set; }
    }

    public class Size
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class Position
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
    public enum FolderState
    {
        Opened,
        Closed
    }
    public enum CollisionType
    {
        None,
        OuterHit,
        InnerHit
    }
    #endregion
}