using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Xml;

namespace LESs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string IntendedVersion = "0.0.1.80";

        private readonly BackgroundWorker worker = new BackgroundWorker();

        public MainWindow()
        {
            InitializeComponent();
            FindButton.AddHandler(Button.MouseDownEvent, new RoutedEventHandler(FindButton_MouseDown), true);
            LeagueVersionLabel.Content = IntendedVersion;
            if (File.Exists("debug.log"))
                File.Delete("debug.log");

            File.Create("debug.log");

            if (Directory.Exists("temp"))
                Directory.Delete("temp", true);
        }

        private void MainGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists("mods"))
            {
                MessageBox.Show("Missing mods directory. Ensure that all files were extracted properly.", "Missing files");
            }

            var ModList = Directory.GetDirectories("mods");

            foreach (string Mod in ModList)
            {
                CheckBox Check = new CheckBox();
                Check.IsChecked = true;
                Check.Content = Mod.Replace("mods\\", "");
                if (File.Exists(Path.Combine(Mod, "disabled")))
                    Check.IsChecked = false;
                ModsListBox.Items.Add(Check);
            }
        }

        private void ModsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CheckBox box = (CheckBox)ModsListBox.SelectedItem;

            if (box == null)
                return;

            string SelectedMod = (string)box.Content;
            using (XmlReader reader = XmlReader.Create(Path.Combine("mods", SelectedMod, "info.xml")))
            {
                while (reader.Read())
                {
                    if (reader.IsStartElement())
                    {
                        switch (reader.Name)
                        {
                            case "name":
                                reader.Read();
                                ModNameLabel.Content = reader.Value;
                                break;
                            case "description":
                                reader.Read();
                                ModDescriptionBox.Text = reader.Value;
                                break;
                        }
                    }
                }
            }
        }

        private void FindButton_MouseDown(object sender, RoutedEventArgs e)
        {
            PatchButton.IsEnabled = false;
            OpenFileDialog FindLeagueDialog = new OpenFileDialog();

            FindLeagueDialog.InitialDirectory = Path.Combine("C:\\", "Riot Games", "League of Legends");
            FindLeagueDialog.DefaultExt = ".exe";
            FindLeagueDialog.Filter = "League of Legends Launcher|lol.launcher.exe";

            Nullable<bool> result = FindLeagueDialog.ShowDialog();

            if (result == true)
            {
                string filename = FindLeagueDialog.FileName.Replace("lol.launcher.exe", "");
                string RADLocation = Path.Combine(filename, "RADS", "projects", "lol_air_client", "releases");

                File.AppendAllText("debug.log", filename + Environment.NewLine + RADLocation + Environment.NewLine);

                var VersionDirectories = Directory.GetDirectories(RADLocation);
                string FinalDirectory = "";
                string Version = "";
                int VersionCompare = 0;
                foreach (string x in VersionDirectories)
                {
                    string Compare1 = x.Substring(x.IndexOf("releases\\")).Replace("releases\\", "");
                    int CompareVersion = Convert.ToInt32(Compare1.Substring(0, 8).Replace(".", ""));

                    if (CompareVersion > VersionCompare)
                    {
                        VersionCompare = CompareVersion;
                        Version = x.Replace(RADLocation + "\\", "");
                        FinalDirectory = x;
                    }

                    File.AppendAllText("debug.log", x + Environment.NewLine + CompareVersion + Environment.NewLine);
                }

                if (Version != IntendedVersion)
                {
                    MessageBoxResult versionMismatchResult = MessageBox.Show("This version of LESs is intended for " + IntendedVersion + ". Your current version of League of Legends is " + Version + ". Continue? This could harm your installation.", "Invalid Version", MessageBoxButton.YesNo);
                    if (versionMismatchResult == MessageBoxResult.No)
                        return;
                }

                PatchButton.IsEnabled = true;

                LocationTextbox.Text = Path.Combine(FinalDirectory, "deploy");

                Directory.CreateDirectory(Path.Combine(LocationTextbox.Text, "LESsBackup"));
            }
        }

        private void PatchButton_Click(object sender, RoutedEventArgs e)
        {
            PatchButton.IsEnabled = false;

            File.AppendAllText("debug.log", "Starting patch" + Environment.NewLine);

            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            ItemCollection modCollection = null;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                modCollection = ModsListBox.Items;
            }));

            //Wait for UI thread to respond...
            while (modCollection == null)
                ;

            foreach (var x in modCollection)
            {
                CheckBox box = (CheckBox)x;
                bool? IsBoxChecked = null;
                string BoxName = "";
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
                {
                    if ((bool)box.IsChecked)
                    {
                        IsBoxChecked = true;
                        BoxName = (string)box.Content;
                    }
                    else
                    {
                        IsBoxChecked = false;
                        BoxName = "blah";
                    }
                }));

                //Wait for UI thread to respond...
                while (IsBoxChecked == null || String.IsNullOrEmpty(BoxName))
                    ;

                if ((bool)IsBoxChecked)
                {
                    int AmountOfPatches = 1;

                    using (XmlReader reader = XmlReader.Create(Path.Combine("mods", BoxName, "info.xml")))
                    {
                        while (reader.Read())
                        {
                            if (reader.IsStartElement())
                            {
                                switch (reader.Name)
                                {
                                    case "files":
                                        reader.Read();
                                        AmountOfPatches = Convert.ToInt32(reader.Value);
                                        break;
                                }
                            }
                        }
                    }
                    
                    for (int i = 0; i < AmountOfPatches; i++)
                    {
                        Directory.CreateDirectory("temp");
                        Patcher(BoxName, i);
                        DeletePathWithLongFileNames(Path.GetFullPath("temp"));
                    }
                }
            }
        }

        private void worker_RunWorkerCompleted(object sender,
                                       RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("LESs has been successfully patched into League of Legends!");
            PatchButton.IsEnabled = true;
            StatusLabel.Content = "Done patching!";
        }

        private void Patcher(string ModName, int AmountOfPatches)
        {
            string PatchNumber = "";
            if (AmountOfPatches >= 1)
                PatchNumber = AmountOfPatches.ToString();

            string[] ModDetails = File.ReadAllLines(Path.Combine("mods", ModName, "patch" + PatchNumber + ".txt"));
            string FileLocation = "null";
            string TryFindClass = "null";
            string TraitToModify = "null";
            foreach (string s in ModDetails)
            {
                if (s.StartsWith("#"))
                {
                    TryFindClass = s.Substring(1);
                }
                else if (s.StartsWith("@@@"))
                {
                    TraitToModify = s.Substring(3);
                }
                else if (s.StartsWith("~"))
                {
                    FileLocation = s.Substring(1);
                }
            }

            File.AppendAllText("debug.log", "Patching " + ModName + PatchNumber + Environment.NewLine);

            string[] FilePart = FileLocation.Split('/');
            string FileName = FilePart[FilePart.Length - 1];

            string n = string.Format("{0:MM-dd_hhmmss}", DateTime.Now);

            string LocationText = "";
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                LocationText = LocationTextbox.Text;
            }));

            //Wait for UI thread to respond...
            while (String.IsNullOrEmpty(LocationText))
                ;

            File.Copy(Path.Combine(LocationText, FileLocation), Path.Combine(LocationText, "LESsBackup", FileName + "." + n + ".bak"));
            File.Copy(Path.Combine(LocationText, FileLocation), Path.Combine("temp", FileName));

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = "Exporting patch " + ModName;
            }));

            File.AppendAllText("debug.log", "Running abcexport" + Environment.NewLine);

            ProcessStartInfo Export = new ProcessStartInfo();
            Export.FileName = "abcexport.exe";
            Export.Arguments = Path.Combine("temp", FileName);
            var ExportProc = Process.Start(Export);
            ExportProc.WaitForExit();

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = "Dissasembling patch (" + ModName + ")";
            }));

            string[] ABCFiles = Directory.GetFiles("temp", "*.abc");

            File.AppendAllText("debug.log", "Got " + ABCFiles.Length + " files" + Environment.NewLine);

            foreach (string s in ABCFiles)
            {
                ProcessStartInfo Disassemble = new ProcessStartInfo();
                Disassemble.FileName = "rabcdasm.exe";
                Disassemble.Arguments = s;
                Disassemble.UseShellExecute = false;
                Disassemble.CreateNoWindow = true;
                var DisasmProc = Process.Start(Disassemble);
                DisasmProc.WaitForExit();
            }

            if (TryFindClass.IndexOf(':') == 0)
            {
                File.AppendAllText("debug.log", "INVALID MOD!!!" + Environment.NewLine);
                throw new Exception("Invalid mod " + ModName);
            }

            List<string> directories = Directory.GetDirectories("temp", "*", SearchOption.AllDirectories).ToList();

            //Get all directories that match the requested class to modify
            string SearchFor = TryFindClass.Substring(0, TryFindClass.IndexOf(':'));
            List<string> FoundDirectories = new List<string>();
            foreach (string s in directories)
            {
                if (!s.Contains("com"))
                    continue;

                string tempS = s;
                tempS = tempS.Substring(tempS.IndexOf("com"));
                tempS = tempS.Replace("\\", ".");
                if (tempS == SearchFor)
                {
                    FoundDirectories.Add(s);
                }
            }

            if (FoundDirectories.Count == 0)
            {
                File.AppendAllText("debug.log", "No class matching " + SearchFor + " for mod " + ModName + Environment.NewLine);
                throw new Exception("No class matching " + SearchFor + " for mod " + ModName);
            }

            string FinalDirectory = "";
            string Class = TryFindClass.Substring(TryFindClass.IndexOf(':')).Replace(":", "");
            //Find the directory that has the requested class
            foreach (string s in FoundDirectories)
            {
                string[] m = Directory.GetFiles(s);
                string x = Path.Combine(s, Class + ".class.asasm");
                if (m.Contains(x))
                {
                    FinalDirectory = s;
                }
            }

            string[] ClassModifier = File.ReadAllLines(Path.Combine(FinalDirectory, Class + ".class.asasm"));
            int TraitStartPosition = 0;
            int TraitEndLocation = 0;
            //Get location of trait
            for (int i = 0; i < ClassModifier.Length; i++)
            {
                if (ClassModifier[i] == TraitToModify)
                {
                    TraitStartPosition = i;
                    break;
                }
            }

            //Get end location of trait
            for (int i = TraitStartPosition; i < ClassModifier.Length; i++)
            {
                if (ClassModifier[i].Trim() == "end ; trait")
                {
                    TraitEndLocation = i + 1;
                    break;
                }
            }

            string[] StartTrait = new string[TraitStartPosition];
            Array.Copy(ClassModifier, StartTrait, TraitStartPosition);
            string[] AfterTrait = new string[ClassModifier.Length - TraitEndLocation];
            Array.Copy(ClassModifier, TraitEndLocation, AfterTrait, 0, ClassModifier.Length - TraitEndLocation);

            string[] FinalClass = new string[StartTrait.Length + (ModDetails.Length - 3) + AfterTrait.Length];
            Array.Copy(StartTrait, FinalClass, TraitStartPosition);
            Array.Copy(ModDetails, 3, FinalClass, TraitStartPosition, (ModDetails.Length - 3));
            Array.Copy(AfterTrait, 0, FinalClass, TraitStartPosition + (ModDetails.Length - 3), AfterTrait.Length);

            File.Delete(Path.Combine(FinalDirectory, Class + ".class.asasm"));
            File.WriteAllLines(Path.Combine(FinalDirectory, Class + ".class.asasm"), FinalClass);

            string ReAssembleLocation = FinalDirectory.Substring(0, FinalDirectory.IndexOf("com")).Replace("temp\\", "");
            string AbcNumber = ReAssembleLocation.Substring(ReAssembleLocation.IndexOf('-')).Replace("-", "").Replace("\\", "");

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = "Repackaging " + ModName;
            }));

            ProcessStartInfo ReAsm = new ProcessStartInfo();
            ReAsm.FileName = "rabcasm.exe";
            ReAsm.RedirectStandardError = true;
            ReAsm.UseShellExecute = false;
            ReAsm.Arguments = Path.Combine("temp", ReAssembleLocation, ReAssembleLocation.Replace("\\", "") + ".main.asasm");
            var ReAsmProc = Process.Start(ReAsm);
            while (!ReAsmProc.StandardError.EndOfStream)
            {
                string line = ReAsmProc.StandardError.ReadLine();
                File.AppendAllText("debug.log", line + Environment.NewLine);
            }
            ReAsmProc.WaitForExit();

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = "Finishing touches for " + ModName;
            }));

            ProcessStartInfo DoPatch = new ProcessStartInfo();
            DoPatch.FileName = "abcreplace.exe";
            DoPatch.RedirectStandardError = true;
            DoPatch.UseShellExecute = false;
            DoPatch.Arguments = Path.Combine("temp", FileName) + " " + AbcNumber + " " + Path.Combine("temp", ReAssembleLocation, ReAssembleLocation.Replace("\\", "") + ".main.abc");
            var FinalPatchProc = Process.Start(DoPatch);
            while (!FinalPatchProc.StandardError.EndOfStream)
            {
                string line = FinalPatchProc.StandardError.ReadLine();
                File.AppendAllText("debug.log", line + Environment.NewLine);
            }
            FinalPatchProc.WaitForExit();

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = "Done patching " + ModName + "!";
            }));

            File.Copy(Path.Combine("temp", FileName), Path.Combine(LocationText, FileLocation), true);
        }

        private static void DeletePathWithLongFileNames(string path)
        {
            var tmpPath = @"\\?\" + path;
            Scripting.FileSystemObject fso = new Scripting.FileSystemObject() as Scripting.FileSystemObject;
            fso.DeleteFolder(tmpPath, true); 
        }
    }
}
