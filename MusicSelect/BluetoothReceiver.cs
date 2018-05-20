using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Widget;
using Exception = System.Exception;

namespace MusicSelect
{
    [BroadcastReceiver(Enabled = true, Exported = true, Label = "Music Selector Bluetooth Receiver")]
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

                var action = intent.Action;
                var extras = intent.Extras;

                switch (action)
                {
                    case BluetoothA2dp.ActionConnectionStateChanged:
                        {
                            switch ((State)extras.GetInt(BluetoothProfile.ExtraState))
                            {
                                case State.Connected:
                                    Toast.MakeText(Application.Context, "Conectado", ToastLength.Long).Show();
                                    break;
                                case State.Disconnected:
                                    Toast.MakeText(Application.Context, "Desconectado", ToastLength.Long).Show();
                                    break;
                            }
                            break;
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