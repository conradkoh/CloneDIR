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

        private string _source = "";
        private string _destination = "";
        private FileSystemWatcher _watcher;
        NotifyIcon _notifyIcon;
        private STATUS _runStatus = STATUS.STOPPED;
        public enum STATUS { STARTED, STOPPED};

        private bool force_cancel = false;

        string FILENAME_CONFIG = "config.txt";
        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            if (File.Exists(FILENAME_CONFIG))
            {
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
            string LABEL_BUTTON_START = "Start";
            string LABEL_BUTTON_STOP = "Stop";
            if(_runStatus == STATUS.STARTED)
            {
                bool success = StopFileWatchers(_source);
                if (success)
                {
                    Feedback_WriteLine("File watchers stopped");
                    _runStatus = STATUS.STOPPED;
                    force_cancel = true;
                    button_StartStop.Content = LABEL_BUTTON_START;
                }
                else
                {
                    Feedback_WriteLine("File watchers failed to be stopped");
                }
            }
            else if(_runStatus == STATUS.STOPPED)
            {
                bool success = StartFileWatchers(_source);
                if (success)
                {
                    Feedback_WriteLine("File watchers started.");
                    _runStatus = STATUS.STARTED;
                    force_cancel = false;
                    HandleDirectoryDiffsBackground(_source, _destination);
                    button_StartStop.Content = LABEL_BUTTON_STOP;
                }
                else
                {
                    Feedback_WriteLine("File watchers failed to be started");
                }
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
            FileInfo file_source = new FileInfo(source);
            int count = 0;
            while (IsFileLocked(file_source) && count < 100)
            {
                System.Threading.Thread.Sleep(1000);
            }
            //check if destination to copy to exists, and create if it doesn't
            if(count >= 100)
            {
                //issues with copying

            }
            else
            {
                //Everything ok
                string target_dir = System.IO.Path.GetDirectoryName(destination);
                if (!Directory.Exists(target_dir))
                {
                    Directory.CreateDirectory(target_dir);
                }
                bool copied = false;
                while (!copied)
                {
                    try
                    {
                        File.Copy(source, destination, true);
                        copied = true;
                        Feedback_WriteLine(String.Format("File copied: \n       {0}\n       {1}\n", source, destination));
                    }
                    catch
                    {
                    }

                }
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
                    Directory.CreateDirectory(target);
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
                            File.Delete(target_filepath);
                            Feedback_WriteLine(String.Format("Removing file: {0}", target_filepath));
                        }
                    }
                }
            }
            
        }

    }
}
