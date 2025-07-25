using System.Text.Json;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Media.Imaging;
using System.Windows.Input;

namespace Launch_2
{
    public partial class Settings : Window
    {

        string Base = System.AppDomain.CurrentDomain.BaseDirectory;

        string MainFolder;
        string jsonFilePath;
        string imgPath;

        public string SetStatus { get; private set; }
        public string AppName { get; private set; }
        public string AppPath { get; private set; }
        public string ImagePath { get; private set; }

        private TextBox AppNameBox;
        private TextBox AppPathBox;
        private TextBox ImagePathBox;


        public Settings()
        {
            MainFolder = Path.Combine(Base, "src");
            jsonFilePath = Path.Combine(MainFolder, "path.json");
            imgPath = Path.Combine(MainFolder, "imgs");
            InitializeComponent();
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
                MessageBox.Show($"Error reading JSON: {ex.Message}");
            }

            main.Children.Add(container);
        }

        public void widgets_tab(object sender, RoutedEventArgs e)
        {
            main.Children.Clear();

            Button apps = new Button
            {
                Width = 100,
                Height = 50,
                Margin = new Thickness(10),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            apps.Content = "Widgets";
            main.Children.Add(apps);
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

            SetStatus = "add";
            AppName = AppNameBox.Text.Trim();
            AppPath = AppPathBox.Text.Trim();
            ImagePath = ImagePathBox.Text.Trim();
            DialogResult = true;
        }
        public void Remove_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("clicked");
            Button btn = sender as Button;
            if (btn == null) return;

            string appName = btn.Tag as string;
            if (string.IsNullOrEmpty(appName)) return;

            // Read and deserialize the JSON
            string json = File.ReadAllText(jsonFilePath);
            var apps = JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Remove the app
            if (apps.Remove(appName))
            {
                // Serialize and save back to file
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

                SetStatus = "remove";
                this.DialogResult = true;
            }
            Refresh_Apps(sender, e);
        }
        public void Refresh_Apps(object sender, RoutedEventArgs e)
        {
            main.Children.Clear();
            apps_tab(sender, e);
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
