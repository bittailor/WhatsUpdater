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
            string file = await Download(_updateInfo.version, _updateInfo.url);
            Android.Util.Log.Info(TAG, $" ... download done");
            progress.SetMessage(Resources.GetString(Resource.String.startInstall));
            Android.Util.Log.Info(TAG, $"install {file} ...");
            Install(file);
            Android.Util.Log.Info(TAG, $"... install done");
            progress.Dismiss();
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

        private async Task<string> Download(string version, string url)
        {
            var httpClient = new System.Net.Http.HttpClient();

            var path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).ToString();
            path = path + "/WhatsApp_" + version + ".apk";
            Android.Util.Log.Info(TAG, $"Download to {path} ...");

            using (FileStream fileStream = new FileStream(path, FileMode.Create)) {
                var stream = await httpClient.GetStreamAsync(url);
                await stream.CopyToAsync(fileStream);
                Android.Util.Log.Info(TAG, $" ... download done");
                return path;
            }
        }

        private void Install(string file) {
            Intent intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(Android.Net.Uri.FromFile(new Java.IO.File(file)), "application/vnd.android.package-archive");
            intent.SetFlags(ActivityFlags.NewTask);
            //StartActivity(intent);
            StartActivityForResult(intent, 4711);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
            Android.Util.Log.Info(TAG, $" ... activity done");        
        }



    }
}

