

using System.ComponentModel;
namespace NotYetRegExTool
{
    class SearchParameters
    {
        public string SearchPath { set; get;  }
        public string SearchFileExts { set; get; }
        public string SearchIgnoreFileExts { set; get; }
        public BackgroundWorker Worker { get; set; }
    }
}
