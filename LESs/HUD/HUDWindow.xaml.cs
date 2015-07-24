using RAFlibPlus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace LESs
{
    /// <summary>
    /// Interaction logic for HUDWindow.xaml
    /// </summary>
    public partial class HUDWindow : Window
    {
        private string _location = "";
        private string _LeagueLocation = "";
        private readonly BackgroundWorker worker;
        private List<HUDItem> _hudItems;
        private HUDItem _selectedItem;

        public HUDWindow(string location)
        {
            InitializeComponent();
            if (!Directory.Exists("HUD"))
            {
                Directory.CreateDirectory("HUD");
            }

            if (!Directory.Exists(Path.Combine("HUD", MainWindow.current_version)))
            {
                Directory.CreateDirectory(Path.Combine("HUD", MainWindow.current_version));
            }

            _location = location;
            location = location.Substring(0, location.IndexOf("lol_air_client"));
            _LeagueLocation = Path.Combine(location, "lol_game_client", "filearchives");

            _hudItems = new List<HUDItem>();
            worker = new BackgroundWorker();
            worker.DoWork += Worker_DoWork;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

            worker.RunWorkerAsync();
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            LocatingGrid.Visibility = Visibility.Collapsed;
            HolderGrid.RowDefinitions.Clear();

            using (BinaryReader b = new BinaryReader(File.Open(Path.Combine("HUD", MainWindow.current_version, "HUDConfig.bin"), FileMode.Open)))
            {
                b.ReadBytes(0x1501); //Garbage bytes?
                bool read = true;
                while (read)
                {
                    try
                    {
                        _hudItems.Add(HUDItem.Read(b));
                    }
                    catch
                    {
                        read = false;
                    }
                }
            }

            CategoryComboBox.Items.Clear();

            foreach (var item in _hudItems)
            {
                if (!CategoryComboBox.Items.Contains(item.Category))
                    CategoryComboBox.Items.Add(item.Category);
            }

            CategoryComboBox.SelectedIndex = 0;
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (File.Exists(Path.Combine("HUD", MainWindow.current_version, "HUDConfig.bin")))
                return;

            RAFMasterFileList list = new RAFMasterFileList(_LeagueLocation);
            var HUDConfig = list.SearchFileEntries("Clarity_RenderUI.bin")[0];
            File.WriteAllBytes(Path.Combine("HUD", MainWindow.current_version, "HUDConfig.bin"), HUDConfig.GetContent());
            File.Copy(Path.Combine("HUD", MainWindow.current_version, "HUDConfig.bin"), Path.Combine("HUD", MainWindow.current_version, "HUDConfig.bin.bak"));
        }

        private void CategoryComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ItemComboBox.Items.Clear();

            foreach (var item in _hudItems)
            {
                if (item.Category == (string)CategoryComboBox.SelectedItem)
                    ItemComboBox.Items.Add(item.Name);
            }

            ItemComboBox.SelectedIndex = 0;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            using (Stream stream = File.Open(Path.Combine("HUD", MainWindow.current_version, "HUDConfig.bin"), FileMode.Open))
            {
                byte[] ReplacedBytes = _selectedItem.ReplaceCoordinates(new Tuple<byte, byte>(XOnePos.Byte, XTwoPos.Byte),
                                                               new Tuple<byte, byte>(YOnePos.Byte, YTwoPos.Byte));
                stream.Position = _selectedItem.Position;
                stream.Write(ReplacedBytes, 0, ReplacedBytes.Length);
            }

            RAFMasterFileList list = new RAFMasterFileList(_LeagueLocation);
            var HUDConfig = list.SearchFileEntries("Clarity_RenderUI.bin")[0];
            HUDConfig.ReplaceContent(File.ReadAllBytes(Path.Combine("HUD", MainWindow.current_version, "HUDConfig.bin")));
            HUDConfig.RAFArchive.SaveRAFFile();

            MessageBox.Show("HUD has been updated! Test it out in a custom game first.");
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            RAFMasterFileList list = new RAFMasterFileList(_LeagueLocation);
            var HUDConfig = list.SearchFileEntries("Clarity_RenderUI.bin")[0];
            HUDConfig.ReplaceContent(File.ReadAllBytes(Path.Combine("HUD", MainWindow.current_version, "HUDConfig.bin.bak")));
            HUDConfig.RAFArchive.SaveRAFFile();

            File.Delete(Path.Combine("HUD", MainWindow.current_version, "HUDConfig.bin"));
            File.Copy(Path.Combine("HUD", MainWindow.current_version, "HUDConfig.bin.bak"), Path.Combine("HUD", MainWindow.current_version, "HUDConfig.bin"));

            MessageBox.Show("HUD has been reset.");
            HUDWindow window = new HUDWindow(_location);
            window.Show();
            this.Close();
        }

        private void ItemComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            foreach (var item in _hudItems)
            {
                if (item.Category == (string)CategoryComboBox.SelectedItem && item.Name == (string)ItemComboBox.SelectedItem)
                {
                    _selectedItem = item;
                    XOnePos.Byte = item.X.Item1;
                    XTwoPos.Byte = item.X.Item2;
                    YOnePos.Byte = item.Y.Item1;
                    YTwoPos.Byte = item.Y.Item2;
                    return;
                }
            }
        }
    }
}
