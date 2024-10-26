
// TODO: Fix the carriage return problem!
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Threading;
using System.Windows.Input;

namespace NotYetRegExTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool searchBeingUpdated = false;
        private uint regexOptions = 0;
        private DelegateUpdateHelper<MainWindow, SearchItem> m_delegateUpdateFoundItems = null;
        private static ObservableCollection<SearchItem> foundItems;
        private static int foundItemsCurrentIndex = 0;
        private List<FrameworkContentElement> matchedContentElements;
        private int lastElementBroughtIntoView = 0;
        private Thread updateSearchTextThread = null;
        private object threadCreationLock = new object();
        private readonly SettingsManager settingsManager = new SettingsManager();
        private BackgroundWorker backgroundSearchWorker = null;
        private AutoResetEvent backgroundFinished = null;

        public MainWindow()
        {
            InitializeComponent();
            if (foundItems != null)
            {
                FoundMatchesListView.ItemsSource = foundItems;
                FoundMatchesListView.SelectedIndex = foundItemsCurrentIndex;
                FoundMatchesListView.ScrollIntoView(FoundMatchesListView.SelectedItem);
            }

            m_delegateUpdateFoundItems = new DelegateUpdateHelper<MainWindow, SearchItem>(this, Dispatcher, AddFoundItem);

            IReadOnlyList<string> paths = settingsManager.GetSetting("Paths", "Path");
            IReadOnlyList<string> excludes = settingsManager.GetSetting("Excludes", "Exclude");
            IReadOnlyList<string> includes = settingsManager.GetSetting("Includes", "Include");

            foreach (string path in paths)
            {
                AddComboBoxItem(path, FileSearchPathComboBox);
            }

            FileSearchPathComboBox.SelectedIndex = 0;

            foreach (string exclude in excludes)
            {
                AddComboBoxItem(exclude, FileIgnorePattern);
            }

            FileIgnorePattern.SelectedIndex = 0;

            foreach (string include in includes)
            {
                AddComboBoxItem(include, FileMatchPattern);
            }

            FileMatchPattern.SelectedIndex = 0;

            IReadOnlyList<string> patterns = settingsManager.GetSetting("Patterns", "Pattern");
            if (patterns.Count > 0)
            {
                Style style = new Style(typeof(Paragraph));
                style.Setters.Add(new Setter(Block.MarginProperty, new Thickness(0)));
                Pattern.Document.Resources.Add(typeof(Paragraph), style);

                Paragraph p = new Paragraph(new Run(patterns[0]));
                Pattern.Document.Blocks.Add(p);

                foreach (string pattern in patterns)
                {
                    AddComboBoxItem(pattern, PatternList);
                }
            }
        }

        public void AddFoundItem(SearchItem foundItem)
        {
            foundItems.Add(foundItem);
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (ApplyButton.Content.ToString() == "Cancel")
            {
                CancelBackgroundWorker();
                return;
            }

            if (SearchTabControl.SelectedIndex == 0)
            {
                UpdateSearchString();
            }
            else
            {
                if (foundItems == null)
                {
                    foundItems = new ObservableCollection<SearchItem>();
                    FoundMatchesListView.ItemsSource = foundItems;
                }
                else
                {
                    foundItems.Clear();
                }

                string searchPath = FileSearchPathComboBox.Text;
                string searchFileExts = FileMatchPattern.Text;
                string searchIgnoreFileExts = FileIgnorePattern.Text;

                backgroundSearchWorker = new BackgroundWorker();
                backgroundSearchWorker.DoWork += DoBackgroundSearch;
                backgroundSearchWorker.RunWorkerCompleted += RunWorkerCompleted;
                backgroundSearchWorker.ProgressChanged += SearchProgressChanged;
                backgroundSearchWorker.WorkerSupportsCancellation = true;
                backgroundSearchWorker.WorkerReportsProgress = true;
                ApplyButton.Content = "Cancel";
                backgroundSearchWorker.RunWorkerAsync(new SearchParameters
                    {
                        SearchPath = searchPath,
                        SearchFileExts = searchFileExts,
                        SearchIgnoreFileExts = searchIgnoreFileExts,
                        Worker  = backgroundSearchWorker
                    });
            }
        }

        private void SearchProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressText.Text = "Files Searched : " + e.ProgressPercentage.ToString();
        }

        private void CancelBackgroundWorker()
        {
            if (backgroundSearchWorker != null && backgroundSearchWorker.IsBusy)
            {
                backgroundFinished = new AutoResetEvent(false);
                if (backgroundSearchWorker.IsBusy)
                {
                    backgroundSearchWorker.CancelAsync();
                    backgroundFinished.WaitOne();
                    backgroundFinished = null;
                }
            }
            else
            {
                ApplyButton.Content = "Apply";
            }
        }

        private void DoBackgroundSearch(object sender, DoWorkEventArgs e)
        {
            SearchParameters sp = e.Argument as SearchParameters;
            FileSearch fs = new FileSearch(sp.SearchPath, sp.SearchFileExts, sp.SearchIgnoreFileExts);
            TextRange patternRange = new TextRange(Pattern.Document.ContentStart, Pattern.Document.ContentEnd);
            string pattern = patternRange.Text.Replace("\r\n", string.Empty);
            fs.DoSearch(pattern, regexOptions, m_delegateUpdateFoundItems, sp.Worker, e);

            if (backgroundFinished != null)
            {
                backgroundFinished.Set();
            }
        }

        private void RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (backgroundFinished != null)
            {
                backgroundFinished.Set();
            }

            ApplyButton.Content = "Apply";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            TextRange patternRange = new TextRange(Pattern.Document.ContentStart, Pattern.Document.ContentEnd);
            string pattern = patternRange.Text.Replace("\r\n", string.Empty);
            settingsManager.SaveSetting("Patterns", "Pattern", pattern);
            AddComboBoxItem(pattern, PatternList);

            if (SearchTabControl.SelectedIndex == 1)
            {
                // save the path, includes and excludes.
                string searchPath = FileSearchPathComboBox.Text;
                string searchFileExts = FileMatchPattern.Text;
                string searchIgnoreFileExts = FileIgnorePattern.Text;

                settingsManager.SaveSetting("Paths", "Path", searchPath);
                settingsManager.SaveSetting("Includes", "Include", searchFileExts);
                settingsManager.SaveSetting("Excludes", "Exclude", searchIgnoreFileExts);

                AddComboBoxItem(searchPath, FileSearchPathComboBox);
                AddComboBoxItem(searchFileExts, FileMatchPattern);
                AddComboBoxItem(searchIgnoreFileExts, FileIgnorePattern);
            }
        }

        private void AddComboBoxItem(string itemContent, ComboBox cb)
        {
            foreach (ComboBoxItem item in cb.Items)
            {
                if (item.Content.ToString() == itemContent)
                {
                    return;
                }
            }

            ComboBoxItem cbi = new ComboBoxItem();
            cbi.Content = itemContent;
            cb.Items.Add(cbi);
        }

        private void UpdateSearchString()
        {
            RichTextBox searchTextBox = SearchText;
            switch (SearchTabControl.SelectedIndex)
            {
                case 0:
                    break;
                case 1:
                    return;
                   // break;
                case 2:
                    searchTextBox = OriginalText;
                    break;
            }

            searchBeingUpdated = true;
            matchedContentElements = new List<FrameworkContentElement>();
            
            TextRange patternRange = new TextRange(Pattern.Document.ContentStart, Pattern.Document.ContentEnd);
            string pattern = patternRange.Text.Replace("\r\n", string.Empty);

            TextRange searchTextRange = new TextRange(searchTextBox.Document.ContentStart, searchTextBox.Document.ContentEnd);
            string searchText = searchTextRange.Text.Replace("\r", string.Empty);

            FlowDocument fd = GenerateFormatedFlowDocument(pattern, searchText);
            if (fd != null)
            {
                searchTextBox.Document = fd;
            }

            searchBeingUpdated = false;
            ApplyButton.IsEnabled = true;

            if (matchedContentElements.Count > 0)
            {
                matchedContentElements[0].BringIntoView();
            }

            if (SearchTabControl.SelectedIndex == 2)
            {
                TextRange replacementRange = new TextRange(ReplacementText.Document.ContentStart, ReplacementText.Document.ContentEnd);
                string replacmentText = replacementRange.Text.Replace("\r\n", string.Empty);

                string replaced = string.Empty;
                try
                {
                    replaced = Regex.Replace(searchText, pattern, replacmentText);
                }
                catch
                {
                    return;
                }

                Run bl = new Run(replaced);
                Paragraph p = new Paragraph(bl);
                FlowDocument replacedFlowDoc = new FlowDocument(p);
                AlteredText.Document = replacedFlowDoc;
            }
        }

        private FlowDocument GenerateFormatedFlowDocument(string pattern, string searchText)
        {
            ObservableCollection<Match> matches = new ObservableCollection<Match>();
            FlowDocument flowDoc = new FlowDocument();
            MatchCollection matchCollection = null;
            Dictionary<TextPointer, TextPointer> tps = new Dictionary<TextPointer, TextPointer>();
            try
            {
                matchCollection = Regex.Matches(searchText, pattern, (RegexOptions)regexOptions);
            }
            catch
            {
                return null;
            }

            if (matchCollection != null)
            {
                flowDoc.Blocks.Clear();
                Paragraph p = new Paragraph();
                p.FontSize = 14;
                if (matchCollection.Count == 0)
                {
                    p.Inlines.Add(new Run(searchText));
                    flowDoc.Blocks.Add(p);
                }
                else
                {
                    int currentIndex = 0;
                    foreach (Match match in matchCollection)
                    {
                        matches.Add(match);
                        string nextAddPlain = searchText.Substring(currentIndex, match.Index - currentIndex);
                        if (!string.IsNullOrEmpty(nextAddPlain))
                        {
                            // add it as plain text.
                            p.Inlines.Add(new Run(nextAddPlain));
                            currentIndex = match.Index;
                        }

                        if (match.Index < searchText.Length - 1 && !string.IsNullOrEmpty(pattern))
                        {
                            string highlightedText = searchText.Substring(currentIndex, Math.Max(match.Length, 1));
                            Run r = new Run(highlightedText);
                            if (match.Length == 0)
                            {
                                TextDecoration td = new TextDecoration(TextDecorationLocation.Underline, new Pen
                                {
                                    Brush = new SolidColorBrush(Colors.Red),
                                    Thickness = 2,
                                    DashStyle = DashStyles.Solid
                                }, 1, TextDecorationUnit.Pixel, TextDecorationUnit.Pixel);

                                r.TextDecorations.Add(td);
                            }
                            else
                            {
                                r.Background = new SolidColorBrush(Colors.LightGreen);
                                r.FontWeight = FontWeights.Bold;
                            }

                            p.Inlines.Add(r);
                            matchedContentElements.Add(r);
                            currentIndex += Math.Max(match.Length, 1);
                        }
                    }

                    // add the remainder of the string.
                    if (currentIndex < searchText.Length)
                    {
                        string lastPlainText = searchText.Substring(currentIndex);
                        p.Inlines.Add(new Run(lastPlainText));
                    }

                    flowDoc.Blocks.Add(p);
                }

                if (matchedContentElements.Count > 0)
                {
                    matchedContentElements[0].BringIntoView();
                    lastElementBroughtIntoView = 0;
                    BackButton.IsEnabled = false;
                    ForwardButton.IsEnabled = matchedContentElements.Count > 1;
                }
            }

            Matches_ListView.ItemsSource = matches;
            return flowDoc;
        }

        private void SearchText_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private void Pattern_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!searchBeingUpdated)
            {
                UpdateSearchString();
            }
        }

        private void RegexOptionChanged(object sender, RoutedEventArgs e)
        {
            uint option = Convert.ToUInt32(((CheckBox)e.OriginalSource).Tag);
            if (e.RoutedEvent == CheckBox.CheckedEvent)
            {
                regexOptions |= option;
            }
            else
            {
                regexOptions ^= option;
            }

            UpdateSearchString();
        }

        private void FileItemClicked(object sender, object e)
        {
            SearchItem item = FoundMatchesListView.SelectedItem as SearchItem;
        }

        private void FoundMatchesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void SearchTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchTabControl.SelectedIndex == 1)
            {
                Title = "MainWindow";
                if (foundItems != null)
                {
                    FoundMatchesListView.ItemsSource = foundItems;
                    FoundMatchesListView.SelectedIndex = foundItemsCurrentIndex;
                    FoundMatchesListView.ScrollIntoView(FoundMatchesListView.SelectedItem);
                }
            }
        }

        private void BringIntoView_Click(object sender, object e)
        {
            if (matchedContentElements == null || matchedContentElements.Count == 0)
            {
                return;
            }

            FrameworkContentElement lastRun = matchedContentElements[lastElementBroughtIntoView];
            Button b = sender as Button;
            if (b == BackButton)
            {
                if (--lastElementBroughtIntoView == 0)
                {
                    BackButton.IsEnabled = false;
                }

                ForwardButton.IsEnabled = true;
            }
            else
            {
                if (++lastElementBroughtIntoView == matchedContentElements.Count - 1)
                {
                    ForwardButton.IsEnabled = false;
                }

                BackButton.IsEnabled = true;
            }

            ((Run)lastRun).Background = new SolidColorBrush(Colors.LightGreen);
            matchedContentElements[lastElementBroughtIntoView].BringIntoView();
            ((Run)matchedContentElements[lastElementBroughtIntoView]).Background = new SolidColorBrush(Colors.Yellow);
        }

        private void SearchText_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (updateSearchTextThread != null)
            {
                lock (threadCreationLock)
                {
                    if (updateSearchTextThread != null)
                    {
                        updateSearchTextThread.Abort();
                        updateSearchTextThread = null;
                    }
                }
            }

            updateSearchTextThread = new Thread(() =>
            {

            });
        }

        private void PatternList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            if (cb.SelectedIndex == 0)
            {
                return;
            }
            Pattern.Document = new FlowDocument();
            Paragraph p = new Paragraph(new Run(cb.Text));
            Pattern.Document.Blocks.Add(p);

            cb.SelectedIndex = 0;
        }

        private void ComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                ComboBox cb = (ComboBox)sender;
                foreach (ComboBoxItem cbi in cb.Items)
                {
                    if (cbi.IsHighlighted)
                    {
                        cb.Items.Remove(cbi);
                        return;
                    }
                }
            }
        }

        private void  GetDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (!string.IsNullOrWhiteSpace(FileSearchPathComboBox.Text))
            {
                dialog.SelectedPath = FileSearchPathComboBox.Text;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                AddComboBoxItem(dialog.SelectedPath, FileSearchPathComboBox);
                FileSearchPathComboBox.Text = dialog.SelectedPath;
            }
        }

        private void FileLinkText_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SearchTabControl.SelectedIndex = 0;
            foundItemsCurrentIndex = FoundMatchesListView.SelectedIndex;
            using (System.IO.StreamReader sr = new System.IO.StreamReader(button.Content.ToString()))
            {
                Paragraph p = new Paragraph();
                p.Inlines.Add(new Run
                    {
                        Text = "File Path: " + button.Content.ToString() + "\r\n\r\n",
                        Background = new SolidColorBrush(Colors.AliceBlue),
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.Blue),
                        FontStyle = FontStyles.Italic
                    });

                FilePath.Document.Blocks.Clear();
                FilePath.Document.Blocks.Add(p);
                
                p = new Paragraph();
                string text = sr.ReadToEnd();
                Run r = new Run(text);
                p.Inlines.Add(r);
                SearchText.Document.Blocks.Clear();
                SearchText.Document.Blocks.Add(p);
                UpdateSearchString();
                Title = button.Content.ToString();
            }
        }

        private void FileLinkText_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Button btn = sender as Button;
            System.Windows.Forms.Clipboard.SetText(btn.Content.ToString());
        }
    }
}
