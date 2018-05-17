using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Media.Session;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Lang;

namespace MusicSelect.Services
{
    public class MediaSessionService
    {
        private readonly Context context;
        private MediaSession mediaSession;

        public MediaSessionService(Context context)
        {
            this.context = context;
        }

        public void RegisterMediaButtonEvents()
        {
            try
            {
                mediaSession = new MediaSession(context, context.PackageName);

                if (mediaSession == null)
                {
                    Toast.MakeText(context, "initMediaSession: _mediaSession = null", ToastLength.Long).Show();
                    return;
                }

                var intent = new Intent(context, Class.FromType(typeof(BluetoothReceiver)));
                var pendingIntent = PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.UpdateCurrent);
                mediaSession.SetMediaButtonReceiver(pendingIntent);

                var mediaSessionToken = mediaSession.SessionToken;

                mediaSession.SetCallback(new MediaSessionCallback(context));

                mediaSession.SetFlags(MediaSessionFlags.HandlesMediaButtons | MediaSessionFlags.HandlesTransportControls);

                PlaybackState state = new PlaybackState.Builder()
                    .SetActions(PlaybackState.ActionPlay
                        | PlaybackState.ActionPause
                        | PlaybackState.ActionStop
                        | PlaybackState.ActionSkipToNext
                        | PlaybackState.ActionSkipToPrevious
                        | PlaybackState.ActionSeekTo
                        | PlaybackState.ActionFastForward)
                    .SetState(PlaybackStateCode.Stopped, PlaybackState.PlaybackPositionUnknown, 0)
                    .Build();

                mediaSession.SetPlaybackState(state);
                mediaSession.Active = true;
                GeneralService.Beep();
                //https://code.tutsplus.com/tutorials/background-audio-in-android-with-mediasessioncompat--cms-27030
                //https://www.programcreek.com/java-api-examples/?api=android.media.session.MediaSession

            }
            catch (System.Exception e)
            {
                Toast.MakeText(context, e.ToString(), ToastLength.Long).Show();
                new DialogService(context).ShowDialog(e.ToString(), "Erro");
            }
        }

        public void UnregisterMediaButtonEvents()
        {
            try
            {
                mediaSession.Active = false;
            }
            catch (System.Exception e)
            {
                Toast.MakeText(context, e.ToString(), ToastLength.Long).Show();
                new DialogService(context).ShowDialog(e.ToString(), "Erro");
            }
        }

    }
}