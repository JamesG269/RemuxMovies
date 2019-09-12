using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Json;
using System.Linq;
using System.Net;
using System.Reflection;
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
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.WindowsAPICodePack.Dialogs;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Enums;

namespace RemuxMovies
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public void UpdateRememberedList()
        {
            List<OldMovie> oldMovieList = new List<OldMovie>();
            copyOldMoviesList(oldMovieList);            
            var longest = oldMovieList.Aggregate((max, cur) => max.MovieName.Length > cur.MovieName.Length ? max : cur).MovieName.Length;
            foreach (var o in oldMovieList)
            {                
                string name = o.MovieName.ToLower();
                if (name.Length < longest)
                {
                    name += new string(' ', longest - name.Length);
                }
                o.MovieName = name;
            }
            RememberedListBox.BeginInit();
            RememberedListBox.ItemsSource = oldMovieList;
            RememberedListBox.EndInit();
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(RememberedListBox.ItemsSource);
            view.Filter = RememberedListFilter;
            UpdateColumnWidths(RememberedGrid);            
            RememberedListBox.Items.Refresh();
        }
        
        private void copyOldMoviesList(List<OldMovie> oldMovieList)
        {
            int num = 0;            
            foreach (var hl in OldHardLinks)
            {                
                if (oldMovieList.Where(x => x.MovieName == hl.MovieName).Count() > 0)
                {
                    continue;
                }
                OldMovie oldMovieHardLink = new OldMovie();
                oldMovieHardLink.FileName = System.IO.Path.GetFileName(hl.SourceFullPath);
                oldMovieHardLink.Num = num;
                oldMovieHardLink.MovieName = hl.MovieName;
                num++;
                oldMovieList.Add(oldMovieHardLink);
            }

            foreach (var oldMov in OldMovies)
            {
                if (oldMovieList.Where(x => x.MovieName == oldMov.MovieName).Count() > 0)
                {
                    continue;
                }
                oldMov.Num = num;
                num++;
                oldMovieList.Add(oldMov);
            }
        }
        private bool RememberedListFilter(object item)
        {
            if (!RememberedSearchCleared || String.IsNullOrWhiteSpace(RememberedSearch.Text))
                return true;
            else
                return ((item as OldMovie).MovieName.IndexOf(RememberedSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        private void RememberedSearch_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!RememberedSearchCleared)
            {
                RememberedSearchCleared = true;
                RememberedSearch.Text = "";
            }
        }

        private void RememberedSearch_KeyUp(object sender, KeyEventArgs e)
        {
            CollectionViewSource.GetDefaultView(RememberedListBox.ItemsSource).Refresh();
        }

        private SortAdorner listViewSortAdorner = null;
        private GridViewColumnHeader listViewSortCol = null;
        private void lvUsersColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = (sender as GridViewColumnHeader);
            string sortBy = column.Tag.ToString();
            if (listViewSortCol != null)
            {
                AdornerLayer.GetAdornerLayer(listViewSortCol).Remove(listViewSortAdorner);
                RememberedListBox.Items.SortDescriptions.Clear();
            }

            ListSortDirection newDir = ListSortDirection.Ascending;
            if (listViewSortCol == column && listViewSortAdorner.Direction == newDir)
                newDir = ListSortDirection.Descending;

            listViewSortCol = column;
            listViewSortAdorner = new SortAdorner(listViewSortCol, newDir);
            AdornerLayer.GetAdornerLayer(listViewSortCol).Add(listViewSortAdorner);
            RememberedListBox.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
        }
        public class SortAdorner : Adorner
        {
            private static Geometry ascGeometry =
                Geometry.Parse("M 0 4 L 3.5 0 L 7 4 Z");

            private static Geometry descGeometry =
                Geometry.Parse("M 0 0 L 3.5 4 L 7 0 Z");

            public ListSortDirection Direction { get; private set; }

            public SortAdorner(UIElement element, ListSortDirection dir)
                : base(element)
            {
                this.Direction = dir;
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                base.OnRender(drawingContext);

                if (AdornedElement.RenderSize.Width < 20)
                    return;

                TranslateTransform transform = new TranslateTransform
                    (
                        AdornedElement.RenderSize.Width - 15,
                        (AdornedElement.RenderSize.Height - 5) / 2
                    );
                drawingContext.PushTransform(transform);

                Geometry geometry = ascGeometry;
                if (this.Direction == ListSortDirection.Descending)
                    geometry = descGeometry;
                drawingContext.DrawGeometry(Brushes.Gray, null, geometry);

                drawingContext.Pop();
            }
        }
    }
}