using Android.App;
using Android.Widget;
using Android.OS;
using Android.Net.Wifi;
using Android.Net;
using Android.Content;
using System.Linq;
using System;
using Android.Runtime;
using System.Threading.Tasks;
using System.Threading;

namespace Wifiautoconnect
{
    [Activity(Label = "Wifiautoconnect", MainLauncher = true, Name = "com.wifi.autoconnect.MainActivity")]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            var prefs = GetSharedPreferences("Wifiautoconnect", FileCreationMode.Private);

            var txtNetworkName = FindViewById<EditText>(Resource.Id.txtNetworkName);
            txtNetworkName.Text = prefs.GetString("network", "My Wi-Fi Network SSID");

            var txtPassword = FindViewById<EditText>(Resource.Id.txtPassword);
            txtPassword.Text = prefs.GetString("password", "MyWiFiPass");

            var btnSave = FindViewById<Button>(Resource.Id.btnSave);
            btnSave.Click += delegate
            {
                var prefsedit = prefs.Edit();
                prefsedit.PutString("network", txtNetworkName.Text.Trim());
                prefsedit.PutString("password", txtPassword.Text.Trim());
                prefsedit.Apply();

                var Wifi = (WifiManager)GetSystemService(WifiService);

                Wifi.Disconnect();

                foreach (var networkconf in Wifi.ConfiguredNetworks.Where(n => n.Ssid != null && n.Ssid.Contains(txtNetworkName.Text.Trim())))
                {
                    if (networkconf != null)
                    {
                        Wifi.RemoveNetwork(networkconf.NetworkId);
                        Wifi.SaveConfiguration();
                    }
                }

                Wifi.StartScan();
            };

            var chkHideToasts = FindViewById<CheckBox>(Resource.Id.chkHideToasts);
            chkHideToasts.Checked = prefs.GetBoolean("hideToasts", false);
            chkHideToasts.CheckedChange += (object sender, CompoundButton.CheckedChangeEventArgs e) =>
            {
                var prefsedit = prefs.Edit();
                prefsedit.PutBoolean("hideToasts", e.IsChecked);
                prefsedit.Apply();
            };

            StartService(this);
            SetAlarm(this);
        }

        public static void MakeText(string Text, bool Short)
        {
            try
            {
                var prefs = Application.Context.GetSharedPreferences("Wifiautoconnect", FileCreationMode.Private);

                if (!prefs.GetBoolean("hideToasts", false))
                {
                    Toast.MakeText(Application.Context, Text, Short ? ToastLength.Short : ToastLength.Long).Show();
                }
            }
            catch { }
        }

        public static void SetAlarm(Context context)
        {
            var alarmTime = 60000; //1minute
            var pIntent = PendingIntent.GetBroadcast(context, 12345, new Intent(context, typeof(BootReceiver)), PendingIntentFlags.UpdateCurrent);
            var alarm = (AlarmManager)context.GetSystemService(AlarmService);
            alarm.SetRepeating(AlarmType.RtcWakeup, 0, alarmTime, pIntent);
        }

        public static void StartService(Context context)
        {
            try
            {
                var i = new Intent(context, typeof(AutoConnectService));
                i.SetAction("START");

                context.ApplicationContext.StartService(i);
            }
            catch (Exception ex)
            {
                MakeText("StartService Error: " + ex.Message, true);
            }
        }
    }

    [BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = true, Name = "com.wifi.autoconnect.BootReceiver")]
    [IntentFilter(new[] { Intent.ActionBootCompleted, Intent.ActionLockedBootCompleted, "android.intent.action.QUICKBOOT_POWERON", "mycustombroadcast" })]
    public class BootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            try
            {
                var Wifi = (WifiManager)context.GetSystemService(Context.WifiService);
                Wifi.StartScan();
            }
            catch (Exception ex)
            {
                var prefs = context.GetSharedPreferences("Wifiautoconnect", FileCreationMode.Private);

                if (!prefs.GetBoolean("hideToasts", false))
                {
                    Toast.MakeText(context, "ERROR BOOT: " + ex.Message + " " + (ex.StackTrace ?? ""), ToastLength.Long);
                }
            }
        }
    }

    [BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = true, Name = "com.wifi.autoconnect.WiFiReceiver")]
    [IntentFilter(new[] { WifiManager.WifiStateChangedAction, WifiManager.ScanResultsAvailableAction, WifiManager.NetworkStateChangedAction, "mycustombroadcast" })]
    public class WiFiReceiver : BroadcastReceiver
    {
        private WifiManager Wifi;
        private ConnectivityManager CManager;
        private string ConnectedSSID { get { return Wifi?.ConnectionInfo?.SSID?.Replace("\"", "").Trim() ?? ""; } }

        public override void OnReceive(Context context, Intent intent)
        {
            try
            {
                Wifi = (WifiManager)context.GetSystemService(Context.WifiService);
                CManager = (ConnectivityManager)context.GetSystemService(Context.ConnectivityService);

                if (!Wifi.IsWifiEnabled)
                {
                    Wifi.SetWifiEnabled(true);

                    MainActivity.MakeText("WIFI IS OFF", true);
                }

                if (string.IsNullOrWhiteSpace(ConnectedSSID) || ConnectedSSID == "<unknown ssid>")
                {
                    MainActivity.MakeText("WIFI NOT CONNECTED", true);

                    var networkInfo = CManager.ActiveNetworkInfo;

                    if (networkInfo == null || !networkInfo.IsConnectedOrConnecting)
                    {
                        var prefs = context.GetSharedPreferences("Wifiautoconnect", FileCreationMode.Private);

                        var ssid = prefs.GetString("network", "");
                        var pass = prefs.GetString("password", "");

                        if (ssid.Trim().Length > 0)
                        {
                            var NetworkConfig = Wifi.ConfiguredNetworks.Where(n => n.Ssid != null && (n.Ssid == ssid || n.Ssid == "\"" + ssid + "\"")).FirstOrDefault();

                            if (NetworkConfig == null)
                            {
                                NetworkConfig = new WifiConfiguration()
                                {
                                    Ssid = "\"" + ssid + "\"",
                                    StatusField = WifiStatus.Enabled,
                                    Priority = Wifi.ConfiguredNetworks.Max(n => n.Priority) + 1,
                                    PreSharedKey = "\"" + pass + "\""
                                };
                                NetworkConfig.AllowedKeyManagement.Set((int)KeyManagementType.WpaPsk);
                                NetworkConfig.NetworkId = Wifi.AddNetwork(NetworkConfig);

                                Wifi.SaveConfiguration();
                            }

                            if (Wifi.EnableNetwork(NetworkConfig.NetworkId, true))
                            {
                                MainActivity.MakeText("WIFI CONNECTED", true);
                            }
                        }
                    }
                }
                else
                {
                    MainActivity.MakeText("CON SSID: " + ConnectedSSID, false);
                }
            }
            catch (Exception ex)
            {
                MainActivity.MakeText("ERROR WIFI: " + ex.Message + " " + (ex.StackTrace ?? ""), false);
            }
        }
    }

    [Service(Enabled = true, DirectBootAware = true, Exported = true, Name = "com.wifi.autoconnect.AutoConnectService")]
    public class AutoConnectService : Service
    {
        private Context context;
        private Handler handler = new Handler();
        private static Java.Lang.Runnable runnable = null;
        private static bool running = false;

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override void OnStart(Intent intent, int startId)
        {
            Toast.MakeText(this, "Service started by user.", ToastLength.Long).Show();

            OnStartCommand(intent, StartCommandFlags.Redelivery, startId);
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            context = this;

            var prefs = context.GetSharedPreferences("Wifiautoconnect", FileCreationMode.Private);

            if (!prefs.GetBoolean("hideToasts", false))
            {
                Toast.MakeText(this, "Service created!", ToastLength.Long).Show();
            }

            runnable = new Java.Lang.Runnable(() =>
            {
                running = true;

                prefs = context.GetSharedPreferences("Wifiautoconnect", FileCreationMode.Private);
                if (!prefs.GetBoolean("hideToasts", false))
                {
                    Toast.MakeText(context, "Service is still running", ToastLength.Long).Show();
                }

                MainActivity.SetAlarm(context);

                running = false;
                handler.PostDelayed(runnable, 10000);
            });

            handler.PostDelayed(runnable, 1000);

            return StartCommandResult.Sticky;
        }
    }
}