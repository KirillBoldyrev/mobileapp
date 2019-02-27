using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.App;
using Toggl.Foundation.Services;
using Toggl.Giskard.BroadcastReceivers;
using Toggl.Giskard.Extensions;
using Toggl.Multivac.Extensions;
using Notification = Android.App.Notification;

namespace Toggl.Giskard.Services
{
    public sealed class NotificationServiceAndroid : INotificationService
    {

        public IObservable<Unit> Schedule(IImmutableList<Multivac.Notification> notifications)
            => Observable.Start(() =>
                {
                    var context = Application.Context;
                    var notificationIntent = context.PackageManager.GetLaunchIntentForPackage(context.PackageName);
                    notificationIntent.SetPackage(null);
                    var pendingIntent = PendingIntent.GetActivity(context, 0, notificationIntent, 0);
                    var notificationManager = NotificationManager.FromContext(context);
                    var notificationsBuilder = context.CreateNotificationBuilderWithDefaultChannel(notificationManager);

                    notifications.Select(notification =>
                        (
                            notificationId: notification.Id.GetHashCode(), // this is a temp measure, might be better to change this in the future
                            androidNofitication: notificationsBuilder.SetAutoCancel(true)
                                .SetContentIntent(pendingIntent)
                                .SetContentTitle(notification.Title)
                                .SetSmallIcon(Resource.Drawable.ic_icon_running)
                                .SetContentText(notification.Description)
                                .Build(),
                            scheduleAt: notification.ScheduledTime
                        )
                    ).ForEach(notificationInfo => scheduleNotification(notificationInfo.notificationId, notificationInfo.androidNofitication, notificationInfo.scheduleAt));
                });

        public IObservable<Unit> UnscheduleAllNotifications()
            => Observable.Start(() =>
                {
                    var context = Application.Context;
                    var notificationManager = NotificationManagerCompat.From(context);
                    notificationManager.CancelAll();
                });

        public static void scheduleNotification(int notificationId, Notification notification, DateTimeOffset scheduleAt)
        {
            var scheduledNotificationIntent = new Intent(Application.Context, typeof(SmartAlertCalendarEventBroadcastReceiver));
            scheduledNotificationIntent.PutExtra(SmartAlertCalendarEventBroadcastReceiver.NotificationId, notificationId);
            scheduledNotificationIntent.PutExtra(SmartAlertCalendarEventBroadcastReceiver.Notification, notification);
            var pendingIntent = PendingIntent.GetBroadcast(Application.Context, notificationId, scheduledNotificationIntent, PendingIntentFlags.CancelCurrent);

            var futureInMillis = (long) (scheduleAt - DateTimeOffset.Now).TotalMilliseconds;
            var alarmManager = (AlarmManager) Application.Context.GetSystemService(Context.AlarmService);
            alarmManager.SetExact(AlarmType.ElapsedRealtimeWakeup, SystemClock.ElapsedRealtime() + futureInMillis, pendingIntent);
        }
    }
}
