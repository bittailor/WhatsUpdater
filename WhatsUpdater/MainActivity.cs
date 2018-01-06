using Android.App;
using Android.Widget;
using Android.OS;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System;

namespace WhatsUpdater
{

    [Activity(
              Label = "@string/title", 
              MainLauncher = true, 
              Icon = "@mipmap/icon", 
              Theme = "@android:style/Theme.Material.Light.DarkActionBar" )]
    public class MainActivity : Activity
    {
        private const string TAG = "AppCode";
        private const int REQUEST_CODE_INSTALL_PACKAGE = 4711;

        private Button _refreshButton;
        private Button _installButton;
        private TextView _installedVersion;
        private TextView _latestVersion;

        private (string version, string url) _updateInfo;
        private TaskCompletionSource<bool> _installTaskCompletionSource;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.Main);

            _refreshButton = FindViewById<Button>(Resource.Id.buttonRefresh);
            _installButton = FindViewById<Button>(Resource.Id.buttonInstall);
            _installedVersion = FindViewById<TextView>(Resource.Id.textViewInstalledVersion);
            _latestVersion = FindViewById<TextView>(Resource.Id.textViewLatestVersion);

            _installButton.Click += async (s, e) => await Install();
            _refreshButton.Click += async (s, e) => await Refresh();
        }

        private async Task OnInstall(object sender, EventArgs e)
        {
            await Install();
        }

        protected override async void OnResume()
        {
            base.OnResume();
            await Refresh();
        }

        private async Task Refresh()
        {
            var progress = ProgressDialog.Show(this, Resources.GetString(Resource.String.refresh), Resources.GetString(Resource.String.refreshMessage));
            try
            {
                _installedVersion.Text = PackageManager.GetPackageInfo("com.whatsapp", 0).VersionName;
            }
            catch (PackageManager.NameNotFoundException)
            {
                _installedVersion.Text = Resources.GetString(Resource.String.notInstalled);
            }
            _updateInfo = await GetLatestVersion();
            _latestVersion.Text = _updateInfo.version;
            _installButton.Enabled = _updateInfo.url != null;
            progress.Dismiss();
        }

        private async Task Install()
        {
            Android.Util.Log.Info(TAG, $"download {_updateInfo.url} ...");
            var progress = ProgressDialog.Show(this, Resources.GetString(Resource.String.install), Resources.GetString(Resource.String.download));
            (bool success, string fileUri, string mimeType) = await Download(_updateInfo.version, _updateInfo.url);
            if (!success)
            {
                Android.Util.Log.Info(TAG, $" ... download failed");
                progress.Dismiss();
                ShowFailedDialog();
                return;
            }
            Android.Util.Log.Info(TAG, $" ... download done");
            progress.SetMessage(Resources.GetString(Resource.String.startInstall));
            Android.Util.Log.Info(TAG, $"install {fileUri} ...");
            await Install(fileUri);
            var path = Android.Net.Uri.Parse(fileUri).Path;
            var file = new Java.IO.File(path);
            if (file.Exists())
            {
                Android.Util.Log.Info(TAG, $"delete file {path} ...");
                bool deleted = file.Delete();
                Android.Util.Log.Info(TAG, $" ... deleted = {deleted}");

            }
            Android.Util.Log.Info(TAG, $"... install done");
            progress.Dismiss();
        }

        private async Task<(string version, string url)> GetLatestVersion()
        {
            if(!isWifiConnected()) {
                return (Resources.GetString(Resource.String.noWifi), null);    
            }

            Android.Util.Log.Info(TAG, "GetLatestVersion ... ");
            var httpClient = new System.Net.Http.HttpClient();

            var pattern = new Regex(@"href=""(?<url>https?://\S*/(?<version>(?:\d|\.)*)/WhatsApp.apk)"">");

            try 
            {
                string contents = await httpClient.GetStringAsync("http://www.whatsapp.com/android/"); // async method!
                foreach (var line in contents.Split(new string[] { System.Environment.NewLine }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    var match = pattern.Match(line);
                    if (match.Success)
                    {
                        (string version, string url) info = (match.Groups["version"].Value, match.Groups["url"].Value);
                        Android.Util.Log.Info(TAG, $" ... version {info.version} url {info.url} ");
                        return info;
                    }
                }
            } catch(System.Net.Http.HttpRequestException e) 
            {
                Android.Util.Log.Info(TAG, $" request failed: {e}");
                return (Resources.GetString(Resource.String.requestFailed), null);        
            }
            Android.Util.Log.Info(TAG, $" ... not found");
            return (Resources.GetString(Resource.String.notFound), null);
        }

        private Task<(bool success, string uri, string mimeType)> Download(string version, string url)
        {
            
            DownloadManager.Request request = new DownloadManager.Request(Android.Net.Uri.Parse(url));
            request.SetDescription(Resources.GetString(Resource.String.download));
            request.SetTitle(Resources.GetString(Resource.String.appName));
            request.SetDestinationInExternalPublicDir(Android.OS.Environment.DirectoryDownloads, $"/WhatsApp_{version}.apk");

            DownloadManager manager = (DownloadManager)GetSystemService(Context.DownloadService);
            long downloadId = manager.Enqueue(request);

            var done = new TaskCompletionSource<(bool success, string uri, string mimeType)>();
            BroadcastReceiver onComplete = new ActionBroadcastReceiver((context, intent, self) =>
            {
                long id = intent.GetLongExtra(DownloadManager.ExtraDownloadId, 0L);
                if (id != downloadId) { return; }

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
                    int reason = cursor.GetInt(cursor.GetColumnIndex(DownloadManager.ColumnReason));
                    Android.Util.Log.Info(TAG, $"download failed: {reason}"); 
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

        private Task<bool> Install(string fileUri) {
            Intent intent = new Intent(Intent.ActionInstallPackage);
            intent.SetDataAndType(Android.Net.Uri.Parse(fileUri), "application/vnd.android.package-archive");
            intent.PutExtra(Intent.ExtraReturnResult, true);
            intent.PutExtra(Intent.ExtraNotUnknownSource, true);
            intent.PutExtra(Intent.ExtraIntent, true);
            StartActivityForResult(intent, REQUEST_CODE_INSTALL_PACKAGE);

            _installTaskCompletionSource = new TaskCompletionSource<bool>();
            return _installTaskCompletionSource.Task;

        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
            if(requestCode == REQUEST_CODE_INSTALL_PACKAGE) {
                if(_installTaskCompletionSource != null) {
                    _installTaskCompletionSource.SetResult(resultCode != Result.Canceled);
                }
            }               
        }

        private bool isWifiConnected()
        {
            var connectivityManager = (ConnectivityManager)GetSystemService(Context.ConnectivityService);
            NetworkInfo activeNetworkInfo = connectivityManager.ActiveNetworkInfo;
            return activeNetworkInfo != null && activeNetworkInfo.Type == ConnectivityType.Wifi && activeNetworkInfo.IsConnected ;
        }

        private void ShowFailedDialog()
        {
            AlertDialog.Builder alert = new AlertDialog.Builder(this);
            alert.SetTitle(Resources.GetString(Resource.String.oops));
            alert.SetMessage(Resources.GetString(Resource.String.downloadFailed));
            alert.SetPositiveButton(Resources.GetString(Resource.String.ok), (senderAlert, args) =>{});
            Dialog dialog = alert.Create();
            dialog.Show();
        }
    }
}

