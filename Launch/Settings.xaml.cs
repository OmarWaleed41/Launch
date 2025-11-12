using System.Text.Json;
using System.Windows;
using System.IO;
using System.IO.Compression;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System;
using System.Windows.Controls.Primitives;

namespace Launch
{
    public partial class Settings : Window
    {
        #region Constants
        private const int DefaultPosition = 20;
        private const int MaxColumnsPerRow = 5;
        private const int ImageButtonWidth = 70;
        #endregion

        #region Fields - Paths
        private readonly string _baseDirectory;
        private readonly string _mainFolder;
        private readonly string _jsonFilePath;
        private readonly string _imgPath;
        private readonly string _widgetsFolder;
        private readonly string _widgetJsonPath;
        #endregion

        #region Fields - Settings
        private double _buttonSize;
        private double _gridSizeX;
        private double _gridSizeY;
        private bool _snapToGrid;
        private bool _showGrid;
        #endregion

        #region Fields - UI Controls
        private TextBox _appNameTextBox;
        private TextBox _appPathTextBox;
        private TextBox _imagePathTextBox;
        private TextBox _widgetNameTextBox;
        private TextBox _widgetWidthTextBox;
        private TextBox _widgetHeightTextBox;
        private TextBox _settingsButtonSizeTextBox;
        private TextBox _settingsGridSizeXTextBox;
        private TextBox _settingsGridSizeYTextBox;
        private CheckBox _settingsSnapToGridCheckBox;
        private CheckBox _settingsShowGridCheckBox;
        #endregion

        #region Fields - References
        private readonly MainWindow _mainWindow;
        #endregion

        #region Events
        public event EventHandler<string> UpdateRequested;
        #endregion

        #region Constructor and Initialization
        public Settings(MainWindow mainWindow)
        {
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _mainFolder = Path.Combine(_baseDirectory, "src");
            _jsonFilePath = Path.Combine(_mainFolder, "path.json");
            _widgetJsonPath = Path.Combine(_mainFolder, "widgets.json");
            _widgetsFolder = Path.Combine(_mainFolder, "Widgets");
            _imgPath = Path.Combine(_mainFolder, "imgs");

            _mainWindow = mainWindow;

            InitializeComponent();
            LoadSettings();

            Loaded += (s, e) => apps_tab(s, e);
            Icon = new BitmapImage(new Uri("pack://application:,,,/src/imgs/gui.ico", UriKind.Absolute));
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

        #region Apps Tab
        public void apps_tab(object sender, RoutedEventArgs e)
        {
            main.Children.Clear();

            var container = CreateMainContainer();

            AddAppsSection(container);
            AddSeparator(container);
            AddRemoveAppsSection(container);

            main.Children.Add(container);
        }

        private void AddAppsSection(StackPanel container)
        {
            container.Children.Add(CreateSectionHeader("Add a New App:"));

            var addGrid = CreateAddAppGrid();
            container.Children.Add(addGrid);
        }

        private Grid CreateAddAppGrid()
        {
            var grid = CreateFormGrid(4);

            // App Name
            AddFormRow(grid, 0, "App Name:", out _appNameTextBox);

            // App Path with Browse button
            AddFormRowWithButton(grid, 1, "App Path:", out _appPathTextBox, "Browse", OnBrowseAppClick);

            // Image with Browse button
            AddFormRowWithButton(grid, 2, "Image:", out _imagePathTextBox, "Browse", OnBrowseImageClick);

            // Add button
            var addButton = CreateActionButton("Add", OnAddAppClick);
            grid.Children.Add(addButton);
            Grid.SetRow(addButton, 3);
            Grid.SetColumn(addButton, 1);

            return grid;
        }

        private void AddRemoveAppsSection(StackPanel container)
        {
            container.Children.Add(CreateSectionHeader("Remove Apps:"));

            try
            {
                var apps = LoadAppsFromJson();
                if (apps == null || apps.Count == 0)
                {
                    MessageBox.Show("No apps found.");
                    return;
                }

                var appsGrid = CreateAppsDisplayGrid(apps);
                container.Children.Add(appsGrid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading JSON: {ex.Message}");
            }
        }

        private Grid CreateAppsDisplayGrid(Dictionary<string, AppInfo> apps)
        {
            var grid = CreateDisplayGrid();
            int rowIndex = 0;
            int columnIndex = 0;

            foreach (var app in apps)
            {
                if (columnIndex >= MaxColumnsPerRow)
                {
                    columnIndex = 0;
                    rowIndex++;
                }

                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var contentPanel = CreateAppDisplayPanel(app.Key);
                Grid.SetRow(contentPanel, rowIndex);
                Grid.SetColumn(contentPanel, columnIndex);
                grid.Children.Add(contentPanel);

                columnIndex++;
            }

            return grid;
        }

        private StackPanel CreateAppDisplayPanel(string appName)
        {
            var panel = new StackPanel
            {
                Width = ImageButtonWidth,
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };

            var image = LoadAppImage(appName);
            var removeButton = CreateRemoveButton(appName, OnRemoveAppClick);

            panel.Children.Add(image);
            panel.Children.Add(removeButton);

            return panel;
        }

        private Image LoadAppImage(string appName)
        {
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 10, 0, 10)
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

            return image;
        }

        private Dictionary<string, AppInfo> LoadAppsFromJson()
        {
            var json = File.ReadAllText(_jsonFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private void OnBrowseAppClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executable Files|*.exe;*.bat;*.cmd|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _appPathTextBox.Text = dialog.FileName;
            }
        }

        private void OnBrowseImageClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp"
            };

            if (dialog.ShowDialog() == true)
            {
                _imagePathTextBox.Text = dialog.FileName;
            }
        }

        private void OnAddAppClick(object sender, RoutedEventArgs e)
        {
            if (!ValidateAppInputs())
            {
                MessageBox.Show("Please fill all fields.");
                return;
            }

            var appName = _appNameTextBox.Text.Trim();
            var appPath = _appPathTextBox.Text.Trim();
            var imagePath = _imagePathTextBox.Text.Trim();

            if (!CopyAppImage(imagePath, appName))
                return;

            AddAppToJson(appName, appPath);
            _mainWindow.CreateAppButton(appName, appPath, new Point(DefaultPosition, DefaultPosition));

            apps_tab(sender, e);
        }

        private bool ValidateAppInputs()
        {
            return !string.IsNullOrWhiteSpace(_appNameTextBox.Text) &&
                   !string.IsNullOrWhiteSpace(_appPathTextBox.Text) &&
                   !string.IsNullOrWhiteSpace(_imagePathTextBox.Text);
        }

        private bool CopyAppImage(string sourcePath, string appName)
        {
            var destinationPath = Path.Combine(_imgPath, $"{appName}.png");
            try
            {
                File.Copy(sourcePath, destinationPath, true);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy image: {ex.Message}");
                return false;
            }
        }

        private void AddAppToJson(string appName, string appPath)
        {
            var apps = LoadAppsFromJson();
            apps[appName] = new AppInfo
            {
                Path = appPath,
                Position = new Position { X = DefaultPosition, Y = DefaultPosition }
            };

            SaveAppsToJson(apps);
        }

        private void SaveAppsToJson(Dictionary<string, AppInfo> apps)
        {
            var json = JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_jsonFilePath, json);
        }

        public void OnRemoveAppClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string appName)
                return;

            if (string.IsNullOrEmpty(appName))
                return;

            RemoveAppFromJson(appName);
            DeleteAppImage(appName);
            UpdateRequested?.Invoke(this, $"remove:{appName}:button");

            apps_tab(sender, e);
        }

        private void RemoveAppFromJson(string appName)
        {
            var apps = LoadAppsFromJson();
            if (apps.Remove(appName))
            {
                SaveAppsToJson(apps);
            }
        }

        private void DeleteAppImage(string appName)
        {
            var imagePath = Path.Combine(_imgPath, $"{appName}.png");
            try
            {
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

        #region Widgets Tab
        public void widgets_tab(object sender, RoutedEventArgs e)
        {
            main.Children.Clear();

            var container = CreateMainContainer();

            AddWidgetsSection(container);
            AddSeparator(container);
            AddWidgetSettingsSection(container);

            main.Children.Add(container);
        }

        private void AddWidgetsSection(StackPanel container)
        {
            container.Children.Add(CreateSectionHeader("Add a New Widget"));

            var addGrid = CreateAddWidgetGrid();
            container.Children.Add(addGrid);
        }

        private Grid CreateAddWidgetGrid()
        {
            var grid = CreateFormGrid(4);

            // Widget Path with Browse button
            AddFormRowWithButton(grid, 0, "Widget:", out _widgetNameTextBox, "Browse", OnBrowseWidgetClick);

            // Widget Width
            AddFormRow(grid, 1, "Widget Width:", out _widgetWidthTextBox, 50);

            // Widget Height
            AddFormRow(grid, 2, "Widget Height:", out _widgetHeightTextBox, 50);

            // Add button
            var addButton = CreateActionButton("Add", OnAddWidgetClick);
            grid.Children.Add(addButton);
            Grid.SetRow(addButton, 3);
            Grid.SetColumn(addButton, 1);

            return grid;
        }

        private void AddWidgetSettingsSection(StackPanel container)
        {
            container.Children.Add(CreateSectionHeader("Widgets Settings", 17));

            try
            {
                var widgets = LoadWidgetsFromJson();
                if (widgets == null || widgets.Count == 0)
                {
                    MessageBox.Show("No widgets found.");
                    return;
                }

                var widgetsGrid = CreateWidgetsDisplayGrid(widgets);
                container.Children.Add(widgetsGrid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading widgets JSON: {ex.Message}");
            }
        }

        private Grid CreateWidgetsDisplayGrid(Dictionary<string, Widget> widgets)
        {
            var grid = CreateDisplayGrid();
            int rowIndex = 0;
            int columnIndex = 0;

            foreach (var widget in widgets)
            {
                if (columnIndex >= MaxColumnsPerRow)
                {
                    columnIndex = 0;
                    rowIndex++;
                }

                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var contentPanel = CreateWidgetDisplayPanel(widget.Key, widget.Value, widgets);
                Grid.SetRow(contentPanel, rowIndex);
                Grid.SetColumn(contentPanel, columnIndex);
                grid.Children.Add(contentPanel);

                columnIndex++;
            }

            return grid;
        }

        private StackPanel CreateWidgetDisplayPanel(string widgetName, Widget widget, Dictionary<string, Widget> allWidgets)
        {
            var panel = new StackPanel
            {
                Width = ImageButtonWidth,
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };

            var nameLabel = CreateWidgetNameLabel(widgetName);
            var toggleButton = CreateWidgetToggleButton(widgetName, widget.Status, allWidgets);
            var removeButton = CreateRemoveButton(widgetName, OnRemoveWidgetClick);

            panel.Children.Add(nameLabel);
            panel.Children.Add(toggleButton);
            panel.Children.Add(removeButton);

            return panel;
        }

        private TextBlock CreateWidgetNameLabel(string widgetName)
        {
            return new TextBlock
            {
                Text = widgetName,
                FontSize = 16,
                Margin = new Thickness(2, 10, 2, 10),
                Foreground = Brushes.White
            };
        }

        private Button CreateWidgetToggleButton(string widgetName, bool isEnabled, Dictionary<string, Widget> widgets)
        {
            var button = new Button
            {
                Content = isEnabled ? "Enabled" : "Disabled",
                Width = ImageButtonWidth,
                Margin = new Thickness(0, 5, 0, 5),
                Tag = widgetName,
                Style = (Style)FindResource("RoundedButtonStyle")
            };

            button.Loaded += (s, e) => UpdateToggleButtonAppearance(button, widgets);
            button.Click += (s, e) => OnToggleWidgetClick(button, widgets);

            return button;
        }

        private void UpdateToggleButtonAppearance(Button button, Dictionary<string, Widget> widgets)
        {
            if (button.Tag is not string key || !widgets.ContainsKey(key))
                return;

            button.Background = widgets[key].Status
                ? new SolidColorBrush(Color.FromRgb(0, 255, 125))
                : new SolidColorBrush(Color.FromRgb(125, 125, 125));
        }

        private void OnToggleWidgetClick(Button button, Dictionary<string, Widget> widgets)
        {
            if (button.Tag is not string key || !widgets.ContainsKey(key))
                return;

            widgets[key].Status = !widgets[key].Status;
            button.Content = widgets[key].Status ? "Enabled" : "Disabled";

            UpdateToggleButtonAppearance(button, widgets);
            SaveWidgetsToJson(widgets);

            if (!widgets[key].Status)
            {
                UpdateRequested?.Invoke(this, $"remove:{key}:widget");
            }
            else
            {
                _mainWindow.InitWebView(widgets[key], key);
            }
        }

        private Dictionary<string, Widget> LoadWidgetsFromJson()
        {
            var json = File.ReadAllText(_widgetJsonPath);
            return JsonSerializer.Deserialize<Dictionary<string, Widget>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private void SaveWidgetsToJson(Dictionary<string, Widget> widgets)
        {
            var json = JsonSerializer.Serialize(widgets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_widgetJsonPath, json);
        }

        private void OnBrowseWidgetClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "ZIP Files (*.zip)|*.zip|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _widgetNameTextBox.Text = dialog.FileName;
            }
        }

        private void OnAddWidgetClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_widgetNameTextBox.Text))
            {
                MessageBox.Show("Please fill the widget name.");
                return;
            }

            var widgetPath = _widgetNameTextBox.Text.Trim();
            var widgetName = Path.GetFileNameWithoutExtension(widgetPath);
            var widgetWidth = _widgetWidthTextBox.Text.Trim();
            var widgetHeight = _widgetHeightTextBox.Text.Trim();

            if (!ExtractWidgetFiles(widgetPath))
                return;

            AddWidgetToJson(widgetName, widgetWidth, widgetHeight);

            var widgets = LoadWidgetsFromJson();
            _mainWindow.InitWebView(widgets[widgetName], widgetName);

            widgets_tab(sender, e);
        }

        private bool ExtractWidgetFiles(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                foreach (var entry in archive.Entries)
                {
                    var destinationPath = Path.Combine(_widgetsFolder, entry.FullName);
                    var destinationDir = Path.GetDirectoryName(destinationPath);

                    if (!Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        entry.ExtractToFile(destinationPath, true);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to extract widget: {ex.Message}");
                return false;
            }
        }

        private void AddWidgetToJson(string widgetName, string width, string height)
        {
            var widgets = LoadWidgetsFromJson();
            widgets[widgetName] = new Widget
            {
                Size = new Size
                {
                    Width = Convert.ToDouble(width),
                    Height = Convert.ToDouble(height)
                },
                Position = new Position { X = DefaultPosition, Y = DefaultPosition },
                Status = true
            };

            SaveWidgetsToJson(widgets);
        }

        public void OnRemoveWidgetClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string widgetName)
                return;

            if (string.IsNullOrEmpty(widgetName))
                return;

            RemoveWidgetFromJson(widgetName);
            UpdateRequested?.Invoke(this, $"remove:{widgetName}:widget");

            widgets_tab(sender, e);
        }

        private void RemoveWidgetFromJson(string widgetName)
        {
            var widgets = LoadWidgetsFromJson();
            if (widgets.Remove(widgetName))
            {
                SaveWidgetsToJson(widgets);
            }
        }
        #endregion

        #region Settings Tab
        public void settings_tab(object sender, RoutedEventArgs e)
        {
            main.Children.Clear();

            var container = CreateMainContainer();
            container.Children.Add(CreateSectionHeader("Settings"));

            var settingsGrid = CreateSettingsGrid();
            container.Children.Add(settingsGrid);

            main.Children.Add(container);

            InitializeSettingsValues();
        }

        private Grid CreateSettingsGrid()
        {
            var grid = CreateFormGrid(5);

            // Button Size
            AddFormRow(grid, 0, "Button Size:", out _settingsButtonSizeTextBox, 50, _buttonSize.ToString());
            _settingsButtonSizeTextBox.TextChanged += OnButtonSizeChanged;

            // Grid Size X
            AddFormRow(grid, 1, "Grid Size X:", out _settingsGridSizeXTextBox, 50, _gridSizeX.ToString());
            _settingsGridSizeXTextBox.TextChanged += OnGridSizeXChanged;

            // Grid Size Y
            AddFormRow(grid, 2, "Grid Size Y:", out _settingsGridSizeYTextBox, 50, _gridSizeY.ToString());
            _settingsGridSizeYTextBox.TextChanged += OnGridSizeYChanged;

            // Snap to Grid
            AddCheckBoxRow(grid, 3, "Snap to Grid:", out _settingsSnapToGridCheckBox);
            _settingsSnapToGridCheckBox.Checked += OnSnapToGridChanged;
            _settingsSnapToGridCheckBox.Unchecked += OnSnapToGridChanged;

            // Show Grid
            AddCheckBoxRow(grid, 4, "Show Grid:", out _settingsShowGridCheckBox);
            _settingsShowGridCheckBox.Checked += OnShowGridChanged;
            _settingsShowGridCheckBox.Unchecked += OnShowGridChanged;

            return grid;
        }

        private void InitializeSettingsValues()
        {
            _settingsShowGridCheckBox.IsChecked = Properties.Settings.Default.ShowGrid;
            _settingsSnapToGridCheckBox.IsChecked = Properties.Settings.Default.SnapToGrid;
        }

        private void OnButtonSizeChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(_settingsButtonSizeTextBox.Text, out int newSize))
            {
                Properties.Settings.Default.ButtonSize = newSize;
                Properties.Settings.Default.Save();
            }
        }

        private void OnGridSizeXChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(_settingsGridSizeXTextBox.Text, out int newSize))
            {
                Properties.Settings.Default.GridSizeX = newSize;
                Properties.Settings.Default.Save();
            }
        }

        private void OnGridSizeYChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(_settingsGridSizeYTextBox.Text, out int newSize))
            {
                Properties.Settings.Default.GridSizeY = newSize;
                Properties.Settings.Default.Save();
            }
        }

        private void OnShowGridChanged(object sender, RoutedEventArgs e)
        {
            _showGrid = _settingsShowGridCheckBox.IsChecked == true;
            Properties.Settings.Default.ShowGrid = _showGrid;
            Properties.Settings.Default.Save();
            _mainWindow.UpdateGrid();
        }

        private void OnSnapToGridChanged(object sender, RoutedEventArgs e)
        {
            _snapToGrid = _settingsSnapToGridCheckBox.IsChecked == true;
            Properties.Settings.Default.SnapToGrid = _snapToGrid;
            Properties.Settings.Default.Save();
        }
        #endregion

        #region UI Helper Methods
        private StackPanel CreateMainContainer()
        {
            return new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10)
            };
        }

        private TextBlock CreateSectionHeader(string text, int fontSize = 16)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                Margin = new Thickness(10),
                Foreground = Brushes.White
            };
        }

        private void AddSeparator(StackPanel container)
        {
            container.Children.Add(new Separator
            {
                Margin = new Thickness(0, 10, 0, 10),
                Background = Brushes.Gray,
                Height = 1
            });
        }

        private Grid CreateFormGrid(int rows)
        {
            var grid = new Grid { Margin = new Thickness(10) };

            for (int i = 0; i < rows; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            return grid;
        }

        private Grid CreateDisplayGrid()
        {
            var grid = new Grid { Margin = new Thickness(10) };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            return grid;
        }

        private void AddFormRow(Grid grid, int row, string labelText, out TextBox textBox, double? width = null, string defaultText = "")
        {
            var label = new TextBlock
            {
                Text = labelText,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(label);
            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);

            textBox = new TextBox
            {
                Text = defaultText,
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedTextBox"),
                VerticalAlignment = VerticalAlignment.Center
            };

            if (width.HasValue)
            {
                textBox.Width = width.Value;
                textBox.HorizontalAlignment = HorizontalAlignment.Left;
            }

            grid.Children.Add(textBox);
            Grid.SetRow(textBox, row);
            Grid.SetColumn(textBox, 1);
        }

        private void AddFormRowWithButton(Grid grid, int row, string labelText, out TextBox textBox,
            string buttonText, RoutedEventHandler buttonClickHandler)
        {
            AddFormRow(grid, row, labelText, out textBox);

            var button = new Button
            {
                Content = buttonText,
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedButtonStyle")
            };
            button.Click += buttonClickHandler;

            grid.Children.Add(button);
            Grid.SetRow(button, row);
            Grid.SetColumn(button, 2);
        }

        private void AddCheckBoxRow(Grid grid, int row, string labelText, out CheckBox checkBox)
        {
            var label = new TextBlock
            {
                Text = labelText,
                FontSize = 16,
                Margin = new Thickness(10),
                Foreground = Brushes.White
            };
            grid.Children.Add(label);
            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);

            checkBox = new CheckBox
            {
                Margin = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            grid.Children.Add(checkBox);
            Grid.SetRow(checkBox, row);
            Grid.SetColumn(checkBox, 1);
        }

        private Button CreateActionButton(string content, RoutedEventHandler clickHandler)
        {
            var button = new Button
            {
                Content = content,
                Width = 80,
                Height = 30,
                Margin = new Thickness(10),
                Style = (Style)FindResource("RoundedButtonStyle")
            };
            button.Click += clickHandler;
            return button;
        }

        private Button CreateRemoveButton(string tag, RoutedEventHandler clickHandler)
        {
            var button = new Button
            {
                Content = "Remove",
                Style = (Style)FindResource("RoundedButtonStyle"),
                Tag = tag
            };
            button.Click += clickHandler;
            return button;
        }
        #endregion

        #region Window Events
        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion
    }
}