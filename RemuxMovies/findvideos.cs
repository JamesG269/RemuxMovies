using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace RemuxMovies
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<NewFileInfo> VideoList;

        bool GetFiles_Cancel = false;
        
        public List<NewFileInfo> GetFiles(string path, string searchPattern)
        {            
            List<NewFileInfo> retFiles = new List<NewFileInfo>();
            string[] patterns = searchPattern.Split(';');
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
                try
                {
                    foreach (string filter in patterns)
                    {
                        if (GetFiles_Cancel)
                        {
                            retFiles.Clear();
                            return retFiles;
                        }
                        DirectoryInfo dirInfo = new DirectoryInfo(currentDir);
                        FileInfo[] fs = dirInfo.GetFiles(filter);
                        
                        foreach (var f in fs)
                        {
                            var NewFInfo = new NewFileInfo();
                            NewFInfo.originalFullName = f.FullName;
                            NewFInfo.originalDirectoryName = f.DirectoryName;
                            NewFInfo.originalName = f.Name;
                            NewFInfo.DirectoryName = f.DirectoryName.ToLower();
                            NewFInfo.FullName = f.FullName.ToLower();
                            NewFInfo.Name = f.Name.ToLower();
                            NewFInfo.length = f.Length;
                            retFiles.Add(NewFInfo);
                        }                        
                    }
                }
                catch { }
            } while (dirs.Count > 0);
            return retFiles;
        }    
    }
}