using System;
using System.Windows;

namespace TaskDock11.Views
{
    public partial class RenameDialog : Window
    {
        public string ResultName { get; private set; } = string.Empty;

        public RenameDialog(string defaultName)
        {
            InitializeComponent();
            TxtCustomName.Text = defaultName;
            Loaded += (s, e) =>
            {
                TxtCustomName.Focus();
                TxtCustomName.SelectAll();
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            ResultName = TxtCustomName.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
