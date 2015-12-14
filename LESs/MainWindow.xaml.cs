using AbcSharp.ABC;
using AbcSharp.SWF;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LESs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string selected_version;
        private const string VersionURL = "http://da.viddiaz.com/LESs/version.php?v=";

        private List<int> _supportedVersions = new List<int>();
        private JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private ErrorLevel _errorLevel = ErrorLevel.NoError;
        private Dictionary<CheckBox, LessMod> _lessMods = new Dictionary<CheckBox, LessMod>();
        private Stopwatch timer;
        private ServerType type;
        private string BackupVersion;

        private string _modsDirectory = "mods";

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Called when the program encounters any unhandled exception. Displays a message box to the user alerting them to the error.
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.ExceptionObject.ToString());
        }

        /// <summary>
        /// Called on the initial loading of Lol Enhancement Suite.
        /// </summary>
        private async void MainGrid_Loaded(object sender, RoutedEventArgs e)
        {
            string AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            LeagueVersionLabel.Content = AssemblyVersion;
            BackupVersion = AssemblyVersion;

            //Set the events for the worker when the patching starts.
            _worker.DoWork += worker_DoWork;
            _worker.RunWorkerCompleted += worker_RunWorkerCompleted;

            //Enable exception logging if the debugger ISNT attached.
            if (!Debugger.IsAttached)
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            //Try to find the mods in the base directory of the solution when the debugger is attached
            if (Debugger.IsAttached && !Directory.Exists(_modsDirectory) && Directory.Exists("../../../mods"))
            {
                _modsDirectory = "../../../mods";
            }

            //Add a reload button if we are debugging
#if DEBUG
            ReloadButton.Visibility = Visibility.Visible;
#endif

            //Make sure that the mods exist in the directory. Warn the user if they dont.
            if (!Directory.Exists(_modsDirectory))
            {
                MessageBox.Show("Missing mods directory. Ensure that all files were extracted properly.", "Missing files");
                Environment.Exit(0);
            }
            
            LoadMods();

            try
            {
                using (WebClient c = new WebClient())
                {
                    _supportedVersions = serializer.Deserialize<List<int>>(await c.DownloadStringTaskAsync($"{VersionURL}{AssemblyVersion}"));
                }
            }
            catch //Couldn't load versions...
            { }
        }

        private void LoadMods()
        {

            ModsListBox.Items.Clear();
            _lessMods.Clear();
            //Get all mods within the mod directory
            var modList = Directory.GetDirectories(_modsDirectory);

            //Add each mod to the mod list.
            foreach (string mod in modList)
            {
                string modJsonFile = Path.Combine(mod, "mod.json");

                if (File.Exists(modJsonFile))
                {
                    LessMod lessMod = serializer.Deserialize<LessMod>(File.ReadAllText(modJsonFile));
                    lessMod.Directory = mod;

                    CheckBox ModCheckBox = new CheckBox()
                    {
                        IsChecked = !lessMod.DisabledByDefault && !lessMod.PermaDisable, //Don't automatically check disabled by default mods
                        IsEnabled = !lessMod.PermaDisable,
                        Content = lessMod.Name
                    };

                    ModsListBox.Items.Add(ModCheckBox);
                    _lessMods.Add(ModCheckBox, lessMod);
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
                ModAuthorLabel.Content = "Author: Anonymous";
        }

        /// <summary>
        /// Called when the user looks for their League of Legends installation
        /// </summary>
        private void FindButton_Click(object sender, RoutedEventArgs e)
        {
            //Disable patching if the user selects another league installation.
            PatchButton.IsEnabled = false;
            RemoveButton.IsEnabled = false;

            //Create a file dialog for the user to locate their league of legends installation.
            OpenFileDialog findLeagueDialog = new OpenFileDialog();
            findLeagueDialog.DefaultExt = ".exe";
            findLeagueDialog.Filter = "League of Legends Launcher|lol.launcher*.exe|Garena Launcher|lol.exe"; //Only show the league of legends executables

            string RiotPath = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "Riot Games", "League of Legends");
            string GarenaPath = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "Program Files (x86)", "GarenaLoL", "GameData", "Apps", "LoL");

            //If they don't have League of Legends in the default path, look for Garena.
            if (!Directory.Exists(RiotPath))
                findLeagueDialog.InitialDirectory = Path.GetFullPath(GarenaPath);
            else
                findLeagueDialog.InitialDirectory = Path.GetFullPath(RiotPath);

            bool? selectedExectuable = findLeagueDialog.ShowDialog();

            if (selectedExectuable == false || selectedExectuable == null)
                return;

            //Remove the executable from the location
            Uri Location = new Uri(findLeagueDialog.FileName);
            string SelectedLocation = Location.LocalPath.Replace(Location.Segments.Last(), string.Empty);

            //Get the executable name to check for Garena
            string LastSegment = Location.Segments.Last();

            //If the file name is lol.exe, then it's garena
            if (!LastSegment.StartsWith("lol.launcher"))
            {
                type = ServerType.GARENA;
                RemoveButton.IsEnabled = true;
                PatchButton.IsEnabled = true;
                LocationTextbox.Text = Path.Combine(SelectedLocation, "Air");
            }
            else
            {
                //Check each RADS installation to find the latest installation
                string radsLocation = Path.Combine(SelectedLocation, "RADS", "projects", "lol_air_client", "releases");

                //Compare the version text with format x.x.x.x to get the largest directory
                var versionDirectories = Directory.GetDirectories(radsLocation);

                string finalDirectory = "";
                int BiggestVersion = 0;

                //Get the biggest version in the directory
                foreach (string x in versionDirectories)
                {
                    string CurrentVersion = new Uri(x).Segments.Last();
                    string[] VersionNumbers = CurrentVersion.Split('.');
                    for (int i = 0; i < VersionNumbers.Length; i++)
                    {
                        //Ensure that numbers like 0.0.2.1 are larger than 0.0.1.999
                        VersionNumbers[i] = VersionNumbers[i].PadLeft(3, '0');
                    }

                    int Version = Convert.ToInt32(string.Join("", VersionNumbers));

                    if (Version > BiggestVersion)
                    {
                        BiggestVersion = Version;
                        finalDirectory = x;
                    }
                }

                BackupVersion = BiggestVersion.ToString();

                //If the version isn't the intended version, show a message to the user. This is just a warning and probably can be ignored
                if (!_supportedVersions.Contains(BiggestVersion) && !Debugger.IsAttached)
                {
                    string Message = "This version of LESs may not support your version of League of Legends. Continue? This could harm your installation.";
                    MessageBoxResult versionMismatchResult = MessageBox.Show(Message, "Invalid Version", MessageBoxButton.YesNo);
                    if (versionMismatchResult == MessageBoxResult.No)
                        return;
                }

                type = ServerType.NORMAL;
                PatchButton.IsEnabled = true;
                RemoveButton.IsEnabled = true;

                LocationTextbox.Text = Path.Combine(finalDirectory, "deploy");
            }

            //Create the LESsBackup directory to allow the user to uninstall if they wish to later on.
            Directory.CreateDirectory(Path.Combine(LocationTextbox.Text, "LESsBackup"));
            Directory.CreateDirectory(Path.Combine(LocationTextbox.Text, "LESsBackup", BackupVersion));
        }

        /// <summary>
        /// Called when the user tries to patch their League of Legends installation.
        /// </summary>
        private void PatchButton_Click(object sender, RoutedEventArgs e)
        {
            PatchButton.IsEnabled = false;
            ReloadButton.IsEnabled = false;
            _worker.RunWorkerAsync();
        }

        /// <summary>
        /// Called when the user wants to remove LESs from their League of Legends installation.
        /// </summary>
        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            new RemovePopup(type, LocationTextbox.Text).Show();
        }

        /// <summary>
        /// Gets all the mods and patches them.
        /// </summary>
        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            _errorLevel = ErrorLevel.NoError;

            //Gets the list of mods
            ItemCollection modCollection = null;
            string lolLocation = null;

            //Get the data from the UI thread
            Dispatcher.Invoke(DispatcherPriority.Input, new ThreadStart(() =>
            {
                modCollection = ModsListBox.Items;
                lolLocation = LocationTextbox.Text;
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

            //Create a new dictionary to hold the SWF definitions in. This stops opening and closing the same SWF file if it's going to be modified more than once.
            Dictionary<string, SwfFile> swfs = new Dictionary<string, SwfFile>();

            //Start the stopwatch to see how long it took to patch (aiming for ~5 sec or less on test machine)
            timer = Stopwatch.StartNew();

            //Go through each modification
            foreach (var lessMod in modsToPatch)
            {
                SetStatusLabelAsync("Patching mod: " + lessMod.Name);

                //Go through each patch within the mod
                foreach (var patch in lessMod.Patches)
                {
                    //If the swf hasn't been loaded, load it into the dictionary.
                    if (!swfs.ContainsKey(patch.Swf))
                    {
                        string fullPath = Path.Combine(lolLocation, patch.Swf);

                        //Backup the SWF
                        string CurrentLocation = "";
                        string[] FileLocation = patch.Swf.Split('/', '\\');
                        foreach (string s in FileLocation.Take(FileLocation.Length - 1))
                        {
                            CurrentLocation = Path.Combine(CurrentLocation, s);
                            if (!Directory.Exists(Path.Combine(lolLocation, "LESsBackup", BackupVersion, CurrentLocation)))
                            {
                                Directory.CreateDirectory(Path.Combine(lolLocation, "LESsBackup", BackupVersion, CurrentLocation));
                            }
                        }
                        if (!File.Exists(Path.Combine(lolLocation, "LESsBackup", BackupVersion, patch.Swf)))
                        {
                            File.Copy(Path.Combine(lolLocation, patch.Swf), Path.Combine(lolLocation, "LESsBackup", BackupVersion, patch.Swf));
                        }

                        swfs.Add(patch.Swf, SwfFile.ReadFile(fullPath));
                    }

                    //Get the SWF file that is being modified
                    SwfFile swf = swfs[patch.Swf];

                    //Get the ABC tags (containing the code) from the swf file.
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
                        //Perform the action based on what the patch defines
                        switch (patch.Action)
                        {
                            case "replace_trait": //replace trait (method)
                                //Load the code from the patch and assemble it to be inserted into the code
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

                                //Modified the found trait
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
                                //Load the code from the patch and assemble it to be inserted into the code
                                asm = new Assembler(File.ReadAllText(Path.Combine(lessMod.Directory, patch.Code)));
                                cls.ClassInit = asm.Assemble() as MethodInfo;
                                break;
                            case "replace_iinit"://replace instance constructor
                                //Load the code from the patch and assemble it to be inserted into the code
                                asm = new Assembler(File.ReadAllText(Path.Combine(lessMod.Directory, patch.Code)));
                                cls.Instance.InstanceInit = asm.Assemble() as MethodInfo;
                                break;
                            case "add_class_trait": //add new class trait (method)
                                //Load the code from the patch and assemble it to be inserted into the code
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
                                //Load the code from the patch and assemble it to be inserted into the code
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
                                throw new NotSupportedException($"Unknown Action \"{patch.Action}\" in mod {lessMod.Name}");
                        }
                    }

                    if (!classFound)
                    {
                        _errorLevel = ErrorLevel.UnableToPatch;
                        throw new ClassNotFoundException($"Class {patch.Class} not found in file {patch.Swf}");
                    }
                }
            }

            //Save the modified SWFS to their original location
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
            timer.Stop();
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
                    MessageBox.Show("LESs has been successfully patched into League of Legends!\n(In " + timer.ElapsedMilliseconds + "ms)");
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
            ReloadButton.IsEnabled = true;
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
        /// Reloads all mods
        /// </summary>
        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMods();
        }
    }
}
