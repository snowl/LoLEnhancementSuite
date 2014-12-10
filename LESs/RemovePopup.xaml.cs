using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace LESs
{
    /// <summary>
    /// Interaction logic for RemovePopup.xaml
    /// </summary>
    public partial class RemovePopup : Window
    {
        string Location;

        public RemovePopup(ServerType type, string[] Versions, string LeagueLocation)
        {
            InitializeComponent();

            if (type == ServerType.NORMAL)
                RemoveRiotButton.IsEnabled = true;

            VersionComboBox.ItemsSource = Versions;
            VersionComboBox.SelectionChanged += VersionComboBox_SelectionChanged;
            VersionComboBox.SelectedItem = Versions[Versions.Length - 1];
            Location = LeagueLocation;

            RemoveBackupButton.Click += RemoveBackupButton_Click;
            RemoveRiotButton.Click += RemoveRiotButton_Click;
        }

        void RemoveBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (VersionComboBox.SelectedItem == null)
                return;

            string FinalBackupPath = Path.Combine(Location, "LESsBackup", (string)VersionComboBox.SelectedItem);

            int FilesCopied = 0;
            foreach (string path in Directory.GetFiles(FinalBackupPath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(path, path.Replace(FinalBackupPath, Location), true);
                FilesCopied += 1;
            }

            MessageBox.Show(string.Format("Restored {0} files. LESs has been removed from League of Legends!", FilesCopied));
        }

        void RemoveRiotButton_Click(object sender, RoutedEventArgs e)
        {
            /*This works by removing S_OK from the AIR installation. This has the same effect as clicking "Repair" in the patcher
              except it only makes it check the AIR installation, not the entire game. This speeds it up from 10-20 minutes
              to only a minute max.*/
            if (File.Exists(Path.Combine(Location.Substring(0, Location.Length - 7), "S_OK")))
            {
                File.Delete(Path.Combine(Location.Substring(0, Location.Length - 7), "S_OK"));
                MessageBox.Show("LESs will be removed next time League of Legends launches!");
            }
        }

        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RemoveBackupButton.IsEnabled = true;
        }
    }
}
