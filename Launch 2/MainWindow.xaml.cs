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
        string widgetPath;
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
                    InitWebView(widget.Value, widget.Key);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading Widgets JSON: {ex.Message}");
            }
        }

        private async void InitWebView(Widget widget, string widgetName)
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
                Child = webView
            };

            MainCanvas.Children.Add(container);
            Canvas.SetLeft(container, widget.Position.X);
            Canvas.SetTop(container, widget.Position.Y);

            // WebView2 initialization
            try
            {
                var envOptions = new CoreWebView2EnvironmentOptions("--disable-gpu --disable-software-rasterizer");
                var env = await CoreWebView2Environment.CreateAsync(null, null, envOptions);

                await webView.EnsureCoreWebView2Async(env);

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
                        string path = (string)button.Tag;

                        var app = apps.FirstOrDefault(a => a.Value.Path == path);
                        if (!app.Equals(default(KeyValuePair<string, AppInfo>)))
                        {
                            app.Value.Position.X = Canvas.GetLeft(button);
                            app.Value.Position.Y = Canvas.GetTop(button);
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
        private void CreateAppButton(string appName, string appPath, Point position)
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
                Tag = appPath
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
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));

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
            appButton.MouseEnter += Button_MouseEnter;
            appButton.MouseLeave += Button_MouseLeave;


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

                WriteJson();
                // right here yes
                if (!isDragging)
                {
                    string fullCommand = (string)((Button)sender).Tag;

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
            }
        }

        // THE  A N I M A T I O N
        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            Button btn = (Button)sender;
            double originalWidth = btn.Width;
            double originalLeft = Canvas.GetLeft(btn);
            double originalTop = Canvas.GetTop(btn);

            if (!btn.Resources.Contains("OriginalWidth"))
                btn.Resources["OriginalWidth"] = originalWidth;
            if (!btn.Resources.Contains("OriginalLeft"))
                btn.Resources["OriginalLeft"] = originalLeft;
            if (!btn.Resources.Contains("OriginalTop"))
                btn.Resources["OriginalTop"] = originalTop;

            double newWidth = originalWidth + 7;
            double newLeft = originalLeft - (newWidth - originalWidth) / 2;
            double newTop = originalTop - (newWidth - originalWidth) / 2;

            AnimateButtonProperty(btn, Button.WidthProperty, newWidth, 0.1);
            AnimateButtonProperty(btn, Canvas.LeftProperty, newLeft, 0.1);
            AnimateButtonProperty(btn, Canvas.TopProperty, newTop, 0.1);
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            Button btn = (Button)sender;
            if (btn.Resources.Contains("OriginalWidth"))
                btn.Width = (double)btn.Resources["OriginalWidth"];
            if (btn.Resources.Contains("OriginalLeft"))
            {
                double originalWidth = (double)btn.Resources["OriginalWidth"];
                double originalLeft = (double)btn.Resources["OriginalLeft"];
                double originalTop = (double)btn.Resources["OriginalTop"];
                AnimateButtonProperty(btn, Button.WidthProperty, originalWidth, 0.2);
                AnimateButtonProperty(btn, Canvas.LeftProperty, originalLeft, 0.2);
                AnimateButtonProperty(btn, Canvas.TopProperty, originalTop, 0.2);
            }
        }
        private void AnimateButtonProperty(DependencyObject target, DependencyProperty property, double toValue, double durationSeconds)
        {
            var animation = new DoubleAnimation
            {
                To = toValue,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath(property));
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }
        // Well it's not really snapping to grid so we'll need to take a look at that
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
        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            IntPtr progman = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
            IntPtr shellViewWin = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            SetParent(hwnd, shellViewWin);
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
                    Refresh_Apps();
                }
                else if (action == "add_widget")
                {

                    Refresh_Page();
                }
                else if (action == "remove")
                {
                    Refresh_Page();
                }
            };

            settingsWindow.Show(); // NOT ShowDialog
        }
        private void SettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Properties.Settings.Default.ShowGrid))
            {
                Refresh_Page(); // re-draw whenever ShowGrid changes
            }
            else if (e.PropertyName == nameof(Properties.Settings.Default.SnapToGrid))
            {
                snapToGrid = Properties.Settings.Default.SnapToGrid;
            }
        }
        public void Refresh_Page()
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
            double width = MainCanvas.ActualWidth;
            double height = MainCanvas.ActualHeight;

            for (double x = 0; x < width; x += gridSizeX)
            {
                Line verticalLine = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = height,
                    Stroke = Brushes.Blue,
                    StrokeThickness = 1,
                    Opacity = 1,
                    IsHitTestVisible = false
                };
                MainCanvas.Children.Add(verticalLine);
            }

            for (double y = 0; y < height; y += gridSizeY)
            {
                Line horizontalLine = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = Brushes.Blue,
                    StrokeThickness = 1,
                    Opacity = 1,
                    IsHitTestVisible = false
                };
                MainCanvas.Children.Add(horizontalLine);
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
