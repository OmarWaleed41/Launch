using System.Text.Json;
using System.Windows;
using System.IO;
using System.Windows.Controls;


namespace Launch_2
{
    public partial class RemoveApp : Window
    {
        string Base = System.AppDomain.CurrentDomain.BaseDirectory;

        string MainFolder;
        string jsonFilePath;
        string imgPath;
        public RemoveApp()
        {
            MainFolder = Path.Combine(Base, "src");
            jsonFilePath = Path.Combine(MainFolder, "path.json");
            imgPath = Path.Combine(MainFolder, "imgs");
            InitializeComponent();
            this.Loaded += (s, e) => ShowApps();
        }
        public void ShowApps()
        {
            string json = File.ReadAllText(jsonFilePath);
            var apps = JsonSerializer.Deserialize<Dictionary<string, AppInfo>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Clear previous items
            //AppsPanel.Children.Clear();

            foreach (var app in apps)
            {
                string name = app.Key;
                // Create a button for each app
                Button btn = new Button
                {
                    Content = name,
                    Margin = new Thickness(5),
                    Tag = name // Store the app name for later use
                };
                btn.Click += RemoveApp_Click; // Attach event handler

                AppsPanel.Children.Add(btn);
            }
        }
        public void RemoveApp_Click(object sender, RoutedEventArgs e)
        {
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

                // Optionally, remove the button from the UI
                AppsPanel.Children.Remove(btn);

                this.DialogResult = true;
            }
        }
    }
}
