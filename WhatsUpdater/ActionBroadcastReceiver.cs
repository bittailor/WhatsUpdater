using Android.Content;
using System;

namespace WhatsUpdater
{
    public class ActionBroadcastReceiver : BroadcastReceiver
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

