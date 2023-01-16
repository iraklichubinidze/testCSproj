using System.ComponentModel;
using System.Configuration;
using System.Net.Http.Json;
using Windows.UI.Notifications;
using CommunityToolkit.WinUI.Notifications;
using SQLite;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace NotificationChecker
{
    public partial class MainForm : Form
    {
        private const string ToastTag = "Test";
        private readonly PeriodicTimer timer;

        private ToastNotification? currentToast;
        private readonly HttpClient httpClient = new();

        private List<NotificationItem> items = new();

        private bool isBrowserOpen;
        private readonly DirectoryInfo directoryInfo;
        private SQLiteConnection db;
        private Logger logger;
        private AboutBox? aboutBox;
        private bool aboutVisible;
        private readonly string server;
        private readonly TimeSpan minimumInterval = TimeSpan.FromMinutes(2);

        public MainForm()
        {
            InitializeComponent();
            
            ToastNotificationManagerCompat.OnActivated += ToastActivated;

            var timeoutValue = ConfigurationManager.AppSettings["TimeBetweenCalls"];

            var timeSpan = TimeSpan.FromSeconds(int.TryParse(timeoutValue, out var timeout) ? timeout : 10);

            if (timeSpan < minimumInterval)
            {
                timeSpan = minimumInterval;
            }

            timer = new(timeSpan);
            server = ConfigurationManager.AppSettings["ServerAddress"] ?? "";

            var userId = UserIdInfo.GetUserId();

            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Hash", userId);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("DeviceId", Environment.MachineName);

            var folder = Path.GetDirectoryName(Application.LocalUserAppDataPath);
            directoryInfo = Directory.CreateDirectory(folder);
            // directoryInfo.Attributes = FileAttributes.Directory | FileAttributes.Hidden;

            ConfigureLogging();
            ConfigureDatabase();

            void ConfigureDatabase()
            {
                var databasePath = Path.Combine(folder, "links.db");

                db = new SQLiteConnection(databasePath);
                db.CreateTable<NotificationItem>();
                logger.Info("DB connection opened");
            }

            void ConfigureLogging()
            {
                var config = new LoggingConfiguration();
                var logfile = new FileTarget("logfile")
                {
                    FileName = Path.Combine(directoryInfo.FullName, "file.txt"),
                    ArchiveEvery = FileArchivePeriod.Day,
                    ArchiveNumbering = ArchiveNumberingMode.Date
                };

                config.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);

                LogManager.Configuration = config;

                logger = LogManager.GetCurrentClassLogger();
            }
        }

        private void MainFormLoad(object sender, EventArgs e)
        {
            Task.Run(StartPolling);
        }

        private async Task StartPolling()
        {
            do
            {
                var hasNewItems = false;

                try
                {
                    var newItems = await httpClient.GetFromJsonAsync<List<NotificationItem>>(server) ?? new List<NotificationItem>();

                    logger.Info("Recieved {0} items from service. {1} have empty url", newItems.Count, newItems.Count(item => string.IsNullOrEmpty(item.Uri)));

                    db.InsertAll(newItems.Where(item => !string.IsNullOrEmpty(item.Uri)));
                    hasNewItems = newItems.Any();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error getting new items from service");
                }

                if (hasNewItems)
                {
                    items = db.Table<NotificationItem>().OrderBy(item => item.Key).ToList();

                    ShowNotification();
                }

            } while (await timer.WaitForNextTickAsync());
        }

        private void ShowNotification()
        {
            //notifyIcon.Icon = (Icon)resourceManager.GetObject(items.Any() ? "notification" : "notifyIcon.Icon");
            notifyIcon.Text = items.Any() ? $"თქვენ გაქვთ {items.Count} შეტყობინება" : "თქვენ არ გაქვთ შეტყობინება";

            if (!items.Any())
            {
                return;
            }

            logger.Info("Found {0} items in db, show notification", items.Count);

            try
            {
                new ToastContentBuilder()
                    .AddText($"თქვენ გაქვთ {items.Count} შეტყობინება")
                    .AddArgument("open")
                    .SetToastScenario(ToastScenario.Reminder)
                    .AddButton("გახსნა", ToastActivationType.Background, "open")
                    .Show(toast =>
                    {
                        toast.Tag = ToastTag;
                        SaveCurrentToast(toast);
                    });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error showing notifications");
            }
        }

        private void SaveCurrentToast(ToastNotification toast)
        {
            if (currentToast != null)
            {
                currentToast.Dismissed -= ToastDismissed;
            }

            currentToast = toast;
            currentToast.Dismissed += ToastDismissed;
        }

        private void ToastDismissed(ToastNotification sender, ToastDismissedEventArgs args)
        {
            if (args.Reason != ToastDismissalReason.ApplicationHidden)
            {
                logger.Info("Toast dismisses. Reason: {0}", args.Reason);
                ShowNotification();
            }
        }

        private void ToastActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            if (e.Argument != "open")
            {
                logger.Info("Toast activated with wrong argument {0}, ignoring.", e.Argument);
                return;
            }

            if (isBrowserOpen || items.Count == 0)
            {
                logger.Info($"Toast activated but {(isBrowserOpen ? "browser is already open" : "there are no items")}");
                ShowNotification();
            }
            else
            {
                Invoke(() =>
                {
                    if (aboutVisible)
                    {
                        aboutBox?.CloseWithResult(DialogResult.Abort);
                        Application.DoEvents();
                    }
                });

                Invoke(() =>
                {
                    try
                    {

                        var notificationItem = items[0];
                        using (var browserForm = new BrowserForm(notificationItem.Uri))
                        {
                            db.Delete<NotificationItem>(notificationItem.Key);

                            items = db.Table<NotificationItem>().OrderBy(item => item.Key).ToList();

                            ToastNotificationManagerCompat.History.Remove(ToastTag);

                            ShowNotification();

                            isBrowserOpen = true;

                            logger.Info("Opening webview for {0}", notificationItem.Uri);
                            browserForm.ShowDialog();
                        }

                        isBrowserOpen = false;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error opening browser");

                        ShowNotification();
                    }
                });
            }
        }

        private void NotifyIconMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (isBrowserOpen)
            {
                return;
            }

            if (aboutVisible)
            {
                aboutBox!.Activate();
                return;
            }

            if (aboutBox == null)
            {
                aboutBox = new AboutBox(UserIdInfo.GetUserId());
                aboutBox.Closed += AboutBoxClosed;
            }

            aboutVisible = true;
            aboutBox.ShowDialog();
        }

        private void AboutBoxClosed(object? sender, EventArgs e)
        {
            aboutVisible = false;
        }
    }
}