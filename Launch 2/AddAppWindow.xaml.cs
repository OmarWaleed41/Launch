using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace Launch_2
{
    public partial class AddAppWindow : Window
    {
        public string AppName { get; private set; }
        public string AppPath { get; private set; }
        public string ImagePath { get; private set; }

        public AddAppWindow()
        {
            InitializeComponent();
        }

        private void BrowseApp_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == true)
                AppPathBox.Text = dlg.FileName;
        }

        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
            if (dlg.ShowDialog() == true)
                ImagePathBox.Text = dlg.FileName;
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

            AppName = AppNameBox.Text.Trim();
            AppPath = AppPathBox.Text.Trim();
            ImagePath = ImagePathBox.Text.Trim();
            DialogResult = true;
        }
    }
}
