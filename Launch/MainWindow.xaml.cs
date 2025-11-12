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

namespace Launch
{
    public partial class MainWindow : Window
    {
        #region Win32 API Imports
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        #endregion

        #region Constants
        private const double DragThreshold = 1;
        private const double CanvasPadding = 30;
        private const int ImageCornerRadius = 8;
        private const double TaskbarHeight = 50;
        #endregion

        #region Fields - Paths
        private readonly string _baseDirectory;
        private readonly string _mainFolder;
        private readonly string _settingsPath;
        private readonly string _jsonFilePath;
        private readonly string _widgetPath;
        private readonly string _widgetsFolder;
        private readonly string _imgPath;
        #endregion

        #region Fields - State
        private double _buttonSize;
        private bool _snapToGrid;
        private bool _showGrid;
        private double _gridSizeX;
        private double _gridSizeY;

        private bool _isDragging;
        private Point _clickPosition;
        private UIElement _draggedElement;

        private static CoreWebView2Environment _sharedEnvironment;
        #endregion

        #region Constructor and Initialization
        public MainWindow()
        {
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _mainFolder = Path.Combine(_baseDirectory, "src");
            _settingsPath = Path.Combine(_mainFolder, "settings.json");
            _jsonFilePath = Path.Combine(_mainFolder, "path.json");
            _widgetPath = Path.Combine(_mainFolder, "widgets.json");
            _widgetsFolder = Path.Combine(_mainFolder, "Widgets");
            _imgPath = Path.Combine(_mainFolder, "imgs");

            Properties.Settings.Default.PropertyChanged += OnSettingsChanged;

            InitializeComponent();
            LoadSettings();
            LoadApplications();
            LoadWidgets();
            ConfigureWindowSize();

            Loaded += OnWindowLoaded;
        }

        private void ConfigureWindowSize()
        {
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight - TaskbarHeight;
            Left = 0;
            Top = 0;
            MainCanvas.Margin = new Thickness(CanvasPadding);
            GridCanvas.Margin = new Thickness(CanvasPadding);
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
                MessageBox.Show("JSON file not found!");
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
                    CreateAppButton(app.Key, app.Value.Path, new Point(app.Value.Position.X, app.Value.Position.Y));
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
                    if (element is Button button && button.Tag != null)
                    {
                        dynamic tag = button.Tag;
                        string path = tag.Path;
                        var app = apps.FirstOrDefault(a => a.Value.Path == path);

                        if (!app.Equals(default(KeyValuePair<string, AppInfo>)))
                        {
                            var (x, y) = GetElementPosition(button);
                            app.Value.Position.X = x;
                            app.Value.Position.Y = y;
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
                MessageBox.Show("Widgets JSON file not found!");
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
                    if (element is Border container && container.Child is WebView2)
                    {
                        var widget = FindWidgetBySize(widgets, container);
                        if (widget.HasValue)
                        {
                            widget.Value.Value.Position.X = Canvas.GetLeft(container);
                            widget.Value.Value.Position.Y = Canvas.GetTop(container);
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

        #region App Button Creation
        public void CreateAppButton(string appName, string appPath, Point position)
        {
            var button = CreateButton(appName, appPath);
            var image = LoadAppImage(appName);
            var contentPanel = CreateButtonContent(image);

            button.Content = contentPanel;
            ApplyButtonTemplate(button);
            PositionButton(button, position);
            AttachButtonEventHandlers(button);
            ApplyButtonAnimations(button);

            MainCanvas.Children.Add(button);
        }

        private Button CreateButton(string appName, string appPath)
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
                Tag = new { ChildType = "button", Name = appName, Path = appPath }
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
            _isDragging = false;
            _draggedElement?.CaptureMouse();

            ClearButtonAnimations(_draggedElement as Button);
        }

        private void OnButtonMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedElement == null || !_draggedElement.IsMouseCaptured)
                return;

            Point currentPosition = e.GetPosition(MainCanvas);
            Vector diff = currentPosition - _clickPosition;

            if (!_isDragging && (Math.Abs(diff.X) > DragThreshold || Math.Abs(diff.Y) > DragThreshold))
            {
                _isDragging = true;
            }

            if (_isDragging)
            {
                MoveElement(_draggedElement, diff);
                _clickPosition = currentPosition;
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
            else if (_snapToGrid)
            {
                SnapElementToGrid(_draggedElement);
            }

            StoreOriginalButtonProperties(_draggedElement as Button);

            _isDragging = false;
            _draggedElement = null;

            SaveApplicationPositions();
        }

        private void MoveElement(UIElement element, Vector offset)
        {
            double newLeft = Canvas.GetLeft(element) + offset.X;
            double newTop = Canvas.GetTop(element) + offset.Y;

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
            if (button?.Tag == null) return;

            dynamic tag = button.Tag;
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
        #endregion

        #region Context Menu
        private void OnButtonRightClick(object sender, EventArgs e)
        {
            if (sender is not Button button || button.Tag == null)
                return;

            dynamic tag = button.Tag;
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
            IntPtr progman = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
            IntPtr shellViewWin = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            SetParent(hwnd, shellViewWin);
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
                if (element is FrameworkElement fe && fe.Tag != null)
                {
                    dynamic tag = fe.Tag;
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
    }

    #region Data Models
    public class AppInfo
    {
        public string Path { get; set; }
        public Position Position { get; set; }
    }

    public class Widget
    {
        public Size Size { get; set; }
        public Position Position { get; set; }
        public bool Status { get; set; }
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
    #endregion
}