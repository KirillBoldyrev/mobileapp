using System;
using Android.App;
using Android.Content;

namespace Toggl.Giskard.BroadcastReceivers
{
    [BroadcastReceiver]
    public class SmartAlertCalendarEventBroadcastReceiver : BroadcastReceiver
    {
        public static string NotificationId = "NotificationId";
        public static string Notification = "Notification";

        public override void OnReceive(Context context, Intent intent)
        {
            var notificationManager = (NotificationManager) context.GetSystemService(Context.NotificationService);

            var notification = (Notification) intent.GetParcelableExtra(Notification);
            var notificationId = intent.GetIntExtra(NotificationId, 0);

            notificationManager.Notify(notificationId, notification);
        }
    }
}
