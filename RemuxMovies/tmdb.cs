using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
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
        readonly Regex movieSplit = new Regex(@"(\b[a-zA-Z]\.|[a-zA-Z0-9']+)", RegexOptions.Compiled);
        readonly Regex akaSplit = new Regex(@"\d+|:|-|\baka\b|\ba\.k\.a\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private async Task<bool> getTMDB(NewFileInfo nfi, string nfoFile)
        {
            bool FoundMovie = false;
            if (TMDBAPIKEY == null || TMDBAPIKEY == "" || TMDBAPIKEY.Length < 7)
            {
                NoTMBDB.Add(nfi.originalFullPath, "Reason: No TMDB API Key.");
                return false;
            }
            if (nfi.type != MovieType && nfi.type != NfoType)
            {
                return false;
            }
            string fileName = nfi.destName.ToLower();
            fileName = fileName.Substring(0, fileName.Length - 4);
            int fileYear = 0;
            int regExIdx = 0;
            bool foundYear = GetFileYear(ref fileName, ref fileYear, ref regExIdx);
            if (foundYear)
            {
                fileName = fileName.Substring(0, regExIdx);
            }
            fileName = removeVidTags(fileName);
            List<string> fileNameParts = new List<string>();
            getFileNameParts(fileName, fileNameParts);

            if (fileNameParts.Count > 30)
            {
                NoTMBDB.Add(nfi.originalFullPath, "Reason: Too many file parts.");
                await createBasicNfo(nfi, fileName, nfoFile);
                return false;
            }
            SearchContainer<SearchMovie> results;

            TMDbClient client = new TMDbClient(TMDBAPIKEY);
            int skip = 0;
            do
            {
                List<string> searchStrs = new List<string>(4) { "", "", "", "" };

                int maxsize = 0;
                for (int i = 0; i < (fileNameParts.Count - skip); i++)
                {
                    string p = fileNameParts[i];

                    if (i > 0)
                    {
                        if (!(fileNameParts[i - 1].EndsWith(".") && p.EndsWith(".")))
                        {
                            searchStrs[0] += " ";
                            searchStrs[1] += " ";
                            searchStrs[2] += " ";
                        }
                        searchStrs[3] += " ";
                    }
                    if (p.Length > maxsize)
                    {
                        maxsize = p.Length;
                    }
                    string np = p.Replace(".", "");
                    searchStrs[0] += roman.ContainsKey(np) && i > 0 ? roman[np] : p;
                    searchStrs[1] += np;
                    searchStrs[2] += wordnumbers.ContainsKey(np) ? wordnumbers[np] : p;
                    searchStrs[3] += p;
                }
                if ((fileNameParts.Count - skip) < 2 && maxsize < 2)
                {
                    await PrintToAppOutputBG("Name parts too small.", 0, 1, "red");
                    NoTMBDB.Add(nfi.originalFullPath, "Created Basic .nfo. Reason: Name parts too small.");
                    await createBasicNfo(nfi, fileName, nfoFile);
                    return false;
                }
                searchStrs = searchStrs.Distinct().ToList();
                foreach (string searchStr in searchStrs)
                {
                    if (!ignoreWords.Contains(searchStr))
                    {
                        await waitForTMDB();
                        results = client.SearchMovieAsync(searchStr, 0, true, fileYear).Result;
                        if (results.Results.Count == 0 && fileYear != 0)
                        {
                            await waitForTMDB();
                            results = client.SearchMovieAsync(searchStr, 0, true, 0).Result;
                        }
                        if (results.Results.Count > 0)
                        {
                            FoundMovie = await IDMovie(results, searchStr, foundYear, nfi, client, fileYear, nfoFile);
                        }
                    }
                    if (FoundMovie)
                    {
                        break;
                    }
                }
                skip++;
            } while ((fileNameParts.Count - skip) > 0 && !FoundMovie);
            if (!FoundMovie)
            {
                await createBasicNfo(nfi, fileName, nfoFile);
                NoTMBDB.Add(nfi.originalFullPath, "Created basic .nfo. Reason: No results at TMDB.org");
            }
            return FoundMovie;
        }
        DateTime TMDBNextAccess;
        private async Task waitForTMDB()
        {
            if (TMDBNextAccess != null && TMDBNextAccess > DateTime.Now)
            {
                await Task.Delay((TMDBNextAccess - DateTime.Now).Milliseconds);
            }
            TMDBNextAccess = DateTime.Now.AddMilliseconds(300);
        }

        private void getFileNameParts(string fileName, List<string> fileNameParts)
        {
            foreach (Match m in movieSplit.Matches(fileName.ToLower()))
            {
                if (m.Value != string.Empty)
                {
                    fileNameParts.Add(m.Value);
                }
            }
        }

        private string removeVidTags(string fileName)
        {
            foreach (var t in vidtags)
            {
                if (fileName.Contains(t))
                {
                    fileName = fileName.Substring(0, fileName.IndexOf(t));
                }
            }
            return fileName;
        }

        private bool GetFileYear(ref string file, ref int fileYear, ref int foundYearIdx)
        {
            bool foundYear = false;
            foundYearIdx = 0;
            int regExIdx;
            Match m = null;
            foreach (var r in regexChecks)
            {
                regExIdx = file.Length;                
                while (regExIdx > 1)
                {
                    m = r.Match(file, regExIdx);
                    regExIdx = m.Index - 1;
                    if (m.Success && foundYearIdx < m.Index)
                    {
                        bool result = Int32.TryParse(m.Groups[0].Value.Substring(1, 4), out fileYear);
                        if (result == true && fileYear > 1900 && fileYear <= (curYear + 1))
                        {
                            foundYear = true;
                            foundYearIdx = m.Index;
                            break;
                        }                        
                    }
                    else
                    {
                        break;
                    }                    
                }
            }
            return foundYear;
        }
        private async Task createBasicNfo(NewFileInfo nfi, string file, string nfoFile)
        {
            string movieURL = $"<movie><title>{nfi.originalName}</title></movie>";
            await createMovNfo(nfi, movieURL, "No TMDB","No TMDB", 0, nfoFile);
            await PrintToAppOutputBG("Created Basic .nfo.", 0, 1, "yellow");
        }

        public class MovWeight
        {
            public int MovieID;
            public int YearDiff;
            public int NameDiff;
            public int LangDiff;
        }

        private async Task<bool> IDMovie(SearchContainer<SearchMovie> results, string searchStr, bool foundYear, NewFileInfo nfi, TMDbClient client, int yearFromFileName, string nfoFile)
        {
            List<int> prevIDs = new List<int>();
            List<MovWeight> closestMovs = new List<MovWeight>();
            for (int i = 0; i < results.Results.Count; i++)
            {
                if (prevIDs.Contains(results.Results[i].Id))
                {
                    continue;
                }
                prevIDs.Add(results.Results[i].Id);
                MovWeight mov = new MovWeight();
                mov.MovieID = results.Results[i].Id;
                int temp = 2;
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
                mov.YearDiff = temp;
                temp = 0;
                if (results.Results[i].OriginalLanguage != audioLanguage)
                {
                    temp++;
                    if (audioLanguage != "en")
                    {
                        temp++;
                    }
                }
                mov.LangDiff = temp;
                int ne = ProcessTitle(searchStr, results.Results[i].OriginalTitle, results.Results[i].Title);
                if (ne == -1)
                {
                    continue;                    
                }
                mov.NameDiff = ne;                
                closestMovs.Add(mov);
            }
            if (closestMovs.Count == 0)
            {
                return false;
            }

            int c = closestMovs.Min(x => x.YearDiff);
            closestMovs.RemoveAll(x => x.YearDiff > c && x.YearDiff > 2);
            c = closestMovs.Min(x => x.NameDiff);
            closestMovs.RemoveAll(x => x.NameDiff > c);            
            c = closestMovs.Min(x => x.LangDiff);
            closestMovs.RemoveAll(x => x.LangDiff > c);            
            int movieID = closestMovs[0].MovieID;
            await waitForTMDB();
            Movie movie = await client.GetMovieAsync(movieID, MovieMethods.Credits);

            string yearStr = "Unknown year";
            if (movie.ReleaseDate != null)
            {
                yearStr = movie.ReleaseDate.Value.Year.ToString();
            }

            string movieURL = "<movie><tagline>" + yearStr + " " + movie.Tagline + "</tagline></movie>" + @"https://www.themoviedb.org/movie/" + movieID;

            int movieYear = movie.ReleaseDate == null ? 0 : movie.ReleaseDate.Value.Year;

            bool ret = await createMovNfo(nfi, movieURL, movie.Title, movie.OriginalTitle, movieYear, nfoFile);

            foreach (Cast cast in movie.Credits.Cast)
            {
                List<string> castParts = cast.Character.ToString().ToLower().Split(" ".ToCharArray()).ToList();
                if (castParts.Where(p => nonChar.Any(x => p == x)).Count() > 0)
                {
                    BadChar.Add(nfi.originalFullPath);
                }
            }
            return ret;
        }
        private int ProcessTitle(string file, string OriginalTitle, string Title)
        {
            int ne = ProcessTitleInner(file, OriginalTitle);
            int ne2 = ProcessTitleInner(file, Title);
            if (ne == ne2)
            {
                return ne;
            }
            if (ne == -1)
            {
                ne = ne2;
            }
            else if (ne2 != -1 && ne > ne2)
            {
                ne = ne2;
            }
            return ne + 1;
        }

        private int ProcessTitleInner(string fileTitle, string Title)
        {
            int ne = CompareTitle(fileTitle, Title);
            if (ne != -1)
            {
                return ne;
            }
            
            Dictionary<string, int> fileParts = new Dictionary<string, int>();
            Dictionary<string, int> titleParts = new Dictionary<string, int>();

            var fp = akaSplit.Split(fileTitle).Where(s => s != string.Empty).Distinct();
            var tp = akaSplit.Split(Title).Where(s => s != string.Empty).Distinct();

            if (fp.Count() == 0 || tp.Count() == 0)
            {
                return -1;
            }
            if (fp.Count() == 1 && tp.Count() == 1)
            {
                return -1;
            }

            fileParts = fp.ToDictionary(s => s, s => fp.Count() - 1);
            titleParts = tp.ToDictionary(s => s, s => tp.Count() - 1);

            if (!fileParts.ContainsKey(fileTitle))
            {
                fileParts.Add(fileTitle, 0);
            }
            if (!titleParts.ContainsKey(Title))
            {
                titleParts.Add(Title, 0);
            }

            List<int> vals = new List<int>();

            foreach (var f in fileParts)
            {
                foreach (var t in titleParts)
                {
                    ne = CompareTitle(f.Key, t.Key);
                    if (ne != -1)
                    {
                        vals.Add(ne + f.Value + t.Value);
                    }
                }
            }
            if (vals.Count() == 0)
            {
                return -1;
            }
            return vals.Min();
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
            {"thirteen","13" }
        };


        private int CompareTitle(string searchStr, string Title)
        {
            List<string> newParts = new List<string>();
            getFileNameParts(Title, newParts);
            List<string> parts = new List<string>();
            getFileNameParts(searchStr, parts);
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
                if (string.Compare(p, n, StringComparison.InvariantCultureIgnoreCase) != 0)
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
                    if (pb)
                    {
                        pi++;
                        if (pi == (parts.Count))
                        {
                            equ = false;
                            break;
                        }
                        continue;
                    }
                    if (nb)
                    {
                        ni++;
                        if (ni == (newParts.Count))
                        {
                            equ = false;
                            break;
                        }
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
        private async Task<bool> createMovNfo(NewFileInfo nfi, string nfoStr, string Title, string OriginalTitle, int year, string nfoFile)
        {
            string err = "";
            
            try
            {
                if (File.Exists(nfoFile))
                {
                    File.SetAttributes(nfoFile, FileAttributes.Normal);
                    File.Delete(nfoFile);
                }
                var file = File.Create(nfoFile);
                byte[] nfoBytes = Encoding.UTF8.GetBytes(nfoStr);
                file.Write(nfoBytes, 0, nfoBytes.Length);
                file.Close();
                file.Dispose();
                if (file != null)
                {
                    file = null;
                }
                if (File.Exists(nfoFile))
                {
                    await PrintToAppOutputBG("Movie .nfo file created: " + nfoFile, 0, 1);
                    await PrintToAppOutputBG("Title: ", 0, 0);
                    await PrintToAppOutputBG(Title, 0, 1, "lightgreen");
                    await PrintToAppOutputBG("Original Title: ", 0, 0);
                    await PrintToAppOutputBG(OriginalTitle, 0, 1, "lightgreen");
                    await PrintToAppOutputBG("year: ", 0, 0);
                    await PrintToAppOutputBG($"{year}", 0, 1 , "lightgreen");

                    File.SetAttributes(nfoFile, FileAttributes.Hidden);
                    return true;
                }
                else
                {
                    err = "Movie .nfo file could not be created.";
                    await PrintToAppOutputBG(err, 0, 1, "red");
                    ErroredListAdd(nfoFile, err);
                    return false;
                }
            }
            catch (Exception e)
            {
                err = "Exception thrown in createMovNfo(): " + e.InnerException.Message;
                await PrintToAppOutputBG(err, 0, 1, "Red");
                ErroredListAdd(nfoFile, err);
                return false;
            }
        }
    }
}