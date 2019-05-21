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
        string AudioMap, SubMap, VidMap, VidMapTo = "";
        bool forceAll = false;
        JsonValue json;
        
        readonly Regex[] regexChecks = new Regex[]
        {
            new Regex(@"\(\d{4}\)", RegexOptions.Compiled),
            new Regex(@"\.\d{4}\.", RegexOptions.Compiled),
            new Regex(@"\s\d{4}\s", RegexOptions.Compiled)
        };
        const int MovieType = 0;
        const int MusicVideoType = 1;
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
                Properties.Settings.Default.OldMovies = new System.Collections.Specialized.StringCollection();
                Properties.Settings.Default.OldMoviesSources = new System.Collections.Specialized.StringCollection();
                Properties.Settings.Default.OldMusicVidsSources = new System.Collections.Specialized.StringCollection();

                Properties.Settings.Default.FirstRun = false;
                Properties.Settings.Default.Save();
                Properties.Settings.Default.Reload();
            }
            await PrintToAppOutputBG("Loading previously saved directories and files ... ", 0, 2);
            await LoadDirs();                        
            tabControl.IsEnabled = true;
        }
        private async Task LoadDirs()
        {
            foreach (var d in Properties.Settings.Default.OldMoviesSources)
            {
                await GotSourceDirRun(d, MovieType);
            }
            foreach (var d in Properties.Settings.Default.OldMusicVidsSources)
            {
                await GotSourceDirRun(d, MusicVideoType);
            }
            if (Directory.Exists(Properties.Settings.Default.MovieOutput))
            {
                ChangeOutputDirRun(Properties.Settings.Default.MovieOutput, MovieType);
            }
            if (Directory.Exists(Properties.Settings.Default.MusicVidOutput))
            {
                ChangeOutputDirRun(Properties.Settings.Default.MusicVidOutput, MusicVideoType);
            }
            await DirReport();
        }

        private async Task DirReport()
        {
            await PrintToAppOutputBG($"{SourceDirsInternal.Where(x => x.type == MovieType).Count()} Movie directories containing {SourceFiles.Where(x => x.type == MovieType && x._Remembered == false).Count()} movies found.", 0, 1);
            await PrintToAppOutputBG($"{SourceDirsInternal.Where(x => x.type == MusicVideoType).Count()} Music Video directories containing {SourceFiles.Where(x => x.type == MusicVideoType && x._Remembered == false).Count()} music videos found.", 0, 1);
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {                 
            await Start_ClickRun(SourceFiles);
        }
        private async Task Start_ClickRun(List<NewFileInfo> sourceFiles)
        { 
            if (0 != Interlocked.Exchange(ref oneInt, 1))
            {
                return;
            }
            tabControl.SelectedIndex = 0;
            StartButton.IsEnabled = false;
            MakeNfosButton.IsEnabled = false;
            ReloadButton.IsEnabled = false;
            TabDirs.IsEnabled = false;
            ConsoleOutputString.Clear();
            AppOutput.Document.Blocks.Clear();
            if (forceCheckBox.IsChecked == true)
            {
                forceAll = true;
                await PrintToAppOutputBG("Force mode, ignoring remembered movies.", 0, 2);
            }
            else
            {
                forceAll = false;
            }
            await Task.Run(() => ProcessVideo(sourceFiles));
            TabDirs.IsEnabled = true;
            StartButton.IsEnabled = true;
            MakeNfosButton.IsEnabled = true;
            ReloadButton.IsEnabled = true;
            Interlocked.Exchange(ref oneInt, 0);
        }
        private async void MakeNfos_Click(object sender, RoutedEventArgs e)
        {
            if (0 != Interlocked.Exchange(ref oneInt, 1))
            {
                return;
            }
            MakeNfosButton.IsEnabled = false;
            StartButton.IsEnabled = false;
            await Task.Run(() => ProcessNfo());
            Interlocked.Exchange(ref oneInt, 0);
            MakeNfosButton.IsEnabled = true;
            StartButton.IsEnabled = true;
        }
        private async Task ProcessNfo()
        {
            AbortProcessing = false;
            GetFiles_Cancel = false;
            if (OutputDirs.Where(x => x.type == MusicVideoType).Count() == 0)
            {
                return;
            }
            nfoList = new List<NewFileInfo>();
            nfoList.AddRange(await Task.Run(() => GetFiles(OutputDirs.Where(x => x.type == MusicVideoType).First().Name , "*.mkv")));
            await PrintToAppOutputBG(nfoList.Count + ".nfo files need to be created.", 0, 1);
            int num = 0;
            await PrintToAppOutputBG("Creating .nfo files", 0, 1);
            foreach (var file in nfoList)
            {
                if (AbortProcessing == true)
                {
                    await PrintToAppOutputBG("Nfo creation aborted!", 0, 1, "red");
                    AbortProcessing = false;
                    GetFiles_Cancel = false;
                    break;
                }
                num++;
                await createNfo(file);
            }
            await PrintToAppOutputBG(num + " .nfo files created.", 0, 1, "green");
            System.Media.SystemSounds.Asterisk.Play();
        }

        public int oneInt = 0;

        private async Task ProcessVideo(List<NewFileInfo>sourceFiles)
        {
            ErroredList = new List<string>();
            SuccessList = new Dictionary<string, string>();
            NoAudioList = new List<string>();
            SkippedList = new List<string>();
            UnusualList = new List<string>();
            GetFiles_Cancel = false;
            AbortProcessing = false;
            
            
            Properties.Settings.Default.Reload();

            
            foreach (var file in sourceFiles)
            {
                await PrintToAppOutputBG(file.originalFullName, 0, 1);
            }
            await PrintToAppOutputBG(" ", 0, 1);
            int num = 0;
            foreach (var file in sourceFiles)
            {
                if (AbortProcessing == true)
                {
                    AbortProcessing = false;
                    break;
                }
                num++;                                
                if (OutputDirs.Where(x => x.type == file.type).Count() == 0)
                {
                    await PrintToAppOutputBG("Output directory not set for " + (file.type == MovieType ? "Movies" : "Music Videos"), 0, 1, "red");
                    ErroredList.Add(file.originalFullName);
                    continue;
                }                
                if (file.type == MusicVideoType)
                {                    ;
                    await createNfo(file);
                }

                if (forceAll == false && file._Remembered == true)
                {
                    await PrintToAppOutputBG($"Video {num} of {SourceFiles.Count} already processed:", 0, 1);
                    await PrintToAppOutputBG(file.originalFullName, 0, 1);
                    SkippedList.Add(file.originalFullName);
                    continue;                    
                }
                await PrintToAppOutputBG($"Processing video {num} of {SourceFiles.Count}:", 0, 1);
                await PrintToAppOutputBG(file.originalFullName, 0, 1);
                await PrintToAppOutputBG($"Size: {file.length.ToString("N0")} bytes.", 0, 2);
                bool ret = await processFile(file);
                if (ret)
                {
                    file._Remembered = true;
                    Dispatcher.Invoke(() => { fileListView.Items.Refresh(); });
                    
                    if (!Properties.Settings.Default.OldMovies.Contains(file.FullName))
                    {
                        Properties.Settings.Default.OldMovies.Add(file.FullName);
                        Properties.Settings.Default.Save();                        
                    }
                }
                else
                {
                    ErroredList.Add(file.originalFullName);
                }
            }
            await displayList(SkippedList, " movies skipped:", "white");
            await displayList(ErroredList, " movies with errors:", "red");
            await displayList(NoAudioList, " movies with no audio:", "red");
            await displayList(UnusualList, " movies with unusal aspects:", "yellow");
            await displayList(SuccessList, " movies processed successfully:", "lightgreen");
            await PrintToAppOutputBG("Complete!", 1, 1, "lightgreen");
            await PrintToConsoleOutputBG("Complete!");
            System.Media.SystemSounds.Asterisk.Play();                   
        }

        private async Task displayList(List<string> list, string displayStr, string color)
        {
            await PrintToAppOutputBG(list.Count + displayStr, 1, 1, list.Count == 0 ? "White" : color);
            foreach (var file in list.Distinct())
            {
                await PrintToAppOutputBG(file, 0, 1, color);
            }
        }
        private async Task displayList(Dictionary<string, string> list, string displayStr, string color)
        {
            await PrintToAppOutputBG(list.Count + displayStr, 1, 1, list.Count == 0 ? "White" : color);
            foreach (var file in list)
            {
                await PrintToAppOutputBG(file.Key + " -> " + file.Value, 0, 1, color);
            }
        }
        private async Task<bool> processFile(NewFileInfo file)
        {
            try
            {
                JsonFFProbe.Clear();

                int exitCode = await RunFFProbe(file.originalFullName);
                if (exitCode != 0)
                {
                    await PrintToAppOutputBG("Error, ffprobe return error code: " + exitCode, 0, 1, "red");
                    return false;
                }
                if (JsonFFProbe.Length == 0)
                {
                    await PrintToAppOutputBG("FFProbe returned nothing: " + file.originalFullName, 0, 1);
                    return false;
                }
                string jsonlen = JsonFFProbe.Length.ToString("N0");
                await PrintToAppOutputBG($"Received Json data from FFProbe.exe ({jsonlen} bytes) ...", 0, 1);
                json = JsonValue.Parse(JsonFFProbe.ToString().ToLower());

                bool FindAudioRet = await FindAudioAndSubtitle(file);
                if (FindAudioRet == false)
                {
                    await PrintToAppOutputBG("Error, No English Audio Found!", 0, 1, "red");
                    NoAudioList.Add(file.originalFullName);
                    return false;
                }
                await PrintToAppOutputBG("Video mapping: " + VidMap, 0, 1);
                await PrintToAppOutputBG("Video destination: " + VidMapTo, 0, 1);
                await PrintToAppOutputBG("Audio mapping: " + AudioMap, 0, 1);
                await PrintToAppOutputBG("Subtitle mapping: " + SubMap, 0, 1);

                if (OutputDirs.Where(x => x.type == file.type).Count() == 0)
                {
                    return false;
                }
                string destFullName = System.IO.Path.Combine(OutputDirs.Where(x => x.type == file.type).First().Name, file.destName);

                string parm = "-y -analyzeduration 2147483647 -probesize 2147483647 -i " + "\"" + file.originalFullName + "\" " + VidMap + AudioMap + SubMap +
                              "-c:v " + VidMapTo + "-c:a ac3 -c:s copy " + "\"" + destFullName + "\"";
                await PrintToAppOutputBG("FFMpeg parms: " + parm, 0, 1);
                int ExitCode = await RunFFMpeg(parm);
                if (ExitCode != 0)
                {
                    await PrintToAppOutputBG("FFMpeg had a possible problem, exit code: " + ExitCode, 0, 1, "red");
                    return false;
                }

                SuccessList.Add(file.originalFullName, file.destName);
                return true;
            }
            catch (Exception e)
            {
                await PrintToAppOutputBG("Something in processFile() has caused an exception: " + e.InnerException.Message, 0, 1, "red");
                return false;
            }
        }
        private async Task createNfo(NewFileInfo nfi)
        {
            string nfo = System.IO.Path.Combine(OutputDirs.Where(x => x.type == MusicVideoType).First().Name, nfi.destName.Substring(0, nfi.destName.Length - 4) + ".nfo");
            try
            {                
                var file = File.Open(nfo, FileMode.Create);
                string nfoStr = "<musicvideo>" + Environment.NewLine + "<title>" + nfi.destName + "</title>" + Environment.NewLine + "</musicvideo>";
                byte[] nfoBytes = Encoding.UTF8.GetBytes(nfoStr);

                file.Write(nfoBytes, 0, nfoBytes.Length);
                file.Close();
                file.Dispose();
                if (file != null)
                {
                    file = null;
                }
                if (File.Exists(nfo))
                {
                    await PrintToAppOutputBG("Music video .nfo file created: " + nfo, 0, 1);
                }
                else
                {
                    await PrintToAppOutputBG("Music Video .nfo file could not be created.", 0, 1, "red");
                    ErroredList.Add(nfo);
                }
            }
            catch (Exception e)
            {
                await PrintToAppOutputBG("Exception thrown in createNfo(): " + e.InnerException.Message, 0, 1, "Red");
                ErroredList.Add(nfo);
            }
        }

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
            public int type; // movies = 0; musicvideos = 1
            public bool process = true;
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
            public string Title;
            public bool _Remembered;
            public string Remembered
            {
                get
                {
                    return _Remembered.ToString();
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
                    if (type == MovieType)
                    {
                        return "Movies";
                    }
                    else
                    {
                        return "Music Videos";
                    }
                }                
            }
        }
        private void ConstructName(NewFileInfo file)
        {
            string[] dirFrags = file.originalDirectoryName.Split('\\');
            string destName = file.originalName.Substring(0, file.originalName.Length - 4) + ".mkv";
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

        private async Task<bool> FindAudioAndSubtitle(NewFileInfo file)
        {
            bool foundAudio = false;
            AudioMap = "";
            SubMap = "";
            VidMap = "-map 0:v ";
            VidMapTo = "copy ";
            if (!json.ContainsKey("streams"))
            {
                await PrintToAppOutputBG("Malformed movie data: No streams: " + file.originalFullName, 0, 1, "red");
                return false;
            }
            var streams = json["streams"];
            int VidNum = 0, AudNum = 0, SubNum = 0;
            for (int x = 0; x < streams.Count; x++)
            {
                if (!streams[x].ContainsKey("index"))
                {
                    await PrintToAppOutputBG("Malformed movie data: No index in json element: " + x, 0, 1, "yellow");
                    UnusualList.Add(file.originalFullName);
                    continue;
                }
                if (!streams[x].ContainsKey("codec_type"))
                {
                    await PrintToAppOutputBG("Malformed movie data: No codec_type in json element: " + x, 0, 1, "yellow");
                    UnusualList.Add(file.originalFullName);
                    continue;
                }
                string codectype = JsonValue.Parse(streams[x]["codec_type"].ToString());
                int index = JsonValue.Parse(streams[x]["index"].ToString());
                switch (codectype)
                {
                    case "video":
                        if (streams[x].ContainsKey("codec_name") && streams[x]["codec_name"] == "vc1")
                        {
                            VidMap = "-map 0:" + index + " ";
                            VidMapTo = "libx264 ";
                            await PrintToAppOutputBG("Video is VC-1, converting to x264.", 0, 1, "yellow");
                        }
                        VidNum++;
                        break;
                    case "audio":
                        AudNum++;
                        if (foundAudio == true)
                        {
                            continue;
                        }
                        // Find first audio track that is english or unspecified language, which is usually english.

                        if (streams[x].ContainsKey("tags") && streams[x]["tags"].ContainsKey("language"))
                        {
                            var language = streams[x]["tags"]["language"];
                            await PrintToAppOutputBG("Language in movie == " + language, 0, 1, "yellow");
                            if (!(language == "eng"))
                            {
                                if (language == null || language == "" || language == "und")
                                {
                                    await PrintToAppOutputBG("Unusual movie, audio language not defined, index #" + index, 0, 1, "yellow");
                                    UnusualList.Add(file.originalFullName);
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            await PrintToAppOutputBG("Unusual movie, audio language not defined, index #" + index, 0, 1, "yellow");
                            UnusualList.Add(file.originalFullName);             // no tags or language in tags, probably english.
                        }
                        if (streams[x].ContainsKey("tags") && streams[x]["tags"].ContainsKey("title"))
                        {
                            if (streams[x]["tags"]["title"].ToString().ToLower().Contains("commentary"))
                            {
                                await PrintToAppOutputBG("Unusual movie, commentary is before audio track, index #" + index, 0, 1, "yellow");
                                UnusualList.Add(file.originalFullName);
                                continue;
                            }
                        }
                        AudioMap = "-map 0:" + index + " ";
                        foundAudio = true;
                        break;
                    case "subtitle":
                        SubNum++;
                        if (streams[x].ContainsKey("tags") && streams[x]["tags"].ContainsKey("language"))
                        {
                            var language = streams[x]["tags"]["language"];
                            if (language != null && language == "eng")
                            {
                                SubMap += "-map 0:" + index + " ";
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            await PrintToAppOutputBG("Number of Video streams: " + VidNum, 0, 1);
            await PrintToAppOutputBG("Number of Audio streams: " + AudNum, 0, 1);
            await PrintToAppOutputBG("Number of Subtitle streams: " + SubNum, 0, 1);
            return foundAudio;
        }

        private async Task PrintToAppOutputBG(string str, int preNewLines, int postNewLines, string color = "White")
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                await PrintToAppOutputThread(str, preNewLines, postNewLines, color);
            }, DispatcherPriority.ApplicationIdle);            
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
                FFMpegProcess.CancelErrorRead();
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
            ConsoleOutputString.Clear();
            ConsoleOutput.Clear();
        }

        private static readonly object AppOutputStringLock = new object();
        StringBuilder ConsoleOutputString = new StringBuilder();

        private async void AddMoviesDir_Click(object sender, RoutedEventArgs e)
        {
            await AddDir(MovieType);
        }

        List<NewDirInfo> SourceDirs = new List<NewDirInfo>();
        List<NewDirInfo> SourceDirsInternal = new List<NewDirInfo>();
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
            if (type == MovieType)
            {
                Properties.Settings.Default.OldMoviesSources.Clear();
                Properties.Settings.Default.OldMoviesSources.AddRange(SourceDirsInternal.Where(x => x.type == MovieType).Select(x => x.Name).ToArray());
            }
            else
            {
                Properties.Settings.Default.OldMusicVidsSources.Clear();
                Properties.Settings.Default.OldMusicVidsSources.AddRange(SourceDirsInternal.Where(x => x.type == MusicVideoType).Select(x => x.Name).ToArray());
            }
            Properties.Settings.Default.Save();
        }
        private async Task GotSourceDirRun(string VidDir, int type)
        {
            await Task.Run(() => GotSourceDir(VidDir, type));
            listView.ItemsSource = SourceDirs;
            listView.Items.Refresh();
        }
        private void GotSourceDir(string VidDir, int type)
        {
            if (!Directory.Exists(VidDir))
            {
                return;
            }
            if (SourceDirs.Where(x => x.Name == VidDir).Count() > 0)
            {
                SourceDirs.Remove(SourceDirs.Where(x => x.Name == VidDir).First());
            }
            NewDirInfo temp = new NewDirInfo();
            temp.Name = VidDir;
            temp.type = type;            
            SourceDirs.Add(temp);
            if (SourceDirsInternal.Where(x => x.Name == VidDir).Count() > 0)
            {
                SourceDirsInternal.Remove(SourceDirsInternal.Where(x => x.Name == VidDir).First());
            }
            SourceDirsInternal.Add(temp);
            List<NewFileInfo> ftemp = GetFiles(VidDir, "*.mkv;*.mp4;*.avi;*.m4v;");
            foreach (var f in ftemp)
            {
                f.type = type;
                if (SourceFiles.Where(x => x.originalFullName == f.originalFullName).Count() > 0)
                {
                    SourceFiles.Remove(SourceFiles.Where(x => x.originalFullName == f.originalFullName).First());
                }
                ConstructName(f);               
                SourceFiles.Add(f);
            }
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
                    if (f.fromDirectory == l.Name)
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
            if (type == MovieType)
            {
                Properties.Settings.Default.MovieOutput = outputDir;
            }
            else
            {
                Properties.Settings.Default.MusicVidOutput = outputDir;
            }
            Properties.Settings.Default.Save();
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
                    SourceDirsInternal.Remove(list1);
                    SourceDirs.Remove(list1);
                }
                listView.ItemsSource = SourceDirs;
                listView.Items.Refresh();
                fileListView.ItemsSource = new Dictionary<string, string>();
                fileListView.Items.Refresh();
                Properties.Settings.Default.OldMusicVidsSources.Clear();
                Properties.Settings.Default.OldMoviesSources.Clear();
                Properties.Settings.Default.OldMoviesSources.AddRange(SourceDirsInternal.Where(x => x.type == MovieType).Select(x => x.Name).ToArray());
                Properties.Settings.Default.OldMusicVidsSources.AddRange(SourceDirsInternal.Where(x => x.type == MusicVideoType).Select(x => x.Name).ToArray());
                Properties.Settings.Default.Save();
            }
        }

        private static readonly object ConsoleOutputStringLock = new object();

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

        string ConsoleOutputTemp = "";

        private async void ProcessDirItem_Click(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedIndex >= 0)
            {
                var listSelectedItems = listView.SelectedItems.OfType<NewDirInfo>().ToList();
                var l = listSelectedItems.Select(x => x.Name).ToList();
                List<NewFileInfo> sourceFiles = SourceFiles.Where(x => l.Any(x.fromDirectory.Contains)).ToList();                                
                await Start_ClickRun(sourceFiles);
            }
        }

        private async void ProcessFileItem_Click(object sender, RoutedEventArgs e)
        {
            if (fileListView.SelectedIndex >= 0)
            {
                var l = fileListView.SelectedItems.OfType<NewFileInfo>().Select(x => x.FullName).ToList();
                List<NewFileInfo> sourceFiles = SourceFiles.Where(x => l.Any(x.FullName.Equals)).ToList();
                await Start_ClickRun(sourceFiles);
            }
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
            var dirs = SourceDirs.ToList();
            SourceDirs.Clear();
            SourceDirsInternal.Clear();
            foreach (var d in dirs)
            {
                await Task.Run(() => { GotSourceDir(d.Name, d.type); });
            }
            await DirReport();
        }

        static SemaphoreSlim semaphoreSlimCO = new SemaphoreSlim(1, 1);
        static SemaphoreSlim semaphoreSlimC2 = new SemaphoreSlim(1, 1);
        private async Task PrintToConsoleOutputBG(string str)
        {
            await semaphoreSlimC2.WaitAsync();
            try
            {
                if (FrameFound == true)
                {
                    if (ConsoleOutputString.Length > LastFrame)
                    {
                        ConsoleOutputString = ConsoleOutputString.Remove(LastFrame, ConsoleOutputString.Length - LastFrame);
                    }
                    FrameFound = false;
                }
                if (str.StartsWith("frame="))
                {
                    LastFrame = ConsoleOutputString.Length;
                    FrameFound = true;
                    ConsoleOutputString.Append(str);
                }
                else
                {
                    ConsoleOutputString.Append(str + Environment.NewLine);
                }
                ConsoleOutputTemp = ConsoleOutputString.ToString();

                await Dispatcher.InvokeAsync(async () =>
                {
                    await semaphoreSlimCO.WaitAsync();
                    try
                    {
                        ConsoleOutput.Text = ConsoleOutputTemp;
                        if (FrameFound == false)
                        {
                            ConsoleScroll.ScrollToEnd();
                        }
                    }
                    finally
                    {
                        semaphoreSlimCO.Release();
                    }

                }, DispatcherPriority.ApplicationIdle);
            }
            finally
            {
                semaphoreSlimC2.Release();
            }
        }
    }
}
