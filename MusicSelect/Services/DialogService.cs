
using Android.App;
using Android.Content;
using Android.Graphics;
using System;

namespace MusicSelect.Services
{
    public class DialogService
    {
        private readonly Context context;

        public DialogService(Context context)
        {
            this.context = context;
        }

        public void ShowDialog(string message, string title = "Aviso")
        {
            AlertDialog[] dialog = { null };
            var builder = new AlertDialog.Builder(context);
            builder.SetMessage(message)
                .SetTitle(title)
                .SetCancelable(false)
                .SetPositiveButton("OK", (sender, args) =>
                {
                    dialog[0]?.Cancel();
                });
            dialog[0] = builder.Create();
            dialog[0].Show();
        }

        public void ShowNotification(int notificationId, string title, string message, Bitmap largeIcon, params Notification.Action[] actions)
        {
            var notificationBuilder =
                new Notification.Builder(context)
                .SetSmallIcon(Resource.Drawable.Icon)
                .SetContentTitle(title)
                .SetActions(actions)
                .SetContentText(message);

            if (largeIcon != null)
                notificationBuilder.SetLargeIcon(largeIcon);

            var resultIntent = new Intent(context, typeof(MainActivity));
            var stackBuilder = TaskStackBuilder.Create(context);
            stackBuilder.AddParentStack(context.Class);
            stackBuilder.AddNextIntent(resultIntent);
            var resultPendingIntent = stackBuilder.GetPendingIntent(1, PendingIntentFlags.UpdateCurrent);
            notificationBuilder.SetContentIntent(resultPendingIntent);
            var mNotificationManager = (NotificationManager)context.GetSystemService(Context.NotificationService);
            mNotificationManager.Notify(notificationId, notificationBuilder.Build());
        }
    }

    public class NotificationAction
    {
        public string ActionTitle { get; set; }
        public Action TouchAction { get; set; }
    }
}