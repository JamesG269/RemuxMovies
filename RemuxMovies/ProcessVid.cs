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
        private async Task ProcessVideo(List<NewFileInfo> sourceFiles)
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
                if (forceAll == true || file._Remembered == false)
                {
                    await PrintToAppOutputBG(file.originalFullName, 0, 1);
                }
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
                    await PrintToAppOutputBG("Output directory not set for " + file.FriendlyType, 0, 1, "red");
                    ErroredList.Add(file.originalFullName);
                    continue;
                }
                if (file.type == MusicVideoType)
                {
                    await createNfo(file);
                }

                if (forceAll == false && file._Remembered == true)
                {
                    await PrintToAppOutputBG($"Video {num} of {SourceFiles.Where(x => !x._Remembered || forceAll).Count()} already processed:", 0, 1);
                    await PrintToAppOutputBG(file.originalFullName, 0, 1);
                    SkippedList.Add(file.originalFullName);
                    continue;
                }
                await PrintToAppOutputBG($"Processing video {num} of {SourceFiles.Where(x => !x._Remembered || forceAll).Count()}:", 0, 1);
                await PrintToAppOutputBG(file.originalFullName, 0, 1);
                await PrintToAppOutputBG($"Size: {file.length.ToString("N0")} bytes.", 0, 2);
                bool ret = await processFile(file);
                if (ret)
                {
                    file._Remembered = true;
                    Dispatcher.Invoke(() => {
                        ListViewUpdater();
                        //fileListView.Items.Refresh();
                    });

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
            PrintToConsoleOutputBG("Complete!");
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

                string parm = "-threads 6 -y -analyzeduration 2147483647 -probesize 2147483647 -i " + "\"" + file.originalFullName + "\" " + VidMap + AudioMap + SubMap +
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
    }
}