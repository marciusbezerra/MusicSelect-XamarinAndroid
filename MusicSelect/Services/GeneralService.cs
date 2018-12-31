using Android.Media;

namespace MusicSelect.Services
{
    public static class GeneralService
    {
        public static void Beep()
        {
            var toneG = new ToneGenerator(Stream.Alarm, 50);
            toneG.StartTone(Tone.CdmaEmergencyRingback, 200);
        }
    }
}