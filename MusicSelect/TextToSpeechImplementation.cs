using Android.Content;
using Android.Speech.Tts;
using Java.Util;
using System.Collections.Generic;

namespace MusicSelect
{
    public class TextToSpeechImplementation : Java.Lang.Object, TextToSpeech.IOnInitListener
    {
        private readonly Context context;
        readonly TextToSpeech tts;
        string speak;

        public TextToSpeechImplementation(Context context)
        {
            this.context = context;
            tts = new TextToSpeech(context, this);
        }

        public void Speak(string text)
        {
            speak = text;
            tts.Speak(speak, QueueMode.Flush, null, null); //todo: dará erro com Android 5-
        }

        #region IOnInitListener implementation
        public void OnInit(OperationResult status)
        {
            if (status.Equals(OperationResult.Success))
                tts.Speak(speak, QueueMode.Flush, null, null); //todo: dará erro com Android 5-
        }
        #endregion
    }
}