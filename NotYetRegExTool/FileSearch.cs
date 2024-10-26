using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;

namespace NotYetRegExTool
{
    public sealed class FileSearch
    {
        private string searchPath;
        private string searchPattern;
        private string searchIgnorePattern;
        public FileSearch(string path, string extensions, string ignorePattern)
        {
            searchPath = path;
            searchPattern = extensions;
            searchIgnorePattern = ignorePattern;
        }

        public void DoSearch(string matchPattern, uint regExOptions, DelegateUpdateHelper<MainWindow, SearchItem> addItemDelegate, BackgroundWorker worker, DoWorkEventArgs eventArgs)
        {
            IEnumerable<string> files = null;
            try
            {
                Regex fileInclude = new Regex(searchPattern, RegexOptions.IgnoreCase);
                Regex fileExclude = null;
                if (!string.IsNullOrEmpty(searchIgnorePattern))
                {
                    fileExclude = new Regex(searchIgnorePattern, RegexOptions.IgnoreCase);
                }

                files = from file in Directory.EnumerateFiles(searchPath, "*", SearchOption.AllDirectories)
                            where fileInclude.IsMatch(file) && !((fileExclude != null) && fileExclude.IsMatch(file))
                            select file;
            }
            catch (Exception e)
            {
                addItemDelegate.UpdateAsync(new SearchItem
                {
                    FileName = e.Message,
                    LineNumber = 0,
                    LineText = "Most likely a bad regular expression in file include or exclude."
                });
            }

            if (files != null)
            {
                Regex expression;
                try
                {
                    expression = new Regex(matchPattern, (RegexOptions)regExOptions);
                }
                catch (Exception e)
                {
                    addItemDelegate.UpdateAsync(new SearchItem
                    {
                        FileName = e.Message,
                        LineNumber = 0,
                        LineText = "Most likely a bad regular expression : " + matchPattern
                    });

                    return;
                }

                int complete = 0;
                Object lockList = new Object();
                Parallel.ForEach(files, (file, loopState) =>
                {
                    if (worker.CancellationPending)
                    {
                        eventArgs.Cancel = true;
                        loopState.Stop();
                    }

                    try
                    {
                        using (System.IO.StreamReader sr = new System.IO.StreamReader(file, Encoding.Default, false, 500000))
                        {
                            string line;
                            int lineNumber = 0;
                            while ((line = sr.ReadLine()) != null)
                            {
                                lineNumber++;
                                if (expression.IsMatch(line))
                                {
                                    lock (lockList)
                                    {
                                        addItemDelegate.UpdateAsync(new SearchItem
                                            {
                                                FileName = Regex.Replace(file, @"^\s+", string.Empty),
                                                LineNumber = lineNumber,
                                                LineText = Regex.Replace(line, @"^\s+", string.Empty)
                                            });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        addItemDelegate.UpdateAsync(new SearchItem
                        {
                            FileName = e.Message,
                            LineNumber = 0,
                            LineText = "File read error."
                        });
                    }

                    if (++complete % 100 == 0)
                    {
                        worker.ReportProgress(++complete);
                    }
                });
            }
        }
    }
}
