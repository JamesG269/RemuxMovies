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
        readonly Regex[] TVShowRegex = new Regex[]
        {
            new Regex(@"\.S\d{2,3}E\d{2,3}\.", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\sS\d{2,3}E\d{2,3}\s", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"-S\d{2,3}E\d{2,3}-", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        const int MovieType = 0;
        const int MusicVideoType = 1;
        const int TVShowsType = 2;
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
                MessageBox.Show("First run, directories cleared.");
                Properties.Settings.Default.OldMovies = new System.Collections.Specialized.StringCollection();
                Properties.Settings.Default.OldMoviesSources = new System.Collections.Specialized.StringCollection();
                Properties.Settings.Default.OldMusicVidsSources = new System.Collections.Specialized.StringCollection();
                Properties.Settings.Default.OldTVShowsSources = new System.Collections.Specialized.StringCollection();

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
            foreach (var d in Properties.Settings.Default.OldTVShowsSources)
            {
                await GotSourceDirRun(d, TVShowsType);
            }
            if (Directory.Exists(Properties.Settings.Default.MovieOutput))
            {
                ChangeOutputDirRun(Properties.Settings.Default.MovieOutput, MovieType);
            }
            if (Directory.Exists(Properties.Settings.Default.MusicVidOutput))
            {
                ChangeOutputDirRun(Properties.Settings.Default.MusicVidOutput, MusicVideoType);
            }
            if (Directory.Exists(Properties.Settings.Default.MusicVidOutput))
            {
                ChangeOutputDirRun(Properties.Settings.Default.TVShowsOutput, TVShowsType);
            }

            await DirReport();
        }

        private async Task DirReport()
        {
            await PrintToAppOutputBG($"{SourceDirs.Where(x => x.type == MovieType).Count()} Movie directories containing {SourceFiles.Where(x => x.type == MovieType && x._Remembered == false).Count()} new movies found.", 0, 1);
            await PrintToAppOutputBG($"{SourceDirs.Where(x => x.type == MusicVideoType).Count()} Music Video directories containing {SourceFiles.Where(x => x.type == MusicVideoType && x._Remembered == false).Count()} new music videos found.", 0, 1);
            await PrintToAppOutputBG($"{SourceDirs.Where(x => x.type == TVShowsType).Count()} TV Shows directories containing {SourceFiles.Where(x => x.type == TVShowsType && x._Remembered == false).Count()} new TV Shows found.", 0, 1);
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
                {
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
                string makePath = OutputDirs.Where(x => x.type == file.type).First().Name;
                if (file.type == TVShowsType)
                {
                    makePath = System.IO.Path.Combine(makePath, file.destPath);
                    if (!Directory.Exists(makePath))
                    {
                        Directory.CreateDirectory(makePath);
                    }
                }
                string destFullName = System.IO.Path.Combine(makePath, file.destName);

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
            public string destPath;
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
                    else if (type == MusicVideoType)
                    {
                        return "Music Videos";
                    }
                    else
                    {
                        return "TV Shows";
                    }
                }                
            }
        }
        private void ConstructName(NewFileInfo file, Match TVShowM)
        {
            string destName;

            if (TVShowM.Success)
            {
                file.destPath = file.Name.Substring(0, TVShowM.Index);                
                file.destName = file.Name;  // TVShowS03E02.mkv -> OutputDir\TVShow\TVShowS03E02.mkv per Kodi guidelines for TV Shows.                
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

        

        static SemaphoreSlim semaphoreSlimCO = new SemaphoreSlim(1, 1);
        static SemaphoreSlim semaphoreSlimC2 = new SemaphoreSlim(1, 1);
        string ConsoleOutputTemp = "";
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
