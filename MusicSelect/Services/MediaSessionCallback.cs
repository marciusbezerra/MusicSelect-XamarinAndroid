using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace MusicSelect.Services
{

    public class MediaSessionCallback : MediaSession.Callback
    {
        private readonly Context context;
        private readonly MediaQueueControl _mediaQueue;

        public MediaSessionCallback(Context context, MediaQueueControl mediaQueue)
        {
            this.context = context;
            _mediaQueue = mediaQueue;
        }

        public override bool OnMediaButtonEvent(Intent mediaButtonIntent)
        {
            var intentAction = mediaButtonIntent.Action;
            if (Intent.ActionMediaButton.Equals(intentAction))
            {
                var @event = (KeyEvent)mediaButtonIntent.GetParcelableExtra(Intent.ExtraKeyEvent);

                if (@event == null)
                    return base.OnMediaButtonEvent(mediaButtonIntent);

                var keycode = @event.KeyCode;
                var action = @event.Action;
                if (@event.RepeatCount >= 1 && action == KeyEventActions.Down)
                {
                    switch (keycode)
                    {
                        case Keycode.MediaPrevious:
                        case Keycode.MediaNext:
                            Toast.MakeText(context, $"{keycode} {@event.RepeatCount} CLICKS!", ToastLength.Long).Show();
                            break;
                    }
                }
            }
            return base.OnMediaButtonEvent(mediaButtonIntent);
        }

        public override void OnPause()
        {
            _mediaQueue.Pause();
            base.OnPlay();
        }

        public override void OnPlay()
        {
            _mediaQueue.PlayPause();
            base.OnPlay();
        }

        public override async void OnSkipToNext()
        {
            await _mediaQueue.StopMoveAndStartPlayerAsync("Selecionadas", false);
            base.OnSkipToNext();
        }

        public override async void OnSkipToPrevious()
        {
            await _mediaQueue.StopMoveAndStartPlayerAsync("Excluir", true);
            base.OnSkipToPrevious();
        }

        public override void OnCustomAction(string action, Bundle extras)
        {
            Toast.MakeText(context, $"OnCustomAction: {action}", ToastLength.Long).Show();
            base.OnCustomAction(action, extras);
        }

        public override void OnSetRating(Rating rating)
        {
            Toast.MakeText(context, $"OnSetRating: {rating}", ToastLength.Long).Show();
            base.OnSetRating(rating);
        }

    }
}

