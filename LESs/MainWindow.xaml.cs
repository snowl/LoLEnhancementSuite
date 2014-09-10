using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Win32;
using Scripting;
using File = System.IO.File;

namespace LESs
{
    public partial class MainWindow
    {
        private const string IntendedVersion = "0.0.1.105";
        private readonly List<WorstHack> _reassembleLocations;

        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private bool _wasPatched = true;

        public MainWindow()
        {
            InitializeComponent();
            _reassembleLocations = new List<WorstHack>();
            FindButton.AddHandler(MouseDownEvent, new RoutedEventHandler(FindButton_MouseDown), true);
            LeagueVersionLabel.Content = IntendedVersion;
            if (File.Exists("debug.log"))
                File.Delete("debug.log");

            File.Create("debug.log").Dispose();

            if (Directory.Exists("temp"))
            {
                DeletePathWithLongFileNames(Path.GetFullPath("temp"));
            }

            if (Directory.Exists(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "wm")))
                MessageBox.Show(
                    "You may have malware on your system due to getting this application from an unknown source. Please delete C:/wm/ and the file inside it and then download this application from http://da.viddiaz.com/LESs");

            _worker.DoWork += worker_DoWork;
            _worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            if (!Debugger.IsAttached)
            {
                AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
            }
        }

        private void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            MessageBox.Show(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
            _wasPatched = false;
        }

        private void MainGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists("mods"))
            {
                MessageBox.Show("Missing mods directory. Ensure that all files were extracted properly.",
                    "Missing files");
            }

            var modList = Directory.GetDirectories("mods");

            foreach (var mod in modList)
            {
                var check = new CheckBox {IsChecked = true, Content = mod.Replace("mods\\", "")};
                if (File.Exists(Path.Combine(mod, "disabled")))
                    check.IsChecked = false;
                ModsListBox.Items.Add(check);
            }
        }

        private void ModsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var box = (CheckBox) ModsListBox.SelectedItem;

            if (box == null)
                return;

            var selectedMod = (string) box.Content;
            using (var reader = XmlReader.Create(Path.Combine("mods", selectedMod, "info.xml")))
            {
                while (reader.Read())
                {
                    if (!reader.IsStartElement()) continue;
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

        private void FindButton_MouseDown(object sender, RoutedEventArgs e)
        {
            PatchButton.IsEnabled = false;
            var findLeagueDialog = new OpenFileDialog();

            if (!Directory.Exists(Path.Combine("C:\\", "Riot Games", "League of Legends")))
            {
                //Gagarena
                findLeagueDialog.InitialDirectory = Path.Combine("C:\\", "Program Files (x86)", "GarenaLoL", "GameData",
                    "Apps", "LoL");
            }
            else
            {
                findLeagueDialog.InitialDirectory = Path.Combine("C:\\", "Riot Games", "League of Legends");
            }
            findLeagueDialog.DefaultExt = ".exe";
            findLeagueDialog.Filter = "League of Legends Launcher|lol.launcher*.exe|Garena Launcher|lol.exe";

            var result = findLeagueDialog.ShowDialog();

            if (result != true) return;
            File.AppendAllText("debug.log", findLeagueDialog.FileName + Environment.NewLine);
            var filename =
                findLeagueDialog.FileName.Replace("lol.launcher.exe", "").Replace("lol.launcher.admin.exe", "");
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
                var radLocation = Path.Combine(filename, "RADS", "projects", "lol_air_client", "releases");

                File.AppendAllText("debug.log", filename + Environment.NewLine + radLocation + Environment.NewLine);

                var versionDirectories = Directory.GetDirectories(radLocation);
                var finalDirectory = "";
                var version = "";
                uint versionCompare = 0;
                foreach (var x in versionDirectories)
                {
                    var compare1 = x.Substring(x.LastIndexOfAny(new[] {'\\', '/'}) + 1);

                    var versionParts = compare1.Split(new[] {'.'});

                    if (!compare1.Contains(".") || versionParts.Length != 4)
                    {
                        continue;
                    }

                    uint compareVersion;
                    try
                    {
                        //versions have the format "x.x.x.x" where every x can be a value between 0 and 255
                        compareVersion = Convert.ToUInt32(versionParts[0]) << 24 |
                                         Convert.ToUInt32(versionParts[1]) << 16 |
                                         Convert.ToUInt32(versionParts[2]) << 8 | Convert.ToUInt32(versionParts[3]);
                    }
                    catch (FormatException) //can happen for directories like "0.0.0.asasd"
                    {
                        continue;
                    }

                    if (compareVersion > versionCompare)
                    {
                        versionCompare = compareVersion;
                        version = x.Replace(radLocation + "\\", "");
                        finalDirectory = x;
                    }

                    File.AppendAllText("debug.log", x + Environment.NewLine + compareVersion + Environment.NewLine);
                }

                if (version != IntendedVersion)
                {
                    var versionMismatchResult =
                        MessageBox.Show(
                            "This version of LESs is intended for " + IntendedVersion +
                            ". Your current version of League of Legends is " + version +
                            ". Continue? This could harm your installation.", "Invalid Version",
                            MessageBoxButton.YesNo);
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

        private void PatchButton_Click(object sender, RoutedEventArgs e)
        {
            PatchButton.IsEnabled = false;

            File.AppendAllText("debug.log", @"Starting patch" + Environment.NewLine);

            _worker.RunWorkerAsync();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(Path.Combine(LocationTextbox.Text.Substring(0, LocationTextbox.Text.Length - 7), "S_OK")))
                return;
            File.Delete(Path.Combine(LocationTextbox.Text.Substring(0, LocationTextbox.Text.Length - 7), "S_OK"));
            MessageBox.Show("LESs will be removed next time League of Legends launches!");
            StatusLabel.Content = "Removed LESs";
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            ItemCollection modCollection = null;
            Dispatcher.BeginInvoke(DispatcherPriority.Input,
                new ThreadStart(() => { modCollection = ModsListBox.Items; }));


            Directory.CreateDirectory("temp");

            foreach (var x in modCollection)
            {
                var box = (CheckBox) x;
                bool? isBoxChecked = null;
                var boxName = "";
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
                {
                    if (box.IsChecked != null && (bool) box.IsChecked)
                    {
                        isBoxChecked = true;
                        boxName = (string) box.Content;
                    }
                    else
                    {
                        isBoxChecked = false;
                        boxName = "blah";
                    }
                }));


                if (isBoxChecked != null && !(bool) isBoxChecked) continue;
                var amountOfPatches = 1;

                using (var reader = XmlReader.Create(Path.Combine("mods", boxName, "info.xml")))
                {
                    while (reader.Read())
                    {
                        if (!reader.IsStartElement()) continue;
                        switch (reader.Name)
                        {
                            case "files":
                                reader.Read();
                                amountOfPatches = Convert.ToInt32(reader.Value);
                                break;
                        }
                    }
                }

                for (var i = 0; i < amountOfPatches; i++)
                {
                    Patcher(boxName, i);
                }
            }

            foreach (var s in _reassembleLocations)
            {
                Repackage(s);
            }

            var copiedNames = new List<string>();

            foreach (var s in _reassembleLocations.Where(s => !copiedNames.Contains(s.FileName)))
            {
                copiedNames.Add(s.FileName);
                CopyToClient(s);
            }

            DeletePathWithLongFileNames(Path.GetFullPath("temp"));
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show(_wasPatched
                ? "LESs has been successfully patched into League of Legends!"
                : "LESs encountered errors during patching. However, some patches may still be applied.");
            PatchButton.IsEnabled = true;
            StatusLabel.Content = "Done patching!";
        }

        private void Patcher(string modName, int amountOfPatches)
        {
            var patchNumber = "";
            if (amountOfPatches >= 1)
                patchNumber = amountOfPatches.ToString(CultureInfo.InvariantCulture);

            var modDetails = File.ReadAllLines(Path.Combine("mods", modName, "patch" + patchNumber + ".txt"));
            var fileLocation = "null";
            var tryFindClass = "null";
            var traitToModify = "null";
            var isNewTrait = false;
            foreach (var s in modDetails)
            {
                if (s.StartsWith("#"))
                {
                    tryFindClass = s.Substring(1);
                }
                else if (s.StartsWith("@@@"))
                {
                    traitToModify = s.Substring(3);
                }
                else if (s.StartsWith("@+@")) //Insert the new trait above this one
                {
                    traitToModify = s.Substring(3);
                    isNewTrait = true;
                }
                else if (s.StartsWith("~"))
                {
                    fileLocation = s.Substring(1);
                }
            }

            File.AppendAllText("debug.log", @"Patching " + modName + patchNumber + Environment.NewLine);

            var filePart = fileLocation.Split('/');
            var fileName = filePart[filePart.Length - 1];

            var locationText = "";
            Dispatcher.BeginInvoke(DispatcherPriority.Input,
                new ThreadStart(() => { locationText = LocationTextbox.Text; }));

            if (!Directory.Exists(Path.Combine("temp", fileLocation.Replace(".dat", ""))))
            {
                Directory.CreateDirectory(Path.Combine("temp", fileLocation.Replace(".dat", "")));

                var n = "";
                foreach (var s in filePart.Take(filePart.Length - 1))
                {
                    n = Path.Combine(n, s);
                    if (!Directory.Exists(Path.Combine(locationText, "LESsBackup", IntendedVersion, n)))
                    {
                        Directory.CreateDirectory(Path.Combine(locationText, "LESsBackup", IntendedVersion, n));
                    }
                }
                if (!File.Exists(Path.Combine(locationText, "LESsBackup", IntendedVersion, fileLocation)))
                {
                    File.Copy(Path.Combine(locationText, fileLocation),
                        Path.Combine(locationText, "LESsBackup", IntendedVersion, fileLocation));
                }

                File.Copy(Path.Combine(locationText, fileLocation),
                    Path.Combine("temp", fileLocation.Replace(".dat", ""), fileName));

                Dispatcher.BeginInvoke(DispatcherPriority.Input,
                    new ThreadStart(() => { StatusLabel.Content = "Exporting patch " + modName; }));

                File.AppendAllText("debug.log", @"Running abcexport" + Environment.NewLine);

                var export = new ProcessStartInfo
                {
                    FileName = "abcexport.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = Path.Combine("temp", fileLocation.Replace(".dat", ""), fileName)
                };
                var exportProc = Process.Start(export);
                if (exportProc != null)
                {
                    exportProc.WaitForExit();
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Input,
                    new ThreadStart(() => { StatusLabel.Content = "Disassembling patch (" + modName + ")"; }));

                var abcFiles = Directory.GetFiles(Path.Combine("temp", fileLocation.Replace(".dat", "")), "*.abc");

                File.AppendAllText("debug.log", @"Got " + abcFiles.Length + @" files" + Environment.NewLine);

                foreach (var disasmProc in abcFiles.Select(s => new ProcessStartInfo
                {
                    FileName = "rabcdasm.exe",
                    Arguments = s,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }).Select(Process.Start).Where(disasmProc => disasmProc != null))
                {
                    disasmProc.WaitForExit();
                }
            }

            if (tryFindClass.IndexOf(':') == 0)
            {
                File.AppendAllText("debug.log", @"INVALID MOD!!!" + Environment.NewLine);
                throw new Exception("Invalid mod " + modName);
            }

            var directories =
                Directory.GetDirectories(Path.Combine("temp", fileLocation.Replace(".dat", "")), "*",
                    SearchOption.AllDirectories).ToList();

            //Get all directories that match the requested class to modify
            var searchFor = tryFindClass.Substring(0, tryFindClass.IndexOf(':'));
            var foundDirectories = new List<string>();
            foreach (var s in directories)
            {
                if (!s.Contains("com"))
                    continue;

                var tempS = s;
                tempS = tempS.Substring(tempS.IndexOf("com", StringComparison.Ordinal));
                tempS = tempS.Replace("\\", ".");
                if (tempS == searchFor)
                {
                    foundDirectories.Add(s);
                }
            }

            if (foundDirectories.Count == 0)
            {
                File.AppendAllText("debug.log",
                    @"No class matching " + searchFor + @" for mod " + modName + Environment.NewLine);
                throw new Exception("No class matching " + searchFor + " for mod " + modName);
            }

            var finalDirectory = "";
            var Class = tryFindClass.Substring(tryFindClass.IndexOf(':')).Replace(":", "");
            //Find the directory that has the requested class
            foreach (var s in from s in foundDirectories
                let m = Directory.GetFiles(s)
                let x = Path.Combine(s, Class + ".class.asasm")
                where m.Contains(x)
                select s)
            {
                finalDirectory = s;
            }

            var classModifier = File.ReadAllLines(Path.Combine(finalDirectory, Class + ".class.asasm"));

            //return if the new trait already exists
            if (isNewTrait)
            {
                if (classModifier.Any(l => l == modDetails[3]))
                {
                    return;
                }
            }

            var traitStartPosition = 0;
            var traitEndLocation = 0;
            //Get location of trait
            for (var i = 0; i < classModifier.Length; i++)
            {
                if (classModifier[i] != traitToModify) continue;
                traitStartPosition = i;
                break;
            }

            if (traitStartPosition == 0)
            {
                File.AppendAllText("debug.log", @"Trait start location was not found! Corrupt mod?");
                throw new Exception("Trait start location was not found! Corrupt mod?");
            }

            if (!isNewTrait)
            {
                //Get end location of trait
                for (var i = traitStartPosition; i < classModifier.Length; i++)
                {
                    if (classModifier[i].Trim() != "end ; method") continue;
                    if (classModifier[i + 1].Trim() == "end ; trait")
                    {
                        traitEndLocation = i + 2;
                    }
                    else
                    {
                        traitEndLocation = i + 1;
                    }
                    break;
                }

                if (traitEndLocation < traitStartPosition)
                {
                    File.AppendAllText("debug.log",
                        @"Trait end location was smaller than trait start location! " + traitEndLocation + @", " +
                        traitStartPosition);
                    throw new Exception("Trait end location was smaller than trait start location! " + traitEndLocation +
                                        ", " + traitStartPosition);
                }

                var startTrait = new string[traitStartPosition];
                Array.Copy(classModifier, startTrait, traitStartPosition);
                var afterTrait = new string[classModifier.Length - traitEndLocation];
                Array.Copy(classModifier, traitEndLocation, afterTrait, 0, classModifier.Length - traitEndLocation);

                var finalClass = new string[startTrait.Length + (modDetails.Length - 3) + afterTrait.Length];
                Array.Copy(startTrait, finalClass, traitStartPosition);
                Array.Copy(modDetails, 3, finalClass, traitStartPosition, (modDetails.Length - 3));
                Array.Copy(afterTrait, 0, finalClass, traitStartPosition + (modDetails.Length - 3), afterTrait.Length);

                File.Delete(Path.Combine(finalDirectory, Class + ".class.asasm"));
                File.WriteAllLines(Path.Combine(finalDirectory, Class + ".class.asasm"), finalClass);
            }
            else
            {
                var finalClass = new string[classModifier.Length + (modDetails.Length - 3)];
                Array.Copy(classModifier, 0, finalClass, 0, traitStartPosition);
                Array.Copy(modDetails, 3, finalClass, traitStartPosition, modDetails.Length - 3);
                Array.Copy(classModifier, traitStartPosition, finalClass, traitStartPosition + modDetails.Length - 3,
                    classModifier.Length - traitStartPosition);

                File.Delete(Path.Combine(finalDirectory, Class + ".class.asasm"));
                File.WriteAllLines(Path.Combine(finalDirectory, Class + ".class.asasm"), finalClass);
            }

            var h = new WorstHack
            {
                FileName = fileName,
                LocationText = locationText,
                ReAssembleLocation =
                    finalDirectory.Substring(0, finalDirectory.IndexOf("com", StringComparison.Ordinal))
                        .Replace("temp\\", ""),
                FileLocation = fileLocation
            };

            if (!_reassembleLocations.Contains(h))
                _reassembleLocations.Add(h);
        }

        private void Repackage(WorstHack data)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input,
                new ThreadStart(() => { StatusLabel.Content = "Patching mods to client..."; }));

            var abcNumber =
                data.ReAssembleLocation.Substring(data.ReAssembleLocation.IndexOf('-'))
                    .Replace("-", "")
                    .Replace("\\", "");

            var reAsm = new ProcessStartInfo
            {
                FileName = "rabcasm.exe",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = Path.Combine("temp",
                    data.ReAssembleLocation + data.FileName.Replace(".dat", "") + "-" + abcNumber + ".main.asasm")
            };
            var reAsmProc = Process.Start(reAsm);
            while (reAsmProc != null && !reAsmProc.StandardError.EndOfStream)
            {
                var line = reAsmProc.StandardError.ReadLine();
                File.AppendAllText("debug.log", line + Environment.NewLine);
            }
            if (reAsmProc != null)
            {
                reAsmProc.WaitForExit();
            }

            var doPatch = new ProcessStartInfo
            {
                FileName = "abcreplace.exe",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = Path.Combine("temp", data.FileLocation.Replace(".dat", ""), data.FileName) + " " +
                            abcNumber + " " +
                            Path.Combine("temp",
                                data.ReAssembleLocation + data.FileName.Replace(".dat", "") + "-" + abcNumber +
                                ".main.abc")
            };
            var finalPatchProc = Process.Start(doPatch);
            while (finalPatchProc != null && !finalPatchProc.StandardError.EndOfStream)
            {
                var line = finalPatchProc.StandardError.ReadLine();
                File.AppendAllText("debug.log", line + Environment.NewLine);
            }
            if (finalPatchProc != null)
            {
                finalPatchProc.WaitForExit();
            }
        }

        private void CopyToClient(WorstHack data)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input,
                new ThreadStart(() => { StatusLabel.Content = "Patched " + data.FileName + "!"; }));

            File.Copy(Path.Combine("temp", data.FileLocation.Replace(".dat", ""), data.FileName),
                Path.Combine(data.LocationText, data.FileLocation), true);
        }

        private static void DeletePathWithLongFileNames(string path)
        {
            var tmpPath = @"\\?\" + path;
            var fso = new FileSystemObject();
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
