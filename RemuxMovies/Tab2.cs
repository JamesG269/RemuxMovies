using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Json;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace RemuxMovies
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<NewDirInfo> SourceDirs = new List<NewDirInfo>();
        List<NewFileInfo> SourceFiles = new List<NewFileInfo>();

        private async void AddHardlinkButton_Click(object sender, RoutedEventArgs e)
        {
            await AddDir(HardlinkType);
            ClearWindows();
            await displayFilesToProcess();
        }
        private async void AddMoviesDir_Click(object sender, RoutedEventArgs e)
        {
            await AddDir(MovieType);
            ClearWindows();
            await displayFilesToProcess();
        }

        private async void AddMusicVideosDir_Click(object sender, RoutedEventArgs e)
        {
            await AddDir(MusicVideoType);
            ClearWindows();
            await displayFilesToProcess();
        }
        private async void AddTVShowsFolder(object sender, RoutedEventArgs e)
        {
            await AddDir(TVShowsType);
            ClearWindows();
            await displayFilesToProcess();
        }
        private async void AddNfosFolder(object sender, RoutedEventArgs e)
        {
            await AddDir(NfoType);
            ClearWindows();
            await displayFilesToProcess();
        }
        private async Task AddDir(int type)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "Input Folder";
            if (dialog.ShowDialog() == false)
            {
                return;
            }
            string VidDir = dialog.SelectedPath;
            dialog.Description = "Output Folder";            
            if (dialog.ShowDialog() == false)
            {
                return;
            }
            string OutputDir = dialog.SelectedPath;
            await GotSourceDirRun(VidDir, type, OutputDir);
            saveToXML();
        }
        private async Task<bool> GotSourceDirRun(string VidDir, int type, string OutputDir)
        {
            if (0 != Interlocked.Exchange(ref oneInt, 1))
            {
                return false;
            }
            await Task.Run(() => GotSourceDir(VidDir, type, OutputDir));                
            listView.ItemsSource = SourceDirs.ToList();                            
            ListViewUpdater();
            Interlocked.Exchange(ref oneInt, 0);
            return true;
        }
        private void GotSourceDir(string VidDir, int type, string OutputDir)
        {
            SourceDirs.RemoveAll(x => x.Directory == VidDir && x.type == type);
            AddNewDir(VidDir, type, OutputDir);                      
            List<NewFileInfo> files = GetFiles(VidDir, type, VidExts);            
            foreach (var file in files)
            {                
                var m = IsTVShow(file.FileName);
                if ((m.Success && type != TVShowsType) || (!m.Success && type == TVShowsType))
                {
                    continue;
                }                
                SourceFiles.RemoveAll(x => x.FullPath == file.FullPath && x.type == file.type);
                ConstructName(file, m);
                SourceFiles.Add(file);
            }
        }
        private void AddNewDir(string Name, int type, string OutputDir)
        {
            NewDirInfo temp = new NewDirInfo();            
            temp.Directory = Name.ToLower();
            temp.type = type;
            temp.OutputDir = OutputDir.ToLower();
            SourceDirs.Add(temp);
        }
        private Match IsTVShow(string nfi)
        {
            Match m = null;
            foreach (var r in TVShowRegex)
            {
                m = r.Match(nfi);
                if (m.Success == true)
                {
                    break;
                }
            }
            return m;
        }
        
        private void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedIndex > -1)
            {
                var listSelectedItems = listView.SelectedItems.OfType<NewDirInfo>().ToList();
                SourceFiles.RemoveAll(x => listSelectedItems.Any(c => c.Directory == x.fromDirectory && c.type == x.type));
                SourceDirs.RemoveAll(x => listSelectedItems.Any(c => c.Directory == x.Directory && c.type == x.type));
                listView.ItemsSource = SourceDirs.ToList();
                fileListView.ItemsSource = new Dictionary<string, string>();
            }
            ListViewUpdater();
            saveToXML();
        }

        private void RemoveFileItem_Click(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedIndex > -1)
            {
                var listSelectedItems = fileListView.SelectedItems.OfType<NewFileInfo>().ToList();
                SourceFiles.RemoveAll(c => listSelectedItems.Any(x => c.FileName == x.FileName && c.type == x.type));
            }
            ListViewUpdater();            
        }

        private void OpenExplorerFileItem_Click(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedIndex > -1)
            {
                var listSelectedItems = fileListView.SelectedItems.OfType<NewFileInfo>().ToList();
                if (listSelectedItems.Count > 0)
                {
                    OpenExplorer(listSelectedItems[0].Directory);
                }
            }            
        }

        private void OpenExplorerDirItem_Click(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedIndex > -1)
            {
                var listSelectedItems = listView.SelectedItems.OfType<NewDirInfo>().ToList();
                if (listSelectedItems.Count > 0)
                {
                    var dir = listSelectedItems[0].Directory;
                    OpenExplorer(dir);
                }
            }
        }

        private void OpenExplorerOutputItem_Click(object sender, RoutedEventArgs e)
        {
            if (outputDirListView.SelectedIndex > -1)
            {
                var listSelectedItems = outputDirListView.SelectedItems.OfType<NewDirInfo>().ToList();
                if (listSelectedItems.Count > 0)
                {
                    OpenExplorer(listSelectedItems[0].Directory);
                }
            }
        }
        private void OpenExplorer(string dir)
        {
            Process.Start("explorer.exe", dir);
        }

        private async void ProcessDirItem_Click(object sender, RoutedEventArgs e)
        {
            if (0 != Interlocked.Exchange(ref oneInt, 1))
            {
                return;
            }
            if (listView.SelectedIndex > -1)
            {
                var listSelectedItems = listView.SelectedItems.OfType<NewDirInfo>().ToList();
                List<NewFileInfo> sourceFiles = SourceFiles.Where(x => listSelectedItems.Any(c => c.Directory == x.fromDirectory && c.type == x.type)).ToList();
                await Start_ClickRun(sourceFiles);
            }
            Interlocked.Exchange(ref oneInt, 0);
        }

        private async void ProcessFileItem_Click(object sender, RoutedEventArgs e)
        {
            if (0 != Interlocked.Exchange(ref oneInt, 1))
            {
                return;
            }
            if (fileListView.SelectedIndex > -1)
            {
                var l = fileListView.SelectedItems.OfType<NewFileInfo>().ToList();
                List<NewFileInfo> sourceFiles = SourceFiles.Where(x => l.Any(c => c.FullPath == x.FullPath)).ToList();
                await Start_ClickRun(sourceFiles);
            }
            Interlocked.Exchange(ref oneInt, 0);
        }

        private void SkipFileItem_Click(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedIndex > -1)
            {
                var l = fileListView.SelectedItems.OfType<NewFileInfo>().ToList();
                foreach (var f in l)
                {
                    f._Remembered = !f._Remembered;
                }
            }
            ListViewUpdater();
        }
        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListViewUpdater();
        }
        private void ListViewUpdater()
        {
            if (listView.SelectedIndex > -1)
            {
                var list1 = listView.SelectedItems.OfType<NewDirInfo>().ToList();
                fileListView.ItemsSource = SourceFiles.Where(x => list1.Any(c => x.fromDirectory == c.Directory && x.type == c.type)).ToList();
            }
            UpdateColumnWidths(UpdateGrid);
            populateInfoLabel();
            return;
        }
        public void UpdateColumnWidths(Grid gridToUpdate)
        {            
            foreach (UIElement element in gridToUpdate.Children)
            {
                element.UpdateLayout();
                if (element is ListView)
                {                    
                    var e = element as ListView;
                    ListViewTargetUpdated(e);
                    e.UpdateLayout();
                }
            }
        }
        private static void UpdateColumnWidthsRun(GridView gridViewToUpdate)
        {
            foreach (var column in gridViewToUpdate.Columns)
            {
                // If this is an "auto width" column...
                
                //if (double.IsNaN(column.Width))
                {
                    // Set its Width back to NaN to auto-size again
                    column.Width = 0;
                    column.Width = double.NaN;
                }
                
            }
        }
        private void ListViewTargetUpdated(ListView listViewToUpdate)
        {
            // Get a reference to the ListView's GridView...        
            if (null != listViewToUpdate)
            {
                var gridView = listViewToUpdate.View as GridView;
                if (null != gridView)
                {
                    // ... and update its column widths
                    UpdateColumnWidthsRun(gridView);
                }
            }
        }

        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            var dirs = SourceDirs.ToList();
            ClearWindows();
            await AddSourceDirs(dirs);
            await displayFilesToProcess();
        }
        private async Task<bool> AddSourceDirs(List<NewDirInfo> dirs)
        {
            SourceDirs.Clear();
            SourceFiles.Clear();
            foreach (var d in dirs)
            {
                await GotSourceDirRun(d.Directory, d.type, d.OutputDir);
            }                        
            return true;
        }
    }
}