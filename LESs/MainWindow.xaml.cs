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
using Microsoft.Win32;

namespace LESs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string IntendedVersion = "0.0.1.94";

        private readonly BackgroundWorker worker = new BackgroundWorker();
        private bool wasPatched = true;

        public MainWindow()
        {
            InitializeComponent();
            FindButton.AddHandler(MouseDownEvent, new RoutedEventHandler(FindButton_MouseDown), true);
            LeagueVersionLabel.Content = IntendedVersion;
            if (File.Exists("debug.log"))
            {
                File.Delete("debug.log");
            }

            File.Create("debug.log");

            if (Directory.Exists("temp"))
            {
                Directory.Delete("temp", true);
            }

            if (Directory.Exists(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "wm")))
            {
                MessageBox.Show(
                    "You may have malware on your system due to getting this application from an unknown source. Please delete C:/wm/ and the file inside it and then download this application from http://da.viddiaz.com/LESs");
            }

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
            this.wasPatched = false;
        }

        private void MainGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists("mods"))
            {
                MessageBox.Show("Missing mods directory. Ensure that all files were extracted properly.", "Missing files");
            }

            var modList = Directory.GetDirectories("mods");

            foreach (string mod in modList)
            {
                var check = new CheckBox();
                check.IsChecked = true;
                check.Content = mod.Replace("mods\\", string.Empty);
                if (File.Exists(Path.Combine(mod, "disabled")))
                {
                    check.IsChecked = false;
                }

                ModsListBox.Items.Add(check);
            }
        }

        private void ModsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var box = (CheckBox)ModsListBox.SelectedItem;

            if (box == null)
            {
                return;
            }

            var selectedMod = (string)box.Content;
            using (XmlReader reader = XmlReader.Create(Path.Combine("mods", selectedMod, "info.xml")))
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
            var findLeagueDialog = new OpenFileDialog();

            findLeagueDialog.InitialDirectory = !Directory.Exists(Path.Combine("C:\\", "Riot Games", "League of Legends")) ? Path.Combine("C:\\", "Program Files (x86)", "GarenaLoL", "GameData", "Apps", "LoL") : Path.Combine("C:\\", "Riot Games", "League of Legends");
            findLeagueDialog.DefaultExt = ".exe";
            findLeagueDialog.Filter = "League of Legends Launcher|lol.launcher*.exe|Garena Launcher|lol.exe";

            bool? result = findLeagueDialog.ShowDialog();

            if (result == true)
            {
                File.AppendAllText("debug.log", findLeagueDialog.FileName + Environment.NewLine);
                string filename = findLeagueDialog.FileName.Replace("lol.launcher.exe", string.Empty).Replace("lol.launcher.admin.exe", string.Empty);
                if (filename.Contains("lol.exe"))
                {
                    // Ga ga ga garena
                    PatchButton.IsEnabled = true;
                    RemoveButton.IsEnabled = false; // Can't automatically remove on garena installations!

                    filename = filename.Replace("lol.exe", string.Empty);

                    LocationTextbox.Text = Path.Combine(filename, "Air");
                }
                else
                {
                    string radLocation = Path.Combine(filename, "RADS", "projects", "lol_air_client", "releases");

                    File.AppendAllText("debug.log", filename + Environment.NewLine + radLocation + Environment.NewLine);

                    var versionDirectories = Directory.GetDirectories(radLocation);
                    string finalDirectory = string.Empty;
                    string version = string.Empty;
                    int versionCompare = 0;
                    foreach (string x in versionDirectories)
                    {
                        string compare1 = x.Substring(x.IndexOf("releases\\")).Replace("releases\\", string.Empty);
                        int compareVersion;
                        try
                        {
                            compareVersion = Convert.ToInt32(compare1.Substring(0, 8).Replace(".", string.Empty));
                        }
                        catch (ArgumentOutOfRangeException) // fix for version numbers < 0.0.1.10
                        {
                            // Ignore
                            compareVersion = 0;
                        }

                        if (compareVersion > versionCompare)
                        {
                            versionCompare = compareVersion;
                            version = x.Replace(radLocation + "\\", string.Empty);
                            finalDirectory = x;
                        }

                        File.AppendAllText("debug.log", x + Environment.NewLine + compareVersion + Environment.NewLine);
                    }

                    if (version != IntendedVersion)
                    {
                        MessageBoxResult versionMismatchResult = MessageBox.Show("This version of LESs is intended for " + IntendedVersion + ". Your current version of League of Legends is " + version + ". Continue? This could harm your installation.", "Invalid Version", MessageBoxButton.YesNo);
                        if (versionMismatchResult == MessageBoxResult.No)
                            return;
                    }

                    PatchButton.IsEnabled = true;
                    RemoveButton.IsEnabled = true;

                    LocationTextbox.Text = Path.Combine(finalDirectory, "deploy");
                }

                Directory.CreateDirectory(Path.Combine(LocationTextbox.Text, "LESsBackup"));
                Directory.CreateDirectory(Path.Combine(LocationTextbox.Text, "LESsBackup", IntendedVersion));
            }
        }

        private void PatchButton_Click(object sender, RoutedEventArgs e)
        {
            PatchButton.IsEnabled = false;

            File.AppendAllText("debug.log", "Starting patch" + Environment.NewLine); // ToDo: Localize

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
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new ThreadStart(() => { modCollection = ModsListBox.Items; }));

            // Wait for UI thread to respond...
            while (modCollection == null)
            {
                ;
            }

            foreach (var x in modCollection)
            {
                var box = (CheckBox)x;
                bool? isBoxChecked = null;
                string boxName = string.Empty;
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Input,
                    new ThreadStart(
                        () =>
                            {
                                if (box.IsChecked != null && (bool)box.IsChecked)
                                {
                                    isBoxChecked = true;
                                    boxName = (string)box.Content;
                                }
                                else
                                {
                                    isBoxChecked = false;
                                    boxName = "blah";
                                }
                            }));

                // Wait for UI thread to respond...
                while (isBoxChecked == null || String.IsNullOrEmpty(boxName))
                {
                    ;
                }

                if ((bool)isBoxChecked)
                {
                    int amountOfPatches = 1;

                    using (XmlReader reader = XmlReader.Create(Path.Combine("mods", boxName, "info.xml")))
                    {
                        while (reader.Read())
                        {
                            if (reader.IsStartElement())
                            {
                                switch (reader.Name)
                                {
                                    case "files":
                                        reader.Read();
                                        amountOfPatches = Convert.ToInt32(reader.Value);
                                        break;
                                }
                            }
                        }
                    }

                    for (int i = 0; i < amountOfPatches; i++)
                    {
                        Directory.CreateDirectory("temp");
                        Patcher(boxName, i);
                        DeletePathWithLongFileNames(Path.GetFullPath("temp"));
                    }
                }
            }
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show(
                this.wasPatched
                    ? "LESs has been successfully patched into League of Legends!"
                    : "LESs encountered errors during patching. However, some patches may still be applied.");
            PatchButton.IsEnabled = true;
            StatusLabel.Content = "Done patching!";
        }

        private void Patcher(string modName, int amountOfPatches)
        {
            string patchNumber = string.Empty;
            if (amountOfPatches >= 1)
            {
                patchNumber = amountOfPatches.ToString();
            }

            string[] modDetails = File.ReadAllLines(Path.Combine("mods", modName, "patch" + patchNumber + ".txt"));
            string fileLocation = "null";
            string tryFindClass = "null";
            string traitToModify = "null";
            bool isNewTrait = false;
            foreach (string s in modDetails)
            {
                if (s.StartsWith("#"))
                {
                    tryFindClass = s.Substring(1);
                }
                else if (s.StartsWith("@@@"))
                {
                    traitToModify = s.Substring(3);
                }
                else if (s.StartsWith("@+@")) 
                {
                    // Insert the new trait above this one
                    traitToModify = s.Substring(3);
                    isNewTrait = true;
                }
                else if (s.StartsWith("~"))
                {
                    fileLocation = s.Substring(1);
                }
            }

            File.AppendAllText("debug.log", "Patching " + modName + patchNumber + Environment.NewLine); // ToDo: Localize

            string[] filePart = fileLocation.Split('/');
            string fileName = filePart[filePart.Length - 1];

            string locationText = string.Empty;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                locationText = LocationTextbox.Text;
            }));

            //Wait for UI thread to respond...
            while (String.IsNullOrEmpty(locationText))
                ;

            string n = string.Empty;
            foreach (string s in filePart.Take(filePart.Length - 1))
            {
                n = Path.Combine(n, s);
                if (!Directory.Exists(Path.Combine(locationText, "LESsBackup", IntendedVersion, n)))
                {
                    Directory.CreateDirectory(Path.Combine(locationText, "LESsBackup", IntendedVersion, n));
                }
            }
            if (!File.Exists(Path.Combine(locationText, "LESsBackup", IntendedVersion, fileLocation)))
            {
                File.Copy(Path.Combine(locationText, fileLocation), Path.Combine(locationText, "LESsBackup", IntendedVersion, fileLocation));
            }

            File.Copy(Path.Combine(locationText, fileLocation), Path.Combine("temp", fileName));

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = "Exporting patch " + modName;
            }));

            File.AppendAllText("debug.log", "Running abcexport" + Environment.NewLine); // ToDo: Localize

            var export = new ProcessStartInfo();
            export.FileName = "abcexport.exe";
            export.Arguments = Path.Combine("temp", fileName);
            var ExportProc = Process.Start(export);
            if (ExportProc != null)
            {
                ExportProc.WaitForExit();
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = "Disassembling patch (" + modName + ")";
            }));

            string[] abcFiles = Directory.GetFiles("temp", "*.abc");

            File.AppendAllText("debug.log", "Got " + abcFiles.Length + " files" + Environment.NewLine); // ToDo: Localize

            foreach (string s in abcFiles)
            {
                var disassemble = new ProcessStartInfo();
                disassemble.FileName = "rabcdasm.exe";
                disassemble.Arguments = s;
                disassemble.UseShellExecute = false;
                disassemble.CreateNoWindow = true;
                var disasmProc = Process.Start(disassemble);
                if (disasmProc != null)
                {
                    disasmProc.WaitForExit();
                }
            }

            if (tryFindClass.IndexOf(':') == 0)
            {
                File.AppendAllText("debug.log", "INVALID MOD!!!" + Environment.NewLine); // ToDo: Localize
                throw new Exception("Invalid mod " + modName);
            }

            List<string> directories = Directory.GetDirectories("temp", "*", SearchOption.AllDirectories).ToList();

            //Get all directories that match the requested class to modify
            string searchFor = tryFindClass.Substring(0, tryFindClass.IndexOf(':'));
            var foundDirectories = new List<string>();
            foreach (string s in directories)
            {
                if (!s.Contains("com"))
                    continue;

                string tempS = s;
                tempS = tempS.Substring(tempS.IndexOf("com"));
                tempS = tempS.Replace("\\", ".");
                if (tempS == searchFor)
                {
                    foundDirectories.Add(s);
                }
            }

            if (foundDirectories.Count == 0)
            {
                File.AppendAllText("debug.log", "No class matching " + searchFor + " for mod " + modName + Environment.NewLine); // ToDo: Localize
                throw new Exception("No class matching " + searchFor + " for mod " + modName);
            }

            string finalDirectory = string.Empty;
            string Class = tryFindClass.Substring(tryFindClass.IndexOf(':')).Replace(":", string.Empty);
            //Find the directory that has the requested class
            foreach (string s in foundDirectories)
            {
                string[] m = Directory.GetFiles(s);
                string x = Path.Combine(s, Class + ".class.asasm");
                if (m.Contains(x))
                {
                    finalDirectory = s;
                }
            }

            string[] ClassModifier = File.ReadAllLines(Path.Combine(finalDirectory, Class + ".class.asasm"));

            //return if the new trait already exists
            if (isNewTrait)
            {
                foreach (string l in ClassModifier)
                {
                    if (l == modDetails[3])
                        return;
                }
            }

            int traitStartPosition = 0;
            int traitEndLocation = 0;

            // Get location of trait
            for (int i = 0; i < ClassModifier.Length; i++)
            {
                if (ClassModifier[i] == traitToModify)
                {
                    traitStartPosition = i;
                    break;
                }
            }

            if (traitStartPosition == 0)
            {
                File.AppendAllText("debug.log", "Trait start location was not found! Corrupt mod?"); // ToDo: Localize
                throw new Exception("Trait start location was not found! Corrupt mod?");
            }

            if (!isNewTrait)
            {
                // Get end location of trait
                for (int i = traitStartPosition; i < ClassModifier.Length; i++)
                {
                    if (ClassModifier[i].Trim() == "end ; method")
                    {
                        if (ClassModifier[i + 1].Trim() == "end ; trait")
                        {
                            traitEndLocation = i + 2;
                        }
                        else
                        {
                            traitEndLocation = i + 1;
                        }
                        break;
                    }
                }

                if (traitEndLocation < traitStartPosition)
                {
                    File.AppendAllText("debug.log", "Trait end location was smaller than trait start location! " + traitEndLocation + ", " + traitStartPosition); // ToDo: Localize
                    throw new Exception("Trait end location was smaller than trait start location! " + traitEndLocation + ", " + traitStartPosition);
                }

                var startTrait = new string[traitStartPosition];
                Array.Copy(ClassModifier, startTrait, traitStartPosition);
                var afterTrait = new string[ClassModifier.Length - traitEndLocation];
                Array.Copy(ClassModifier, traitEndLocation, afterTrait, 0, ClassModifier.Length - traitEndLocation);

                var finalClass = new string[startTrait.Length + (modDetails.Length - 3) + afterTrait.Length];
                Array.Copy(startTrait, finalClass, traitStartPosition);
                Array.Copy(modDetails, 3, finalClass, traitStartPosition, modDetails.Length - 3);
                Array.Copy(afterTrait, 0, finalClass, traitStartPosition + (modDetails.Length - 3), afterTrait.Length);

                File.Delete(Path.Combine(finalDirectory, Class + ".class.asasm"));
                File.WriteAllLines(Path.Combine(finalDirectory, Class + ".class.asasm"), finalClass);
            }
            else
            {
                var finalClass = new string[ClassModifier.Length + (modDetails.Length - 3)];
                Array.Copy(ClassModifier, 0, finalClass, 0, traitStartPosition);
                Array.Copy(modDetails, 3, finalClass, traitStartPosition, modDetails.Length - 3);
                Array.Copy(ClassModifier, traitStartPosition, finalClass, traitStartPosition + modDetails.Length - 3, ClassModifier.Length - traitStartPosition);

                File.Delete(Path.Combine(finalDirectory, Class + ".class.asasm"));
                File.WriteAllLines(Path.Combine(finalDirectory, Class + ".class.asasm"), finalClass);

            }

            var reAssembleLocation = finalDirectory.Substring(0, finalDirectory.IndexOf("com")).Replace("temp\\", string.Empty);
            string abcNumber = reAssembleLocation.Substring(reAssembleLocation.IndexOf('-')).Replace("-", string.Empty).Replace("\\", string.Empty);

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = "Repackaging " + modName;
            }));

            var reAsm = new ProcessStartInfo();
            reAsm.FileName = "rabcasm.exe";
            reAsm.RedirectStandardError = true;
            reAsm.UseShellExecute = false;
            reAsm.Arguments = Path.Combine("temp", reAssembleLocation, reAssembleLocation.Replace("\\", string.Empty) + ".main.asasm");
            var reAsmProc = Process.Start(reAsm);
            while (reAsmProc != null && !reAsmProc.StandardError.EndOfStream)
            {
                string line = reAsmProc.StandardError.ReadLine();
                File.AppendAllText("debug.log", line + Environment.NewLine);
            }
            if (reAsmProc != null)
            {
                reAsmProc.WaitForExit();
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = "Finishing touches for " + modName;
            }));

            var doPatch = new ProcessStartInfo();
            doPatch.FileName = "abcreplace.exe";
            doPatch.RedirectStandardError = true;
            doPatch.UseShellExecute = false;
            doPatch.Arguments = Path.Combine("temp", fileName) + " " + abcNumber + " " + Path.Combine("temp", reAssembleLocation, reAssembleLocation.Replace("\\", string.Empty) + ".main.abc");
            var finalPatchProc = Process.Start(doPatch);
            while (finalPatchProc != null && !finalPatchProc.StandardError.EndOfStream)
            {
                string line = finalPatchProc.StandardError.ReadLine();
                File.AppendAllText("debug.log", line + Environment.NewLine);
            }
            if (finalPatchProc != null)
            {
                finalPatchProc.WaitForExit();
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = "Done patching " + modName + "!";
            }));

            File.Copy(Path.Combine("temp", fileName), Path.Combine(locationText, fileLocation), true);
        }

        private static void DeletePathWithLongFileNames(string path)
        {
            var tmpPath = @"\\?\" + path;
            var fso = new Scripting.FileSystemObject();
            fso.DeleteFolder(tmpPath, true);
        }
    }
}
