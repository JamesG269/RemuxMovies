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
        List<NewDirInfo> OutputDirs = new List<NewDirInfo>();
        private void SaveSources()
        {
            Properties.Settings.Default.VidSources.Clear();
            foreach (var dir in SourceDirs)
            {
                Properties.Settings.Default.VidSources.Add(dir.type.ToString());
                Properties.Settings.Default.VidSources.Add(dir.Name);
            }
            Properties.Settings.Default.Save();
        }
        private void SaveOutputs()
        {
            Properties.Settings.Default.VidOutputs.Clear();
            foreach (var dir in OutputDirs)
            {
                Properties.Settings.Default.VidOutputs.Add(dir.type.ToString());
                Properties.Settings.Default.VidOutputs.Add(dir.Name);
            }
            Properties.Settings.Default.Save();
        }
        private async void AddMoviesDir_Click(object sender, RoutedEventArgs e)
        {
            await AddDir(MovieType);
        }

        private async void AddMusicVideosDir_Click(object sender, RoutedEventArgs e)
        {
            await AddDir(MusicVideoType);
        }
        private async void AddTVShowsFolder(object sender, RoutedEventArgs e)
        {
            await AddDir(TVShowsType);
        }
        private async Task AddDir(int type)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if (dialog.ShowDialog() == false)
            {
                return;
            }
            string VidDir = dialog.SelectedPath;
            await GotSourceDirRun(VidDir, type);
            SaveSources();
        }

        private async Task GotSourceDirRun(string VidDir, int type)
        {
            if (0 != Interlocked.Exchange(ref oneInt, 1))
            {
                return;
            }
            if (Directory.Exists(VidDir))
            {
                await Task.Run(() => GotSourceDir(VidDir, type));                
                listView.ItemsSource = SourceDirs.ToList();                
            }
            ListViewUpdater();
            Interlocked.Exchange(ref oneInt, 0);
        }
        private void GotSourceDir(string VidDir, int type)
        {
            SourceDirs.RemoveAll(x => x.Name == VidDir && x.type == type);
            NewDirInfo temp = new NewDirInfo();
            temp.Name = VidDir;
            temp.type = type;
            SourceDirs.Add(temp);            
            List<NewFileInfo> files = GetFiles(VidDir, "*.mkv;*.mp4;*.avi;*.m4v;");            
            foreach (var file in files)
            {                
                var m = IsTVShow(file.Name);
                if ((m.Success && type != TVShowsType) || (!m.Success && type == TVShowsType))
                {
                    continue;
                }
                file.type = type;
                SourceFiles.RemoveAll(x => x.originalFullName == file.originalFullName && x.type == file.type);
                ConstructName(file, m);
                SourceFiles.Add(file);
            }
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

        private void ChangeMovieOutputDir(object sender, RoutedEventArgs e)
        {
            ChangeOutputDir(MovieType);
        }

        private void ChangeMusicVidOutputDir(object sender, RoutedEventArgs e)
        {
            ChangeOutputDir(MusicVideoType);
        }
        private void AddTVShowsOutputFolder(object sender, RoutedEventArgs e)
        {
            ChangeOutputDir(TVShowsType);
        }
        private void ChangeOutputDir(int type)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if (dialog.ShowDialog() == false)
            {
                return;
            }
            string outputDir = dialog.SelectedPath;
            ChangeOutputDirRun(outputDir, type);
            SaveOutputs();
        }
        private void ChangeOutputDirRun(string outputDir, int type)
        {
            if (!Directory.Exists(outputDir))
            {
                return;
            }
            OutputDirs.RemoveAll(x => x.type == type);
            NewDirInfo temp = new NewDirInfo();
            temp.Name = outputDir;
            temp.type = type;

            OutputDirs.Add(temp);
            outputDirListView.ItemsSource = OutputDirs.ToList();            
            ListViewUpdater();
        }

        private void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedIndex > -1)
            {
                var listSelectedItems = listView.SelectedItems.OfType<NewDirInfo>().ToList();
                SourceFiles.RemoveAll(x => listSelectedItems.Any(c => c.Name == x.fromDirectory && c.type == x.type));
                SourceDirs.RemoveAll(x => listSelectedItems.Any(c => c.Name == x.Name && c.type == x.type));
                listView.ItemsSource = SourceDirs.ToList();
                fileListView.ItemsSource = new Dictionary<string, string>();
            }
            ListViewUpdater();
            SaveSources();
        }

        private void RemoveFileItem_Click(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedIndex > -1)
            {
                var listSelectedItems = fileListView.SelectedItems.OfType<NewFileInfo>().ToList();
                SourceFiles.RemoveAll(c => listSelectedItems.Any(x => c.Name == x.Name && c.type == x.type));
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
                    OpenExplorer(listSelectedItems[0].DirectoryName);
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
                    var dir = listSelectedItems[0].Name;
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
                    OpenExplorer(listSelectedItems[0].Name);
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
                List<NewFileInfo> sourceFiles = SourceFiles.Where(x => listSelectedItems.Any(c => c.Name == x.fromDirectory && c.type == x.type)).ToList();
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
                List<NewFileInfo> sourceFiles = SourceFiles.Where(x => l.Any(c => c.FullName == x.FullName)).ToList();
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
                fileListView.ItemsSource = SourceFiles.Where(x => list1.Any(c => x.fromDirectory == c.Name && x.type == c.type)).ToList();
            }
            UpdateColumnWidths();
            populateInfoLabel();
        }
        public void UpdateColumnWidths()
        {            
            foreach (UIElement element in UpdateGrid.Children)
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
        private static void UpdateColumnWidthsRun(GridView gridView)
        {
            foreach (var column in gridView.Columns)
            {
                // If this is an "auto width" column...
                if (double.IsNaN(column.Width))
                {
                    // Set its Width back to NaN to auto-size again
                    column.Width = 0;
                    column.Width = double.NaN;
                }
            }
        }
        private void ListViewTargetUpdated(ListView listView)
        {
            // Get a reference to the ListView's GridView...        
            if (null != listView)
            {
                var gridView = listView.View as GridView;
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
            SourceDirs.Clear();
            SourceFiles.Clear();
            foreach (var d in dirs)
            {
                await GotSourceDirRun(d.Name, d.type);
            }
        }

    }
}