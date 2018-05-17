using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Views;
using Android.Widget;
using MusicSelect.Services;
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
                                var state = (State)extras.GetInt(BluetoothProfile.ExtraState);

                                switch (state)
                                {
                                    case State.Connected:
                                        Toast.MakeText(Application.Context, "Bluetooth Conectado", ToastLength.Long).Show();
                                        break;
                                    case State.Disconnected:
                                        Toast.MakeText(Application.Context, "Bluetooth Desconectado", ToastLength.Long).Show();
                                        MainActivity.CurrentActivity.Pause();
                                        break;
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