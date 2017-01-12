using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.IO;
using System.Linq;
namespace CloneDIR_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public class Flags
        {
            public static bool exit;
        }

        //Constants
        private int _verify_count = 0;
        private const int TIMER_DELAY = 5000; //Timer delay in ms
        string FILENAME_CONFIG = "config.txt";
        const string LABEL_BUTTON_START = "Start";
        const string LABEL_BUTTON_STOP = "Stop";


        //Instance variables
        private string _source = "";
        private string _destination = "";

        private FileSystemWatcher _watcher;
        NotifyIcon _notifyIcon;
        System.Timers.Timer _timer;

        //State variables
        private STATUS _runStatus = STATUS.STOPPED;
        public enum STATUS { STARTED, STOPPED};
        private bool force_cancel = false;
        private bool backoff = false;

        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            if (File.Exists(FILENAME_CONFIG))
            {
                //Restore previous state
                StreamReader sr = new StreamReader(FILENAME_CONFIG); 
                _source = sr.ReadLine(); 
                textBox_Source.Text = _source;
                _destination = sr.ReadLine();
                textBox_Destination.Text = _destination;
                sr.Close();
            }
            
        }

        private void SaveSettings()
        {
            StreamWriter sw = new StreamWriter(FILENAME_CONFIG);
            sw.WriteLine(_source);
            sw.WriteLine(_destination);
            sw.Flush();
            sw.Close();
        }
        //===================================================
        //EVENTS
        //===================================================
        private void button_Source_Explore_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (Directory.Exists(this._source))
            {
                dialog.SelectedPath = this._source;
            }
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if(result == System.Windows.Forms.DialogResult.OK)
            {
                _source = dialog.SelectedPath;
                textBox_Source.Text = dialog.SelectedPath;
                SaveSettings();
            }
        }

        private void button_Destination_Explore_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (Directory.Exists(this._destination))
            {
                dialog.SelectedPath = this._destination;
            }
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                _destination = dialog.SelectedPath;
                textBox_Destination.Text = dialog.SelectedPath;
                SaveSettings();
            }
        }

        private void button_StartStop_Click(object sender, RoutedEventArgs e)
        {
            if(_runStatus == STATUS.STARTED)
            {
                this.SetState(STATUS.STOPPED);
            }
            else if(_runStatus == STATUS.STOPPED)
            {
                this.SetState(STATUS.STARTED);
            }
            else
            {
                Feedback_WriteLine("Unknown state. Starting file watchers");
                StartFileWatchers(textBox_Source.Text);
                _runStatus = STATUS.STARTED;
                force_cancel = false;
                button_StartStop.Content = LABEL_BUTTON_STOP;
            }
        }

        private void SetState(STATUS newState)
        {
            if(newState == STATUS.STARTED)
            {
                StartTimedSync();
                bool success = StartFileWatchers(_source);
                if (success)
                {
                    Feedback_WriteLine("File watchers started.");
                    _runStatus = STATUS.STARTED;
                    force_cancel = false;
                    HandleDirectoryDiffsBackground(_source, _destination);

                    //UI Elements must be started on the main thread
                    Dispatcher.Invoke(new Action(() => {
                        button_StartStop.Content = LABEL_BUTTON_STOP;
                    }));
                    
                }
                else
                {
                    Feedback_WriteLine("File watchers failed to be started");
                }
            }
            else if(newState == STATUS.STOPPED)
            {
                this.StopTimedSync();
                bool success = StopFileWatchers(_source);
                if (success)
                {
                    Feedback_WriteLine("File watchers stopped");
                    _runStatus = STATUS.STOPPED;
                    force_cancel = true;

                    //UI Elements must be started on the main thread
                    Dispatcher.Invoke(new Action(() => {
                        button_StartStop.Content = LABEL_BUTTON_START;
                    }));
                }
                else
                {
                    Feedback_WriteLine("File watchers failed to be stopped");
                }
            }
            else
            {
                Feedback_WriteLine(String.Format("Invalid state: {0}", newState));
            }

        }

        private void StartTimedSync()
        {
            this._timer = new System.Timers.Timer();
            this._timer.Interval = TIMER_DELAY;
            this._timer.Elapsed += Sync;
            this._timer.Start();
            Feedback_WriteLine(String.Format("Timed sync started with duration: {0} s", TIMER_DELAY / 1000f));
        }
        private void StopTimedSync()
        {
            this._timer.Stop();
            Feedback_WriteLine(String.Format("Timed sync stopped"));
        }
        private void Sync(object sender, System.Timers.ElapsedEventArgs e)
        {
            HandleDirectoryDiffsBackground(_source, _destination);
        }
        //===================================================
        //UI
        //===================================================
        public void Feedback_WriteLine(string line)
        {
            this.Dispatcher.Invoke(new Action(() => {
                textBox_Feedback.Text += (System.Environment.NewLine + line);
                textBox_Feedback.Text = textBox_Feedback.Text.Trim();
                textBox_Feedback.ScrollToEnd();
            }));
        }


        //===================================================
        //FRAMEWORK
        //===================================================

        private void button_Hide_Click(object sender, RoutedEventArgs e)
        {
            if(_notifyIcon != null)
            {
                _notifyIcon.Dispose();
            }
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = Properties.Resources.syncIcon;
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Server running";
            _notifyIcon.DoubleClick += Notify_Icon_Double_Click;
            _notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu();
            //Notify icon context menu
            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem();
            exit.Text = "Exit";
            exit.Click += Exit;
            exit.Index = 0;
            _notifyIcon.ContextMenu.MenuItems.Add(exit);

            this.Hide();
        }

        
        private void Exit(object sender, EventArgs args)
        {
            Flags.exit = true;
            _notifyIcon.Dispose();
            this.Close();
        }

        private void Notify_Icon_Double_Click(object sender, EventArgs args)
        {
            this.Show(); //make visible
            this.Activate(); //bring to front
            this.WindowState = WindowState.Normal; //in the case where window is minimized
        }
        //===================================================
        //WATCHERS
        //===================================================
        private bool StopFileWatchers(string directory)
        {
            _watcher.Dispose();
            return true;
        }

        private bool StartFileWatchers(string directory)
        {
            bool started = false;
            if (Directory.Exists(directory))
            {
                _watcher = new FileSystemWatcher();
                _watcher.Path = directory;
                _watcher.NotifyFilter = NotifyFilters.Attributes |
                                        NotifyFilters.CreationTime |
                                        NotifyFilters.DirectoryName |
                                        NotifyFilters.FileName |
                                        NotifyFilters.LastAccess |
                                        NotifyFilters.LastWrite |
                                        NotifyFilters.Security |
                                        NotifyFilters.Size;
                _watcher.Changed += new FileSystemEventHandler(WatchedDirectoryChanged);
                _watcher.Deleted += new FileSystemEventHandler(WatchedDirectoryChanged);
                _watcher.Created += new FileSystemEventHandler(WatchedDirectoryChanged);
                _watcher.Renamed += new RenamedEventHandler(WatchedDirectoryChanged);
                _watcher.EnableRaisingEvents = true;
                _watcher.IncludeSubdirectories = true;
                started = true;
            }
            return started;
        }
        
        private void WatchedDirectoryChanged(object sender, FileSystemEventArgs args)
        {
            if(this.backoff == false)
            {
                HandleWatchedDirectories(sender, args);
            }
        }

        private void HandleWatchedDirectories(object sender, FileSystemEventArgs args)
        {
            Feedback_WriteLine("==========================================");
            string filePath = args.FullPath;
            string relativePath = filePath.Substring(_source.Length);

            if (System.IO.Path.IsPathRooted(relativePath))
            {
                relativePath = relativePath.TrimStart(System.IO.Path.DirectorySeparatorChar);
                relativePath = relativePath.TrimStart(System.IO.Path.AltDirectorySeparatorChar);
            }


            string scoped_source = System.IO.Path.Combine(_source, relativePath);
            string scoped_target = System.IO.Path.Combine(_destination, relativePath);
            Feedback_WriteLine(String.Format("Watched directory changed. Updating content: \n       Source: \"{0}\"\n       Destination: \"{1}\"\n", scoped_source, scoped_target));

            bool isFile = File.Exists(scoped_source);
            if (!isFile)
            {
                //if is not a file, either directory or not exist
                if (Directory.Exists(scoped_source))
                {
                    //if directory, set as the source to update
                    HandleDirectoryDiffsBackground(scoped_source, scoped_target);
                }
                else
                {
                    //if source does not exist, display message
                    //attempt to delete the non-existent folder in target directory, if it exists
                    if (Directory.Exists(scoped_target))
                    {
                        Directory.Delete(scoped_target, true);
                        Feedback_WriteLine(String.Format("Deleting directory: {0}", scoped_source));
                    }
                    else if (File.Exists(scoped_target))
                    {
                        File.Delete(scoped_target);
                        Feedback_WriteLine(String.Format("Deleting file: {0}", scoped_source));
                    }
                    else
                    {
                        Feedback_WriteLine(String.Format("File does not exist: {0}", scoped_source));

                    }
                }
            }
            else
            {
                //if a file has been specified, get directory and use HandleDirectoryDiffs
                string containing_directory = System.IO.Path.GetDirectoryName(scoped_source);
                string relative_dir = containing_directory.Substring(_source.Length);
                if (System.IO.Path.IsPathRooted(relative_dir))
                {
                    relative_dir = relative_dir.TrimStart(System.IO.Path.DirectorySeparatorChar);
                    relative_dir = relative_dir.TrimStart(System.IO.Path.AltDirectorySeparatorChar);
                }

                string resolved_source_dir = System.IO.Path.Combine(_source, relative_dir);
                string resolved_target_dir = System.IO.Path.Combine(_destination, relative_dir);
                HandleDirectoryDiffsBackground(resolved_source_dir, resolved_target_dir);

                //in the event that the file content has been modified, update that specific file
                CopyFileWhenReadyBackground(scoped_source, scoped_target);

            }
            Feedback_WriteLine("==========================================\r\n");
        }

        private void CopyFileWhenReadyBackground(string source, string destination)
        {
            System.Threading.Thread thread = new System.Threading.Thread(() =>
            {
                CopyFileWhenReady(source, destination);
            });
            thread.Start();
        }

        private void CopyFileWhenReady(string source, string destination)
        {
            //Everything ok
            string target_dir = System.IO.Path.GetDirectoryName(destination);

            //check if destination to copy to exists, and create if it doesn't
            if (!Directory.Exists(target_dir))
            {
                try
                {
                    Directory.CreateDirectory(target_dir);
                }
                catch
                {
                    this.SetState(STATUS.STOPPED);
                    Feedback_WriteLine("Unable to create destination folder. Stopping.");
                }
            }
            FileInfo fileinfo = new FileInfo(source);
            if (IsFileLocked(fileinfo))
            {
                //If file is locked don't bother
                this.backoff = true;
                System.Threading.Thread.Sleep(5000);
                this.backoff = false;
                Feedback_WriteLine(String.Format("File locked: {0}, backing off\n", source));
            }
            else
            {
                //If not backing off, and file is not locked, copy
                File.Copy(source, destination, true);
                Feedback_WriteLine(String.Format("File copied: \n       {0}\n       {1}\n", source, destination));
            }
        }
        protected virtual bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        //===================================================
        //DIRECTORIES
        //===================================================
        string MESSAGE_DIR_NOT_FOUND = "Directory not found: \n\t{0}\n\t{1}";

        private void HandleDirectoryDiffsBackground(string source, string target)
        {
            System.Threading.Thread thread = new System.Threading.Thread(() => {
                Feedback_WriteLine(String.Format("Synchonising {0} with {1}.", source, target));
                HandleDirectoryDiffs(source, target);
                Feedback_WriteLine(String.Format("{0} and {1} synced.", source, target));
            });
            thread.Start();
        }
        private void HandleDirectoryDiffs(string source, string target)
        {
            if (!force_cancel)
            {
                //Feedback_WriteLine(String.Format("Handling directory diffs: \n       {0}\n       {1}\n", source, target));
                if (!Directory.Exists(target))
                {
                    try
                    {
                        Directory.CreateDirectory(target);
                    }
                    catch
                    {
                        this.SetState(STATUS.STOPPED);
                        Feedback_WriteLine("Unable to create destination folder. Stopping.");
                    }
                }
                if (Directory.Exists(source) && Directory.Exists(target))
                {
                    //Get the current state of both the source and target directories
                    string[] source_directories = Directory.GetDirectories(source);
                    string[] target_directories = Directory.GetDirectories(target);
                    string[] source_files = Directory.GetFiles(source);
                    string[] target_files = Directory.GetFiles(target);

                    //Declare the arrays for the relative paths
                    List<string> source_directories_relative = new List<string>();
                    List<string> target_directories_relative = new List<string>();
                    List<string> source_files_relative = new List<string>();
                    List<string> target_files_relative = new List<string>();

                    //For each of the source directories, remove the root value to get the relative path
                    foreach (string source_dir in source_directories)
                    {
                        string path_relative = source_dir.Substring(source.Length);
                        source_directories_relative.Add(path_relative);
                    }

                    //For each of the target directories, remove the root value to get the relative path
                    foreach (string target_dir in target_directories)
                    {
                        string path_relative = target_dir.Substring(target.Length);
                        target_directories_relative.Add(path_relative);
                    }

                    //For each of the source files, remove the root value to get the relative path
                    foreach (string source_file in source_files)
                    {
                        string path_relative = source_file.Substring(source.Length);
                        source_files_relative.Add(path_relative);
                    }

                    //For each of the target files, remove the root vlaue to get the relative path
                    foreach (string target_file in target_files)
                    {
                        string path_relative = target_file.Substring(target.Length);
                        target_files_relative.Add(path_relative);
                    }

                    //Find the files/directories that were added in the source directory and update them
                    var new_directories_in_source_relative = source_directories_relative.Except(target_directories_relative);
                    var new_files_in_source_relative = source_files_relative.Except(target_files_relative);

                    //Find the files/directories that were removed in the source directory and update them
                    var directories_removed_from_source_relative = target_directories_relative.Except(source_directories_relative);
                    var files_removed_from_source_relative = target_files_relative.Except(source_files_relative);

                    //Handle new directories recursively
                    foreach (string new_directory_relative in new_directories_in_source_relative)
                    {
                        string relative_path = new_directory_relative;
                        if (System.IO.Path.IsPathRooted(relative_path))
                        {
                            relative_path = relative_path.TrimStart(System.IO.Path.DirectorySeparatorChar);
                            relative_path = relative_path.TrimStart(System.IO.Path.AltDirectorySeparatorChar);
                        }
                        string target_directory = System.IO.Path.Combine(target, relative_path);
                        string source_directory = System.IO.Path.Combine(source, relative_path);
                        HandleDirectoryDiffs(source_directory, target_directory);
                        Feedback_WriteLine(String.Format("New directory: {0}", target_directory));
                    }

                    //Handle new files
                    foreach (string new_file_relative in new_files_in_source_relative)
                    {
                        string relative_path = new_file_relative;
                        if (System.IO.Path.IsPathRooted(relative_path))
                        {
                            relative_path = relative_path.TrimStart(System.IO.Path.DirectorySeparatorChar);
                            relative_path = relative_path.TrimStart(System.IO.Path.AltDirectorySeparatorChar);
                        }
                        string target_file = System.IO.Path.Combine(target, relative_path);
                        string source_file = System.IO.Path.Combine(source, relative_path);
                        CopyFileWhenReadyBackground(source_file, target_file);
                        Feedback_WriteLine(String.Format("New file: {0}", target_file));
                    }

                    //Handle removed directories within source
                    foreach (string directory_relative in directories_removed_from_source_relative)
                    {
                        string relative_path = directory_relative;
                        if (System.IO.Path.IsPathRooted(relative_path))
                        {
                            relative_path = relative_path.TrimStart(System.IO.Path.DirectorySeparatorChar);
                            relative_path = relative_path.TrimStart(System.IO.Path.AltDirectorySeparatorChar);
                        }
                        string target_directory = System.IO.Path.Combine(target, relative_path);
                        if (Directory.Exists(target_directory))
                        {
                            try
                            {
                                Directory.Delete(target_directory, true);
                                Feedback_WriteLine(String.Format("Removing directory: {0}", target_directory));
                            }
                            catch
                            {
                                Feedback_WriteLine(String.Format("Failed to remove directory: {0}", target_directory));
                            }

                        }
                    }

                    //Handle files removed in source
                    foreach (string filepath_relative in files_removed_from_source_relative)
                    {
                        string relative_path = filepath_relative;
                        if (System.IO.Path.IsPathRooted(relative_path))
                        {
                            relative_path = relative_path.TrimStart(System.IO.Path.DirectorySeparatorChar);
                            relative_path = relative_path.TrimStart(System.IO.Path.AltDirectorySeparatorChar);
                        }
                        string target_filepath = System.IO.Path.Combine(target, relative_path);
                        if (File.Exists(target_filepath))
                        {
                            try
                            {
                                File.Delete(target_filepath);
                                Feedback_WriteLine(String.Format("Removing file: {0}", target_filepath));
                            }
                            catch
                            {
                                Feedback_WriteLine(String.Format("Failed to delete file at destination: {0}", target_filepath));
                            }
                            
                        }
                    }
                }
            }
            
        }

        private void button_verify_Click(object sender, RoutedEventArgs e)
        {

            System.Threading.Thread thread = new System.Threading.Thread(() => {

                Feedback_WriteLine(String.Format("Verifying source and destinations."));
                VerifyDirectoryFiles(this._source, this._destination, true);
                Feedback_WriteLine(String.Format("Verification complete."));
            });
            thread.Start();
            
        }

        private void VerifyDirectoryFiles(string source, string target, bool update = false)
        {
            if(Flags.exit != true)
            {
                string[] subdirectories_source = Directory.GetDirectories(source);
                string[] subdirectories_target = Directory.GetDirectories(target);
                string[] files_source = Directory.GetFiles(source);
                //Check directories
                foreach (string subdirectory_source in subdirectories_source)
                {
                    string relative_path = subdirectory_source.Substring(source.Length);
                    string subdirectory_target = System.IO.Path.Combine(target, relative_path.Trim( new char[] {'\\','/' }));
                    VerifyDirectoryFiles(subdirectory_source, subdirectory_target);
                }

                //Check files
                foreach (string file_source in files_source)
                {
                    string relative_path = file_source.Substring(source.Length);
                    string file_target = System.IO.Path.Combine(target, relative_path.Trim(new char[] { '\\', '/' }));
                    //VERIFY the hashes of the files, if they both exist
                    if (File.Exists(file_source) && File.Exists(file_target))
                    {
                        //start verification
                        using (var md5 = System.Security.Cryptography.MD5.Create())
                        {
                            Feedback_WriteLine(String.Format("Verifying: {0}", relative_path));
                            var hash_source = md5.ComputeHash(File.OpenRead(file_source));
                            var hash_target = md5.ComputeHash(File.OpenRead(file_target));
                            if (hash_source != hash_target)
                            {
                                //copy the file to the target again
                                Feedback_WriteLine(String.Format("Modified file: {0}", file_source));
                                if(update == true)
                                {
                                    CopyFileWhenReadyBackground(file_source, file_target);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void button_Clear_Click(object sender, RoutedEventArgs e)
        {
            textBox_Feedback.Text = "";
        }
    }
}
