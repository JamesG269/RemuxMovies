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
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
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
        string[] VidExts = { ".mkv", ".mp4", ".avi", ".m4v", ".wmv" };
        string SourceXML = "";
        string OldMoviesXML = "";
        string HardLinksXML = "";
        const int MovieType = 0;
        const int MusicVideoType = 1;
        const int TVShowsType = 2;
        const int NfoType = 3;
        const int HardlinkType = 4;
        public static Dictionary<int, string> typeFriendlyName = new Dictionary<int, string>()
        {
            {MovieType, "Movies" },
            {MusicVideoType, "Music Videos"},
            {TVShowsType, "TV Shows"},
            {NfoType, "Nfo's" },
            {HardlinkType, "Hardlinks" }
        };

        Dictionary<string, string> ErroredList;
        List<string> vc1List;
        List<string> NoAudioList;
        List<string> SkippedList;
        Dictionary<string, string> UnusualList;
        List<string> BadChar;
        List<string> nonChar;
        List<OldMovie> OldMovies = new List<OldMovie>();
        List<OldHardLink> OldHardLinks = new List<OldHardLink>();

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
            OldMoviesXML = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"RemuxMovies\OldMovies.xml");
            HardLinksXML = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"RemuxMovies\HardLinks.xml");
            curYear = DateTime.Today.Year;

            await PrintToAppOutputBG("MovieRemux v1.1 - Remux movies using FFMpeg (and FFProbe for movie data) to " +
                "convert first English audio to .ac3 and remove all other audio " +
                "and non-English subtitles. Written by James Gentile.", 0, 2);

            await loadFromXML();
            await Task.Run(() => AddMovieNames());
            UpdateRememberedList();

            await PrintToAppOutputBG("Downloading latest FFMpeg/FFProbe if version not up to date.", 0, 1);
            FFmpeg.ExecutablesPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RemuxMovies\\FFmpeg\\");
            var ffmpegDir = System.IO.Path.Combine(FFmpeg.ExecutablesPath, "ffmpeg.exe");
            try
            {
                await FFmpeg.GetLatestVersion();
            }
            catch
            {
                await PrintToAppOutputBG("Error checking for FFMpeg update.", 0, 1, "red");
            }
            if (!File.Exists(ffmpegDir))
            {
                MessageBox.Show("FFMpeg/FFProbe not found at: " + ffmpegDir);
                //Close();
            }
            await PrintToAppOutputBG("Ready. ", 0, 2, "green");
            if (await CheckAutoJob() == true)
            {
                //Close();
            }
            ToggleButtons(true);
        }

        private void AddMovieNames()
        {
            bool save = false;
            foreach (var mov in OldMovies)
            {
                if (!string.IsNullOrWhiteSpace(mov.MovieName))
                {
                    continue;
                }                      
                save = true;
                mov.MovieName = AddMovieName(mov.FileName);
            }
            foreach (var mov in OldHardLinks)
            {
                if (!string.IsNullOrWhiteSpace(mov.MovieName))
                {
                    continue;
                }
                save = true;
                mov.MovieName = AddMovieName(System.IO.Path.GetFileName(mov.TargetFullPath));
            }
            if (save == true)
            {
                saveToXML();
            }
        }
        private string AddMovieName(string fileName)
        {
            fileName = fileName.ToLower();
            fileName = fileName.Substring(0, fileName.Length - 4);
            int fileYear = 0;
            int regExIdx = 0;
            bool foundYear = GetFileYear(ref fileName, ref fileYear, ref regExIdx);
            if (foundYear)
            {
                fileName = fileName.Substring(0, regExIdx);
            }
            fileName = removeVidTags(fileName);
            List<string> fileNameParts = new List<string>();
            getFileNameParts(fileName, fileNameParts);
            string retStr = string.Join(" ", fileNameParts);
            if (foundYear)
            {
                retStr += " (" + fileYear + ")";
            }
            return retStr;
        }
        private async Task<bool> CheckAutoJob()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Count() < 2)
            {
                return false;
            }
            bool doAutoJob = false;
            for (int i = 1; i < args.Count(); i++)
            {
                if (string.Compare(args[i], "/auto", true) == 0 || string.Compare(args[i], "-auto", true) == 0)
                {
                    doAutoJob = true;
                }
            }
            if (doAutoJob)
            {
                await Task.Run(() => MakeHardlinks());
                await MakeNfos();
                ToggleButtons(false);
                await Task.Run(() => CheckNfoDupes());
            }
            return true;
        }
        public class OldMovie
        {
            public string FileName { get; set; }
            public int Num { get; set; }
            public string FullPath { get; set; }
            public string displayName { get; set; }
            public string MovieName { get; set; }
            public long Size { get; set; }
        }        

        public class OldHardLink
        {
            public string TargetFullPath { get; set; }
            public int Num { get; set; }
            public string displayFullPath { get; set; }
            public string SourceFullPath { get; set; }
            public string SourceDir { get; set; }
            public string MovieName { get; set; }
        }



        public void saveToXML()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<NewDirInfo>));
            using (StreamWriter streamWriter = new StreamWriter(SourceXML))
            {
                serializer.Serialize(streamWriter, SourceDirs);
            }

            serializer = new XmlSerializer(typeof(List<OldMovie>));
            using (StreamWriter streamWriter = new StreamWriter(OldMoviesXML))
            {
                serializer.Serialize(streamWriter, OldMovies);
            }
            serializer = new XmlSerializer(typeof(List<OldHardLink>));
            using (StreamWriter streamWriter = new StreamWriter(HardLinksXML))
            {
                serializer.Serialize(streamWriter, OldHardLinks);
            }
        }
        public async Task<bool> loadFromXML()
        {
            bool save = false;
            XmlSerializer serializer = new XmlSerializer(typeof(List<OldMovie>));
            if (File.Exists(OldMoviesXML))
            {
                using (StreamReader streamReader = new StreamReader(OldMoviesXML))
                {
                    OldMovies = (List<OldMovie>)serializer.Deserialize(streamReader);
                    foreach (var o in OldMovies)
                    {
                        //if (string.IsNullOrWhiteSpace(o.MovieName))
                        {
                            o.MovieName = AddMovieName(o.FileName);
                        }
                    }                    
                    int t = OldMovies.Where(c => c.Size > 0).Count();
                    int i = 0;
                    foreach (var o in OldMovies.ToList())
                    {
                        if (o.Size == 0)
                        {
                            continue;
                        }
                        var m = IsTVShow(o.FullPath);
                        if (m.Success == true)
                        {
                            continue;
                        }
                        var x = OldMovies.Where(c => string.Compare(c.MovieName, o.MovieName, true) == 0).ToList();
                        if (x.Count() > 1)
                        {
                            save = true;
                            OldMovies.RemoveAll(c => string.Compare(c.MovieName, o.MovieName, true) == 0 && c.Size == 0);
                            i += x.Where(c => c.Size == 0).Count();
                        }
                    }
                    await PrintToAppOutputBG(OldMovies.Count + " movies remembered.", 0, 1);
                    await PrintToAppOutputBG(i.ToString() + " 0 sized movies removed. ", 0, 2);
                    
                }
            }
            serializer = new XmlSerializer(typeof(List<OldHardLink>));
            if (File.Exists(HardLinksXML))
            {
                using (StreamReader streamReader = new StreamReader(HardLinksXML))
                {
                    OldHardLinks = (List<OldHardLink>)serializer.Deserialize(streamReader);
                }
                await PrintToAppOutputBG(OldHardLinks.Count + " HardLinks remembered.", 0, 2);
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

            await displayFilesToProcess();
            await LoadNonChar();
            if (save == true)
            {
                saveToXML();
            }
            return true;
        }

        private void populateInfoLabel()
        {
            populateInfoLabel(out List<NewFileInfo> movsList, out List<NewFileInfo> musicVideosList, out List<NewFileInfo> tvShowsList, out List<NewFileInfo> hardLinkList);
            return;
        }
        private void populateInfoLabel(out List<NewFileInfo> numMovs, out List<NewFileInfo> numMusicVideos, out List<NewFileInfo> numTvShows, out List<NewFileInfo> hardLinkList)
        {
            numMovs = SourceFiles.Where(c => c.type == MovieType && ((c._Remembered & !forceAll) == false)).ToList();
            numMusicVideos = SourceFiles.Where(c => c.type == MusicVideoType && ((c._Remembered & !forceAll) == false)).ToList();
            numTvShows = SourceFiles.Where(c => c.type == TVShowsType && ((c._Remembered & !forceAll) == false)).ToList();
            hardLinkList = SourceFiles.Where(c => c.type == HardlinkType && ((c._Remembered & !forceAll) == false)).ToList();
            infoLabel.Content = $"{numMovs.Count} Movies ready to process." + Environment.NewLine +
                                $"{numMusicVideos.Count} Music Videos ready to process." + Environment.NewLine +
                                $"{numTvShows.Count} TV Shows ready to process.";
        }
        private async Task<bool> displayFilesToProcess()
        {
            populateInfoLabel(out List<NewFileInfo> movsList, out List<NewFileInfo> musicVideosList, out List<NewFileInfo> tvShowsList, out List<NewFileInfo> hardLinkList);
            if (movsList.Count > 0)
            {
                await PrintToAppOutputBG($"{movsList.Count} Movie(s) to be processed: ", 0, 1);
                foreach (var f in movsList)
                {
                    await PrintToAppOutputBG(f.originalFullPath, 0, 1);
                }
            }
            if (musicVideosList.Count > 0)
            {
                await PrintToAppOutputBG($"{musicVideosList.Count} Music Video(s) to be processed: ", 0, 1);
                foreach (var f in musicVideosList)
                {
                    await PrintToAppOutputBG(f.originalFullPath, 0, 1);
                }
            }
            if (tvShowsList.Count > 0)
            {
                await PrintToAppOutputBG($"{tvShowsList.Count} TV Show(s) to be processed: ", 0, 1);
                foreach (var f in tvShowsList)
                {
                    await PrintToAppOutputBG(f.originalFullPath, 0, 1);
                }
            }
            if (hardLinkList.Count > 0)
            {
                await PrintToAppOutputBG($"{hardLinkList.Count} Hard Links to be processed: ", 0, 1);
                foreach (var f in hardLinkList)
                {
                    await PrintToAppOutputBG(f.originalFullPath, 0, 1);
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
            MakeHardLinksButton.IsEnabled = t;
        }

        public int oneInt = 0;

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

            string[] dirFrags = file.originalDirectory.Split('\\');
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
                    var tempList = GetFiles(file.Directory, file.type, VidExts);
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
            await Dispatcher.Invoke(async () =>
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
                if (IsTVShow(o.FileName).Success)
                {
                    continue;
                }
                files.Add(System.IO.Path.GetFileName(o.FileName));
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

        private async void MakeHardLinksButton_Click(object sender, RoutedEventArgs e)
        {
            tabControl.SelectedIndex = 0;
            ToggleButtons(false);
            await Task.Run(() => MakeHardlinks());
            ToggleButtons(true);
        }
        private async Task<bool> MakeHardlinks()
        {
            int num = 0;

            foreach (var OldHL in OldHardLinks.ToList())
            {
                string drv = System.IO.Path.GetPathRoot(OldHL.SourceFullPath);
                if (!Directory.Exists(drv))
                {
                    continue;
                }
                if (SourceDirs.Where(x => x.type == HardlinkType && x.Directory.Equals(OldHL.SourceDir)).Count() == 0)
                {
                    continue;
                }
                if (!File.Exists(OldHL.TargetFullPath))
                {
                    OldHardLinks.Remove(OldHL);
                    var s = SourceFiles.Where(c => c.type == HardlinkType && c.FullPath == OldHL.SourceFullPath);
                    s.All(c => c._Remembered = false);
                }
                if (!File.Exists(OldHL.SourceFullPath))
                {
                    if (File.Exists(OldHL.TargetFullPath))
                    {
                        File.Delete(OldHL.TargetFullPath);
                        await PrintToAppOutputBG("Deleted hardlink: " + OldHL.TargetFullPath, 0, 1);
                    }
                    OldHardLinks.Remove(OldHL);
                }
            }

            var hlSourceFiles = SourceFiles.Where(x => x.type == HardlinkType && x._Remembered == false).ToList();
            foreach (var hlSourceFile in hlSourceFiles)
            {
                var dirs = SourceDirs.Where(x => x.type == HardlinkType && x.Directory == hlSourceFile.fromDirectory);
                if (dirs.Count() != 1)
                {
                    continue;
                }
                var dir = dirs.First();
                string hlTargetFile = System.IO.Path.Combine(dir.OutputDir, hlSourceFile.FileName);
                if (File.Exists(hlTargetFile))
                {
                    await PrintToAppOutputBG("Hardlink already exists: " + hlTargetFile, 0, 1, "red");
                    hlSourceFile._Remembered = true;
                    AddHardLink(hlSourceFile, hlTargetFile);
                    continue;
                }
                bool result = CreateHardLink(hlTargetFile, hlSourceFile.originalFullPath, IntPtr.Zero);
                if (result && File.Exists(hlTargetFile))
                {
                    num++;
                    await PrintToAppOutputBG(hlTargetFile + " hardlinked to source: " + hlSourceFile.originalFullPath, 0, 1, "lightgreen");
                    hlSourceFile._Remembered = true;
                    AddHardLink(hlSourceFile, hlTargetFile);
                    AddToOldMovies(hlSourceFile);
                }
                else
                {
                    await PrintToAppOutputBG(hlTargetFile + " NOT hardlinked to source: " + hlSourceFile.originalFullPath, 0, 1, "red");
                    hlSourceFile._Remembered = false;
                }
            }
            Dispatcher.Invoke(() => UpdateRememberedList());
            await PrintToAppOutputBG(num.ToString() + " hard links created.", 0, 1);
            saveToXML();
            return true;
        }

        private void AddHardLink(NewFileInfo hlSourceFile, string hlTargetFile)
        {
            OldHardLink oldHL = new OldHardLink();
            oldHL.TargetFullPath = hlTargetFile.ToLower();
            oldHL.Num = OldHardLinks.Count;
            oldHL.SourceDir = hlSourceFile.originalDirectory.ToLower();
            oldHL.SourceFullPath = hlSourceFile.originalFullPath.ToLower();
            oldHL.MovieName = AddMovieName(hlSourceFile.FileName);
            if (OldHardLinks.Where(x => x.SourceFullPath == hlSourceFile.FullPath).Count() == 0)
            {
                OldHardLinks.Add(oldHL);
            }

        }

        private async void CheckNfoDupesButton_Click(object sender, RoutedEventArgs e)
        {
            tabControl.SelectedIndex = 0;
            await Task.Run(() => CheckNfoDupes());
        }
        private async Task<bool> CheckNfoDupes()
        {
            Dispatcher.Invoke(() => ClearWindows());
            Dictionary<string, List<string>> HashName = new Dictionary<string, List<string>>();
            List<NewFileInfo> nfoFiles = new List<NewFileInfo>();

            foreach (var dir in SourceDirs.Where(x => x.type == NfoType))
            {
                nfoFiles.AddRange(GetFiles(dir.Directory, NfoType, new string[] { "*.nfo" }));
            }
            if (nfoFiles.Count() == 0)
            {
                await PrintToAppOutputBG("No .nfo files found.", 0, 1);
                return false;
            }

            using (SHA512 sha512 = new SHA512Managed())
            {
                foreach (var nfoFile in nfoFiles)
                {
                    bool foundVid = false;
                    foreach (var ext in VidExts)
                    {
                        var vidFile = nfoFile.originalFullPath.Substring(0, nfoFile.originalFullPath.Length - 4) + ext;
                        if (File.Exists(vidFile))
                        {
                            foundVid = true;
                        }
                    }
                    if (foundVid == false)
                    {
                        File.Delete(nfoFile.originalFullPath);
                        await PrintToAppOutputBG("Lone .nfo file deleted: " + nfoFile.originalFullPath, 0, 1, "yellow");
                        continue;
                    }
                    using (FileStream fs = new FileStream(nfoFile.originalFullPath, FileMode.Open))
                    using (BufferedStream bs = new BufferedStream(fs))
                    {
                        byte[] hash = sha512.ComputeHash(bs);

                        StringBuilder formatted = new StringBuilder(2 * hash.Length);
                        foreach (byte b in hash)
                        {
                            formatted.AppendFormat("{0:X2}", b);
                        }
                        string hashStr = formatted.ToString();

                        if (!HashName.ContainsKey(hashStr))
                        {
                            HashName.Add(hashStr, new List<string>());
                        }
                        HashName[hashStr].Add(nfoFile.originalFullPath);
                    }
                }
            }
            foreach (var h in HashName)
            {
                if (h.Value.Count > 1)
                {
                    await displayList(h.Value, " matches in dupe group: ", "white");
                }
            }
            return true;
        }


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
