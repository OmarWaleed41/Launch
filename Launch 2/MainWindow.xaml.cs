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
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.DirectoryServices;
using System.IO.Compression;
using System.Windows.Shapes;
using Path = System.IO.Path;
using System.Reflection;


// Well Welcome to my Humble little (it's not) project, so if you like great customization other apps don't offer (yes im talking to you rainmeter) you have come to the right place

namespace Launch_2
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        string Base = System.AppDomain.CurrentDomain.BaseDirectory;

        string MainFolder;
        string settingsPath;
        string jsonFilePath;
        public string widgetPath;
        string widgetsFolder;
        string imgPath;
        double button_size;
        private bool isDragging = false;
        private Point clickPosition;
        private UIElement draggedElement;

        public bool snapToGrid;
        public bool showGrid;

        private const double DragThreshold = 1;
        private double gridSizeX;
        private double gridSizeY;
        private double CanvasPadding = 30;

        private static CoreWebView2Environment sharedEnv;

        public MainWindow()
        {
            MainFolder = Path.Combine(Base, "src");
            settingsPath = Path.Combine(MainFolder, "settings.json");
            jsonFilePath = Path.Combine(MainFolder, "path.json");
            widgetPath = Path.Combine(MainFolder, "widgets.json");
            widgetsFolder = Path.Combine(MainFolder, "Widgets");
            imgPath = Path.Combine(MainFolder, "imgs");

            Properties.Settings.Default.PropertyChanged += SettingsChanged;

            InitializeComponent();
            ReadSettingsJson();
            ReadPathJson();
            ReadWidgetJson();
            SetWindowSize();
   
            Loaded += OnWindowLoaded;
        }
        private void SetWindowSize()
        {
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight - 50;
            this.Left = 0;
            this.Top = 0;
            MainCanvas.Margin = new Thickness(30);
            GridCanvas.Margin = new Thickness(30);
        }

        private void ReadSettingsJson()
        {
            button_size = Properties.Settings.Default.ButtonSize;
            snapToGrid = Properties.Settings.Default.SnapToGrid;
            gridSizeX = Properties.Settings.Default.GridSizeX;
            gridSizeY = Properties.Settings.Default.GridSizeY;
            showGrid = Properties.Settings.Default.ShowGrid;
        }

        private void ReadPathJson()
        {
            if (!File.Exists(jsonFilePath))
            {
                MessageBox.Show("JSON file not found!");
                return;
            }

            try
            {
                string json = File.ReadAllText(jsonFilePath);
                var apps = JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                foreach (var app in apps)
                {
                    Debug.WriteLine(app.Key);
                    CreateAppButton(app.Key, app.Value.Path, new Point(app.Value.Position.X, app.Value.Position.Y));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading JSON: {ex.Message}");
            }
        }
        private void ReadWidgetJson()
        {
            if (!File.Exists(widgetPath))
            {
                MessageBox.Show("Widgets JSON file not found!");
                return;
            }

            try
            {
                string json = File.ReadAllText(widgetPath);
                var widgets = JsonSerializer.Deserialize<Dictionary<string, Widget>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
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

            var webView = new WebView2
            {
                Width = widget.Size.Width,
                Height = widget.Size.Height,
                DefaultBackgroundColor = System.Drawing.Color.Transparent,
                IsHitTestVisible = false
            };

            var container = new Border
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

            MainCanvas.Children.Add(container);
            Canvas.SetLeft(container, widget.Position.X);
            Canvas.SetTop(container, widget.Position.Y);

            // WebView2 initialization
            try
            {
                await webView.EnsureCoreWebView2Async(sharedEnv);

                // Configure WebView2 to prevent interference
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Set up virtual host 
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app", MainFolder, CoreWebView2HostResourceAccessKind.Allow);

                webView.CoreWebView2.Navigate($"http://app/Widgets/{widgetName}/{widgetName}.html");

                webView.WebMessageReceived += (s, e) =>
                {
                    try
                    {
                        var msg = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                        if (msg.GetProperty("type").GetString() == "drag")
                        {
                            var dx = msg.GetProperty("dx").GetInt32();
                            var dy = msg.GetProperty("dy").GetInt32();

                            double left = Canvas.GetLeft(container);
                            double top = Canvas.GetTop(container);

                            Canvas.SetLeft(container, left + dx);
                            Canvas.SetTop(container, top + dy);
                            UpdateWidgetPositions();
                        }
                        else if (msg.GetProperty("type").GetString() == "openBrowser")
                        {
                            string url = msg.GetProperty("url").ToString();
                            OpenInBrowser(url);
                        }
                    }
                    catch
                    {
                        MessageBox.Show($"Error Loading From JS");
                    }
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebView2 initialization failed for {widgetName}: {ex.Message}");
            }
        }
        private void UpdateWidgetPositions()
        {
            try
            {
                string json = File.ReadAllText(widgetPath);
                var widgets = JsonSerializer.Deserialize<Dictionary<string, Widget>>(json);

                foreach (UIElement element in MainCanvas.Children)
                {
                    // Change Grid to Border here
                    if (element is Border container && container.Child is WebView2 webView)
                    {
                        // Find the widget by size
                        var widget = widgets.FirstOrDefault(w =>
                            Math.Abs(w.Value.Size.Width - container.Width) < 0.1 &&
                            Math.Abs(w.Value.Size.Height - container.Height) < 0.1);

                        if (!widget.Equals(default(KeyValuePair<string, Widget>)))
                        {
                            // Update position
                            widget.Value.Position.X = Canvas.GetLeft(container);
                            widget.Value.Position.Y = Canvas.GetTop(container);
                        }
                    }
                }

                string updatedJson = JsonSerializer.Serialize(widgets, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(widgetPath, updatedJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating widget positions: {ex.Message}");
            }
        }
        // Update the changegs you made while dragging etc.
        private void WriteJson()
        {
            try
            {
                string json = File.ReadAllText(jsonFilePath);
                var apps = JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                foreach (UIElement element in MainCanvas.Children)
                {
                    if (element is Button button)
                    {
                        var tag = (dynamic)button.Tag;
                        string path = tag.Path;

                        var app = apps.FirstOrDefault(a => a.Value.Path == path);
                        if (!app.Equals(default(KeyValuePair<string, AppInfo>)))
                        {
                            if (snapToGrid)
                            {
                                double left = Canvas.GetLeft(button);
                                double top = Canvas.GetTop(button);

                                double snappedLeft = Math.Round(left / gridSizeX) * gridSizeX;
                                double snappedTop = Math.Round(top / gridSizeY) * gridSizeY;

                                app.Value.Position.X = snappedLeft;
                                app.Value.Position.Y = snappedTop;
                            }
                            else
                            {
                                app.Value.Position.X = Canvas.GetLeft(button);
                                app.Value.Position.Y = Canvas.GetTop(button);
                            }
                        }
                    }
                }

                string updatedJson = JsonSerializer.Serialize(apps, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(jsonFilePath, updatedJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error writing JSON: {ex.Message}");
            }
        }
        // Self Explanatory
        public void CreateAppButton(string appName, string appPath, Point position)
        {
            Button appButton = new Button
            {
                Content = appName,
                Width = button_size,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                BorderBrush = Brushes.Transparent,
                Padding = new Thickness(0),
                FocusVisualStyle = null,
                Tag = new { ChildType = "button", Name = appName, Path = appPath }
            };

            Image image = new Image();
            try
            {
                string img = Path.Combine(imgPath, $"{appName}.png");
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(img, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                image.Source = bitmap;
            }
            catch
            {
                // Put a case here or we are screwed if something went bad
            }
            image.Stretch = Stretch.Uniform;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            int radius = 8;
            void UpdateClip()
            {
                image.Clip = new RectangleGeometry(new Rect(0, 0, image.ActualWidth, image.ActualHeight),radius, radius);
            }
            image.Loaded += (s, e) => UpdateClip();
            image.SizeChanged += (s, e) => UpdateClip();

            StackPanel contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };
            contentPanel.Children.Add(image);

            appButton.Content = contentPanel;
            appButton.OverridesDefaultStyle = true;

            // This part is for handeling the stupid wpf border and bg thing DO NOT TOUCH IT!!
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });

            var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentPresenterFactory);

            appButton.Template = new ControlTemplate(typeof(Button))
            {
                VisualTree = borderFactory
            };
            Canvas.SetLeft(appButton, position.X);
            Canvas.SetTop(appButton, position.Y);
            
            MainCanvas.Children.Add(appButton);

            appButton.PreviewMouseLeftButtonDown += Button_MouseLeftButtonDown;
            appButton.PreviewMouseMove += Button_MouseMove;
            appButton.PreviewMouseLeftButtonUp += Button_MouseLeftButtonUp;

            appButton.MouseRightButtonDown += Button_Menu;

            appButton.RenderTransformOrigin = new Point(0.5, 0.5);
            appButton.RenderTransform = new ScaleTransform(1, 1);

            appButton.MouseEnter += (s, e) =>
            {
                var anim = new DoubleAnimation(1.1, TimeSpan.FromMilliseconds(100));
                ((ScaleTransform)appButton.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                ((ScaleTransform)appButton.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            };
            appButton.MouseLeave += (s, e) =>
            {
                var anim = new DoubleAnimation(1, TimeSpan.FromMilliseconds(100));
                ((ScaleTransform)appButton.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                ((ScaleTransform)appButton.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            };


        }

        private void Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            draggedElement = sender as UIElement;
            clickPosition = e.GetPosition(MainCanvas);
            isDragging = false;
            draggedElement.CaptureMouse();

            Button btn = draggedElement as Button;
            if (btn != null)
            {
                btn.BeginAnimation(Button.WidthProperty, null);
                btn.BeginAnimation(Canvas.LeftProperty, null);
                btn.BeginAnimation(Canvas.TopProperty, null);
            }
        }

        private void Button_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedElement != null && draggedElement.IsMouseCaptured)
            {
                Point currentPosition = e.GetPosition(MainCanvas);
                Vector diff = currentPosition - clickPosition;

                if (!isDragging && (Math.Abs(diff.X) > DragThreshold || Math.Abs(diff.Y) > DragThreshold))
                {
                    isDragging = true;
                }

                if (isDragging)
                {
                    double newLeft = Canvas.GetLeft(draggedElement) + diff.X;
                    double newTop = Canvas.GetTop(draggedElement) + diff.Y;

                    Canvas.SetLeft(draggedElement, newLeft);
                    Canvas.SetTop(draggedElement, newTop);

                    clickPosition = currentPosition;
                }
            }
        }
        // here we also handle the Launch event (it works best here)
        private void Button_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (draggedElement != null && draggedElement.IsMouseCaptured)
            {
                draggedElement.ReleaseMouseCapture();

                // right here yes
                if (!isDragging)
                {
                    var tag = (dynamic)((Button)sender).Tag;
                    string fullCommand = tag.Path;

                    // Extract quoted path
                    string pattern = "^\"([^\"]+)\"\\s*(.*)";
                    var match = System.Text.RegularExpressions.Regex.Match(fullCommand, pattern);

                    if (match.Success)
                    {
                        string exePath = match.Groups[1].Value;
                        string arguments = match.Groups[2].Value;

                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = exePath,
                                Arguments = arguments,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error launching application: {ex.Message}");
                        }
                    }
                    else
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = fullCommand,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error launching application: {ex.Message}");
                        }
                    }
                }
                else if (isDragging && snapToGrid)
                {
                    SnapToGrid(draggedElement);
                    
                }
                Button btn = draggedElement as Button;
                if (btn != null)
                {
                    btn.Resources["OriginalWidth"] = btn.Width;
                    btn.Resources["OriginalLeft"] = Canvas.GetLeft(btn);
                    btn.Resources["OriginalTop"] = Canvas.GetTop(btn);
                }
                isDragging = false;
                draggedElement = null;

                WriteJson();
            }
        }

        private void Button_Menu(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            var tag = (dynamic)((Button)sender).Tag;
            string appname = tag.Name;

            if (btn != null)
            {
                Style menuItemStyle = new Style(typeof(MenuItem));
                menuItemStyle.Setters.Add(new Setter(MenuItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(34, 40, 49))));
                menuItemStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, Brushes.White));
                menuItemStyle.Setters.Add(new Setter(MenuItem.PaddingProperty, new Thickness(10, 5, 10, 5)));
                menuItemStyle.Setters.Add(new Setter(MenuItem.BorderThicknessProperty, new Thickness(0)));
                menuItemStyle.Setters.Add(new Setter(MenuItem.IconProperty, null));

                ControlTemplate template = new ControlTemplate(typeof(MenuItem));
                FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
                border.Name = "Border";
                border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(34, 40, 49)));

                FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
                content.SetValue(ContentPresenter.ContentSourceProperty, "Header");
                content.SetValue(ContentPresenter.MarginProperty, new Thickness(5, 2, 5, 2));
                border.AppendChild(content);

                template.VisualTree = border;

                Trigger highlightTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
                highlightTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(57, 62, 70)), "Border"));
                template.Triggers.Add(highlightTrigger);

                menuItemStyle.Setters.Add(new Setter(MenuItem.TemplateProperty, template));

                ContextMenu contextMenu = new ContextMenu
                {
                    Background = new SolidColorBrush(Color.FromRgb(34, 40, 49)),
                    BorderBrush = Brushes.Transparent,
                    Foreground = Brushes.White
                };

                MenuItem removeItem = new MenuItem
                {
                    Header = $"Remove {appname}",
                    Style = menuItemStyle
                };

                removeItem.Click += (s, args) =>
                {
                    string json = File.ReadAllText(jsonFilePath);
                    var apps = JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apps.Remove(appname))
                    {
                        string updatedJson = JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(jsonFilePath, updatedJson);

                        string img = Path.Combine(imgPath, appname + ".png");

                        try
                        {
                            File.Delete(img);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Failed to delete image: " + ex.Message);
                        }
                    }

                    MainCanvas.Children.Remove(btn);
                };

                contextMenu.Items.Add(removeItem);

                btn.ContextMenu = contextMenu;
                contextMenu.IsOpen = true;
            }
        }


        private void SnapToGrid(UIElement element)
        {
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);

            double snappedLeft = Math.Round(left / gridSizeX) * gridSizeX;
            double snappedTop = Math.Round(top / gridSizeY) * gridSizeY;

            Canvas.SetLeft(element, snappedLeft);
            Canvas.SetTop(element, snappedTop);
        }

        // Stay on desktop Part
        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            IntPtr progman = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
            IntPtr shellViewWin = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            SetParent(hwnd, shellViewWin);

            if (sharedEnv == null)
            {
                // Use a shared, app-local user data folder
                var userData = Path.Combine(MainFolder, "WebViewData");
                var opts = new CoreWebView2EnvironmentOptions("--disable-gpu --disable-software-rasterizer");
                sharedEnv = await CoreWebView2Environment.CreateAsync(null, userData, opts);
            }
            
        }

        // now to the taskbar options

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new Settings(this);
            settingsWindow.Owner = this;

            settingsWindow.UpdateRequested += (s, action) =>
            {
                if (action == "add")
                {
                }
                else if (action == "add_widget")
                {

                }
                else if (action.StartsWith("remove:"))
                {
                    var parts = action.Split(':');
                    if (parts.Length == 3)
                    {
                        string name = parts[1];
                        string type = parts[2];
                        RemoveByName(name, type);
                    }
                }
            };

            settingsWindow.Show(); // NOT ShowDialog
        }
        private void SettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Properties.Settings.Default.ButtonSize))
            {
                Refresh_Apps();
            }
            else if (e.PropertyName == nameof(Properties.Settings.Default.SnapToGrid))
            {
                snapToGrid = Properties.Settings.Default.SnapToGrid;
            }
            else if (e.PropertyName == nameof(Properties.Settings.Default.GridSizeX) ||
                     e.PropertyName == nameof(Properties.Settings.Default.GridSizeY) ||
                     e.PropertyName == nameof(Properties.Settings.Default.ShowGrid))
            {
                UpdateGrid();
            }
        }
        private void Refresh_Page(object sender, RoutedEventArgs e)
        {
            MainCanvas.Children.Clear();
            ReadPathJson();
            ReadWidgetJson();
            if (Properties.Settings.Default.ShowGrid)
            {
                DrawGridLines();
            }
        }
        private void Refresh_Apps()
        {
            MainCanvas.Children.Clear();
            ReadPathJson();
        }
        private void Refresh_Widgets()
        {
            MainCanvas.Children.Clear();
            ReadWidgetJson();
        }
        private void RemoveByName(string name, string type = null)
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
            this.Show();
            this.WindowState = WindowState.Normal;
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void DrawGridLines()
        {
            double width = MainCanvas.ActualWidth;   // use main canvas dimensions
            double height = MainCanvas.ActualHeight;

            double gridX = Math.Max(1, Properties.Settings.Default.GridSizeX);
            double gridY = Math.Max(1, Properties.Settings.Default.GridSizeY);

            for (double x = 0; x < width; x += gridX)
            {
                Line verticalLine = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = height,
                    Stroke = Brushes.Blue,
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                };
                GridCanvas.Children.Add(verticalLine);
            }

            for (double y = 0; y < height; y += gridY)
            {
                Line horizontalLine = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = Brushes.Blue,
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                };
                GridCanvas.Children.Add(horizontalLine);
            }
        }
        public void UpdateGrid()
        {
            GridCanvas.Children.Clear();

            if (Properties.Settings.Default.ShowGrid)
            {
                DrawGridLines();
            }
            gridSizeX = Properties.Settings.Default.GridSizeX;
            gridSizeY = Properties.Settings.Default.GridSizeY;
        }
        private void OpenInBrowser(string url)
        {
            try
            {
                // Opens in the user's default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open browser: " + ex.Message);
            }
        }
    }

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
}
