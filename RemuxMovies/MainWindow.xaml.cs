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
using Xabe.FFmpeg;
using Xabe.FFmpeg.Enums;

namespace RemuxMovies
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string AudioMap, SubMap, VidMap, VidMapTo = "";
        bool forceAll = false;
        JsonValue json;

        readonly Regex[] regexChecks = new Regex[]
        {
            new Regex(@"\(\d{4}\)", RegexOptions.Compiled),
            new Regex(@"\.\d{4}\.", RegexOptions.Compiled),
            new Regex(@"\s\d{4}\s", RegexOptions.Compiled)
        };
        readonly Regex[] TVShowRegex = new Regex[]
        {
            new Regex(@"\.S\d{1,3}E\d{1,3}\.", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\sS\d{1,3}E\d{1,3}\s", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"-S\d{1,3}E\d{1,3}-", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        const int MovieType = 0;
        const int MusicVideoType = 1;
        const int TVShowsType = 2;
        public static Dictionary<int, string> types = new Dictionary<int, string>()
        {
            {MovieType, "Movies" },
            {MusicVideoType, "Music Videos"},
            {TVShowsType, "TV Shows"}
        };
            
        List<string> ErroredList;
        Dictionary<string, string> SuccessList;
        List<string> NoAudioList;
        List<string> SkippedList;
        List<string> UnusualList;

        public MainWindow()
        {
            InitializeComponent();            
        }
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await PrintToAppOutputBG("MovieRemux v1.1 - Remux movies using FFMpeg (and FFProbe for movie data) to " +
                "convert first English audio to .ac3 and remove all other audio " +
                "and non-English subtitles. Written by James Gentile.", 0, 2);
            await PrintToAppOutputBG(Properties.Settings.Default.OldMovies.Count + " movies remembered.", 0, 2);
            if (Properties.Settings.Default.FirstRun == true)
            {
                string str = "First run, saved directories list cleared.";
                ClearSettings(str);
            }
            await LoadDirs();
            
            await PrintToAppOutputBG("Downloading latest FFMpeg/FFProbe if version not up to date.",0,1);
            FFmpeg.ExecutablesPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FFmpeg");
            var ffmpegDir = System.IO.Path.Combine(FFmpeg.ExecutablesPath, "ffmpeg.exe");
            await FFmpeg.GetLatestVersion();
            if (!File.Exists(ffmpegDir))
            {
                MessageBox.Show("FFMpeg/FFProbe not found at: " + ffmpegDir);
                Close();
            }
            ToggleButtons(true);
            await PrintToAppOutputBG("Ready. ", 0, 2, "green");
        }

        private static void ClearSettings(string str)
        {
            MessageBox.Show(str);
            Properties.Settings.Default.OldMovies = new System.Collections.Specialized.StringCollection();
            Properties.Settings.Default.VidSources = new System.Collections.Specialized.StringCollection();
            Properties.Settings.Default.VidOutputs = new System.Collections.Specialized.StringCollection();
            Properties.Settings.Default.FirstRun = false;
            Properties.Settings.Default.Save();
            Properties.Settings.Default.Reload();
        }

        private async Task LoadDirs()
        {
            var sources = Properties.Settings.Default.VidSources;
            if (sources.Count > 0 && (sources.Count % 2) == 0)
            {
                for (int i = 0; i < sources.Count; i += 2)
                {
                    bool isInt = int.TryParse(sources[i], out int type);
                    if (isInt == false)
                    {
                        ClearSettings("Source settings Invalid, reset.");
                        break;
                    }
                    string dir = sources[i + 1];
                    await GotSourceDirRun(dir, type);
                }
            }
            var outputs = Properties.Settings.Default.VidOutputs;
            if (outputs.Count > 0 && (outputs.Count % 2) == 0)
            {
                for (int i = 0; i < outputs.Count; i += 2)
                {
                    bool isInt = int.TryParse(outputs[i], out int type);
                    if (isInt == false)
                    {
                        ClearSettings("Output directories setting Invalid, reset.");
                        break;
                    }
                    string dir = outputs[i + 1];
                    ChangeOutputDirRun(dir, type);
                }
            }
            populateInfoLabel();
        }
        private void populateInfoLabel()
        {
            int movs = SourceFiles.Where(c => c.type == MovieType && ((c._Remembered & !forceAll) == false)).Count();
            int musicvideos = SourceFiles.Where(c => c.type == MusicVideoType && ((c._Remembered & !forceAll) == false)).Count();
            int tvshows = SourceFiles.Where(c => c.type == TVShowsType && ((c._Remembered & !forceAll) == false)).Count();
            infoLabel.Content = $"{movs} Movies ready to process." + Environment.NewLine +
                                $"{musicvideos} Music Videos ready to process." + Environment.NewLine +
                                $"{tvshows} TV Shows ready to process.";
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (0 != Interlocked.Exchange(ref oneInt, 1))
            {
                return;
            }
            await Start_ClickRun(SourceFiles);
            Interlocked.Exchange(ref oneInt, 0);
        }
        private async Task Start_ClickRun(List<NewFileInfo> sourceFiles)
        {
            tabControl.SelectedIndex = 0;
            ToggleButtons(false);
            ConsoleOutput.Clear();
            AppOutput.Document.Blocks.Clear();
            if (forceAll == true)
            {                
                await PrintToAppOutputBG("Force mode, ignoring remembered movies.", 0, 2);
            }            
            await Task.Run(() => ProcessVideo(sourceFiles));
            ToggleButtons(true);
        }

        private void ToggleButtons(bool t)
        {
            StartButton.IsEnabled = t;
            MakeNfosButton.IsEnabled = t;
            ReloadButton.IsEnabled = t;
            stackPanel.IsEnabled = t;
        }
        
        public int oneInt = 0;

        public class NewFileInfo
        {
            public string originalFullName;
            private string _originalName;
            public string originalName
            {
                get
                {
                    return _originalName;
                }
                set
                {
                    _originalName = value;
                }
            }

            public string originalDirectoryName;
            public long length;
            public string FullName;
            public string Name;
            public string DirectoryName;
            public string fromDirectory;
            public int type; // movies = 0; musicvideos = 1; TV Shows = 2            
            private string _destName;
            public string destName
            {
                get
                {
                    return _destName;
                }
                set
                {
                    _destName = value;
                }
            }
            
            public bool _Remembered;
            public string Remembered
            {
                get
                {
                    return _Remembered.ToString();
                }
            }
            public string destPath;
            public string FriendlyType
            {
                get
                {
                    return types[type];
                }
            }
        }
        public class NewDirInfo
        {
            private string _Name;
            private int _type;
            public Boolean Process = true;
            public string Name
            {
                get
                {
                    return _Name;
                }
                set
                {
                    _Name = value;
                }
            }
            public int type
            {
                get
                {
                    return _type;
                }
                set
                {
                    _type = value;
                }
            }
            public string FriendlyType
            {
                get
                {
                    return types[_type];
                }
            }
        }
        private void ConstructName(NewFileInfo file, Match TVShowM)
        {
            string destName;

            if (TVShowM.Success)
            {
                file.destPath = file.originalName.Substring(0, TVShowM.Index);
                file.destName = file.originalName;                                // TVShowS03E02.mkv -> OutputDir\TVShow\TVShowS03E02.mkv per Kodi guidelines for TV Shows.                
                return;
            }

            // if regular movie, this tries to find the more complete name from the directory, as some movies are in the form \Movie.Year.mkv\mov.mkv or some varient of this.

            string[] dirFrags = file.originalDirectoryName.Split('\\');
            destName = file.originalName.Substring(0, file.originalName.Length - 4) + ".mkv";
            string destDirName = dirFrags.Last() + ".mkv";
            string[] vidtags = new string[] { "x264", "x265", "avc", "vc-1","vc1","hevc","bluray","blu-ray","dts","truehd","ddp","flac","ac3","aac","mpeg-2","mpeg2","remux","h264","h265",
                                              "h.264","h.265","1080p","1080i","720p","2160p"};
            int y = 0;
            bool takeDirName = false;
            int curYear = DateTime.Today.Year;
            if (dirFrags.Length > 1)
            {
                if (vidtags.Any(destDirName.ToLower().Contains))
                {
                    takeDirName = true;
                }
                else
                {
                    foreach (var r in regexChecks)
                    {
                        Match m = r.Match(destDirName);
                        if (m.Success)
                        {
                            bool result = Int32.TryParse(m.Groups[0].Value.Substring(1, 4), out y);
                            if (result == true && y > 1900 && y <= curYear)
                            {
                                takeDirName = true;
                                break;
                            }
                        }
                    }
                }
                if (takeDirName)
                {
                    var tempList = GetFiles(file.DirectoryName, "*.mkv;");
                    if (tempList.Count == 1)
                    {
                        destName = destDirName;
                    }
                }
            }
            destName = destName.Replace("-", ".");
            file.destName = destName;
        }

        private async Task PrintToAppOutputBG(string str, int preNewLines, int postNewLines, string color = "White")
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                await PrintToAppOutputThread(str, preNewLines, postNewLines, color);
            }, DispatcherPriority.Background);
        }
        static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        BrushConverter bc = new BrushConverter();

        private async Task PrintToAppOutputThread(string str, int preNewLines, int postNewLines, string color)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                TextRange tr = new TextRange(AppOutput.Document.ContentEnd, AppOutput.Document.ContentEnd);
                tr.Text = (new string('\r', preNewLines) + str + new string('\r', postNewLines));
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, bc.ConvertFromString(color));
                AppScroll.ScrollToEnd();
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        int LastFrame = 0;
        bool FrameFound = false;
        bool AbortProcessing = false;
        private async void Abort_Click(object sender, RoutedEventArgs e)
        {
            AbortProcessing = true;
            GetFiles_Cancel = true;
            if (FFMpegProcess != null && FFMpegProcess.HasExited != true)
            {
                FFMpegProcess.CancelErrorRead();
                FFMpegProcess.CancelOutputRead();
                FFMpegProcess.Kill();
                while (FFMpegProcess != null && FFMpegProcess.HasExited != true)
                {
                    await Task.Delay(10);
                }
                await PrintToAppOutputBG("FFMpeg process killed.", 0, 1, "red");
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            AppOutput.Document.Blocks.Clear();            
            ConsoleOutput.Clear();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            AppOutput.Document.Blocks.Clear();
            List<string> files = new List<string>();
            foreach (var o in Properties.Settings.Default.OldMovies)
            {
                files.Add(System.IO.Path.GetFileName(o));
            }
            files.Sort();
            foreach (var f in files)
            {
                await PrintToAppOutputBG(f, 0, 1);
            }
        }

        private void ForceCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            forceAll = forceCheckBox.IsChecked.Value;
            populateInfoLabel();
        }

        static SemaphoreSlim semaphoreSlimC2 = new SemaphoreSlim(1, 1);
        private void PrintToConsoleOutputBG(string str)
        {
            semaphoreSlimC2.Wait();
            try
            {                
                int ret = Dispatcher.Invoke(() =>
                {                    
                    if (FrameFound == true)
                    {
                        if (ConsoleOutput.Text.Length > LastFrame)
                        {
                            ConsoleOutput.Text = ConsoleOutput.Text.Substring(0,LastFrame);
                        }
                        FrameFound = false;
                    }
                    if (str.StartsWith("frame="))
                    {
                        LastFrame = ConsoleOutput.Text.Length;
                        FrameFound = true;
                        ConsoleOutput.Text += str;
                    }
                    else
                    {
                        ConsoleOutput.Text += str + Environment.NewLine;
                        ConsoleScroll.ScrollToEnd();
                    }                                      
                    return 42;
                },DispatcherPriority.Background);
                if (ret != 42)
                {
                    MessageBox.Show("ret = " + ret);
                }
            }
            finally
            {
                semaphoreSlimC2.Release();
            }
        }
    }
}
