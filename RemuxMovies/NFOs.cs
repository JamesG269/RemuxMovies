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
        private async void MakeNfos_Click(object sender, RoutedEventArgs e)
        {
            if (0 != Interlocked.Exchange(ref oneInt, 1))
            {
                return;
            }
            tabControl.SelectedIndex = 0;
            ToggleButtons(false);
            await MakeNfos();
            Interlocked.Exchange(ref oneInt, 0);
            ToggleButtons(true);
        }

        private async Task MakeNfos()
        {           
            if (SourceDirs.Where(x => x.type == NfoType).Count() != 0)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                await Task.Run(() => ProcessNfo());
                sw.Stop();
                await PrintToAppOutputBG("Time (ms): " + sw.ElapsedMilliseconds, 0, 1);
            }            
        }

        private async Task ProcessNfo()
        {            
            InitLists();
            nfoList = new List<NewFileInfo>();
            foreach (var nfoDir in SourceDirs.Where(x => x.type == NfoType))
            {
                nfoList.AddRange(await Task.Run(() => GetFiles(nfoDir.Directory, NfoType, VidExts)));
            }
            if (nfoList.Count == 0)
            {
                return;
            }
            
            await PrintToAppOutputBG(nfoList.Count + " movies found in nfo directory.", 0, 1);
            int num = 0;
            await PrintToAppOutputBG("Creating .nfo files", 0, 1);
            foreach (var file in nfoList)
            {                
                string nfoFile = file.originalFullPath.Substring(0, file.originalFullPath.Length - 4) + ".nfo";
                if (File.Exists(nfoFile))
                {
                    //await PrintToAppOutputBG(nfoFile + " already exists, skipping.", 0, 1);
                    continue;
                }
                await PrintToAppOutputBG($"Creating .nfo for: {file.originalName}", 0, 1);
                if (await GetMovInfo(file) == false)
                {
                    continue;
                }                
                if (AbortProcessing == true)
                {
                    await PrintToAppOutputBG("Nfo creation aborted!", 0, 1, "red");
                    AbortProcessing = false;
                    GetFiles_Cancel = false;
                    break;
                }
                num++;
                file.destPath = file.Directory;
                file.destName = file.originalName;
                bool ret = await getTMDB(file, nfoFile);
                if (!ret)
                {
                    await PrintToAppOutputBG($"Error making .nfo for: {file.originalFullPath}", 0, 1);
                }
            }
            await displaySummary();
            await PrintToAppOutputBG(num + " .nfo files created.", 0, 1, "green");            
        }
        private async Task createMusicVideoNfo(NewFileInfo nfi)
        {
            string err = "";
            string nfo = System.IO.Path.Combine(nfi.destPath, nfi.originalFullPath.Substring(0, nfi.originalFullPath.Length - 4) + ".nfo");
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
                    err = "Music Video .nfo file could not be created.";
                    await PrintToAppOutputBG(err, 0, 1, "red");
                    ErroredListAdd(nfo, err);
                }
            }
            catch (Exception e)
            {
                err = "Exception thrown in createNfo(): " + e.InnerException.Message;
                await PrintToAppOutputBG(err, 0, 1, "Red");
                ErroredListAdd(nfo, err);
            }
        }
    }
}