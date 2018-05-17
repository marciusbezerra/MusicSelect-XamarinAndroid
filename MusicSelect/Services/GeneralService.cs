using Android.Media;

namespace MusicSelect.Services
{
    public class GeneralService
    {
        public static void Beep()
        {
            var toneG = new ToneGenerator(Stream.Alarm, 100);
            toneG.StartTone(Tone.CdmaAlertCallGuard, 200);
        }
    }
}