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
            List<NewFileInfo> sourceFiles = sFiles.Where(x => x._Remembered == false || forceAll == true).ToList();
            if (sourceFiles.Count == 0)
            {
                return;
            }
            InitLists();

            Properties.Settings.Default.Reload();

            foreach (var file in sourceFiles)
            {
                await PrintToAppOutputBG(file.originalFullName, 0, 1);
            }
            await PrintToAppOutputBG(" ", 0, 1);
            int num = 0;
            int total = SourceFiles.Count();
            foreach (var file in sourceFiles)
            {
                if (AbortProcessing == true)
                {
                    AbortProcessing = false;
                    break;
                }
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
                num++;
                await PrintToAppOutputBG($"Processing video {num} of {total}:" + Environment.NewLine +
                    file.originalFullName + Environment.NewLine +
                    $"Size: {file.length.ToString("N0")} bytes.", 0, 2);
                bool ret = await processFile(file);
                if (ret)
                {
                    file._Remembered = true;
                    Dispatcher.Invoke(() =>
                    {
                        ListViewUpdater();
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
            ErroredList = new List<string>();
            SuccessList = new Dictionary<string, string>();
            NoAudioList = new List<string>();
            SkippedList = new List<string>();
            UnusualList = new List<string>();
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
            foreach (var file in list.Distinct())
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
            return FindAudioRet;
        }
        private async Task<bool> processFile(NewFileInfo file)
        {
            try
            {
                bool FindAudioRet = await GetMovInfo(file);
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
                file.destPath = makePath;
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
        string splitChars = "[]() .-_:&!¡?*`$#@;,{}+=\"";

        private async Task<bool> getTMDB(NewFileInfo nfi)
        {
            bool FoundMovie = false;
            if (TMDBAPIKEY == null || TMDBAPIKEY == "" || TMDBAPIKEY.Length < 7)
            {
                NoTMBDB.Add(nfi.originalFullName, "Reason: No TMDB API Key.");
                return false;
            }
            if (nfi.type != MovieType)
            {
                return false;
            }
            if (await GetMovInfo(nfi) == false)
            {
                return false;
            }
            string file = nfi.destName.ToLower();
            bool foundYear = false;
            file = file.Substring(0, file.Length - 4);

            int fileYear = 0;
            int regexIdx = 0;
            foreach (var r in regexChecks)
            {
                bool endW;
                do
                {
                    endW = true;
                    Match m = r.Match(file, regexIdx);
                    if (m.Success)
                    {
                        bool result = Int32.TryParse(m.Groups[0].Value.Substring(1, 4), out fileYear);
                        if (result == true && fileYear > 1900 && fileYear <= curYear)
                        {
                            foundYear = true;
                            file = file.Substring(0, m.Index);
                            break;
                        }
                        else
                        {
                            regexIdx = m.Index + m.Value.Length;
                            endW = false;
                        }
                    }
                } while (!endW);
                if (foundYear)
                {
                    break;
                }
            }
            foreach (var t in vidtags)
            {
                if (file.Contains(t))
                {
                    file = file.Substring(0, file.IndexOf(t));
                }
            }
            List<string> parts = file.Split(splitChars.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count > 30)
            {
                NoTMBDB.Add(nfi.originalFullName, "Reason: Too many file parts.");
                await createBasicNfo(nfi, file);
                return false;
            }
            SearchContainer<SearchMovie> results;

            TMDbClient client = new TMDbClient(TMDBAPIKEY);
            int skip = 0;
            do
            {
                bool numB = false;
                bool romB = false;

                List<string> searchStr = new List<string>() { "", "", "" };

                int maxsize = 0;
                for (int i = 0; i < (parts.Count - skip); i++)
                {
                    if (i > 0)
                    {
                        searchStr[0] += " ";
                        searchStr[1] += " ";
                        searchStr[2] += " ";
                    }
                    string p = parts[i];
                    if (p.Length > maxsize)
                    {
                        maxsize = p.Length;
                    }
                    searchStr[0] += p;
                    if (wordnumbers.ContainsKey(p))
                    {
                        p = wordnumbers[p];
                        numB = true;
                    }
                    searchStr[1] += p;
                    p = parts[i];
                    if (roman.ContainsKey(p))
                    {
                        if (i > 0)
                        {
                            p = roman[p];
                            romB = true;
                        }
                    }
                    searchStr[2] += p;
                }
                if ((parts.Count - skip) < 3 && maxsize < 2)
                {
                    await PrintToAppOutputBG("Name parts too small.", 0, 1, "red");
                    NoTMBDB.Add(nfi.originalFullName, "Created Basic .nfo. Reason: Name parts too small.");
                    await createBasicNfo(nfi, file);
                    return false;
                }
                if (!romB)
                {
                    searchStr.RemoveAt(2);
                }
                if (!numB)
                {
                    searchStr.RemoveAt(1);
                }
                if (!ignoreWords.Contains(searchStr[0]))
                {
                    foreach (string s in searchStr)
                    {
                        results = client.SearchMovieAsync(s).Result;
                        await Task.Delay(300);                                  // tmdb limits to 40 requests per 10 seconds.   
                        if (results.Results.Count > 0)
                        {
                            FoundMovie = await IDMovie(results, s, foundYear, nfi, client, fileYear);
                            if (FoundMovie)
                            {
                                break;
                            }
                        }
                    }
                }
                skip++;
            } while ((parts.Count - skip) > 0 && !FoundMovie);
            if (!FoundMovie)
            {
                await createBasicNfo(nfi, file);
                NoTMBDB.Add(nfi.originalFullName, "Created basic .nfo. Reason: No results at TMDB.org");
            }
            return FoundMovie;
        }

        private async Task createBasicNfo(NewFileInfo nfi, string file)
        {
            string movieURL = $"<movie><title>{file}</title></movie>";
            await createMovNfo(nfi, movieURL);
            await PrintToAppOutputBG("Created Basic .nfo.", 0, 1, "yellow");
        }

        public class MovEquWeight
        {
            public int MovieID;
            public int YearDiff;
            public int NameDiff;
        }

        private async Task<bool> IDMovie(SearchContainer<SearchMovie> results, string file, bool foundYear, NewFileInfo nfi, TMDbClient client, int yearFromFileName)
        {
            List<int> prevIDs = new List<int>();
            List<MovEquWeight> closestMovs = new List<MovEquWeight>();
            for (int i = 0; i < results.Results.Count; i++)
            {
                if (prevIDs.Contains(results.Results[i].Id))
                {
                    continue;
                }
                prevIDs.Add(results.Results[i].Id);
                MovEquWeight mov = new MovEquWeight();
                mov.MovieID = results.Results[i].Id;
                int temp = 6;
                if (foundYear)
                {
                    if (results.Results[i].ReleaseDate != null)
                    {
                        temp = results.Results[i].ReleaseDate.Value.Year - yearFromFileName;
                        if (temp < 0)
                        {
                            temp = -temp;
                        }
                        if (temp > 5)
                        {
                            continue;
                        }
                    }
                }
                if (results.Results[i].OriginalLanguage != audioLanguage)
                {
                    if (audioLanguage == "en")
                    {
                        temp += 1;
                    }
                    else
                    {
                        temp += 3;
                    }
                }
                mov.YearDiff = temp;
                int ne = ProcessTitle(file, results.Results[i].OriginalTitle, results.Results[i].Title);
                if (ne == -1)
                {
                    continue;
                }
                mov.NameDiff = ne;
                closestMovs.Add(mov);
            }
            string movieURL = "";
            if (closestMovs.Count == 0)
            {
                return false;
            }
            int movieID = 0;
            int c = closestMovs.Min(x => x.YearDiff);
            closestMovs.RemoveAll(x => x.YearDiff > c);
            c = closestMovs.Min(x => x.NameDiff);
            closestMovs.RemoveAll(x => x.NameDiff > c);
            movieID = closestMovs[0].MovieID;
            Movie movie = await client.GetMovieAsync(movieID, MovieMethods.Credits);
            await Task.Delay(300);

            movieURL = @"https://www.themoviedb.org/movie/" + movieID;
            bool ret = await createMovNfo(nfi, movieURL);

            foreach (Cast cast in movie.Credits.Cast)
            {
                List<string> castParts = cast.Character.ToString().ToLower().Split(" ".ToCharArray()).ToList();
                if (castParts.Where(p => nonChar.Any(x => p == x)).Count() > 0)
                {
                    BadChar.Add(nfi.originalFullName);
                }
            }
            return ret;
        }
        private int ProcessTitle(string file, string OriginalTitle, string Title)
        {
            int ne = 0;
            int ne2 = 0;
            ne = ProcessTitleInner(file, OriginalTitle);
            ne2 = ProcessTitleInner(file, Title);
            if (ne == -1 && ne2 == -1)
            {
                return -1;
            }
            else
            {
                if (ne != ne2)
                {
                    if ((uint)ne > (uint)ne2)
                    {
                        ne = ne2;
                    }
                    ne++;
                }
            }
            return ne;
        }

        private int ProcessTitleInner(string file, string Title)
        {
            int ne = 0;
            int ne2 = 0;
            int ad = 0;
            int ad2 = 0;
            bool loop = false;
            bool loop2 = false;
            string str2 = string.Copy(file);
            do
            {
                string str = string.Copy(Title);
                ad2 = 0;
                do
                {
                    ne = CompareTitle(str, str2);
                    loop2 = false;
                    if (ne == -1)
                    {
                        loop2 = ShortenName(ref str);
                        ad2++;
                    }
                } while (loop2);
                loop = false;
                if (ne == -1)
                {
                    loop = ShortenName(ref str2);
                    ad++;
                }
            } while (loop);
            if (ne != -1)
            {
                ne += ad + ad2;
            }
            return ne;
        }

        private bool ShortenName(ref string name)
        {
            Regex re = new Regex(@"\d+|:|-");
            Match m = re.Match(name);
            if (m.Success)
            {
                if (name.Length <= (m.Index + m.Value.Length))
                {
                    return false;
                }
                name = name.Substring(m.Index + m.Value.Length);
            }
            return m.Success;
        }

        Dictionary<string, string> roman = new Dictionary<string, string>()
        {
            { "i", "1" },
            { "ii", "2" },
            { "iii", "3" },
            { "iv", "4" },
            { "v", "5" },
            { "vi", "6" },
            { "vii", "7" },
            { "viii", "8" },
            { "ix", "9" },
            { "x", "10" },
            { "xi", "11" },
            { "xii", "12" }
        };
        Dictionary<string, string> wordnumbers = new Dictionary<string, string>()
        {
            {"one","1" },
            {"two","2" },
            {"three","3" },
            {"four","4" },
            {"five","5" },
            {"six","6" },
            {"seven","7" },
            {"eight","8" },
            {"nine","9" },
            {"ten","10" },
            {"eleven","11" },
            {"twelve","12" },
        };


        private int CompareTitle(string searchStr, string Title)
        {
            List<string> newParts = Title.ToLower().Split(splitChars.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
            List<string> parts = searchStr.ToLower().Split(splitChars.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();

            if (newParts.Count == 0 || parts.Count == 0)
            {
                return -1;
            }
            int ni = 0;
            int pi = 0;
            int outp;
            int outn;
            bool equ;
            int ne = 0;
            do
            {
                equ = true;

                string p = parts[pi].Replace("'", "");
                string n = newParts[ni].Replace("'", "");
                if (roman.ContainsKey(p))
                {
                    p = roman[p];
                }
                if (roman.ContainsKey(n))
                {
                    n = roman[n];
                }
                if (wordnumbers.ContainsKey(p))
                {
                    p = wordnumbers[p];
                }
                if (wordnumbers.ContainsKey(n))
                {
                    n = wordnumbers[n];
                }
                if (p != n)
                {
                    ne++;
                    bool pb = Int32.TryParse(p, out outp);
                    bool nb = Int32.TryParse(n, out outn);
                    if (pb && nb)
                    {
                        equ = false;
                        break;
                    }
                    if (ignoreWords.Contains(p))
                    {
                        pi++;
                        continue;
                    }
                    if (ignoreWords.Contains(n))
                    {
                        ni++;
                        continue;
                    }                    
                    if (pb && pi == (parts.Count - 1))
                    {
                        equ = false;
                        break;
                    }
                    if (nb && ni == (newParts.Count - 1))
                    {
                        equ = false;
                        break;
                    }
                    if (pb)
                    {
                        pi++;
                        continue;
                    }
                    if (nb)
                    {
                        ni++;
                        continue;
                    }
                    equ = false;
                    break;
                }
                pi++;
                ni++;
            } while (pi < parts.Count && ni < newParts.Count);
            if (equ == true)
            {
                return ne + (parts.Count - pi) + (newParts.Count - ni);
            }
            return -1;
        }
        private async Task<bool> createMovNfo(NewFileInfo nfi, string nfoStr)
        {
            string nfo = System.IO.Path.Combine(nfi.destPath, nfi.destName.Substring(0, nfi.destName.Length - 4) + ".nfo");
            try
            {
                if (File.Exists(nfo))
                {
                    File.SetAttributes(nfo, FileAttributes.Normal);
                    File.Delete(nfo);
                }
                var file = File.Create(nfo);
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
                    await PrintToAppOutputBG("Movie .nfo file created: " + nfo, 0, 1);
                    File.SetAttributes(nfo, FileAttributes.Hidden);
                    return true;
                }
                else
                {
                    await PrintToAppOutputBG("Movie .nfo file could not be created.", 0, 1, "red");
                    ErroredList.Add(nfo);
                    return false;
                }
            }
            catch (Exception e)
            {
                await PrintToAppOutputBG("Exception thrown in createMovNfo(): " + e.InnerException.Message, 0, 1, "Red");
                ErroredList.Add(nfo);
                return false;
            }
        }
    }
}