   M S B A M L   `   `   `  ����F  BRemuxMovies, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null)clr-namespace:RemuxMoviesRemuxMovies  5   	   Y UConsoleControl.WPF, Version=1.3.0.0, Culture=neutral, PublicKeyToken=bb812711dbc508e1S<clr-namespace:ConsoleControl.WPF;assembly=ConsoleControl.WPFConsoleControl.WPF 5   	       RemuxMovies.MainWindow   5   	   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   +localclr-namespace:RemuxMovies     5   	   LWPF<clr-namespace:ConsoleControl.WPF;assembly=ConsoleControl.WPF     5   	   -   5          � Title$"  RemuxMovies by James Gentile��5	   	   $	��700��6.   $
��1200��6;   .��5
      � -   5         �Name$ mainGrid��5
       � RowDefinitions 5   
   �� 5      $��* �6   �� 5      $��* �6   �� 5      $��* �6   $��60��6'    � ColumnDefinitions 5   
   �� 5      $��50 �6   $��50��6*   �� 5      $
��auto �6   $��50��6,   �� 5      $
��auto �6   $��50��6,   �� 5      $
��auto �6   $��50��6,   �� 5      $
��auto �6   $��50��6,   �� 5      $
��auto �6   $��50��6,   �� 5      $
��auto �6   $��80��6,   �� 5      $
��auto �6   $	��110��6,   �� 5      $
��auto �6   $	��110��6,   �� 5      $��50 �6   $��50��6*   �� 5      $��50 �6   �� 5      $��50 �6   �� 5      $��* �6   �� 5      $��* �6   .+�5    
   �� -   5        $��
tabControl��5       $��2��6'   $��14��68   .@�5!      �� $��Output��6   .��5"      � $��Stretch=�6   $��Stretch=�63   $
��auto��6Q   $
��auto��6^    5#      �� 5$      $��* �6(   �� 5%      $��* �6(   .+�5'      �� -   5         ��Name$ 	AppScroll��5'   #   $��0��66   .��5(      �� -   5         ��Name$ 	AppOutput��5(   &   $��consolas)�69   $��12(�6O   $��0��6]   $��0��6j   $��0,0,0,0q�6z   
�������
���   � �� IsUndoEnabled .  6�   5)      �� 5*      -   5        $ ConsoleScroll��5*   #   $��1��6:   $��Visible=�6G   $��Visible=�6o   .��5+      �� -   5         ��Name$ ConsoleOutput��5+   "   $��consolas)�69   $��12(�6O   $��1��6]   $��0,0,0,0q�6j   
�������
���   � �� TextWrapping$ NoWrap=�6�   5,      5-      5.      �� 5/      -   5        $��TabDirs��5/      $��Directories��6%   .��50      � -	   5        $��
UpdateGrid��50      $��Stretch=�6)   $��Stretch=�6G   $
��auto��6c   $
��auto��6p    51      �� 52      $��* �6(   �� 53      $��* �6(   �� 54      $��* �6(    56      �� 57      $
��auto �6+   $	��162��68   �� 58      $��* �6+   .+�5:      �� -
   5        $��
stackPanel��5:   !   $��0��63   $��0��6C   {�.  .+�5;      �� -   5        $��  Add Movies Folders��5;   !   $
��Left=�6@   $
��auto��6v   $��20��6�   �� 5<      -   5        $!��  Add Music Videos Folders ��5<   !   $
��Left=�6G   $
��auto��6�   $��20��6�   �� 5=      -   5        $��  Add TV Shows Folders ��5=   !   $
��Left=�6C   $
��auto��6w   $��20��6�   �� 5>      -   5        $��  Nfo Output Folder ��5>   !   $
��Left=�6@   $
��auto��6r   $��20��6   �� 5?      -   5        $��  Add Hardlink Folders ��5?   !   $
��Left=�6C   $
��auto��6   $��20��6�   5@      �� 5A      -   5        $��listView��5A      $��1��6/   $��0��6?   	 � ContextMenu	 5B      �� 5C      -   5        $��ZoneIformationList��5C   *   
 �� 	StaysOpen
 . .@�5D   "   m� -   5        $��ProcessDirItem��5D   F   $��Process Directory��6+   6\   m� 5E   "   -   5        $��RemoveMenuItem��5E   ;   $��Remove��6+   6Q   m� 5F   "   -   5        $��OpenExplorerDirItem��5F   G   $��Open Explorer here��6+   6b   5G       �� View 5I      �� 5J      -   5        gv3��.P�5K   "   �� $��Source Directory��61    �� Width$
 auto��6v    �� DisplayMemberBinding 6K   �� *	Directory+6v   �� 5L   "   $��Target Directory��61   $
 auto��6v    6K   �� *	OutputDir+6v   �� 5M   "   $
��Type��61   $
 auto��6m    6?   �� *FriendlyType+6m   5N      5P      �� 5Q      -   5        $��outputDirListView��5Q      $��1��68   $��1��6H   	 5R      �� 5S      -   5        $��outputListViewContext��5S   *   
 . .@�5T   "   m� -   5        $��OpenExplorerOutputItem��5T   G   $��Open Explorer here��6+   6e   5U       5W      �� 5X      -   5        gv2��.P�5Y   "   �� $��Output Directories��61   $
 auto��5Z   C    6   �� *Name+6C   �� 5[   "   $��Output Directory Type��61   $
 auto��5\   K    6   �� *FriendlyType+6K   5]      5_      �� 5`      -   5        $��fileListView��5`      $��1��63   $��2��6C   $
��auto��6P   	 5a      �� 5b      -   5        $��fileListViewContext��5b   *   
 . .@�5c   "   m� -   5        $��ProcessFileItem��5c   D   $��Process file(s)��6+   6[   m� 5d   "   -   5        $��SkipFileItem��5d   K   $��Toggle Remembered/skip��6+   6_   m� 5e   "   -   5        $��RemoveFileItem��5e   ;   $��Remove��6+   6Q   m� 5f   "   -   5        $��OpenExplorerFileItem��5f   G   $��Open Explorer here��6+   6c   5g       5i      �� 5j      -    5        gv1��.P�5k   "   �� $��Files��5l      $
 auto��5m   O    6   �� *originalFullPath+6O   �� 5n   "   $��Skip?��5o      $
 auto��5p   I    6   �� *
Remembered+6I   �� 5q   "   $��Destination File Names��61   $
 auto��5r   G    6   �� *
destName+5s   #   5t      5v      5w      5x      �� 5y      -!   5        $
��tab3��5y      $��Remembered Files��6"   .��5z      � -"   5        $��RememberedGrid��5z      $��Stretch=�6-   $��Stretch=�6K   $
��auto��6g   $
��auto��6t    5{      �� 5|      $��* �6(   �� 5}      $��20 �6(   .+�5�      �� -#   5        $��RememberedListBox��5�      $��0��68   $
��auto��6E   $��consolas)�6R    5�      �� 5�      -$   5        RememberedGridView��.P�5�   "   �� $
 auto��61    6>   �� *	MovieName+��5�   &   �� 5�   *   -%   5        $
��auto��5�   ?   
 � Tag$
 Name��6L   $
��Name��6y   5�   #   �� 5�   "   $
 auto��61    6>   �� *Size+��5�   &   �� 5�   *   -&   5        $��80��5�   ?   $
��auto��6M   $
 Size��6Z   $��
Size (GBs)��6�   5�   #   �� 5�   "   $
 auto��61    6>   �� *Num+��5�   &   �� 5�   *   -'   5        $��80��5�   ?   $
��auto��6M   $	 Num��6Z   $	��Num��6�   5�   #   5�      5�      �� 5�      -(   5        $��RememberedSearch��5�      $��1��66   $��Enter search term��6C   6�   5�      5�      5�      �� 5�   
   -)   5        $��forceCheckBox��5�      $��Force��6(   $��10��68   $��4��6I   $
��Left=�6V   $��Center=�6q   6�   �� 5�   
   -*   5        $��StartButton��5�      {�.  $��	  Start  ��66   $��Center=�6J   $
��auto��6{   $��20��6�   $��1��6�   $��2��6�   �� 5�   
   -+   5        $��
SkipButton��5�      {�. $��  Skip  ��64   $��Center=�6G   $
��auto��6~   $��20��6�   $��2��6�   $��2��6�   �� 5�   
   -,   5        $��	  Abort  ��5�      $��Center=�6%   $
��auto��6V   $��20��6c   $��3��6o   $��2��6   �� 5�   
   --   5        $��	  Clear  ��5�      $��Center=�6%   $
��auto��6V   $��20��6c   $��4��6o   $��2��6   �� 5�   
   -.   5        $��ReloadButton��5�      {�.  $��
  Reload  ��67   $��Center=�6L   $
��auto��6~   $��20��6�   $��5��6�   $��2��6�   �� 5�   
   -/   5        $��MakeNfosButton��5�      {�.  $��  Make .Nfos  ��69   $��Center=�6R   $
��auto��6�   $��20��6�   $��6��6�   $��2��6�   �� 5�   
   -0   5        $��MakeHardLinksButton��5�      {�. $�� Make Hardlinks ��6=   $��Center=�6X   $
��auto��6�   $��20��6�   $��7��6�   $��2��6�   �� 5�   
   -1   5        $��	infoLabel��5�      $��	No files.��6!   $��12��65   $��2��6F   �� 5�   
   -2   5        $��View Saved Files��5�      $��8��6,   $��2��6<   $��20��6I   6U   �� 5�   
   $��13��6   $��2��6&   $
��auto��63   .+�5�      �� -3   5        $��Check Nfo dupes��5�      6/   5�      5�      5�      