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
        private string Location;

        public RemovePopup(ServerType type, string LeagueLocation)
        {
            InitializeComponent();

            //Get the versions that have been backed up
            string CurrentLocation = Path.Combine(LeagueLocation, "LESsBackup");
            string[] targetVersions = Directory.GetDirectories(CurrentLocation);
            for (int i = 0; i < targetVersions.Length; i++)
            {
                targetVersions[i] = targetVersions[i].Remove(0, CurrentLocation.Length).Replace("\\", "").Replace("/", "");
            }

            //Only allow removing through RIOT patcher if not garena
            if (type == ServerType.NORMAL)
                RemoveRiotButton.IsEnabled = true;

            VersionComboBox.ItemsSource = targetVersions;
            VersionComboBox.SelectionChanged += VersionComboBox_SelectionChanged;
            VersionComboBox.SelectedItem = targetVersions[targetVersions.Length - 1];
            Location = LeagueLocation;

            RemoveBackupButton.Click += RemoveBackupButton_Click;
            RemoveRiotButton.Click += RemoveRiotButton_Click;
        }

        /// <summary>
        /// Called when the restore from backup button is clicked
        /// </summary>
        void RemoveBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (VersionComboBox.SelectedItem == null)
                return;

            //Copy the files from the LESsBackup into the league folder, with the same directory structure
            string FinalBackupPath = Path.Combine(Location, "LESsBackup", (string)VersionComboBox.SelectedItem);

            int FilesCopied = 0;
            foreach (string path in Directory.GetFiles(FinalBackupPath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(path, path.Replace(FinalBackupPath, Location), true);
                FilesCopied += 1;
            }

            MessageBox.Show($"Restored {FilesCopied} files. LESs has been removed from League of Legends!");
        }

        /// <summary>
        /// Called when the restore from riot patcher button is called
        /// </summary>
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

        /// <summary>
        /// Called when the version combo box selection changes
        /// </summary>
        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Only allow removing through backup if a version has been selected
            RemoveBackupButton.IsEnabled = true;
        }
    }
}
