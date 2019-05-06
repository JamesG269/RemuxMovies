﻿using System;
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
using System.Xml;

namespace RemuxMovies
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string AudioMap, SubMap = "";
        bool forceAll = false;
        JsonValue json;
        List<string> ErroredList;
        List<string> NoAudioList;
        List<string> SkippedList;
        List<string> UnusualList;
        Dictionary<string, string> SuccessList;
        readonly Regex YearRegEx = new Regex(@"\(\d{4}\)", RegexOptions.Compiled);
        readonly Regex YearRegEx2 = new Regex(@"\.\d{4}\.", RegexOptions.Compiled);
        readonly Regex YearRegEx3 = new Regex(@"\s\d{4}\s", RegexOptions.Compiled);


        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PrintToAppOutputBG("MovieRemux v1.1 - Remux movies using FFMpeg (and FFProbe for movie data) to " + Environment.NewLine +
                "convert first English audio to .ac3 and remove all other audio " + Environment.NewLine +
                "and non-English subtitles." + Environment.NewLine +
                "Written by James Gentile." + Environment.NewLine);

        }
        
        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            ErroredList = new List<string>();
            SuccessList = new Dictionary<string, string>();
            NoAudioList = new List<string>();
            SkippedList = new List<string>();
            UnusualList = new List<string>();
            GetFiles_Cancel = false;

            if (forceCheckBox.IsChecked == true)
            {
                forceAll = true;
                PrintToAppOutputBG("Force mode, ignoring remembered movies.");
            }
            else
            {
                forceAll = false;
            }
            if (Properties.Settings.Default.FirstRun == true)
            {
                Properties.Settings.Default.OldMovies.Clear();
                Properties.Settings.Default.FirstRun = false;
            }
            PrintToAppOutputBG(Properties.Settings.Default.OldMovies.Count + " movies remembered.");
            VideoList = new List<NewFileInfo>();
            VideoList.AddRange(await Task.Run(() => GetFiles(@"f:\process", "*.mkv;")));
            VideoList.AddRange(await Task.Run(() => GetFiles(@"g:\process", "*.mkv;")));
            PrintToAppOutputBG(VideoList.Count + " movies found:");
            foreach (var file in VideoList)
            {
                PrintToAppOutputBG(file.originalFullName);
            }
            PrintToAppOutputBG(" ");
            int num = 0;
            foreach (var file in VideoList)
            {
                if (AbortProcessing == true)
                {
                    AbortProcessing = false;
                    break;
                }
                num++;
                if (forceAll == false)
                {
                    if (Properties.Settings.Default.OldMovies.Contains(file.FullName))
                    {
                        PrintToAppOutputBG($"Movie {num} of {VideoList.Count} already processed: {file.originalFullName}");
                        SkippedList.Add(file.originalFullName);
                        continue;
                    }
                }
                PrintToAppOutputBG($"Processing Movie {num} of {VideoList.Count}: {file.originalFullName} {Environment.NewLine}" + 
                                   $"Size: {file.length.ToString("N0")} bytes."
                    );
                bool ret = await Task.Run(() => processFile(file));
                if (ret)
                {
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
            displayList(SkippedList, " movies skipped:");
            displayList(ErroredList, " movies with errors:");
            displayList(NoAudioList, " movies with no audio:");
            displayList(UnusualList, " movies with unusal aspects:");
            displayList(SuccessList, " movies processed successfully:");
            PrintToAppOutputBG("Complete!");
            PrintToConsoleOutputBG("Complete!");
            System.Media.SystemSounds.Asterisk.Play();
        }
        private void displayList(List<string> list, string displayStr)
        {
            PrintToAppOutputBG(Environment.NewLine + list.Count + displayStr);
            foreach (var file in list.Distinct())
            {
                PrintToAppOutputBG(file);
            }
        }
        private void displayList(Dictionary<string, string> list, string displayStr)
        {
            PrintToAppOutputBG(Environment.NewLine + list.Count + displayStr);
            foreach (var file in list)
            {
                PrintToAppOutputBG(file.Key + " -> " + file.Value);
            }
        }
        private bool processFile(NewFileInfo file)
        {
            try
            {
                JsonFFProbe.Clear();
                RunFFProbe(file.FullName);
                if (JsonFFProbe.Length == 0)
                {
                    PrintToAppOutputBG("FFProbe returned nothing: " + file.originalFullName);
                    return false;
                }

                string jsonlen = JsonFFProbe.Length.ToString("N0");
                PrintToAppOutputBG($"Received Json data from FFProbe.exe ({jsonlen} bytes) ...");
                json = JsonValue.Parse(JsonFFProbe.ToString());

                if (FindAudioAndSubtitle(file) == false)
                {
                    PrintToAppOutputBG("Error, No English Audio Found!");
                    NoAudioList.Add(file.originalFullName);
                    return false;
                }
                PrintToAppOutputBG("Audio mapping: " + AudioMap + "\n" +
                                   "Subtitle mapping: " + SubMap
                    );
                string destName = ConstructName(file);

                string destFile = @"h:\media\movies\" + destName;

                string parm = "-y -analyzeduration 64147483647 -probesize 4000000000 -i " + "\"" + file.originalFullName + "\"" + " -map 0:v " + AudioMap + SubMap +
                    "-c:v copy -c:a ac3 -c:s copy " + "\"" + destFile + "\"";
                PrintToAppOutputBG("FFMpeg parms: " + parm);
                int ExitCode = RunFFMpeg(parm);
                if (ExitCode != 0)
                {
                    PrintToAppOutputBG("FFMpeg had a possible problem, exit code: " + ExitCode);
                    return false;
                }
                SuccessList.Add(file.originalFullName, destFile);
                return true;
            }
            catch (Exception e)
            {
                PrintToAppOutputBG("Something in processFile() has caused an exception: " + e.InnerException.Message);
                return false;
            }
        }
        
        public class NewFileInfo
        {            
            public string originalFullName;
            public string originalName;
            public string originalDirectoryName;
            public long length;            
            public string FullName
            {
                get { return originalFullName.ToLower();}  
            }
            public string Name
            {
                get { return originalName.ToLower(); }
            }            
            public string DirectoryName
            {
                get { return originalDirectoryName.ToLower(); }
            }            
        }
        private string ConstructName(NewFileInfo file)
        {            
            string[] dirFrags = file.originalDirectoryName.Split('\\');            
            string destName = file.originalName;
            string destDirName = dirFrags.Last() + ".mkv";
            string[] vidtags = new string[] { "x264", "x265", "avc", "vc-1","vc1","hevc","bluray","blu-ray","dts","truehd","mpeg-2","mpeg2","remux","h264","h265",
                                              "h.264","h.265","1080p","1080i","720p","2160p","ddp","flac"};
            int y = 0;
            bool takeDirName = false;
            if (dirFrags.Length > 1)
            {
                Match m = YearRegEx.Match(destDirName);
                if (m.Success == false)
                {
                    m = YearRegEx2.Match(destDirName);
                }
                if (m.Success == true)
                {
                    int curYear = DateTime.Today.Year;
                    bool result = Int32.TryParse(m.Groups[0].Value.Substring(1, 4), out y);
                    if (result == true && y > 1900 && y < curYear)
                    {
                        takeDirName = true;
                    }
                }
                if (vidtags.Any(destDirName.ToLower().Contains))
                {
                    takeDirName = true;
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
            return destName;
        }

        private bool FindAudioAndSubtitle(NewFileInfo file)
        {
            bool foundAudio = false;
            AudioMap = "";
            SubMap = "";
            if (!json.ContainsKey("streams"))
            {
                PrintToAppOutputBG("Malformed movie data: No streams: " + file.originalFullName);
                return false;
            }
            var streams = json["streams"];
            int VidNum = 0, AudNum = 0, SubNum = 0;
            for (int x = 0; x < streams.Count; x++)
            {
                if (!streams[x].ContainsKey("index"))
                {
                    PrintToAppOutputBG("Malformed movie data: No index in json element: " + x);
                }
                if (!streams[x].ContainsKey("codec_type"))
                {
                    PrintToAppOutputBG("Malformed movie data: No codec_type in json element: " + x);
                }
                string codectype = JsonValue.Parse(streams[x]["codec_type"].ToString());
                int index = JsonValue.Parse(streams[x]["index"].ToString());
                switch (codectype)
                {
                    case "video":
                        VidNum++;
                        break;
                    case "audio":
                        AudNum++;
                        if (foundAudio == true)
                        {
                            continue;
                        }

                        // Find first audio file that is english or unspecified language, which is usually english.

                        if (streams[x].ContainsKey("tags") && streams[x]["tags"].ContainsKey("language"))
                        {
                            var language = streams[x]["tags"]["language"];
                            if (!(language == "eng"))
                            {
                                if (language == null || language == "")  // check for no language, usually if not labeled, the first audio track is english.
                                {
                                    PrintToAppOutputBG("Unusual movie, audio language not defined, index #" + index);
                                    UnusualList.Add(file.originalFullName);     // empty language tag, probably english.
                                }
                                else
                                {
                                    continue;                           // not empty and not english
                                }
                            }
                        }
                        else
                        {
                            PrintToAppOutputBG("Unusual movie, audio language not defined, index #" + index);
                            UnusualList.Add(file.originalFullName);             // no tags or language in tags, probably english.
                        }
                        if (streams[x].ContainsKey("tags") && streams[x]["tags"].ContainsKey("title"))
                        {
                            if (streams[x]["tags"]["title"].ToString().ToLower().Contains("commentary"))
                            {
                                PrintToAppOutputBG("Unusual movie, commentary is before audio track, index #" + index);
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
            PrintToAppOutputBG("Number of Video streams: " + VidNum);
            PrintToAppOutputBG("Number of Audio streams: " + AudNum);
            PrintToAppOutputBG("Number of Subtitle streams: " + SubNum);
            return foundAudio;
        }

        private void PrintToAppOutputBG(string str)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AppOutput.Text += str;
                AppOutput.Text += Environment.NewLine;
                AppScroll.ScrollToEnd();
            }));
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
            }
            while (FFMpegProcess.HasExited != true)
            {
                await Task.Delay(10);
            }
            PrintToAppOutputBG("FFMpeg process killed.");
        }

        private void PrintToConsoleOutputBG(string str)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (FrameFound == true)
                {
                    ConsoleOutput.Text = ConsoleOutput.Text.Substring(0, LastFrame);
                    FrameFound = false;
                }
                if (str.StartsWith("frame"))
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
            }));
        }
    }
}
