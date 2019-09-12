using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System;

namespace RemuxMovies
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        List<NewFileInfo> nfoList;

        bool GetFiles_Cancel = false;

        public List<NewFileInfo> GetFiles(string path, int type, string[] patterns)
        {
            List<NewFileInfo> retFiles = new List<NewFileInfo>();            
            Stack<string> dirs = new Stack<string>();
            if (!Directory.Exists(path))
            {
                return retFiles;
            }
            dirs.Push(path);
            do
            {
                string currentDir = dirs.Pop();
                try
                {
                    string[] subDirs = Directory.GetDirectories(currentDir);
                    foreach (string str in subDirs)
                    {
                        dirs.Push(str);
                    }
                }
                catch { }
                //try
                {
                    foreach (string filter in patterns)
                    {
                        if (GetFiles_Cancel)
                        {
                            retFiles.Clear();
                            return retFiles;
                        }
                        DirectoryInfo dirInfo = new DirectoryInfo(currentDir);
                        FileInfo[] fs = dirInfo.GetFiles("*" + filter);

                        foreach (var f in fs)
                        {
                            if (f.FullName.ToLower().Contains("sample"))
                            {
                                continue;
                            }
                            var NewFInfo = new NewFileInfo();
                            NewFInfo.originalFullPath = f.FullName;
                            NewFInfo.originalDirectory = f.DirectoryName;
                            NewFInfo.originalName = f.Name;
                            NewFInfo.Directory = f.DirectoryName.ToLower();
                            NewFInfo.FullPath = f.FullName.ToLower();
                            NewFInfo.FileName = f.Name.ToLower();                            
                            NewFInfo.length = f.Length;
                            NewFInfo.fromDirectory = path.ToLower();
                            NewFInfo.type = type;
                            retFiles.Add(NewFInfo);
                            if (type == MovieType && forceAll == false && OldMovies.Where(x => x.FullPath.Equals(NewFInfo.FullPath)).Count() > 0)
                            {
                                NewFInfo._Remembered = true;
                            }
                            else if (type == MovieType)
                            {
                                NewFInfo._Remembered = false;
                            }
                            if (type == HardlinkType && forceAll == false && OldHardLinks.Where(x => x.SourceFullPath.Equals(NewFInfo.FullPath)).Count() > 0)
                            {
                                NewFInfo._Remembered = true;
                            }
                            else if (type == HardlinkType)
                            {
                                NewFInfo._Remembered = false;
                            }
                            
                        }                        
                    }
                }
                //catch (Exception e)
                {
                    //MessageBox.Show(e.InnerException.Message);
                }
            } while (dirs.Count > 0);
            return retFiles;
        }
        public class NewFileInfo
        {
            public string _originalFullPath;

            public string originalFullPath
            {
                get
                {
                    return _originalFullPath;
                }
                set
                {
                    _originalFullPath = value;
                }
            }
            public string originalName;            
            public string originalDirectory;
            public long length;
            public string FullPath;
            public string FileName;
            public string Directory;
            public string fromDirectory;
            public int type; // movies = 0; musicvideos = 1; TV Shows = 2            
            private string _destName;
            public string destName
            {
                get
                {
                    return _destName;
                }
                set
                {
                    _destName = value;
                }
            }

            public bool _Remembered;
            public string Remembered
            {
                get
                {
                    return _Remembered.ToString();
                }
            }
            public string destPath;
            public string FriendlyType
            {
                get
                {
                    return typeFriendlyName[type];
                }
            }
        }
        public class NewDirInfo
        {            
            public Boolean Process = true;            
            public string _Directory;
            public string _OutputDir;
            public string Directory
            {
                get
                {
                    return _Directory;
                }
                set
                {
                    _Directory = value;
                }
            }
            public string OutputDir
            {
                get
                {
                    return _OutputDir;
                }
                set
                {
                    _OutputDir = value;
                }
            }
            public int _type;
            public int type
            {
                get
                {
                    return _type;
                }
                set
                {
                    _type = value;
                }
            }
            public string FriendlyType
            {
                get
                {
                    return typeFriendlyName[_type];
                }
            }
        }
    }
}