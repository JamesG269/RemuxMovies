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
        private async void AddMoviesDir_Click(object sender, RoutedEventArgs e)
        {
            await AddDir(MovieType);
        }

        List<NewDirInfo> SourceDirs = new List<NewDirInfo>();
        List<NewFileInfo> SourceFiles = new List<NewFileInfo>();
        List<NewDirInfo> OutputDirs = new List<NewDirInfo>();

        private async void AddMusicVideosDir_Click(object sender, RoutedEventArgs e)
        {
            await AddDir(MusicVideoType);
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

        private async Task GotSourceDirRun(string VidDir, int type)
        {
            if (0 != Interlocked.Exchange(ref oneInt, 1))
            {
                return;
            }
            await Task.Run(() => GotSourceDir(VidDir, type));
            listView.ItemsSource = SourceDirs;
            listView.Items.Refresh();
            Interlocked.Exchange(ref oneInt, 0);
        }
        private void GotSourceDir(string VidDir, int type)
        {
            if (!Directory.Exists(VidDir))
            {
                return;
            }
            if (SourceDirs.Where(x => x.Name == VidDir && x.type == type).Count() > 0)
            {
                SourceDirs.Remove(SourceDirs.Where(x => x.Name == VidDir).First());
            }
            NewDirInfo temp = new NewDirInfo();
            temp.Name = VidDir;
            temp.type = type;
            SourceDirs.Add(temp);
            List<NewFileInfo> ftemp = GetFiles(VidDir, "*.mkv;*.mp4;*.avi;*.m4v;");
            foreach (var f in ftemp)
            {
                var m = IsTVShow(f);
                if ((m.Success && type != TVShowsType) || (!m.Success && type == TVShowsType))
                {
                    continue;
                }
                f.type = type;
                if (SourceFiles.Where(x => x.originalFullName == f.originalFullName).Count() > 0)
                {
                    SourceFiles.Remove(SourceFiles.Where(x => x.originalFullName == f.originalFullName).First());
                }
                ConstructName(f, m);
                SourceFiles.Add(f);
            }
        }

        private Match IsTVShow(NewFileInfo nfi)
        {
            Match m = null;
            foreach (var r in TVShowRegex)
            {
                m = r.Match(nfi.Name);
                if (m.Success == true)
                {
                    break;
                }
            }
            return m;
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView_SelectionChangedRun();
        }
        private void ListView_SelectionChangedRun()
        {
            if (listView.SelectedIndex == -1)
            {
                return;
            }
            var list1 = listView.SelectedItems.OfType<NewDirInfo>().ToList();
            if (list1.Count == 0)
            {
                return;
            }
            List<NewFileInfo> files = new List<NewFileInfo>();
            foreach (var l in list1)
            {
                foreach (var f in SourceFiles)
                {
                    if (f.fromDirectory == l.Name && f.type == l.type)
                    {
                        files.Add(f);
                    }
                }
            }
            fileListView.ItemsSource = files;
            fileListView.Items.Refresh();
            UpdateColumnWidths();
        }


        private void ChangeMovieOutputDir(object sender, RoutedEventArgs e)
        {
            ChangeOutputDir(MovieType);
        }

        private void ChangeMusicVidOutputDir(object sender, RoutedEventArgs e)
        {
            ChangeOutputDir(MusicVideoType);
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
            if (OutputDirs.Where(x => x.type == type).Count() > 0)
            {
                OutputDirs.Remove(OutputDirs.Where(x => x.type == type).First());
            }
            NewDirInfo temp = new NewDirInfo();
            temp.Name = outputDir;
            temp.type = type;

            OutputDirs.Add(temp);
            outputDirListView.ItemsSource = OutputDirs;
            outputDirListView.Items.Refresh();
        }

        private void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedIndex >= 0)
            {
                var listSelectedItems = listView.SelectedItems.OfType<NewDirInfo>().ToList();
                foreach (var list1 in listSelectedItems)
                {
                    List<NewFileInfo> fileList = new List<NewFileInfo>();
                    fileList.AddRange(SourceFiles);

                    foreach (var f in fileList)
                    {
                        if (f.fromDirectory == list1.Name)
                        {
                            SourceFiles.Remove(f);
                        }
                    }
                    SourceDirs.Remove(list1);
                }
                listView.ItemsSource = SourceDirs;
                listView.Items.Refresh();
                fileListView.ItemsSource = new Dictionary<string, string>();
                fileListView.Items.Refresh();
                SaveSources();
            }
        }

        private void RemoveFileItem_Click(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedIndex >= 0)
            {
                var listSelectedItems = fileListView.SelectedItems.OfType<NewFileInfo>().ToList();
                foreach (var list1 in listSelectedItems)
                {
                    SourceFiles.Remove(list1);
                }
                ListView_SelectionChangedRun();
            }
        }

        private void OpenExplorerFileItem_Click(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedIndex >= 0)
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
            if (listView.SelectedIndex >= 0)
            {
                var listSelectedItems = listView.SelectedItems.OfType<NewDirInfo>().ToList();
                if (listSelectedItems.Count > 0)
                {
                    var dir = listSelectedItems[0].Name;
                    OpenExplorer(dir);
                }
            }
        }
        private void OpenExplorer(string dir)
        {
            Process.Start("explorer.exe", dir);
        }

        private void OpenExplorerOutputItem_Click(object sender, RoutedEventArgs e)
        {
            if (outputDirListView.SelectedIndex >= 0)
            {
                var listSelectedItems = outputDirListView.SelectedItems.OfType<NewDirInfo>().ToList();
                if (listSelectedItems.Count > 0)
                {
                    OpenExplorer(listSelectedItems[0].Name);
                }
            }
        }

        private async void ProcessDirItem_Click(object sender, RoutedEventArgs e)
        {
            if (0 != Interlocked.Exchange(ref oneInt, 1))
            {
                return;
            }
            if (listView.SelectedIndex >= 0)
            {
                var listSelectedItems = listView.SelectedItems.OfType<NewDirInfo>().ToList();
                List<NewFileInfo> sourceFiles = SourceFiles.Where(x => listSelectedItems.Select(c => c.Name).Any(x.fromDirectory.Contains)).ToList();
                await Start_ClickRun(sourceFiles.Where(x => listSelectedItems.Select(c => c.type).Any(x.type.Equals)).ToList());
            }
            Interlocked.Exchange(ref oneInt, 0);
        }

        private async void ProcessFileItem_Click(object sender, RoutedEventArgs e)
        {
            if (0 != Interlocked.Exchange(ref oneInt, 1))
            {
                return;
            }
            if (fileListView.SelectedIndex >= 0)
            {
                var l = fileListView.SelectedItems.OfType<NewFileInfo>().Select(x => x.FullName).ToList();
                List<NewFileInfo> sourceFiles = SourceFiles.Where(x => l.Any(x.FullName.Equals)).ToList();
                await Start_ClickRun(sourceFiles);
            }
            Interlocked.Exchange(ref oneInt, 0);
        }

        private void SkipFileItem_Click(object sender, RoutedEventArgs e)
        {
            var l = fileListView.SelectedItems.OfType<NewFileInfo>().ToList();
            foreach (var f in l)
            {
                f._Remembered = !f._Remembered;
            }
            ListView_SelectionChangedRun();
        }
        public void UpdateColumnWidths()
        {
            foreach (UIElement element in UpdateGrid.Children)
            {
                if (element is ListView)
                {
                    var e = element as ListView;
                    ListViewTargetUpdated(e);
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
            if (0 != Interlocked.Exchange(ref oneInt, 1))
            {
                return;
            }
            var dirs = SourceDirs.ToList();
            SourceDirs.Clear();
            SourceFiles.Clear();
            await Task.Run(() =>
            {
                foreach (var d in dirs)
                {
                    GotSourceDir(d.Name, d.type);
                }
            });
            await DirReport();
            Interlocked.Exchange(ref oneInt, 0);
        }

        private void AddTVShowsOutputFolder(object sender, RoutedEventArgs e)
        {
            ChangeOutputDir(TVShowsType);
        }

        private async void AddTVShowsFolder(object sender, RoutedEventArgs e)
        {
            await AddDir(TVShowsType);
        }
    }
}