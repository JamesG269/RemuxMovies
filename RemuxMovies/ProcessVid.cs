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
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;

namespace RemuxMovies
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private async Task ProcessVideo(List<NewFileInfo> sFiles)
        {
            List<NewFileInfo> processFiles = sFiles.Where(x => x._Remembered == false || forceAll == true).ToList();
            if (processFiles.Count == 0)
            {
                return;
            }
            var typeList = processFiles.Select(x => x.type).Distinct();
            foreach (var t in typeList)
            {
                if (OutputDirs.Where(x => x.type == t).Count() == 0 || !Directory.Exists(OutputDirs.Where(x => x.type == t).First().Name))
                {
                    await PrintToAppOutputBG("Output Directory does not exist for type: " + typeFriendlyName[t], 0, 1, "red");             
                    return;
                }
            }            
            InitLists();            
            foreach (var file in processFiles)
            {
                await PrintToAppOutputBG(file.originalFullName, 0, 1);
            }
            await PrintToAppOutputBG(" ", 0, 1);
            int num = 0;
            int total = processFiles.Count();
            foreach (var file in processFiles)
            {
                if (AbortProcessing == true)
                {
                    AbortProcessing = false;
                    GetFiles_Cancel = false;
                    break;
                }                    
                if (file.type == MusicVideoType)
                {
                    await createMusicVideoNfo(file);
                }
                num++;
                await PrintToAppOutputBG($"Processing video {num} of {total}:" + Environment.NewLine +
                    file.originalFullName + Environment.NewLine +
                    $"Size: {file.length.ToString("N0")} bytes.", 0, 2);
                bool ret = await processFile(file);                
                file._Remembered = true;
                Dispatcher.Invoke(() =>
                {
                    ListViewUpdater();
                });
                if (ret)
                {                            
                    if (OldMovies.Where(x => x.Name.Equals(file.FullName)).Count() == 0)
                    {
                        OldMovie oldMov = new OldMovie();
                        oldMov.Name = System.IO.Path.GetFileName(file.FullName);
                        oldMov.Num = OldMovies.Count;
                        oldMov.FullName = file.FullName;
                        OldMovies.Add(oldMov);
                        if (oldMov.Name.Length > oldMovMaxLen)
                        {
                            oldMovMaxLen = oldMov.Name.Length;
                        }
                        Dispatcher.Invoke(() => UpdateRememberedList());
                        saveToXML();
                    }
                }   
                else
                {
                    if (AbortProcessing == true)
                    {
                        AbortProcessing = false;
                        GetFiles_Cancel = false;
                        break;
                    }
                    if (SkipProcessing == true)
                    {
                        SkipProcessing = false;
                        continue;
                    }
                }
            }
            await displaySummary();
        }

        private async Task displaySummary()
        {
            await displayList(SkippedList, " movies skipped:", "white");
            await displayList(ErroredList, " movies with errors:", "red");
            await displayList(NoAudioList, " movies with no audio:", "red");
            await displayList(UnusualList, " movies with unusal aspects:", "yellow");
            await displayList(NoTMBDB, " movies not found at TMDB.org", "yellow");
            await displayList(SuccessList, " movies processed successfully:", "lightgreen");
            await displayList(BadChar, " movies with bad char:", "red");
            await PrintToAppOutputBG("Complete!", 1, 1, "lightgreen");
            PrintToConsoleOutputBG("Complete!");
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void InitLists()
        {
            ErroredList = new Dictionary<string, string>();
            SuccessList = new Dictionary<string, string>();
            NoAudioList = new List<string>();
            SkippedList = new List<string>();
            UnusualList = new Dictionary<string, string>();
            BadChar = new List<string>();
            NoTMBDB = new Dictionary<string, string>();
            GetFiles_Cancel = false;
            AbortProcessing = false;
        }

        private async Task displayList(List<string> list, string displayStr, string color)
        {
            if (list.Count == 0)
            {
                return;
            }
            await PrintToAppOutputBG(list.Count + displayStr, 1, 1, list.Count == 0 ? "White" : color);
            foreach (var file in list)
            {
                await PrintToAppOutputBG(file, 0, 1, color);
            }
        }
        private async Task displayList(Dictionary<string, string> list, string displayStr, string color)
        {
            if (list.Count == 0)
            {
                return;
            }
            await PrintToAppOutputBG(list.Count + displayStr, 1, 1, list.Count == 0 ? "White" : color);
            foreach (var file in list)
            {
                await PrintToAppOutputBG(file.Key + " -> " + file.Value, 0, 1, color);
            }
        }

        private async Task<bool> GetMovInfo(NewFileInfo file)
        {
            string err = "";
            JsonFFProbe.Clear();
            int exitCode = await RunFFProbe(file.originalFullName);
            if (exitCode != 0)
            {
                err = "Error, ffprobe return error code: " + exitCode;
                await PrintToAppOutputBG(err, 0, 1, "red");
                ErroredList.Add(file.originalFullName, err);
                return false;
            }
            if (JsonFFProbe.Length == 0)
            {
                err = "FFProbe returned nothing: " + file.originalFullName;
                await PrintToAppOutputBG(err, 0, 1,"red");
                ErroredList.Add(file.originalFullName, err);
                return false;
            }
            string jsonlen = JsonFFProbe.Length.ToString("N0");
            await PrintToAppOutputBG($"Received Json data from FFProbe.exe ({jsonlen} bytes) ...", 0, 1);
            json = JsonValue.Parse(JsonFFProbe.ToString().ToLower());
            bool FindAudioRet = await FindAudioAndSubtitle(file);
            return FindAudioRet;
        }
        private async Task<bool> processFile(NewFileInfo file)
        {
            try
            {
                bool FindAudioRet = await GetMovInfo(file);
                if (FindAudioRet == false)
                {                    
                    return false;
                }
                await PrintToAppOutputBG("Video mapping: " + VidMap, 0, 1);
                await PrintToAppOutputBG("Video destination: " + VidMapTo, 0, 1);
                await PrintToAppOutputBG("Audio mapping: " + AudioMap, 0, 1);
                await PrintToAppOutputBG("Subtitle mapping: " + SubMap, 0, 1);

                string makePath = OutputDirs.Where(x => x.type == file.type).First().Name;
                if (file.type == TVShowsType)
                {
                    makePath = System.IO.Path.Combine(makePath, file.destPath);
                    if (!Directory.Exists(makePath))
                    {
                        Directory.CreateDirectory(makePath);
                    }
                }
                file.destPath = makePath;
                string destFullName = System.IO.Path.Combine(makePath, file.destName);
                string parm = "-y -analyzeduration 2147483647 -probesize 2147483647 -i " + "\"" + file.originalFullName + "\" " + VidMap + AudioMap + SubMap +
                              "-c:v " + VidMapTo + "-c:a ac3 -c:s copy " + "\"" + destFullName + "\"";
                await PrintToAppOutputBG("FFMpeg parms: " + parm, 0, 1);
                int ExitCode = await RunFFMpeg(parm);
                if (ExitCode != 0)
                {
                    string err = "FFMpeg had a possible problem, exit code: " + ExitCode;
                    await PrintToAppOutputBG(err, 0, 1, "red");
                    ErroredList.Add(file.originalFullName, err);
                    return false;
                }
                await getTMDB(file);
                SuccessList.Add(file.originalFullName, file.destName);
                return true;
            }
            catch (Exception e)
            {
                await PrintToAppOutputBG("Something in processFile() has caused an exception: " + e.InnerException.Message, 0, 1, "red");
                return false;
            }
        }
       
    }
}