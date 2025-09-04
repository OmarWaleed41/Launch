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

namespace Launch_2
{
    public partial class Settings : Window
    {

        string Base = System.AppDomain.CurrentDomain.BaseDirectory;

        string MainFolder;
        string jsonFilePath;
        string imgPath;
        string widgetsFolder;
        string widgetJson;

        double button_size;
        double gridSizeX;
        double gridSizeY;

        bool snapToGrid;
        bool showGrid = false;

        public string SetStatus { get; private set; }
        public string AppName { get; private set; }
        public string AppPath { get; private set; }
        public string ImagePath { get; private set; }
        public string WidgetPath { get; private set; }
        public string WidgetName { get; private set; }
        public string WidgetWidth { get; private set; }
        public string WidgetHeight { get; private set; }

        private TextBox AppNameBox;
        private TextBox AppPathBox;
        private TextBox ImagePathBox;
        private TextBox WidgetNameBox;
        private TextBox WidgetSize_X_Box;
        private TextBox WidgetSize_Y_Box;
        private TextBox Settings_ButtonSize;
        private CheckBox Settings_SnapToGrid;
        private CheckBox Settings_ShowGrid;
        private TextBox Settings_GridSizeX;
        private TextBox Settings_GridSizeY;

        public event EventHandler<string> UpdateRequested;

        private MainWindow _MainWindow;


        public Settings(MainWindow mainWindow)
        {
            MainFolder = Path.Combine(Base, "src");
            jsonFilePath = Path.Combine(MainFolder, "path.json");
            widgetJson = Path.Combine(MainFolder, "widgets.json");
            widgetsFolder = Path.Combine(MainFolder, "Widgets");
            imgPath = Path.Combine(MainFolder, "imgs");

            _MainWindow = mainWindow;

            InitializeComponent();
            ReadSettingsJson();
        }
        private void ReadSettingsJson()
        {
            button_size = Properties.Settings.Default.ButtonSize;
            snapToGrid = Properties.Settings.Default.SnapToGrid;
            gridSizeX = Properties.Settings.Default.GridSizeX;
            gridSizeY = Properties.Settings.Default.GridSizeY;
            showGrid = Properties.Settings.Default.ShowGrid;
        }
        public void apps_tab(object sender, RoutedEventArgs e)
        {
            //--------------------------
            main.Children.Clear();
            //--------------------------

            StackPanel container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10)
            };


            // Add a new app section
            TextBlock addText = new TextBlock
            {
                Text = "Add a New App:",
                FontSize = 16,
                Margin = new Thickness(10),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"))
            };
            container.Children.Add(addText);

            Grid add_grid = new Grid
            {
                Margin = new Thickness(10)
            };

            add_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            add_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            add_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            add_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            add_grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            add_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            add_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            add_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            add_grid.Children.Add(new TextBlock
            {
                Text = "App Name:",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White")),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(add_grid.Children[^1], 0);
            Grid.SetColumn(add_grid.Children[^1], 0);

            AppNameBox = new TextBox
            {
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedTextBox")
            };
            add_grid.Children.Add(AppNameBox);
            Grid.SetRow(AppNameBox, 0);
            Grid.SetColumn(AppNameBox, 1);

            add_grid.Children.Add(new TextBlock
            {
                Text = "App Path:",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White")),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(add_grid.Children[^1], 1);
            Grid.SetColumn(add_grid.Children[^1], 0);

            AppPathBox = new TextBox
            {
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedTextBox")
            };
            add_grid.Children.Add(AppPathBox);
            Grid.SetRow(AppPathBox, 1);
            Grid.SetColumn(AppPathBox, 1);


            Button browseAppBtn = new Button
            {
                Content = "Browse",
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedButtonStyle")
            };
            browseAppBtn.Click += BrowseApp_Click;
            add_grid.Children.Add(browseAppBtn);
            Grid.SetRow(browseAppBtn, 1);
            Grid.SetColumn(browseAppBtn, 2);

            add_grid.Children.Add(new TextBlock
            {
                Text = "Image:",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White")),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(add_grid.Children[^1], 2);
            Grid.SetColumn(add_grid.Children[^1], 0);

            ImagePathBox = new TextBox
            {
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedTextBox")
            };
            add_grid.Children.Add(ImagePathBox);
            Grid.SetRow(ImagePathBox, 2);
            Grid.SetColumn(ImagePathBox, 1);


            Button browseImgBtn = new Button
            {
                Content = "Browse",
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedButtonStyle")
            };
            browseImgBtn.Click += BrowseImage_Click;
            add_grid.Children.Add(browseImgBtn);
            Grid.SetRow(browseImgBtn, 2);
            Grid.SetColumn(browseImgBtn, 2);

            Button addBtn = new Button
            {
                Content = "Add",
                Width = 80,
                Height = 30,
                Margin = new Thickness(10),
                Style = (Style)FindResource("RoundedButtonStyle")
            };
            addBtn.Click += Add_Click;
            add_grid.Children.Add(addBtn);
            Grid.SetRow(addBtn, 3);
            Grid.SetColumn(addBtn, 1);

            container.Children.Add(add_grid);

            // Seperator between add and remove
            Separator separator = new Separator
            {
                Margin = new Thickness(0, 10, 0, 10),
                Background = Brushes.Gray,
                Height = 1
            };
            container.Children.Add(separator);

            // Remove apps section 

            TextBlock removeText = new TextBlock
            {
                Text = "Remove Apps:",
                FontSize = 16,
                Margin = new Thickness(10),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"))
            };
            container.Children.Add(removeText);

            Grid remove_grid = new Grid
            {
                Margin = new Thickness(10)
            };
            remove_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            remove_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            remove_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            remove_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            try
            {
                string json = File.ReadAllText(jsonFilePath);
                var apps = JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apps == null || apps.Count == 0)
                {
                    MessageBox.Show("No apps found.");
                    return;
                }

                Grid appsGrid = new Grid
                {
                    Margin = new Thickness(10)
                };

                appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                int rowIndex = 0;
                int colIndex = 0;
                foreach (var app in apps)
                {
                    appsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    Image image = new Image();
                    try
                    {
                        string img = Path.Combine(imgPath, $"{app.Key}.png");
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(img, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        image.Source = bitmap;
                        image.Margin = new Thickness(0, 10, 0, 10);
                    }
                    catch
                    {
                        // Put a case here or we are screwed if something went bad
                    }
                    image.Stretch = Stretch.Uniform;
                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                    Button remove_button = new Button
                    {
                        Content = "Remove",
                        Style = (Style)FindResource("RoundedButtonStyle"),
                        Tag = app.Key
                    };
                    remove_button.Click += Remove_Click;

                    StackPanel contentPanel = new StackPanel
                    {
                        Width = 70,
                        Orientation = Orientation.Vertical,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    contentPanel.Children.Add(image);
                    contentPanel.Children.Add(remove_button);


                    if (colIndex >= 5)
                    {
                        colIndex = 0;
                        rowIndex++;
                    }

                    Grid.SetRow(contentPanel, rowIndex);
                    Grid.SetColumn(contentPanel, colIndex);
                    appsGrid.Children.Add(contentPanel);

                    colIndex++;
                }

                container.Children.Add(appsGrid); // Add below add_grid
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading JSON at adding the app: {ex.Message}");
            }

            main.Children.Add(container);
        }

        public void widgets_tab(object sender, RoutedEventArgs e)
        {
            //--------------------------
            main.Children.Clear();
            //--------------------------

            StackPanel container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10)
            };


            // Add a new app section
            TextBlock addText = new TextBlock
            {
                Text = "Add a New Widget:",
                FontSize = 16,
                Margin = new Thickness(10),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"))
            };
            container.Children.Add(addText);

            Grid add_grid = new Grid
            {
                Margin = new Thickness(10)
            };

            add_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            add_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            add_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            add_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            add_grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            add_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            add_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            add_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            add_grid.Children.Add(new TextBlock
            {
                Text = "Widget:",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White")),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(add_grid.Children[^1], 0);
            Grid.SetColumn(add_grid.Children[^1], 0);

            WidgetNameBox = new TextBox
            {
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedTextBox")
            };
            add_grid.Children.Add(WidgetNameBox);
            Grid.SetRow(WidgetNameBox, 0);
            Grid.SetColumn(WidgetNameBox, 1);

            Button browseWidgetBtn = new Button
            {
                Content = "Browse",
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedButtonStyle")
            };
            browseWidgetBtn.Click += BrowseWidget_Click;
            add_grid.Children.Add(browseWidgetBtn);
            Grid.SetRow(browseWidgetBtn, 0);
            Grid.SetColumn(browseWidgetBtn, 2);

            add_grid.Children.Add(new TextBlock
            {
                Text = "Widget Width:",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White")),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(add_grid.Children[^1], 1);
            Grid.SetColumn(add_grid.Children[^1], 0);

            WidgetSize_X_Box = new TextBox
            {
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedTextBox"),
                Width = 50,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            add_grid.Children.Add(WidgetSize_X_Box);
            Grid.SetRow(WidgetSize_X_Box, 1);
            Grid.SetColumn(WidgetSize_X_Box, 1);

            add_grid.Children.Add(new TextBlock
            {
                Text = "Widget Height:",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White")),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(add_grid.Children[^1], 2);
            Grid.SetColumn(add_grid.Children[^1], 0);

            WidgetSize_Y_Box = new TextBox
            {
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedTextBox"),
                Width = 50,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            add_grid.Children.Add(WidgetSize_Y_Box);
            Grid.SetRow(WidgetSize_Y_Box, 2);
            Grid.SetColumn(WidgetSize_Y_Box, 1);

            Button addBtn = new Button
            {
                Content = "Add",
                Width = 80,
                Height = 30,
                Margin = new Thickness(10),
                Style = (Style)FindResource("RoundedButtonStyle")
            };
            addBtn.Click += AddWidget_Click;
            add_grid.Children.Add(addBtn);
            Grid.SetRow(addBtn, 3);
            Grid.SetColumn(addBtn, 1);

            container.Children.Add(add_grid);

            // Seperator between add and remove
            Separator separator = new Separator
            {
                Margin = new Thickness(0, 10, 0, 10),
                Background = Brushes.Gray,
                Height = 1
            };
            container.Children.Add(separator);

            // Remove apps section 

            TextBlock removeText = new TextBlock
            {
                Text = "Remove Widgets:",
                FontSize = 16,
                Margin = new Thickness(10),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"))
            };
            container.Children.Add(removeText);

            Grid remove_grid = new Grid
            {
                Margin = new Thickness(10)
            };
            remove_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            remove_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            remove_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            remove_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            try
            {
                string json = File.ReadAllText(widgetJson);
                var widgets = JsonSerializer.Deserialize<Dictionary<string, Widget>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (widgets == null || widgets.Count == 0)
                {
                    MessageBox.Show("No apps found.");
                    return;
                }

                Grid widgetsGrid = new Grid
                {
                    Margin = new Thickness(10)
                };

                widgetsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                widgetsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                widgetsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                widgetsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                widgetsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                int rowIndex = 0;
                int colIndex = 0;
                foreach (var widget in widgets)
                {
                    widgetsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    TextBlock Widget_Name = new TextBlock
                    {
                        Text = widget.Key,
                        FontSize = 16,
                        Margin = new Thickness(2,10,2,10),
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"))
                    };

                    Button remove_button = new Button
                    {
                        Content = "Remove",
                        Style = (Style)FindResource("RoundedButtonStyle"),
                        Tag = widget.Key
                    };
                    remove_button.Click += Remove_Widget_Click;

                    StackPanel contentPanel = new StackPanel
                    {
                        Width = 70,
                        Orientation = Orientation.Vertical,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    contentPanel.Children.Add(Widget_Name);
                    contentPanel.Children.Add(remove_button);


                    if (colIndex >= 5)
                    {
                        colIndex = 0;
                        rowIndex++;
                    }

                    Grid.SetRow(contentPanel, rowIndex);
                    Grid.SetColumn(contentPanel, colIndex);
                    widgetsGrid.Children.Add(contentPanel);

                    colIndex++;
                }

                container.Children.Add(widgetsGrid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading JSON at adding the widget: {ex.Message}");
            }

            main.Children.Add(container);
        }
        private void settings_tab(object sender, RoutedEventArgs e)
        {
            //--------------------------
            main.Children.Clear();
            //--------------------------
            StackPanel container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10)
            };

            TextBlock settings_text = new TextBlock
            {
                Text = "Settings",
                FontSize = 16,
                Margin = new Thickness(10),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"))
            };
            container.Children.Add(settings_text);

            Grid settings_grid = new Grid
            {
                Margin = new Thickness(10)
            };
            settings_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            settings_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            settings_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            settings_grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            settings_grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            settings_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            settings_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            settings_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            settings_grid.Children.Add(new TextBlock
            {
                Text = "Button Size:",
                FontSize = 16,
                Margin = new Thickness(10),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"))
            });
            Grid.SetRow(settings_grid.Children[^1], 0);
            Grid.SetColumn(settings_grid.Children[^1], 0);

            Settings_ButtonSize = new TextBox
            {
                Text = $"{button_size}",
                Margin = new Thickness(5),
                Width = 50,
                Style = (Style)FindResource("RoundedTextBox"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Settings_ButtonSize.TextChanged += (s, e) =>
            {
                int.TryParse(Settings_ButtonSize.Text, out int newSize);
                Properties.Settings.Default.ButtonSize = newSize;
                Properties.Settings.Default.Save();
            };
            settings_grid.Children.Add(Settings_ButtonSize);
            Grid.SetRow(Settings_ButtonSize, 0);
            Grid.SetColumn(Settings_ButtonSize, 1);

            settings_grid.Children.Add(new TextBlock
            {
                Text = "Snap to Grid:",
                FontSize = 16,
                Margin = new Thickness(10),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"))
            });
            Grid.SetRow(settings_grid.Children[^1], 1);
            Grid.SetColumn(settings_grid.Children[^1], 0);

            Settings_SnapToGrid = new CheckBox
            {
                Margin = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            settings_grid.Children.Add(Settings_SnapToGrid);
            Grid.SetRow(Settings_SnapToGrid, 1);
            Grid.SetColumn(Settings_SnapToGrid, 1);
            Settings_SnapToGrid.Checked += Settings_SnapToGrid_Checked;
            Settings_SnapToGrid.Unchecked += Settings_SnapToGrid_Checked;

            settings_grid.Children.Add(new TextBlock
            {
                Text = "Grid Size X:",
                FontSize = 16,
                Margin = new Thickness(10),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"))
            });
            Grid.SetRow(settings_grid.Children[^1], 2);
            Grid.SetColumn(settings_grid.Children[^1], 0);

            Settings_GridSizeX = new TextBox
            {
                Text = $"{gridSizeX}",
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedTextBox"),
                Width = 50,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Settings_GridSizeX.TextChanged += (s, e) =>
            {
                int.TryParse(Settings_GridSizeX.Text, out int newSize);
                Debug.WriteLine(newSize);
                Properties.Settings.Default.GridSizeX = newSize;
                Properties.Settings.Default.Save();
            };
            settings_grid.Children.Add(Settings_GridSizeX);
            Grid.SetRow(Settings_GridSizeX, 2);
            Grid.SetColumn(Settings_GridSizeX, 1);

            settings_grid.Children.Add(new TextBlock
            {
                Text = "Grid Size Y:",
                FontSize = 16,
                Margin = new Thickness(10),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"))
            });
            Grid.SetRow(settings_grid.Children[^1], 3);
            Grid.SetColumn(settings_grid.Children[^1], 0);

            Settings_GridSizeY = new TextBox
            {
                Text = $"{gridSizeY}",
                Margin = new Thickness(5),
                Style = (Style)FindResource("RoundedTextBox"),
                Width = 50,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Settings_GridSizeY.TextChanged += (s, e) =>
            {
                int.TryParse(Settings_GridSizeY.Text, out int newSize);
                Debug.WriteLine(newSize);
                Properties.Settings.Default.GridSizeY = newSize;
                Properties.Settings.Default.Save();
            };
            settings_grid.Children.Add(Settings_GridSizeY);
            Grid.SetRow(Settings_GridSizeY, 3);
            Grid.SetColumn(Settings_GridSizeY, 1);

            settings_grid.Children.Add(new TextBlock
            {
                Text = "Show Grid:",
                FontSize = 16,
                Margin = new Thickness(10),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"))
            });
            Grid.SetRow(settings_grid.Children[^1], 4);
            Grid.SetColumn(settings_grid.Children[^1], 0);

            Settings_ShowGrid = new CheckBox
            {
                Margin = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            settings_grid.Children.Add(Settings_ShowGrid);
            Grid.SetRow(Settings_ShowGrid, 4);
            Grid.SetColumn(Settings_ShowGrid, 1);
            Settings_ShowGrid.Checked += Settings_ShowGrid_Checked;
            Settings_ShowGrid.Unchecked += Settings_ShowGrid_Checked;

            container.Children.Add(settings_grid);

            main.Children.Add(container);

            Settings_ShowGrid.IsChecked = Properties.Settings.Default.ShowGrid;
            Settings_SnapToGrid.IsChecked = Properties.Settings.Default.SnapToGrid;

        }

        private void Settings_ShowGrid_Checked(object sender, RoutedEventArgs e)
        {
            showGrid = Settings_ShowGrid.IsChecked == true;
            //MessageBox.Show(Convert.ToString(showGrid));
            Properties.Settings.Default.ShowGrid = showGrid;
            Properties.Settings.Default.Save();
            _MainWindow.UpdateGrid();
        }
        private void Settings_SnapToGrid_Checked(object sender, RoutedEventArgs e)
        {
            snapToGrid = Settings_SnapToGrid.IsChecked == true;
            //MessageBox.Show(Convert.ToString(showGrid));
            Properties.Settings.Default.SnapToGrid = snapToGrid;
            Properties.Settings.Default.Save();
            //_MainWindow.Refresh_Page();
            //MessageBox.Show((Convert.ToString(snapToGrid)));
        }

        private void BrowseApp_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Executable Files|*.exe;*.bat;*.cmd|All Files|*.*";
            if (dlg.ShowDialog() == true)
            {
                AppPathBox.Text = dlg.FileName;
            }
        }

        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
            if (dlg.ShowDialog() == true)
            {
                ImagePathBox.Text = dlg.FileName;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AppNameBox.Text) ||
                string.IsNullOrWhiteSpace(AppPathBox.Text) ||
                string.IsNullOrWhiteSpace(ImagePathBox.Text))
            {
                MessageBox.Show("Please fill all fields.");
                return;
            }

            //SetStatus = "add";
            AppName = AppNameBox.Text.Trim();
            AppPath = AppPathBox.Text.Trim();
            ImagePath = ImagePathBox.Text.Trim();

                // Add App logic
            string destImgPath = Path.Combine(imgPath, AppName + ".png");
            try
            {
                File.Copy(ImagePath, destImgPath, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to copy image: " + ex.Message);
                return;
            }

            string json = File.ReadAllText(jsonFilePath);
            var apps = JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json);
            var position = new Position { X = 20, Y = 20 };

            apps[AppName] = new AppInfo
            {
                Path = AppPath,
                Position = position
            };
            string updatedJson = JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonFilePath, updatedJson);
            
            UpdateRequested?.Invoke(this, "add");
            ////DialogResult = true;
            Refresh_Apps(sender, e);
        }
        public void Remove_Click(object sender, RoutedEventArgs e)
        {

            //MessageBox.Show("clicked");

            Button btn = sender as Button;
            if (btn == null) return;

            string appName = btn.Tag as string;
            if (string.IsNullOrEmpty(appName)) return;

            string json = File.ReadAllText(jsonFilePath);
            var apps = JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (apps.Remove(appName))
            {
                string updatedJson = JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonFilePath, updatedJson);

                string img = Path.Combine(imgPath, appName + ".png");

                try
                {
                    File.Delete(img);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to delete image: " + ex.Message);
                }

                UpdateRequested?.Invoke(this, "remove");
                //SetStatus = "remove";
                //this.DialogResult = true;
            }
            Refresh_Apps(sender, e);
        }
        public void Remove_Widget_Click(object sender, RoutedEventArgs e)
        {

            //MessageBox.Show("clicked");

            Button btn = sender as Button;
            if (btn == null) return;

            string widgetName = btn.Tag as string;
            if (string.IsNullOrEmpty(widgetName)) return;

            string json = File.ReadAllText(widgetJson);
            var widgets = JsonSerializer.Deserialize<Dictionary<string, Widget>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (widgets.Remove(widgetName))
            {
                string updatedJson = JsonSerializer.Serialize(widgets, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(widgetJson, updatedJson);

                UpdateRequested?.Invoke(this, "remove");
                //SetStatus = "remove";
                //this.DialogResult = true;
            }
            Refresh_Widgets(sender, e);
        }
        public void BrowseWidget_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "ZIP Files (*.zip)|*.zip|All Files (*.*)|*.*";
            if (dlg.ShowDialog() == true)
            {
                WidgetNameBox.Text = dlg.FileName;
            }
        }
        private void AddWidget_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(WidgetNameBox.Text))
            {
                MessageBox.Show("Please fill the widget name.");
                return;
            }
            //SetStatus = "add_widget";
            WidgetPath = WidgetNameBox.Text.Trim();
            WidgetName = Path.GetFileNameWithoutExtension(WidgetPath);
            WidgetWidth = WidgetSize_X_Box.Text.Trim();
            WidgetHeight = WidgetSize_Y_Box.Text.Trim();

            // Add Widget logic
            using (ZipArchive archive = ZipFile.OpenRead(WidgetPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.Combine(widgetsFolder, entry.FullName);
                    string destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDir)) Directory.CreateDirectory(destinationDir);

                    if (!string.IsNullOrEmpty(entry.Name))
                        entry.ExtractToFile(destinationPath, true);
                }
            }

            //string rawText = File.ReadAllText(WidgetPath);
            //MessageBox.Show(rawText);

            var widgets = JsonSerializer.Deserialize<Dictionary<string, Widget>>(File.ReadAllText(widgetJson));
            widgets[WidgetName] = new Widget
            {
                Size = new Size
                {
                    Width = Convert.ToDouble(WidgetWidth),
                    Height = Convert.ToDouble(WidgetHeight)
                },
                Position = new Position { X = 20, Y = 20 }
            };
            string updatedWidgetJson = JsonSerializer.Serialize(widgets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(widgetJson, updatedWidgetJson);


            //DialogResult = true;
            UpdateRequested?.Invoke(this, "add_widget");
            Refresh_Widgets(sender, e);
        }
        public void Refresh_Apps(object sender, RoutedEventArgs e)
        {
            main.Children.Clear();
            apps_tab(sender, e);
        }
        public void Refresh_Widgets(object sender, RoutedEventArgs e)
        {
            main.Children.Clear();
            widgets_tab(sender, e);
        }
        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
