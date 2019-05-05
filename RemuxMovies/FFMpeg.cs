using System.Diagnostics;
using System.Text;
using System.Windows;

namespace RemuxMovies
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Process FFMpegProcess;
        private int RunFFMpeg(string parm)
        {
            var processStartInfo = new ProcessStartInfo(@"c:\users\jgentile\software\ffmpeg\bin\ffmpeg.exe", parm);

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
        private void RunFFProbe(string file)
        {            
            var processStartInfo = new ProcessStartInfo(@"c:\users\jgentile\software\ffmpeg\bin\ffprobe.exe", "\"" + file + "\"" + " -v quiet -print_format json -show_streams");

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
        }

        StringBuilder JsonFFProbe = new StringBuilder();
        private void FFProbe_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            JsonFFProbe.Append(e.Data);
        }
        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }
            PrintToConsoleOutputBG(e.Data);
        }
        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }
            PrintToConsoleOutputBG(e.Data);
        }
    }
}