using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml;

namespace LESs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string IntendedVersion = "0.0.1.99";

        private readonly BackgroundWorker worker = new BackgroundWorker();
        private bool WasPatched = true;

        private List<WorstHack> ReassembleLocations;

        public MainWindow()
        {
            InitializeComponent();
            ReassembleLocations = new List<WorstHack>();
            FindButton.AddHandler(MouseDownEvent, new RoutedEventHandler(FindButton_MouseDown), true);
            LeagueVersionLabel.Content = IntendedVersion;
            if (File.Exists("debug.log"))
                File.Delete("debug.log");

            File.Create("debug.log");

            if (Directory.Exists("temp"))
            {
                DeletePathWithLongFileNames(Path.GetFullPath("temp"));
            }

            if (Directory.Exists(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "wm")))
                MessageBox.Show("You may have malware on your system due to getting this application from an unknown source. Please delete C:/wm/ and the file inside it and then download this application from http://da.viddiaz.com/LESs");

            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            if (!Debugger.IsAttached)
            {
                AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
            }
        }

        void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            MessageBox.Show(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
            WasPatched = false;
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

            if (!Directory.Exists(Path.Combine("C:\\", "Riot Games", "League of Legends")))
            {
                //Gagarena
                FindLeagueDialog.InitialDirectory = Path.Combine("C:\\", "Program Files (x86)", "GarenaLoL", "GameData", "Apps", "LoL");
            }
            else
            {
                FindLeagueDialog.InitialDirectory = Path.Combine("C:\\", "Riot Games", "League of Legends");
            }
            FindLeagueDialog.DefaultExt = ".exe";
            FindLeagueDialog.Filter = "League of Legends Launcher|lol.launcher*.exe|Garena Launcher|lol.exe";

            Nullable<bool> result = FindLeagueDialog.ShowDialog();

            if (result == true)
            {
                File.AppendAllText("debug.log", FindLeagueDialog.FileName + Environment.NewLine);
                string filename = FindLeagueDialog.FileName.Replace("lol.launcher.exe", "").Replace("lol.launcher.admin.exe", "");
                if (filename.Contains("lol.exe"))
                {
                    //Ga ga ga garena

                    PatchButton.IsEnabled = true;
                    RemoveButton.IsEnabled = false; //Can't automatically remove on garena installations!

                    filename = filename.Replace("lol.exe", "");

                    LocationTextbox.Text = Path.Combine(filename, "Air");
                }
                else
                {
                    string RADLocation = Path.Combine(filename, "RADS", "projects", "lol_air_client", "releases");

                    File.AppendAllText("debug.log", filename + Environment.NewLine + RADLocation + Environment.NewLine);

                    var VersionDirectories = Directory.GetDirectories(RADLocation);
                    string FinalDirectory = "";
                    string Version = "";
                    int VersionCompare = 0;
                    foreach (string x in VersionDirectories)
                    {
                        string Compare1 = x.Substring(x.IndexOf("releases\\")).Replace("releases\\", "") + " ";
                        int CompareVersion;
                        try
                        {
                            CompareVersion = Convert.ToInt32(Compare1.Substring(0, 9).Replace(".", ""));
                        }
                        catch (ArgumentOutOfRangeException)//fix for version numbers < 0.0.1.10
                        {
                            //Ignore
                            CompareVersion = 0;
                        }

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
                    RemoveButton.IsEnabled = true;

                    LocationTextbox.Text = Path.Combine(FinalDirectory, "deploy");
                }

                Directory.CreateDirectory(Path.Combine(LocationTextbox.Text, "LESsBackup"));
                Directory.CreateDirectory(Path.Combine(LocationTextbox.Text, "LESsBackup", IntendedVersion));
            }
        }

        private void PatchButton_Click(object sender, RoutedEventArgs e)
        {
            PatchButton.IsEnabled = false;

            File.AppendAllText("debug.log", "Starting patch" + Environment.NewLine);

            worker.RunWorkerAsync();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(Path.Combine(LocationTextbox.Text.Substring(0, LocationTextbox.Text.Length - 7), "S_OK")))
            {
                File.Delete(Path.Combine(LocationTextbox.Text.Substring(0, LocationTextbox.Text.Length - 7), "S_OK"));
                MessageBox.Show("LESs will be removed next time League of Legends launches!");
                StatusLabel.Content = "Removed LESs";
            }
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

            Directory.CreateDirectory("temp");

            foreach (var x in modCollection)
            {
                CheckBox box = (CheckBox)x;
                bool? IsBoxChecked = null;
                string BoxName = "";
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
                {
                    if (box.IsChecked != null && (bool)box.IsChecked)
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
                        Patcher(BoxName, i);
                    }
                }
            }

            foreach (WorstHack s in ReassembleLocations)
            {
                Repackage(s);
            }

            List<string> CopiedNames = new List<string>();

            foreach (WorstHack s in ReassembleLocations)
            {
                if (!CopiedNames.Contains(s.FileName))
                {
                    CopiedNames.Add(s.FileName);
                    CopyToClient(s);
                }
            }

            DeletePathWithLongFileNames(Path.GetFullPath("temp"));
        }

        private void worker_RunWorkerCompleted(object sender,
                                       RunWorkerCompletedEventArgs e)
        {
            if (WasPatched)
            {
                MessageBox.Show("LESs has been successfully patched into League of Legends!");
            }
            else
            {
                MessageBox.Show("LESs encountered errors during patching. However, some patches may still be applied.");
            }
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
            bool IsNewTrait = false;
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
                else if (s.StartsWith("@+@"))//Insert the new trait above this one
                {
                    TraitToModify = s.Substring(3);
                    IsNewTrait = true;
                }
                else if (s.StartsWith("~"))
                {
                    FileLocation = s.Substring(1);
                }
            }

            File.AppendAllText("debug.log", "Patching " + ModName + PatchNumber + Environment.NewLine);

            string[] FilePart = FileLocation.Split('/');
            string FileName = FilePart[FilePart.Length - 1];

            string LocationText = "";
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                LocationText = LocationTextbox.Text;
            }));

            //Wait for UI thread to respond...
            while (String.IsNullOrEmpty(LocationText))
                ;

            if (!Directory.Exists(Path.Combine("temp", FileLocation.Replace(".dat", ""))))
            {
                Directory.CreateDirectory(Path.Combine("temp", FileLocation.Replace(".dat", "")));

                string n = "";
                foreach (string s in FilePart.Take(FilePart.Length - 1))
                {
                    n = Path.Combine(n, s);
                    if (!Directory.Exists(Path.Combine(LocationText, "LESsBackup", IntendedVersion, n)))
                    {
                        Directory.CreateDirectory(Path.Combine(LocationText, "LESsBackup", IntendedVersion, n));
                    }
                }
                if (!File.Exists(Path.Combine(LocationText, "LESsBackup", IntendedVersion, FileLocation)))
                {
                    File.Copy(Path.Combine(LocationText, FileLocation), Path.Combine(LocationText, "LESsBackup", IntendedVersion, FileLocation));
                }

                File.Copy(Path.Combine(LocationText, FileLocation), Path.Combine("temp", FileLocation.Replace(".dat", ""), FileName));

                Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
                {
                    StatusLabel.Content = "Exporting patch " + ModName;
                }));

                File.AppendAllText("debug.log", "Running abcexport" + Environment.NewLine);

                ProcessStartInfo Export = new ProcessStartInfo();
                Export.FileName = "abcexport.exe";
                Export.CreateNoWindow = true;
                Export.UseShellExecute = false;
                Export.Arguments = Path.Combine("temp", FileLocation.Replace(".dat", ""), FileName);
                var ExportProc = Process.Start(Export);
                if (ExportProc != null)
                {
                    ExportProc.WaitForExit();
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
                {
                    StatusLabel.Content = "Disassembling patch (" + ModName + ")";
                }));

                string[] ABCFiles = Directory.GetFiles(Path.Combine("temp", FileLocation.Replace(".dat", "")), "*.abc");

                File.AppendAllText("debug.log", "Got " + ABCFiles.Length + " files" + Environment.NewLine);

                foreach (string s in ABCFiles)
                {
                    ProcessStartInfo Disassemble = new ProcessStartInfo();
                    Disassemble.FileName = "rabcdasm.exe";
                    Disassemble.Arguments = s;
                    Disassemble.UseShellExecute = false;
                    Disassemble.CreateNoWindow = true;
                    var DisasmProc = Process.Start(Disassemble);
                    if (DisasmProc != null)
                    {
                        DisasmProc.WaitForExit();
                    }
                }
            }
            
            if (TryFindClass.IndexOf(':') == 0)
            {
                File.AppendAllText("debug.log", "INVALID MOD!!!" + Environment.NewLine);
                throw new Exception("Invalid mod " + ModName);
            }

            List<string> directories = Directory.GetDirectories(Path.Combine("temp", FileLocation.Replace(".dat", "")), "*", SearchOption.AllDirectories).ToList();

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

            //return if the new trait already exists
            if (IsNewTrait)
            {
                foreach (string l in ClassModifier)
                {
                    if (l == ModDetails[3])
                        return;
                }
            }

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

            if (TraitStartPosition == 0)
            {
                File.AppendAllText("debug.log", "Trait start location was not found! Corrupt mod?");
                throw new Exception("Trait start location was not found! Corrupt mod?");
            }

            if (!IsNewTrait)
            {
                //Get end location of trait
                for (int i = TraitStartPosition; i < ClassModifier.Length; i++)
                {
                    if (ClassModifier[i].Trim() == "end ; method")
                    {
                        if (ClassModifier[i + 1].Trim() == "end ; trait")
                        {
                            TraitEndLocation = i + 2;
                        }
                        else
                        {
                            TraitEndLocation = i + 1;
                        }
                        break;
                    }
                }

                if (TraitEndLocation < TraitStartPosition)
                {
                    File.AppendAllText("debug.log", "Trait end location was smaller than trait start location! " + TraitEndLocation + ", " + TraitStartPosition);
                    throw new Exception("Trait end location was smaller than trait start location! " + TraitEndLocation + ", " + TraitStartPosition);
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
            }
            else
            {
                string[] FinalClass = new string[ClassModifier.Length + (ModDetails.Length - 3)];
                Array.Copy(ClassModifier, 0, FinalClass, 0, TraitStartPosition);
                Array.Copy(ModDetails, 3, FinalClass, TraitStartPosition, ModDetails.Length - 3);
                Array.Copy(ClassModifier, TraitStartPosition, FinalClass, TraitStartPosition + ModDetails.Length - 3, ClassModifier.Length - TraitStartPosition);

                File.Delete(Path.Combine(FinalDirectory, Class + ".class.asasm"));
                File.WriteAllLines(Path.Combine(FinalDirectory, Class + ".class.asasm"), FinalClass);
            }

            WorstHack h = new WorstHack();
            h.FileName = FileName;
            h.LocationText = LocationText;
            h.ReAssembleLocation = FinalDirectory.Substring(0, FinalDirectory.IndexOf("com")).Replace("temp\\", "");
            h.FileLocation = FileLocation;

            if (!ReassembleLocations.Contains(h))
                ReassembleLocations.Add(h);
        }

        private void Repackage(WorstHack data)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = "Patching mods to client...";
            }));

            string AbcNumber = data.ReAssembleLocation.Substring(data.ReAssembleLocation.IndexOf('-')).Replace("-", "").Replace("\\", "");

            ProcessStartInfo ReAsm = new ProcessStartInfo();
            ReAsm.FileName = "rabcasm.exe";
            ReAsm.RedirectStandardError = true;
            ReAsm.UseShellExecute = false;
            ReAsm.CreateNoWindow = true;
            ReAsm.Arguments = Path.Combine("temp", data.ReAssembleLocation + data.FileName.Replace(".dat", "") + "-" + AbcNumber + ".main.asasm");
            var ReAsmProc = Process.Start(ReAsm);
            while (ReAsmProc != null && !ReAsmProc.StandardError.EndOfStream)
            {
                string line = ReAsmProc.StandardError.ReadLine();
                File.AppendAllText("debug.log", line + Environment.NewLine);
            }
            if (ReAsmProc != null)
            {
                ReAsmProc.WaitForExit();
            }

            ProcessStartInfo DoPatch = new ProcessStartInfo();
            DoPatch.FileName = "abcreplace.exe";
            DoPatch.RedirectStandardError = true;
            DoPatch.UseShellExecute = false;
            DoPatch.CreateNoWindow = true;
            DoPatch.Arguments = Path.Combine("temp", data.FileLocation.Replace(".dat", ""), data.FileName) + " " + AbcNumber + " " + Path.Combine("temp", data.ReAssembleLocation + data.FileName.Replace(".dat", "") + "-" + AbcNumber + ".main.abc");
            var FinalPatchProc = Process.Start(DoPatch);
            while (FinalPatchProc != null && !FinalPatchProc.StandardError.EndOfStream)
            {
                string line = FinalPatchProc.StandardError.ReadLine();
                File.AppendAllText("debug.log", line + Environment.NewLine);
            }
            if (FinalPatchProc != null)
            {
                FinalPatchProc.WaitForExit();
            }
        }

        private void CopyToClient(WorstHack data)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = "Patched " + data.FileName + "!";
            }));

            File.Copy(Path.Combine("temp", data.FileLocation.Replace(".dat", ""), data.FileName), Path.Combine(data.LocationText, data.FileLocation), true);
        }

        private static void DeletePathWithLongFileNames(string path)
        {
            var tmpPath = @"\\?\" + path;
            Scripting.FileSystemObject fso = new Scripting.FileSystemObject();
            fso.DeleteFolder(tmpPath, true);
        }
    }

    //cbf doing this properly, just do a quick thing that works just as well
    public class WorstHack
    {
        public string ReAssembleLocation { get; set; }
        public string FileName { get; set; }
        public string LocationText { get; set; }
        public string FileLocation { get; set; }
    }
}
