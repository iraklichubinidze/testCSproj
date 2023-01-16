using Microsoft.Web.WebView2.Core;
using NLog;

namespace NotificationChecker
{
    public partial class BrowserForm : Form
    {
        private readonly Logger logger;

        public BrowserForm(string url)
        {
            Url = url;
            InitializeComponent();

            logger = LogManager.GetCurrentClassLogger();
        }

        public string Url { get; set; }

        private void BrowserFormShown(object sender, EventArgs e)
        {
            try
            {
                webView.Source = new Uri(Url);

                Activate();

                var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                logger.Info("CoreWebView2 version {0}", version);
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Error getting  CoreWebView2 version");
            }
        }

        private void WebViewNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                logger.Error("Error navigating to requested url. {0} {1}", e.HttpStatusCode, e.WebErrorStatus);
            }
        }

        private void CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                logger.Error(e.InitializationException, "Could not initialize CoreWebView2");
            }
        }
    }
}
