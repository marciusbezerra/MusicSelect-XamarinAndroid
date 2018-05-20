
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Widget;
using Java.Lang;

namespace MusicSelect.Services
{
    public class MediaSessionService
    {
        private readonly Context _context;
        private MediaSession _mediaSession;
        private MediaQueueControl _mediaQueue;

        public MediaSessionService(Context context, MediaQueueControl mediaQueue)
        {
            _context = context;
            _mediaQueue = mediaQueue;
        }

        public void NewMediaSession()
        {
            try
            {
                _mediaSession = new MediaSession(_context, _context.PackageName);

                if (_mediaSession == null)
                {
                    Toast.MakeText(_context, "initMediaSession: _mediaSession = null", ToastLength.Long).Show();
                    return;
                }

                var intent = new Intent(_context, Class.FromType(typeof(BluetoothReceiver)));
                var pendingIntent = PendingIntent.GetBroadcast(_context, 0, intent, PendingIntentFlags.UpdateCurrent);
                _mediaSession.SetMediaButtonReceiver(pendingIntent);

                var mediaSessionToken = _mediaSession.SessionToken;

                _mediaSession.SetCallback(new MediaSessionCallback(_context, _mediaQueue));

                _mediaSession.SetFlags(MediaSessionFlags.HandlesMediaButtons | MediaSessionFlags.HandlesTransportControls);

                SetState(_mediaQueue.PlayerIsPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Stopped, _mediaQueue.PlayerPosition);

                GeneralService.Beep();
                //https://code.tutsplus.com/tutorials/background-audio-in-android-with-mediasessioncompat--cms-27030
                //https://www.programcreek.com/java-api-examples/?api=android.media.session.MediaSession

            }
            catch (System.Exception e)
            {
                Toast.MakeText(_context, e.ToString(), ToastLength.Long).Show();
                new DialogService(_context).ShowDialog(e.ToString(), "Erro");
            }
        }

        public void ReleaseMediaSession()
        {
            try
            {
                _mediaSession.Active = false;
            }
            catch (System.Exception e)
            {
                Toast.MakeText(_context, e.ToString(), ToastLength.Long).Show();
                new DialogService(_context).ShowDialog(e.ToString(), "Erro");
            }
        }

        public void SetState(PlaybackStateCode stateCode, long playPosition) {
            PlaybackState state = new PlaybackState.Builder()
                .SetActions(PlaybackState.ActionPlay
                    | PlaybackState.ActionPause
                    | PlaybackState.ActionStop
                    | PlaybackState.ActionSkipToNext
                    | PlaybackState.ActionSkipToPrevious
                    | PlaybackState.ActionSeekTo
                    | PlaybackState.ActionFastForward
                    | PlaybackState.ActionPlayFromMediaId)
                .SetState(stateCode, playPosition, 1.0f)
                .Build();

            SetMediaSessionMetadata(_mediaQueue.CurrentMusicArtist, _mediaQueue.CurrentMusicTitle, _mediaQueue.CurrentMusicAlbumArt);

            _mediaSession.SetPlaybackState(state);
            _mediaSession.Active = true; //TODO: Isso aqui não poderia ficar no final do NewMediaSession
        }

        public void SetMediaSessionMetadata(string artist, string title, Bitmap albumArt)
        {
            //https://www.b4x.com/android/forum/threads/send-text-display-bluetooth.68503/
            //https://stackoverflow.com/questions/30942054/media-session-compat-not-showing-lockscreen-controls-on-pre-lollipop
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                var metadataBuilder = new MediaMetadata.Builder();

                metadataBuilder.PutString(MediaMetadata.MetadataKeyTitle, title);
                metadataBuilder.PutString(MediaMetadata.MetadataKeyArtist, artist);

                if (albumArt != null)
                    metadataBuilder.PutBitmap(MediaMetadata.MetadataKeyAlbumArt, albumArt);

                _mediaSession.SetMetadata(metadataBuilder.Build());
            }
        }

    }
}