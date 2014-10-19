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
using Newtonsoft.Json;
using AbcSharp.ABC;
using AbcSharp.SWF;

namespace LESs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string IntendedVersion = "0.0.1.113";

        private readonly BackgroundWorker worker = new BackgroundWorker();
        private bool WasPatched = true;
        private Dictionary<CheckBox, LessMod> LessMods = new Dictionary<CheckBox, LessMod>();


        public MainWindow()
        {
            InitializeComponent();
            FindButton.AddHandler(MouseDownEvent, new RoutedEventHandler(FindButton_MouseDown), true);
            LeagueVersionLabel.Content = IntendedVersion;
            if (File.Exists("debug.log"))
                File.Delete("debug.log");

            File.Create("debug.log").Dispose();

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
                string modJsonFile = Path.Combine(Mod, "mod.json");

                if (File.Exists(modJsonFile))
                {
                    LessMod lessMod = JsonConvert.DeserializeObject<LessMod>(File.ReadAllText(modJsonFile));
                    CheckBox Check = new CheckBox();
                    lessMod.Directory = Mod;
                    Check.IsChecked = !lessMod.DisabledByDefault;
                    Check.Content = lessMod.Name;
                    LessMods.Add(Check, lessMod);
                    ModsListBox.Items.Add(Check);
                }
            }
        }

        private void ModsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CheckBox box = (CheckBox)ModsListBox.SelectedItem;

            if (box == null)
                return;

            LessMod lessMod = LessMods[box];

            ModNameLabel.Content = lessMod.Name;
            ModDescriptionBox.Text = lessMod.Description;

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
                    uint VersionCompare = 0;
                    foreach (string x in VersionDirectories)
                    {
                        string Compare1 = x.Substring(x.LastIndexOfAny(new char[] { '\\', '/' }) + 1);

                        string[] VersionParts = Compare1.Split(new char[] { '.' });

                        if (!Compare1.Contains(".") || VersionParts.Length != 4)
                        {
                            continue;
                        }

                        uint CompareVersion;
                        try
                        {
                            //versions have the format "x.x.x.x" where every x can be a value between 0 and 255 
                            CompareVersion = Convert.ToUInt32(VersionParts[0]) << 24 | Convert.ToUInt32(VersionParts[1]) << 16 | Convert.ToUInt32(VersionParts[2]) << 8 | Convert.ToUInt32(VersionParts[3]);
                        }
                        catch (FormatException) //can happen for directories like "0.0.0.asasd"
                        {
                            continue;
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
            Dispatcher.Invoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                modCollection = ModsListBox.Items;
            }));


            Directory.CreateDirectory("temp");
            List<LessMod> modsToPatch = new List<LessMod>();
            foreach (var x in modCollection)
            {
                CheckBox box = (CheckBox)x;
                bool isBoxChecked = false;
                Dispatcher.Invoke(DispatcherPriority.Input, new ThreadStart(() =>
                {
                    isBoxChecked = box.IsChecked ?? false;
                }));

                if (isBoxChecked)
                {
                    modsToPatch.Add(LessMods[box]);
                }
            }


            Dictionary<string, SwfFile> swfs = new Dictionary<string, SwfFile>();

            string lolLocation = null;

            Dispatcher.Invoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                lolLocation = LocationTextbox.Text;
            }));

            foreach (var lessMod in modsToPatch)
            {
                Debug.Assert(lessMod.Patches.Length > 0);
                foreach (var patch in lessMod.Patches)
                {
                    if (!swfs.ContainsKey(patch.Swf))
                    {
                        string fullPath = Path.Combine(lolLocation, patch.Swf);
                        if (!swfs.ContainsKey(patch.Swf))
                            swfs.Add(patch.Swf, SwfFile.ReadFile(fullPath));
                    }

                    SwfFile swf = swfs[patch.Swf];
                    List<DoAbcTag> tags = swf.GetDoAbcTags();
                    bool classFound = false;
                    foreach (var tag in tags)
                    {
                        //check if this tag contains our script
                        ScriptInfo si = tag.GetScriptByClassName(patch.Class);

                        //check next tag if it doesn't
                        if (si == null)
                            continue;

                        ClassInfo cls = si.GetClassByClassName(patch.Class);
                        classFound = true;
                        
                        Assembler asm;
                        switch (patch.Action)
                        {
                            case "replace_trait":
                                asm = new Assembler(File.ReadAllText(Path.Combine(lessMod.Directory, patch.Code)));
                                TraitInfo newTrait = asm.Assemble() as TraitInfo;

                                int traitIndex = cls.Instance.GetTraitIndexByTypeAndName(newTrait.Type, newTrait.Name.Name);
                                bool classTrait = false;
                                if (traitIndex<0)
                                {
                                    traitIndex = cls.GetTraitIndexByTypeAndName(newTrait.Type, newTrait.Name.Name);
                                    classTrait = true;
                                }
                                if (traitIndex<0)
                                    throw new TraitNotFoundException(String.Format("Can't find trait \"{0}\" in class \"{1}\"", newTrait.Name.Name, patch.Class));

                                if (classTrait)
                                {
                                    cls.Traits[traitIndex] = newTrait;
                                }
                                else
                                {
                                    cls.Instance.Traits[traitIndex] = newTrait;
                                }
                                break;
                            case "replace_cinit"://replace class constructor
                                asm = new Assembler(File.ReadAllText(Path.Combine(lessMod.Directory, patch.Code)));
                                cls.ClassInit = asm.Assemble() as MethodInfo;
                                break;
                            case "replace_iinit"://replace instance constructor
                                asm = new Assembler(File.ReadAllText(Path.Combine(lessMod.Directory, patch.Code)));
                                cls.Instance.InstanceInit = asm.Assemble() as MethodInfo;
                                break;
                            case "add_class_trait":
                                asm = new Assembler(File.ReadAllText(Path.Combine(lessMod.Directory, patch.Code)));
                                newTrait = asm.Assemble() as TraitInfo;
                                traitIndex = cls.GetTraitIndexByTypeAndName(newTrait.Type, newTrait.Name.Name);
                                if (traitIndex < 0)
                                {
                                    cls.Traits.Add(newTrait);
                                }
                                else
                                {
                                    cls.Traits[traitIndex] = newTrait;
                                }
                                break;
                            case "add_instance_trait":
                                asm = new Assembler(File.ReadAllText(Path.Combine(lessMod.Directory, patch.Code)));
                                newTrait = asm.Assemble() as TraitInfo;
                                traitIndex = cls.Instance.GetTraitIndexByTypeAndName(newTrait.Type, newTrait.Name.Name);
                                if (traitIndex < 0)
                                {
                                    cls.Instance.Traits.Add(newTrait);
                                }
                                else
                                {
                                    cls.Instance.Traits[traitIndex] = newTrait;
                                }
                                break;
                            case "remove_class_trait":
                                throw new NotImplementedException();
                                break;
                            case "remove_instance_trait":
                                throw new NotImplementedException();
                                break;
                            default:
                                throw new NotSupportedException("Unknown Action \"" + patch.Action + "\" in mod "+lessMod.Name);
                        }
                    }

                    if (!classFound)
                        throw new ClassNotFoundException(string.Format("Class {0} not found in file {1}", patch.Class, patch.Swf));
                }
            }

            foreach (var swfkv in swfs)
            {
                string swfLoc = Path.Combine(lolLocation, swfkv.Key);
                Debug.WriteLine("output: " + swfLoc);

                SwfFile.WriteFile(swfkv.Value, swfLoc);
            }
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
    }
}
