using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Views;
using Android.Widget;
using Exception = System.Exception;

namespace MusicSelect
{
    [BroadcastReceiver(Enabled = true, Exported = true, Label = "MusicSelect Bluetooth Receiver")]
    [IntentFilter(new[]
    {
        Intent.ActionBootCompleted,
        "android.bluetooth.adapter.action.STATE_CHANGED",
        "android.bluetooth.device.action.BOND_STATE_CHANGED",
        "android.bluetooth.a2dp.profile.action.CONNECTION_STATE_CHANGED",
        "android.intent.action.MEDIA_BUTTON",
        "android.bluetooth.headset.action.VENDOR_SPECIFIC_HEADSET_EVENT"
    })]
    public class BluetoothReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            try
            {
                if (MainActivity.CurrentActivity != null)
                {

                    var action = intent.Action;
                    var extras = intent.Extras;

                    switch (action)
                    {
                        case BluetoothA2dp.ActionConnectionStateChanged:
                            {
                                //android.bluetooth.profile.extra.STATE"
                                var state = (State)extras.GetInt(BluetoothProfile.ExtraState);

                                switch (state)
                                {
                                    case State.Connected:
                                        MainActivity.CurrentActivity.RegisterMediaButtonEvents();
                                        MainActivity.CurrentActivity.Play();
                                        break;
                                    case State.Disconnected:
                                        MainActivity.CurrentActivity.Pause();
                                        MainActivity.CurrentActivity.UnregisterMediaButtonEvents();
                                        break;
                                }
                                break;
                            }
                        case Intent.ActionMediaButton:
                            {
                                var @event = (KeyEvent)intent.GetParcelableExtra(Intent.ExtraKeyEvent);
                                if (@event?.Action == KeyEventActions.Up)
                                {
                                    switch (@event.KeyCode)
                                    {
                                        case Keycode.MediaNext:
                                            MainActivity.CurrentActivity.SelectAndNext();
                                            break;
                                        case Keycode.MediaPrevious:
                                            MainActivity.CurrentActivity.DeleteAndNext();
                                            break;
                                        case Keycode.MediaPause:
                                            MainActivity.CurrentActivity.Pause();
                                            break;
                                        case Keycode.MediaPlay:
                                            MainActivity.CurrentActivity.Play();
                                            break;
                                        case Keycode.MediaPlayPause:
                                            MainActivity.CurrentActivity.TooglePlayPause();
                                            break;
                                    }
                                }
                                break;
                            }
                    }
                }
            }
            catch (Exception exception)
            {
                Toast.MakeText(Application.Context, exception.ToString(), ToastLength.Long).Show();
            }
        }
    }
}