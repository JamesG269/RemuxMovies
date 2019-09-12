using System.Json;
using System.Threading.Tasks;
using System.Windows;


namespace RemuxMovies
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string audioLanguage = "";
        private async Task<bool> FindAudioAndSubtitle(NewFileInfo file)
        {
            string err = "";
            bool foundEngAudio = false;
            bool foundNonEngAudio = false;
            AudioMap = "";
            SubMap = "";
            VidMap = "-map 0:v ";
            VidMapTo = "copy ";
            if (!json.ContainsKey("streams"))
            {
                await PrintToAppOutputBG("Malformed movie data: No streams: " + file.originalFullPath, 0, 1, "red");
                return false;
            }
            var streams = json["streams"];
            int VidNum = 0, AudNum = 0, SubNum = 0;
            for (int x = 0; x < streams.Count; x++)
            {
                if (!streams[x].ContainsKey("index"))
                {
                    err = "Malformed movie data: No index in json element: " + x;
                    await PrintToAppOutputBG(err, 0, 1, "yellow");
                    UnusualListAdd(file.originalFullPath, err);
                    continue;
                }
                if (!streams[x].ContainsKey("codec_type"))
                {
                    err = "Malformed movie data: No codec_type in json element: " + x;
                    await PrintToAppOutputBG(err, 0, 1, "yellow");
                    UnusualListAdd(file.originalFullPath, err);
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
                                bitrate /= 1000;
                                VidMap = "-map 0:" + index + " ";
                                VidMapTo = "libx264 -b:v " + bitrate.ToString() + "k ";
                            }
                            else
                            {                                
                                await PrintToAppOutputBG("VC-1 bit rate [tags][bps-eng] not found.",0,1,"yellow");
                                VidMap = "-map 0:" + index + " ";
                                VidMapTo = "libx264 -crf 18 ";                                
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
                                err = "Unusual movie, commentary is before audio track, index #" + index;
                                await PrintToAppOutputBG(err, 0, 1, "yellow");
                                UnusualListAdd(file.originalFullPath, err);
                                break;
                            }
                        }

                        // Find first audio track that is english or unspecified language, which is usually english.

                        audioLanguage = "en";
                        if (streams[x].ContainsKey("tags") && streams[x]["tags"].ContainsKey("language"))
                        {                            
                            var language = streams[x]["tags"]["language"];

                            audioLanguage = JsonValue.Parse(language.ToString());
                            if (audioLanguage.Length > 2)
                            {
                                audioLanguage = audioLanguage.Substring(0, 2);
                            }
                            await PrintToAppOutputBG("Language in movie == " + language, 0, 1, "yellow");
                            if (!(language == "eng"))
                            {
                                if (language == null || language == "" || language == "und")
                                {
                                    err = "Unusual movie, audio language not defined, index #" + index;
                                    await PrintToAppOutputBG(err, 0, 1, "yellow");
                                    UnusualListAdd(file.originalFullPath, err);
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
                            err = "Unusual movie, audio language not defined, index #" + index;
                            await PrintToAppOutputBG(err, 0, 1, "yellow");
                            UnusualListAdd(file.originalFullPath, err);             // no tags or language in tags, probably english.
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
        private void UnusualListAdd(string key, string val)
        {
            if (UnusualList.ContainsKey(key))
            {
                UnusualList.Remove(key);
            }
            UnusualList.Add(key, val);
        }
        private void SuccessListAdd(string key, string val)
        {
            if (SuccessList.ContainsKey(key))
            {
                SuccessList.Remove(key);
            }
            SuccessList.Add(key, val);
        }
        private void ErroredListAdd(string key, string val)
        {
            if (ErroredList.ContainsKey(key))
            {
                ErroredList.Remove(key);
            }
            ErroredList.Add(key, val);
        }       
    }
}