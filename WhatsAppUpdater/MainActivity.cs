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


    [Activity(Label = "WhatsApp Updater", MainLauncher = true, Icon = "@mipmap/icon")]
    public class MainActivity : Activity
    {
        private const string TAG = "AppCode";

        private Button _installButton;
        private TextView _statusText;

        private (string version, string url) _updateInfo;
        int count = 1;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            // Get our button from the layout resource,
            // and attach an event to it
            Button button = FindViewById<Button>(Resource.Id.myButton);
            _installButton = FindViewById<Button>(Resource.Id.buttonInstall);
            _statusText = FindViewById<TextView>(Resource.Id.textViewStatus);
            _statusText.Text = "--";

            TextView installedVersion = FindViewById<TextView>(Resource.Id.textViewInstalledVersion);



            button.Click += delegate { button.Text = $"{count++} clicks!"; };
            _installButton.Click += Install;

            try
            {
                installedVersion.Text = PackageManager.GetPackageInfo("com.whatsapp", 0).VersionName;
            }
            catch (PackageManager.NameNotFoundException)
            {
                installedVersion.Text = "not installed";
            }

            button.Click += (sender, e) => {
                UpdateLatestVersionAsync();
            };

            UpdateLatestVersionAsync();
        }

        private async void Install(object sender, EventArgs e)
        {
            _statusText.Text = "download ...";
            string file = await Download(_updateInfo.version, _updateInfo.url);
            _statusText.Text = "... download done => install ...";
            Android.Util.Log.Info(TAG, $"File is {file}");
            Install(file);
            _statusText.Text = "... install done";
        }

        protected override void OnResume() {
            base.OnResume();
            UpdateLatestVersionAsync();
        }


        private async void UpdateLatestVersionAsync()
        {
            TextView latestVersion = FindViewById<TextView>(Resource.Id.textViewLatestVersion);
            Button button = FindViewById<Button>(Resource.Id.myButton);
            latestVersion.Text = "loading...";
            _updateInfo = await GetLatestVersion();
            latestVersion.Text = _updateInfo.version;
            _installButton.Enabled = _updateInfo.url != null;
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
            StartActivity(intent); 
        }
    }
}

