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
            ToggleButtons(false);
            if (OutputDirs.Where(x => x.type == MovieType).Count() != 0)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                await Task.Run(() => ProcessNfo());
                sw.Stop();
                await PrintToAppOutputBG("Time (ms): " + sw.ElapsedMilliseconds, 0, 1);
            }
            ToggleButtons(true);
            Interlocked.Exchange(ref oneInt, 0);
        }
        private async Task ProcessNfo()
        {            
            InitLists();
            nfoList = new List<NewFileInfo>();
            nfoList.AddRange(await Task.Run(() => GetFiles(OutputDirs.Where(x => x.type == MovieType).First().Name, "*.mkv;")));            
            if (nfoList.Count == 0)
            {
                return;
            }
            await PrintToAppOutputBG(nfoList.Count + " .nfo files need to be created.", 0, 1);
            int num = 0;
            await PrintToAppOutputBG("Creating .nfo files", 0, 1);
            foreach (var file in nfoList)
            {
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
                file.destPath = file.DirectoryName;
                file.destName = file.originalName;
                bool ret = await getTMDB(file);
                if (!ret)
                {
                    await PrintToAppOutputBG($"Error making .nfo for: {file.originalFullName}", 0, 1);
                }
            }
            await displaySummary();
            await PrintToAppOutputBG(num + " .nfo files created.", 0, 1, "green");            
        }
        private async Task createNfo(NewFileInfo nfi)
        {
            string nfo = System.IO.Path.Combine(OutputDirs.Where(x => x.type == MusicVideoType).First().Name, nfi.originalFullName.Substring(0, nfi.originalFullName.Length - 4) + ".nfo");
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
    }
}