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
            InitLists();


            await PrintToAppOutputBG(" ", 0, 1);
            int num = 0;
            int total = processFiles.Count();
            foreach (var file in processFiles)
            {
                await PrintToAppOutputBG(file.originalFullPath, 0, 1);
                var dirs = SourceDirs.Where(x => x.type == file.type && x.Directory == file.Directory);
                if (dirs.Count() != 1)
                {
                    await PrintToAppOutputBG("Output Directory does not exist for type: " + typeFriendlyName[file.type], 0, 1, "red");
                    continue;
                }
                if (AbortProcessing == true)
                {
                    AbortProcessing = false;
                    GetFiles_Cancel = false;
                    break;
                }
                if (file.type == MusicVideoType)
                {
                    file.destPath = dirs.First().Directory;
                    await createMusicVideoNfo(file);
                }
                num++;
                await PrintToAppOutputBG($"Processing video {num} of {total}:" + Environment.NewLine +
                    file.originalFullPath + Environment.NewLine +
                    $"Size: {file.length.ToString("N0")} bytes.", 0, 2);
                bool ret = await processFile(file);
                file._Remembered = true;
                if (ret)
                {
                    AddToOldMovies(file);
                    saveToXML();
                }
                Dispatcher.Invoke(() =>
                {
                    ListViewUpdater();
                    UpdateRememberedList();
                });
                if (!ret)
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

        private void AddToOldMovies(NewFileInfo file)
        {
            OldMovies.RemoveAll(x => string.Compare(x.FileName, file.FullPath, true) == 0);            
            OldMovie oldMov = new OldMovie();
            oldMov.FileName = System.IO.Path.GetFileName(file.FullPath);
            oldMov.Num = OldMovies.Count;
            oldMov.FullPath = file.FullPath;
            oldMov.MovieName = AddMovieName(oldMov.FileName);
            oldMov.Size = file.length / 1000000000;
            OldMovies.Add(oldMov);
        }

        private async Task displaySummary()
        {
            await displayList(SkippedList, " movies skipped:", "white");
            await displayList(ErroredList, " movies with errors:", "red");
            await displayList(NoAudioList, " movies with no audio:", "red");
            await displayList(UnusualList, " movies with unusal aspects:", "yellow");
            await displayList(NoTMBDB, " movies not found at TMDB.org", "yellow");
            await displayList(SuccessList, " movies processed successfully:", "lightgreen");
            await displayList(vc1List, " movies use the VC-1 codec.", "red");
            await displayList(BadChar, " movies with bad char:", "lightblue");
            await PrintToAppOutputBG("Complete!", 1, 1, "lightgreen");
            PrintToConsoleOutputBG("Complete!");
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void InitLists()
        {
            vc1List = new List<string>();
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
            int exitCode = await RunFFProbe(file.originalFullPath);
            if (exitCode != 0)
            {
                err = "Error, ffprobe return error code: " + exitCode;
                await PrintToAppOutputBG(err, 0, 1, "red");
                ErroredListAdd(file.originalFullPath, err);
                return false;
            }
            if (JsonFFProbe.Length == 0)
            {
                err = "FFProbe returned nothing: " + file.originalFullPath;
                await PrintToAppOutputBG(err, 0, 1, "red");
                ErroredListAdd(file.originalFullPath, err);
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

                var dirs = SourceDirs.Where(x => x.type == file.type && x.Directory == file.Directory);
                if (dirs.Count() != 1)
                {
                    await PrintToAppOutputBG("Output Directory does not exist for type: " + typeFriendlyName[file.type], 0, 1, "red");
                    return false;
                }
                string makePath = dirs.First().Directory;
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
                string parm = "-y -analyzeduration 2147483647 -probesize 2147483647 -i " + "\"" + file.originalFullPath + "\" " + VidMap + AudioMap + SubMap +
                              "-c:v " + VidMapTo + "-c:a ac3 -c:s copy " + "\"" + destFullName + "\"";
                await PrintToAppOutputBG("FFMpeg parms: " + parm, 0, 1);
                int ExitCode = await RunFFMpeg(parm);
                if (ExitCode != 0)
                {
                    string err = "FFMpeg had a possible problem, exit code: " + ExitCode;
                    await PrintToAppOutputBG(err, 0, 1, "red");
                    ErroredListAdd(file.originalFullPath, err);
                    return false;
                }
                string nfoFile = System.IO.Path.Combine(file.destPath, file.destName.Substring(0, file.destName.Length - 4) + ".nfo");
                await getTMDB(file, nfoFile);
                SuccessListAdd(file.originalFullPath, file.destName);
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