using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Json;
using System.Linq;
using System.Net;
using System.Reflection;
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
using System.Xml.Linq;
using System.Xml.Serialization;
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
        string TMDBAPIKEY = "";

        int curYear;

        bool forceAll = false;
        JsonValue json;

        readonly Regex[] regexChecks = new Regex[]
        {
            new Regex(@"\(\d{4}\)", RegexOptions.Compiled | RegexOptions.RightToLeft),
            new Regex(@"\.\d{4}\.", RegexOptions.Compiled | RegexOptions.RightToLeft),
            new Regex(@"\s\d{4}\s", RegexOptions.Compiled | RegexOptions.RightToLeft)
        };
        readonly Regex[] TVShowRegex = new Regex[]
        {
            new Regex(@"\.S\d{1,3}E\d{1,3}\.", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\sS\d{1,3}E\d{1,3}\s", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"-S\d{1,3}E\d{1,3}-", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };
        string[] vidtags = new string[] { "x264", "x265", "avc", "vc-1","vc1","hevc","bluray","blu-ray","dts","truehd","ddp","flac","ac3","aac","mpeg-2","mpeg2","remux","h264","h265",
                                          "h.264","h.265","1080p","1080i","720p","2160p","web.dl","unrated","theatrical","extended","dvd","dd5","directors","director's","remastered",
                                          "uhd","hdr","sdr","4k","atmos","webrip","amzn",
        };
        string SourceXML = "";
        string OutputXML = "";
        string OldMoviesXML = "";
        const int MovieType = 0;
        const int MusicVideoType = 1;
        const int TVShowsType = 2;
        public static Dictionary<int, string> typeFriendlyName = new Dictionary<int, string>()
        {
            {MovieType, "Movies" },
            {MusicVideoType, "Music Videos"},
            {TVShowsType, "TV Shows"}
        };

        Dictionary<string, string> ErroredList;
        List<string> NoAudioList;
        List<string> SkippedList;
        Dictionary<string, string> UnusualList;
        List<string> BadChar;
        List<string> nonChar;
        List<OldMovie> OldMovies = new List<OldMovie>();

        List<string> ignoreWords = new List<string>() { "an", "the", "a", "and", "part", "&", "3d", "episode" };

        Dictionary<string, string> SuccessList;
        Dictionary<string, string> NoTMBDB;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SourceXML = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"RemuxMovies\SourceDirs.xml");
            OutputXML = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"RemuxMovies\OutputDirs.xml");
            OldMoviesXML = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"RemuxMovies\OldMovies.xml");
            curYear = DateTime.Today.Year;

            await PrintToAppOutputBG("MovieRemux v1.1 - Remux movies using FFMpeg (and FFProbe for movie data) to " +
                "convert first English audio to .ac3 and remove all other audio " +
                "and non-English subtitles. Written by James Gentile.", 0, 2);

            await loadFromXML();
            UpdateRememberedList();

            await PrintToAppOutputBG("Downloading latest FFMpeg/FFProbe if version not up to date.", 0, 1);
            FFmpeg.ExecutablesPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RemuxMovies\\FFmpeg\\");
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
        public class OldMovie
        {
            public string Name { get; set; }
            public int Num { get; set; }
            public string FullName { get; set; }
            public string displayName { get; set; }
        }
        

        public void saveToXML()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<NewDirInfo>));
            using (StreamWriter streamWriter = new StreamWriter(SourceXML))
            {
                serializer.Serialize(streamWriter, SourceDirs);
            }
            using (StreamWriter streamWriter = new StreamWriter(OutputXML))
            {
                serializer.Serialize(streamWriter, OutputDirs);
            }
            serializer = new XmlSerializer(typeof(List<string>));
            using (StreamWriter streamWriter = new StreamWriter(OldMoviesXML))
            {
                List<string> om = new List<string>();
                foreach (var o in OldMovies)
                {
                    om.Add(o.FullName);
                }
                serializer.Serialize(streamWriter, om);
            }
        }
        public async Task<bool> loadFromXML()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<string>));
            if (File.Exists(OldMoviesXML))
            {
                using (StreamReader streamReader = new StreamReader(OldMoviesXML))
                {
                    var om = (List<string>)serializer.Deserialize(streamReader);
                    addToOldMovies(om);
                }
                await PrintToAppOutputBG(OldMovies.Count + " movies remembered.", 0, 2);
            }
            serializer = new XmlSerializer(typeof(List<NewDirInfo>));
            if (File.Exists(SourceXML))
            {
                using (StreamReader streamReader = new StreamReader(SourceXML))
                {
                    List<NewDirInfo> dirs = (List<NewDirInfo>)serializer.Deserialize(streamReader);
                    await AddSourceDirs(dirs);
                }
            }
            if (File.Exists(OutputXML))
            {
                using (StreamReader streamReader = new StreamReader(OutputXML))
                {
                    List<NewDirInfo> odirs = (List<NewDirInfo>)serializer.Deserialize(streamReader);
                    foreach (var o in odirs)
                    {
                        ChangeOutputDirRun(o.Name, o.type);
                    }
                }
            }
            await displayFilesToProcess();
            await LoadNonChar();
            return true;
        }

        int oldMovMaxLen = 0;
        private void addToOldMovies(List<string> oldMovList)
        {
            int i = 0;
            foreach (var oldMovItem in oldMovList)
            {
                OldMovie oldMovie = new OldMovie();
                oldMovie.Name = System.IO.Path.GetFileName(oldMovItem);
                oldMovie.Num = i;
                oldMovie.FullName = oldMovItem;
                OldMovies.Add(oldMovie);                
                if (oldMovie.Name.Length > oldMovMaxLen)
                {
                    oldMovMaxLen = oldMovie.Name.Length;
                }
                i++;
            }
        }

        private void populateInfoLabel()
        {
            populateInfoLabel(out List<NewFileInfo> movsList, out List<NewFileInfo> musicVideosList, out List<NewFileInfo> tvShowsList);
            return;
        }
        private void populateInfoLabel(out List<NewFileInfo> numMovs, out List<NewFileInfo> numMusicVideos, out List<NewFileInfo> numTvShows)
        {
            numMovs = SourceFiles.Where(c => c.type == MovieType && ((c._Remembered & !forceAll) == false)).ToList();
            numMusicVideos = SourceFiles.Where(c => c.type == MusicVideoType && ((c._Remembered & !forceAll) == false)).ToList();
            numTvShows = SourceFiles.Where(c => c.type == TVShowsType && ((c._Remembered & !forceAll) == false)).ToList();
            infoLabel.Content = $"{numMovs.Count} Movies ready to process." + Environment.NewLine +
                                $"{numMusicVideos.Count} Music Videos ready to process." + Environment.NewLine +
                                $"{numTvShows.Count} TV Shows ready to process.";
        }
        private async Task<bool> displayFilesToProcess()
        {
            populateInfoLabel(out List<NewFileInfo> movsList, out List<NewFileInfo> musicVideosList, out List<NewFileInfo> tvShowsList);
            if (movsList.Count > 0)
            {
                await PrintToAppOutputBG($"{movsList.Count} Movie(s) to be processed: ", 0, 1);
                foreach (var f in movsList)
                {
                    await PrintToAppOutputBG(f.originalFullName, 0, 1);
                }
            }
            if (musicVideosList.Count > 0)
            {
                await PrintToAppOutputBG($"{musicVideosList.Count} Music Video(s) to be processed: ", 0, 1);
                foreach (var f in musicVideosList)
                {
                    await PrintToAppOutputBG(f.originalFullName, 0, 1);
                }
            }
            if (tvShowsList.Count > 0)
            {
                await PrintToAppOutputBG($"{tvShowsList.Count} TV Show(s) to be processed: ", 0, 1);
                foreach (var f in tvShowsList)
                {
                    await PrintToAppOutputBG(f.originalFullName, 0, 1);
                }
            }
            return true;
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
            ClearWindows();
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
                    return typeFriendlyName[type];
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
                    return typeFriendlyName[_type];
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

            int y = 0;
            int regExIdx = 0;
            bool takeDirName = false;
            if (dirFrags.Length > 1)
            {
                if (vidtags.Any(destDirName.ToLower().Contains))
                {
                    takeDirName = true;
                }
                else
                {
                    takeDirName = GetFileYear(ref destDirName, ref y, ref regExIdx);
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
        private async void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            SkipProcessing = true;
            await KillFFMpeg();
        }
        bool AbortProcessing = false;
        bool SkipProcessing = false;
        private async void Abort_Click(object sender, RoutedEventArgs e)
        {
            AbortProcessing = true;
            GetFiles_Cancel = true;
            await KillFFMpeg();
        }
        private async Task<bool> KillFFMpeg()
        {
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
            return true;
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ClearWindows();
        }
        private void ClearWindows()
        {
            AppOutput.Document.Blocks.Clear();
            ConsoleOutput.Clear();
        }

        private async void DisplayOld(object sender, RoutedEventArgs e)
        {            
            AppOutput.Document.Blocks.Clear();
            List<string> files = new List<string>();
            foreach (var o in OldMovies)
            {
                if (IsTVShow(o.Name).Success)
                {
                    continue;
                }
                files.Add(System.IO.Path.GetFileName(o.Name));
            }
            files.Sort();
            foreach (var f in files)
            {
                await PrintToAppOutputBG(f, 0, 1);
            }
            await PrintToAppOutputBG(files.Count() + " old items.", 0, 1);
        }

        private void ForceCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            forceAll = forceCheckBox.IsChecked.Value;
            populateInfoLabel();
        }
        int LastFrameConsoleOutput = 0;
        bool FoundFrameConsoleOutput = false;

        bool RememberedSearchCleared = false;

        

        private void PrintToConsoleOutputBG(string str)
        {
            ConsoleOutput.Dispatcher.InvokeAsync(() =>
            {
                PrintToConsoleWorker(str);
            }, DispatcherPriority.Background);
        }

        private void PrintToConsoleWorker(string str)
        {
            if (FoundFrameConsoleOutput)
            {
                ConsoleOutput.Text = ConsoleOutput.Text.Remove(LastFrameConsoleOutput);
            }
            FoundFrameConsoleOutput = false;
            if (str.StartsWith("frame="))
            {
                LastFrameConsoleOutput = ConsoleOutput.Text.Length;
                FoundFrameConsoleOutput = true;
            }
            ConsoleOutput.AppendText(str + Environment.NewLine);
            if (!FoundFrameConsoleOutput)
            {
                ConsoleScroll.ScrollToEnd();
            }
        }

        private async Task<bool> LoadNonChar()
        {
            string tmdbfile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TMDB_API_KEY.txt");
            if (File.Exists(tmdbfile))
            {
                TMDBAPIKEY = File.ReadAllText(tmdbfile);
                await PrintToAppOutputBG("TMDB Key found.", 0, 1);
            }
            string file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NonChar.txt");
            if (File.Exists(file))
            {
                nonChar = File.ReadAllLines(file).ToList();
            }
            return true;
        }

    }
}
