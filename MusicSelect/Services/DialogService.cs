
using Android.App;
using Android.Content;

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

        public void ShowNotification(string title, string message, int notificationId)
        {
            var mBuilder =
                new Notification.Builder(context)
                .SetSmallIcon(Resource.Drawable.Icon)
                .SetContentTitle(title)
                .SetContentText(message);

            var resultIntent = new Intent(context, typeof(MainActivity));
            var stackBuilder = TaskStackBuilder.Create(context);
            stackBuilder.AddParentStack(context.Class);
            stackBuilder.AddNextIntent(resultIntent);
            var resultPendingIntent = stackBuilder.GetPendingIntent(1, PendingIntentFlags.UpdateCurrent);
            mBuilder.SetContentIntent(resultPendingIntent);
            var mNotificationManager = (NotificationManager)context.GetSystemService(Context.NotificationService);
            mNotificationManager.Notify(notificationId, mBuilder.Build());
        }
    }
}