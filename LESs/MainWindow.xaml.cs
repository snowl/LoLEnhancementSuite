using AbcSharp.ABC;
using AbcSharp.SWF;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Windows.Media;

namespace LESs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string INTENDED_VERSION = "0.0.1.115";
        private bool IsGarena;

        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private readonly BackgroundWorker GarenaBackupWorker = new BackgroundWorker();
        private ErrorLevel _errorLevel = ErrorLevel.NoError;
        private Dictionary<CheckBox, LessMod> _lessMods = new Dictionary<CheckBox, LessMod>();
        private Stopwatch Stahpwatch;

        private string _modsDirectory="mods";

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Called when the program encounters any exception. Displays a message box to the user alerting them to the error.
        /// </summary>
        private void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            Exception Error = e.Exception;
            MessageBox.Show(Error.Message + Environment.NewLine + Error.StackTrace + Environment.NewLine);
            
        }

        /// <summary>
        /// Called on the initial loading of Lol Enhancement Suite.
        /// </summary>
        private void MainGrid_Loaded(object sender, RoutedEventArgs e)
        {
            LeagueVersionLabel.Content = INTENDED_VERSION;

            //Create the debug log. Delete it if it already exists.
            if (File.Exists("debug.log"))
                File.Delete("debug.log");

            File.Create("debug.log").Dispose();

            //Set the events for the worker when the patching starts.
            _worker.DoWork += worker_DoWork;
            _worker.RunWorkerCompleted += worker_RunWorkerCompleted;

            //Set the events for the Garena version removing mechanism worker.
            GarenaBackupWorker.DoWork += GarenaWorker_DoWork;
            GarenaBackupWorker.RunWorkerCompleted += GarenaWorker_WorkCompleted;

            //Enable exception logging if the debugger ISNT attached.
            if (!Debugger.IsAttached)
                AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;

            //try to find the mods in the base directory of the solution when the debugger is attached
            if (Debugger.IsAttached && !Directory.Exists(_modsDirectory) && Directory.Exists("../../../mods"))
            {
                _modsDirectory = "../../../mods";
            }

            //Make sure that the mods exist in the directory. Warn the user if they dont.
            if (!Directory.Exists(_modsDirectory))
                MessageBox.Show("Missing mods directory. Ensure that all files were extracted properly.", "Missing files");

            var modList = Directory.GetDirectories(_modsDirectory);

            //Add each mod to the mod list.
            foreach (string mod in modList)
            {
                string modJsonFile = Path.Combine(mod, "mod.json");

                if (File.Exists(modJsonFile))
                {
                    JavaScriptSerializer s = new JavaScriptSerializer();
                    LessMod lessMod = s.Deserialize<LessMod>(File.ReadAllText(modJsonFile));
                    lessMod.Directory = mod;

                    CheckBox Check = new CheckBox();
                    Check.IsChecked = !lessMod.DisabledByDefault;
                    Check.Content = lessMod.Name;
                    ModsListBox.Items.Add(Check);

                    _lessMods.Add(Check, lessMod);
                }
            }
        }

        /// <summary>
        /// Change the label & description when the mod is hovered over.
        /// </summary>
        private void ModsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CheckBox box = (CheckBox)ModsListBox.SelectedItem;

            if (box == null)
                return;

            LessMod lessMod = _lessMods[box];

            ModNameLabel.Content = lessMod.Name;
            ModDescriptionBox.Text = lessMod.Description;
            //see if our mod has an author and display it
            if (!string.IsNullOrEmpty(lessMod.Author))
                ModAuthorLabel.Content = "Author: " + lessMod.Author;
            else
                ModAuthorLabel.Content = "Author: Dark Voodoo Magicks";
        }

        /// <summary>
        /// Called when the user looks for their League of Legends installation
        /// </summary>
        private void FindButton_Click(object sender, RoutedEventArgs e)
        {
            //Disable patching if the user selects another league installation.
            PatchButton.IsEnabled = false;
            RemoveButton.Visibility = Visibility.Collapsed;

            //Create a file dialog for the user to locate their league of legends installation.
            OpenFileDialog findLeagueDialog = new OpenFileDialog();
            
            //If they don't have League of Legends in the default path, look for Garena.
            if (!Directory.Exists(Path.Combine("C:\\", "Riot Games", "League of Legends")))
                findLeagueDialog.InitialDirectory = Path.Combine("C:\\", "Program Files (x86)", "GarenaLoL", "GameData", "Apps", "LoL");
            else
                findLeagueDialog.InitialDirectory = Path.Combine("C:\\", "Riot Games", "League of Legends");

            findLeagueDialog.DefaultExt = ".exe";
            findLeagueDialog.Filter = "League of Legends Launcher|lol.launcher*.exe|Garena Launcher|lol.exe";

            bool? foundLeague = findLeagueDialog.ShowDialog();

            if (foundLeague == true)
            {
                LogToFile("Selected Location", findLeagueDialog.FileName);

                //Remove the executable from the location
                Uri Location = new Uri(findLeagueDialog.FileName);
                string SelectedLocation = Location.LocalPath.Replace(Location.Segments.Last(), string.Empty);

                //Get the executable name to check for Garena
                string LastSegment = Location.Segments.Last();

                if (!LastSegment.StartsWith("lol.launcher"))
                {
                    PatchButton.IsEnabled = true;
                    LocationTextbox.Text = Path.Combine(SelectedLocation, "Air");
                    this.IsGarena = true;
                    //Enable LESs uninstalling if backup file(s) exist
                    if(Directory.Exists(Path.Combine(LocationTextbox.Text, "LESsBackup")))
                    {
                        RemoveButton.Visibility = Visibility.Visible;
                        EnableButtons();
                    }
                }
                else
                {
                    RemoveButton.Visibility = Visibility.Visible;

                    //Check each RADS installation to find the latest installation
                    string radsLocation = Path.Combine(SelectedLocation, "RADS", "projects", "lol_air_client", "releases");

                    LogToFile("RADS Location", radsLocation);

                    var versionDirectories = Directory.GetDirectories(radsLocation);
                    string finalDirectory = "";
                    string version = "";
                    uint versionCompare = 0;
                    foreach (string x in versionDirectories)
                    {
                        string compare1 = x.Substring(x.LastIndexOfAny(new char[] { '\\', '/' }) + 1);

                        string[] versionParts = compare1.Split(new char[] { '.' });

                        if (!compare1.Contains(".") || versionParts.Length != 4)
                            continue;

                        uint CompareVersion;
                        try //versions have the format "x.x.x.x" where every x can be a value between 0 and 255 
                        {
                            CompareVersion = Convert.ToUInt32(versionParts[0]) << 24 | Convert.ToUInt32(versionParts[1]) << 16 | Convert.ToUInt32(versionParts[2]) << 8 | Convert.ToUInt32(versionParts[3]);
                        }
                        catch (FormatException) //can happen for directories like "0.0.0.asasd"
                        {
                            continue;
                        }

                        if (CompareVersion > versionCompare)
                        {
                            versionCompare = CompareVersion;
                            version = x.Replace(radsLocation + "\\", "");
                            finalDirectory = x;
                        }

                        LogToFile("Version Found", CompareVersion.ToString());
                    }

                    if (version != INTENDED_VERSION)
                    {
                        string Message = string.Format("This version of LESs is intended for {0}. Your current version of League of Legends is {1}. Continue? This could harm your installation.", INTENDED_VERSION, version);
                        MessageBoxResult versionMismatchResult = MessageBox.Show(Message, "Invalid Version", MessageBoxButton.YesNo);
                        if (versionMismatchResult == MessageBoxResult.No)
                            return;
                    }

                    EnableButtons();

                    LocationTextbox.Text = Path.Combine(finalDirectory, "deploy");
                    IsGarena = false;
                }

                Directory.CreateDirectory(Path.Combine(LocationTextbox.Text, "LESsBackup"));
                // We don't need this on Garena
                if(!IsGarena)
                    Directory.CreateDirectory(Path.Combine(LocationTextbox.Text, "LESsBackup", INTENDED_VERSION));
            }
        }

        /// <summary>
        /// Called when the user tries to patch their League of Legends installation.
        /// </summary>
        private void PatchButton_Click(object sender, RoutedEventArgs e)
        {
            PatchButton.IsEnabled = false;
            LogToFile("Patch", "Patching started");
            _worker.RunWorkerAsync();
        }

        /// <summary>
        /// Called when the user wants to remove LESs from their League of Legends installation.
        /// </summary>
        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if(IsGarena)
            {
                DisableButtons();
                Uri lolLocation = new Uri(LocationTextbox.Text);
                if(!Directory.Exists(Path.Combine(lolLocation.LocalPath, "LESsBackup")))
                {
                    StatusLabel.Content = "Backup file not found.";
                    EnableButtons();
                    return;
                }
                //Strip /Air from location string
                lolLocation = new Uri(lolLocation.LocalPath.Replace(lolLocation.Segments.Last(), String.Empty));
                if(File.Exists(Path.Combine(lolLocation.LocalPath, "LESs_recent.version")))
                {
                    // This LoL installation appears to be patched at least once
                    String RecentVersion = File.ReadAllText(Path.Combine(lolLocation.LocalPath, "LESs_recent.version"));
                    String CurrentVersion = File.ReadAllText(Path.Combine(lolLocation.LocalPath, "lol.version"));
                    if(!RecentVersion.Equals(CurrentVersion))
                    {
                        MessageBoxResult diagRst = MessageBox.Show("Current LoL version is not the same version as your backup file." + Environment.NewLine + "This can cause some damage." + Environment.NewLine + "Would you like to continue?", "Different version found!", MessageBoxButton.YesNo);
                        if(diagRst == MessageBoxResult.Yes)
                        {
                            GarenaBackupWorker.RunWorkerAsync();
                        }
                        else
                        {
                            StatusLabel.Content = "Aborted";
                            EnableButtons();
                            return;
                        }
                    }
                    else
                    {
                        GarenaBackupWorker.RunWorkerAsync();
                    }
                }
                else
                {
                    GarenaBackupWorker.RunWorkerAsync();
                }
            }
            else
            {
                /*This works by removing S_OK from the AIR installation. This has the same effect as clicking "Repair" in the patcher
                except it only makes it check the AIR installation, not the entire game. This speeds it up from 10-20 minutes
                to only a minute max.*/
                if (File.Exists(Path.Combine(LocationTextbox.Text.Substring(0, LocationTextbox.Text.Length - 7), "S_OK")))
                {
                    File.Delete(Path.Combine(LocationTextbox.Text.Substring(0, LocationTextbox.Text.Length - 7), "S_OK"));
                    MessageBox.Show("LESs will be removed next time League of Legends launches!");
                    StatusLabel.Content = "Removed LESs";
                }
            }
        }

        /// <summary>
        /// Gets all the mods and patches them.
        /// </summary>
        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            _errorLevel = ErrorLevel.NoError;

            //Gets the list of mods
            ItemCollection modCollection = null;
            Dispatcher.Invoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                modCollection = ModsListBox.Items;
            }));

            SetStatusLabelAsync("Gathering mods...");
            //Gets the list of mods that have been checked.
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
                    modsToPatch.Add(_lessMods[box]);
                }
            }

            string lolLocation = null;
            bool overwrite = true;
            Dispatcher.Invoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                lolLocation = LocationTextbox.Text;
            }));
            if (IsGarena)
            {
                MessageBox.Show("Garena detected! Please note that you must uninstall LESs before patching your LoL with Garena." + Environment.NewLine + "Otherwise, your Garena LoL patcher will complain hard and we are not responsible for it ;).");
                Uri lolRootLocation = new Uri(lolLocation);
                lolRootLocation = new Uri(lolRootLocation.LocalPath.Replace(lolRootLocation.Segments.Last(), String.Empty));
                // Get LoL latest patched date
                String versionLocation = Path.Combine(lolRootLocation.LocalPath, "lol.version");
                if (File.Exists(versionLocation))
                {
                    // Store the date in another file. It will be used in LESs removing.
                    File.Copy(versionLocation, Path.Combine(lolRootLocation.LocalPath, "LESs_recent.version"), true);
                }
                if(Directory.Exists(Path.Combine(lolLocation, "LESsBackup")))
                {
                    MessageBoxResult diagRst = MessageBox.Show("We found that you already have backup files. Overwriting it may result in you losing your original files." + Environment.NewLine + "Would you like to overwrite your old files?", "You already have backup files", MessageBoxButton.YesNo);
                    if (diagRst == MessageBoxResult.No)
                    {
                        overwrite = false;
                    }
                }
            }
            Dictionary<string, SwfFile> swfs = new Dictionary<string, SwfFile>();
            Stahpwatch = Stopwatch.StartNew();
            foreach (var lessMod in modsToPatch)
            {
                Debug.Assert(lessMod.Patches.Length > 0);
                SetStatusLabelAsync("Patching mod: " + lessMod.Name);
                foreach (var patch in lessMod.Patches)
                {
                    if (!swfs.ContainsKey(patch.Swf))
                    {
                        string fullPath = Path.Combine(lolLocation, patch.Swf);

                        //Backup the SWF
                        string CurrentLocation = "";
                        string[] FileLocation = patch.Swf.Split('/');
                        foreach (string s in FileLocation.Take(FileLocation.Length - 1))
                        {
                            CurrentLocation = Path.Combine(CurrentLocation, s);
                            if(IsGarena)
                            {
                                if (!Directory.Exists(Path.Combine(lolLocation, "LESsBackup", CurrentLocation)))
                                {
                                    Directory.CreateDirectory(Path.Combine(lolLocation, "LESsBackup", CurrentLocation));
                                }
                                if (!File.Exists(Path.Combine(lolLocation, "LESsBackup", INTENDED_VERSION, patch.Swf)))
                                {
                                    if(overwrite) File.Copy(Path.Combine(lolLocation, patch.Swf), Path.Combine(lolLocation, "LESsBackup", patch.Swf));
                                }
                            }
                            else
                            {
                                if (!Directory.Exists(Path.Combine(lolLocation, "LESsBackup", INTENDED_VERSION, CurrentLocation)))
                                {
                                    Directory.CreateDirectory(Path.Combine(lolLocation, "LESsBackup", INTENDED_VERSION, CurrentLocation));
                                }
                                if (!File.Exists(Path.Combine(lolLocation, "LESsBackup", INTENDED_VERSION, patch.Swf)))
                                {
                                    File.Copy(Path.Combine(lolLocation, patch.Swf), Path.Combine(lolLocation, "LESsBackup", INTENDED_VERSION, patch.Swf));
                                }
                            }                            
                        }

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
                            case "replace_trait": //replace trait (method)
                                asm = new Assembler(File.ReadAllText(Path.Combine(lessMod.Directory, patch.Code)));
                                TraitInfo newTrait = asm.Assemble() as TraitInfo;

                                int traitIndex = cls.Instance.GetTraitIndexByTypeAndName(newTrait.Type, newTrait.Name.Name);
                                bool classTrait = false;
                                if (traitIndex < 0)
                                {
                                    traitIndex = cls.GetTraitIndexByTypeAndName(newTrait.Type, newTrait.Name.Name);
                                    classTrait = true;
                                }
                                if (traitIndex < 0)
                                {
                                    throw new TraitNotFoundException(String.Format("Can't find trait \"{0}\" in class \"{1}\"", newTrait.Name.Name, patch.Class));
                                }

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
                            case "add_class_trait": //add new class trait (method)
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
                            case "add_instance_trait": //add new instance trait (method)
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
                            case "remove_instance_trait":
                                throw new NotImplementedException();
                            default:
                                throw new NotSupportedException("Unknown Action \"" + patch.Action + "\" in mod "+lessMod.Name);
                        }
                    }

                    if (!classFound)
                    {
                        _errorLevel = ErrorLevel.UnableToPatch;
                        throw new ClassNotFoundException(string.Format("Class {0} not found in file {1}", patch.Class, patch.Swf));
                    }
                }
            }
            //return;

            foreach (var patchedSwf in swfs)
            {
                try
                {
                    SetStatusLabelAsync("Applying mods: " + patchedSwf.Key);
                    string swfLoc = Path.Combine(lolLocation, patchedSwf.Key);
                    SwfFile.WriteFile(patchedSwf.Value, swfLoc);
                }
                catch
                {
                    _errorLevel = ErrorLevel.GoodJobYourInstallationIsProbablyCorruptedNow;
                    if (Debugger.IsAttached)
                        throw;
                }
            }
            Stahpwatch.Stop();
        }

        /// <summary>
        /// Called once LESs has been successfully patched into the client.
        /// </summary>
        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            switch (_errorLevel)
            {
                case ErrorLevel.NoError:
                    StatusLabel.Content = "Done patching!";
                    MessageBox.Show("LESs has been successfully patched into League of Legends!\n(In " + Stahpwatch.ElapsedMilliseconds + "ms)");
                    break;
                case ErrorLevel.UnableToPatch:
                    SetStatusLabelAsync("[Error] Please check debug.log for more information.");
                    MessageBox.Show("LESs encountered errors during patching. No mods have been applied.");
                    break;
                case ErrorLevel.GoodJobYourInstallationIsProbablyCorruptedNow:
                    SetStatusLabelAsync("[Critical Error] Please check debug.log for more information.");
                    MessageBox.Show("LESs encountered errors during patching.\nIt is possible your client is corrupted.\nPlease repair before trying again.");
                    break;
            }
            PatchButton.IsEnabled = true;
            RemoveButton.IsEnabled = true;
        }

        /// <summary>
        /// Called on backup when Garena is detected.
        /// </summary>
        private void GarenaWorker_DoWork(object sender, DoWorkEventArgs args)
        {
            String lolPath = null;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
                {
                    lolPath = LocationTextbox.Text;
                }
            ));
            Thread.Sleep(1000);
            String BackupPath = Path.Combine(lolPath, "LESsBackup");
            String[] BackupFiles = Directory.GetFiles(BackupPath, "*.*", SearchOption.AllDirectories);
            foreach(String file in BackupFiles)
            {
                Uri fileUri = new Uri(file);
                FileAttributes attr = File.GetAttributes(file);
                if(!((attr & FileAttributes.Directory) == FileAttributes.Directory))
                {
                    String newPath = file.Replace("\\LESsBackup", String.Empty);
                    SetStatusLabelAsync("Copying : " + fileUri.Segments.Last());
                    File.Copy(file, newPath, true);
                }
            }
        }

        /// <summary>
        /// Called once LESs has been removed from Garena LoL installation.
        /// </summary>
        private void GarenaWorker_WorkCompleted(object sender, RunWorkerCompletedEventArgs args)
        {
            SetStatusLabelAsync("Done removing!");
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
                {
                    EnableButtons();
                }
                ));
        }

        /// <summary>
        /// Outputs debug information to a file.
        /// </summary>
        private void LogToFile(string subject, string message)
        {
            File.AppendAllText("debug.log", string.Format("[{0}] {1}{2}", subject, message, Environment.NewLine));
        }

        /// <summary>
        /// Sets the text of the status label
        /// </summary>
        private void SetStatusLabelAsync(string text)
        {

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                StatusLabel.Content = text;
            }));
        }

        /// <summary>
        /// Disables PatchButton and RemoveButton.
        /// </summary>
        private void DisableButtons()
        {
            PatchButton.IsEnabled = false;
            RemoveButton.IsEnabled = false;
        }

        /// <summary>
        /// Enables PatchButton and RemoveButton.
        /// </summary>
        private void EnableButtons()
        {
            PatchButton.IsEnabled = true;
            RemoveButton.IsEnabled = true;
        }
    }
}
