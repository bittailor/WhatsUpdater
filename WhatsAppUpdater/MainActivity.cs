using Android.App;
using Android.Widget;
using Android.OS;
using Android.Content;
using Android.Content.PM;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System;

namespace WhatsAppUpdater
{


    [Activity(Label = "@string/title", 
              MainLauncher = true, 
              Icon = "@mipmap/icon", 
              Theme = "@android:style/Theme.Material.Light.DarkActionBar" )]
    public class MainActivity : Activity
    {
        private const string TAG = "AppCode";

        private Button _refreshButton;
        private Button _installButton;
        private TextView _installedVersion;
        private TextView _latestVersion;

        private (string version, string url) _updateInfo;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.Main);

            _refreshButton = FindViewById<Button>(Resource.Id.buttonRefresh);
            _installButton = FindViewById<Button>(Resource.Id.buttonInstall);
            _installedVersion = FindViewById<TextView>(Resource.Id.textViewInstalledVersion);
            _latestVersion = FindViewById<TextView>(Resource.Id.textViewLatestVersion);

            _installButton.Click += (s,e) => Install();
            _refreshButton.Click += (s,e) => Refresh();
        }

        protected override void OnResume()
        {
            base.OnResume();
            Refresh();
        }

        private async void Refresh()
        {
            var progress = ProgressDialog.Show(this, Resources.GetString(Resource.String.refresh), Resources.GetString(Resource.String.refreshMessage));
            try
            {
                _installedVersion.Text = PackageManager.GetPackageInfo("com.whatsapp", 0).VersionName;
            }
            catch (PackageManager.NameNotFoundException)
            {
                _installedVersion.Text = "not installed";
            }
            _updateInfo = await GetLatestVersion();
            _latestVersion.Text = _updateInfo.version;
            _installButton.Enabled = _updateInfo.url != null;
            progress.Dismiss();
        }

        private async void Install()
        {
            Android.Util.Log.Info(TAG, $"download {_updateInfo.url} ...");
            var progress = ProgressDialog.Show(this, Resources.GetString(Resource.String.install), Resources.GetString(Resource.String.download));
            (bool success, string fileUri, string mimeType) = await Download(_updateInfo.version, _updateInfo.url);
            if(!success) {
                Android.Util.Log.Info(TAG, $" ... download failed");
                progress.Dismiss();
                ShowFailedDialog(fileUri);
                return;
            }
            Android.Util.Log.Info(TAG, $" ... download done");
            progress.SetMessage(Resources.GetString(Resource.String.startInstall));
            Android.Util.Log.Info(TAG, $"install {fileUri} ...");
            Install(fileUri);
            Android.Util.Log.Info(TAG, $"... install done");
            progress.Dismiss();
        }

        private void ShowFailedDialog(string message) {
            AlertDialog.Builder alert = new AlertDialog.Builder(this);
            alert.SetTitle("Failed");
            alert.SetMessage($"Download failed : {message}");
            alert.SetPositiveButton("OK", (senderAlert, args) =>
            {
            });
            Dialog dialog = alert.Create();
            dialog.Show();
        }

        private async Task<(string version, string url)> GetLatestVersion()
        {
            Android.Util.Log.Info(TAG, "GetLatestVersion ... ");
            var httpClient = new System.Net.Http.HttpClient();

            var patternVersion = new Regex(@"<p class=""version"" align=""center"">Version\s*(?<version>(?:\d|\.)*)\s*</p>");
            var patternDownload = new Regex(@"href=""(?<url>https://www.cdn.whatsapp.net/android/(?:\d|\.)*/WhatsApp.apk)"">");

            string contents = await httpClient.GetStringAsync("http://www.whatsapp.com/android/"); // async method!
            foreach (var line in contents.Split(new string[] { System.Environment.NewLine  }, System.StringSplitOptions.RemoveEmptyEntries)) {
                var matchVersion = patternVersion.Match(line);
                var matchDownload = patternDownload.Match(line);
                if(matchVersion.Success && matchDownload.Success) {
                    (string version, string url) info = (matchVersion.Groups["version"].Value, matchDownload.Groups["url"].Value);
                    Android.Util.Log.Info(TAG, $" ... version {info.version} url {info.url} ");
                    return info;                        
                }
            }
            Android.Util.Log.Info(TAG, $" ... not found");
            return ("invalid",null);
        }

        private Task<(bool success, string uri, string mimeType)> Download(string version, string url)
        {
            var destination = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).ToString();
            destination = destination + "/WhatsApp_" + version + ".apk";

            if(System.IO.File.Exists(destination)) 
            {
                Android.Util.Log.Info(TAG, $"delete file {destination}");
                System.IO.File.Delete(destination);    
            }

            //set downloadmanager
            DownloadManager.Request request = new DownloadManager.Request(Android.Net.Uri.Parse(url));
            request.SetDescription(Resources.GetString(Resource.String.download));
            request.SetTitle(Resources.GetString(Resource.String.app_name));

            //set destination
            request.SetDestinationUri(Android.Net.Uri.Parse("file://" + destination));

            // get download service and enqueue file
            DownloadManager manager = (DownloadManager)GetSystemService(Context.DownloadService);
            long downloadId = manager.Enqueue(request);

            var done = new TaskCompletionSource<(bool success, string uri, string mimeType)>();
            BroadcastReceiver onComplete = new ActionBroadcastReceiver((context, intent, self) =>
            {
                long id = intent.GetLongExtra(DownloadManager.ExtraDownloadId, 0L);
                if (id != downloadId)
                {
                    return;
                }
                UnregisterReceiver(self);
                DownloadManager.Query query = new DownloadManager.Query();
                query.SetFilterById(id);
                var cursor = manager.InvokeQuery(query);
                if (!cursor.MoveToFirst())
                {
                    Android.Util.Log.Error(TAG, "Failed to get download properties");
                    return;
                }
                int statusIndex = cursor.GetColumnIndex(DownloadManager.ColumnStatus);
                int status = cursor.GetInt(statusIndex);

                if ((int)Android.App.DownloadStatus.Successful != status)
                {
                    int reasonCode = cursor.GetInt(cursor.GetColumnIndex(DownloadManager.ColumnReason));
                    var reason = (DownloadError)reasonCode;
                    var message = $"Failed {reasonCode} => {reason}";
                    Android.Util.Log.Info(TAG, $"download failed: {message}"); 
                    done.SetResult((false, "", ""));
                    return;
                }

                int uriIndex = cursor.GetColumnIndex(DownloadManager.ColumnLocalUri);
                string localUri = cursor.GetString(uriIndex);

                string mimeType = manager.GetMimeTypeForDownloadedFile(id);

                done.SetResult((true, localUri, mimeType));    
                   
            });
            RegisterReceiver(onComplete, new IntentFilter(DownloadManager.ActionDownloadComplete));
            return done.Task;
        }

        private void Install(string fileUri) {
            Intent intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(Android.Net.Uri.Parse(fileUri), "application/vnd.android.package-archive");
            intent.SetFlags(ActivityFlags.NewTask);
            //StartActivity(intent);
            StartActivityForResult(intent, 4711);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
            Android.Util.Log.Info(TAG, $" ... activity done");        
        }


        private class ActionBroadcastReceiver : BroadcastReceiver
        {
            private readonly Action<Context, Intent, BroadcastReceiver> _onReceive;     

            public ActionBroadcastReceiver(Action<Context, Intent, BroadcastReceiver> onReceive)
            {
                _onReceive = onReceive;    
            }

            public override void OnReceive(Context context, Intent intent)
            {
                _onReceive(context, intent, this);    
            }
        }


    }
}

