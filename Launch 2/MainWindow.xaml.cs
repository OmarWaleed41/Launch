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

// Well Welcome to my Humble little (it's not) project, so if you like great customization other apps don't offer (yes im talking to you rainmeter) you have come to the right place

namespace Launch_2
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        string MainFolder = "C:\\Users\\omarz\\source\\repos\\Launch 2\\Launch 2\\src";

        string jsonFilePath;
        string widgetPath;
        string imgPath;
        double button_size;
        private bool isDragging = false;
        private Point clickPosition;
        private UIElement draggedElement;
        private bool snapToGrid = false;
        private const double DragThreshold = 1;
        private double gridSize = 50;

        public MainWindow()
        {
            jsonFilePath = Path.Combine(MainFolder, "path.json");
            widgetPath = Path.Combine(MainFolder, "widgets.json");
            imgPath = Path.Combine(MainFolder, "imgs");

            button_size = 70;

            InitializeComponent();
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

            // Create the WebView2 control
            var webView = new WebView2
            {
                Width = widget.Size.Width,
                Height = widget.Size.Height,
                DefaultBackgroundColor = System.Drawing.Color.Transparent,
                IsHitTestVisible = false // Crucial for allowing events to pass through
            };

            // Create a container that will handle all interactions
            var container = new Border
            {
                Width = widget.Size.Width,
                Height = widget.Size.Height,
                Background = Brushes.Red, // Hit-testable but invisible
                Child = webView,
                Cursor = Cursors.Hand,
                BorderBrush = Brushes.Blue,
                BorderThickness = new Thickness(2)
            };

            // Add debug event handlers
            container.MouseEnter += (s, e) => Debug.WriteLine($"Mouse ENTERED {widgetName}");
            container.MouseLeave += (s, e) => Debug.WriteLine($"Mouse LEFT {widgetName}");
            container.PreviewMouseDown += (s, e) => Debug.WriteLine($"Mouse DOWN on {widgetName}");
            container.PreviewMouseUp += (s, e) => Debug.WriteLine($"Mouse UP on {widgetName}");

            // Add to canvas
            MainCanvas.Children.Add(container);
            Canvas.SetLeft(container, widget.Position.X);
            Canvas.SetTop(container, widget.Position.Y);

            // Set up drag behavior
            container.PreviewMouseLeftButtonDown += Widget_MouseLeftButtonDown;
            container.PreviewMouseMove += Widget_MouseMove;
            container.PreviewMouseLeftButtonUp += Widget_MouseLeftButtonUp;

            // WebView2 initialization
            try
            {
                Debug.WriteLine($"Starting WebView2 initialization for {widgetName}");
                await webView.EnsureCoreWebView2Async();

                // Configure WebView2 to prevent interference
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Set up virtual host mapping
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app", MainFolder, CoreWebView2HostResourceAccessKind.Allow);

                Debug.WriteLine($"Navigating WebView2 to widget content for {widgetName}");
                webView.CoreWebView2.Navigate($"http://app/Widgets/{widgetName}.html");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebView2 initialization failed for {widgetName}: {ex.Message}");
            }
        }

        // Widget drag
        private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine("Widget drag started");
            if (e.ChangedButton == MouseButton.Left && sender is Border container)
            {
                // Visual feedback
                container.BorderBrush = Brushes.White;
                container.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));

                draggedElement = container;
                clickPosition = e.GetPosition(MainCanvas);
                isDragging = false;
                draggedElement.CaptureMouse();
                Panel.SetZIndex(draggedElement, 100);
                e.Handled = true;
            }
        }

        private void Widget_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(MainCanvas);
                Vector diff = currentPosition - clickPosition;

                if (!isDragging && diff.Length > DragThreshold)
                {
                    isDragging = true;
                }

                if (isDragging && draggedElement is Border container)
                {
                    double newLeft = Canvas.GetLeft(container) + diff.X;
                    double newTop = Canvas.GetTop(container) + diff.Y;

                    newLeft = Math.Max(0, Math.Min(newLeft, MainCanvas.ActualWidth - container.ActualWidth));
                    newTop = Math.Max(0, Math.Min(newTop, MainCanvas.ActualHeight - container.ActualHeight));

                    Canvas.SetLeft(container, newLeft);
                    Canvas.SetTop(container, newTop);

                    clickPosition = currentPosition;
                    e.Handled = true;
                }
            }
        }

        private void Widget_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine("Widget drag ended");
            if (draggedElement is Border container)
            {
                // Reset visual feedback
                container.BorderBrush = Brushes.Transparent;
                container.Background = Brushes.Transparent;

                container.ReleaseMouseCapture();
                if (isDragging) UpdateWidgetPositions();
                Panel.SetZIndex(container, 0);
            }
            isDragging = false;
            draggedElement = null;
            e.Handled = true;
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

        // no clue why is this here and so far it looks like it does nothing but i do not understand it so i'll leave it

        //public string GetVisualTreeAsXaml(Panel panel)
        //{
        //    StringBuilder sb = new StringBuilder();
        //    foreach (UIElement child in panel.Children)
        //    {
        //        if (child is Button btn)
        //        {
        //            sb.AppendLine($"<Button Content=\"{btn.Content}\" Margin=\"{btn.Margin}\" />");
        //        }
        //        // Add more cases for other element types if needed
        //    }
        //    return sb.ToString();
        //}

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
                    string path = (string)((Button)sender).Tag;
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = path,

                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error launching application: {ex.Message}");
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

            double snappedLeft = Math.Round(left / gridSize) * gridSize;
            double snappedTop = Math.Round(top / gridSize) * gridSize;

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
        private void AddAppButton_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new AddAppWindow();
            addWindow.Owner = this;
            if (addWindow.ShowDialog() == true)
            {
                
                string destImgPath = Path.Combine(imgPath, addWindow.AppName + ".png");
                try
                {
                    File.Copy(addWindow.ImagePath, destImgPath, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to copy image: " + ex.Message);
                    return;
                }
                // Add to JSON
                string json = File.ReadAllText(jsonFilePath);
                var apps = JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                // Default position will be in the upper left corner
                var position = new Position
                {
                    X = 20,
                    Y = 20
                };

                apps[addWindow.AppName] = new AppInfo
                {
                    Path = addWindow.AppPath,
                    Position = position
                };

                string updatedJson = JsonSerializer.Serialize(apps, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(jsonFilePath, updatedJson);

                // Add the button in real time
                CreateAppButton(addWindow.AppName, addWindow.AppPath, new Point(position.X, position.Y));
            }
        }
        //private void RemoveAppButton_Click(object sender, RoutedEventArgs e)
        //{
        //    var removeWindow = new RemoveApp();
        //    removeWindow.Owner = this;
        //    bool? removed = removeWindow.ShowDialog();
        //    if (removed == true)
        //    {
        //        Refresh_Page();
        //    }
        //}
        private void Refresh_Page()
        {
            MainCanvas.Children.Clear();
            ReadPathJson();
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