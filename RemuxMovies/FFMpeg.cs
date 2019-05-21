using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RemuxMovies
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Process FFMpegProcess;
        private async Task<int> RunFFMpeg(string parm)
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var ffmpeg = path + @"\software\ffmpeg\bin\ffmpeg.exe";
            if (!(File.Exists(ffmpeg)))
            {
                await PrintToAppOutputBG("Error, no ffmpeg.exe found.", 0, 1, "red");
                return -1;
            }
            var processStartInfo = new ProcessStartInfo(ffmpeg, parm);

            processStartInfo.UseShellExecute = false;
            processStartInfo.ErrorDialog = false;

            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.CreateNoWindow = true;

            FFMpegProcess = new Process();
            FFMpegProcess.StartInfo = processStartInfo;
            
            FFMpegProcess.OutputDataReceived += Process_OutputDataReceived;
            FFMpegProcess.ErrorDataReceived += Process_ErrorDataReceived;
            FFMpegProcess.Start();
            FFMpegProcess.BeginOutputReadLine();
            FFMpegProcess.BeginErrorReadLine();
            
            FFMpegProcess.WaitForExit();
            return FFMpegProcess.ExitCode;
        }
        private async Task<int> RunFFProbe(string file)
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var ffprobe = path + @"\software\ffmpeg\bin\ffprobe.exe";
            if (!(File.Exists(ffprobe)))
            {
                await PrintToAppOutputBG("Error, no ffprobe.exe found at: " + path + " " + ffprobe, 0, 1, "red");
                return -1;
            }
            var processStartInfo = new ProcessStartInfo(ffprobe, "\"" + file + "\"" + " -v quiet -print_format json -show_streams");

            processStartInfo.UseShellExecute = false;
            processStartInfo.ErrorDialog = false;

            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.CreateNoWindow = true;

            Process process = new Process();
            process.StartInfo = processStartInfo;
            
            process.OutputDataReceived += FFProbe_OutputDataReceived;
            process.ErrorDataReceived += FFProbe_OutputDataReceived;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            return process.ExitCode;
        }

        StringBuilder JsonFFProbe = new StringBuilder();
        private void FFProbe_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            JsonFFProbe.Append(e.Data);
        }
        private async void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }
            await PrintToConsoleOutputBG(e.Data);
        }
        private async void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }
            await PrintToConsoleOutputBG(e.Data);
        }
    }
}