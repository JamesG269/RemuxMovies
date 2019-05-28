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
        private async Task<bool> FindAudioAndSubtitle(NewFileInfo file)
        {
            bool foundEngAudio = false;
            bool foundNonEngAudio = false;
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
                        VidNum++;
                        if (streams[x].ContainsKey("codec_name") && streams[x]["codec_name"] == "vc1")
                        {
                            if (streams[x].ContainsKey("tags") && streams[x]["tags"].ContainsKey("bps-eng"))
                            {
                                int bitrate = JsonValue.Parse(streams[x]["tags"]["bps-eng"].ToString());
                                bitrate = bitrate / 1000;
                                VidMap = "-map 0:" + index + " ";
                                VidMapTo = "libx264 -b:v " + bitrate.ToString() + "k ";
                            }
                            else
                            {
                                MessageBox.Show("VC-1 bit rate [tags][bps-eng] not found.");
                                return false;
                                //VidMap = "-map 0:" + index + " ";
                                // VidMapTo = "libx264 -crf 18 ";
                            }

                            await PrintToAppOutputBG("Video is VC-1, converting to x264 using flags: " + VidMapTo, 0, 1, "yellow");
                        }                        
                        break;
                    case "audio":
                        AudNum++;
                        if (foundEngAudio == true)
                        {
                            break;
                        }
                        if (streams[x].ContainsKey("tags") && streams[x]["tags"].ContainsKey("title"))
                        {
                            if (streams[x]["tags"]["title"].ToString().ToLower().Contains("commentary"))
                            {
                                await PrintToAppOutputBG("Unusual movie, commentary is before audio track, index #" + index, 0, 1, "yellow");
                                UnusualList.Add(file.originalFullName);
                                break;
                            }
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
                                    if (foundNonEngAudio == true)
                                    {
                                        break;
                                    }
                                    foundNonEngAudio = true;
                                    AudioMap = "-map 0:" + index + " ";
                                    break;
                                }
                            }
                        }
                        else
                        {
                            await PrintToAppOutputBG("Unusual movie, audio language not defined, index #" + index, 0, 1, "yellow");
                            UnusualList.Add(file.originalFullName);             // no tags or language in tags, probably english.
                        }
                        AudioMap = "-map 0:" + index + " ";
                        foundEngAudio = true;
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
            return foundEngAudio | foundNonEngAudio;
        }
    }
}