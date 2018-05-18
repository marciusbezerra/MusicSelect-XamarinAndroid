using Android.Content;
using Android.Speech.Tts;
using Java.Util;
using System.Collections.Generic;

namespace MusicSelect
{
    public class TextToSpeechImplementation : Java.Lang.Object, TextToSpeech.IOnInitListener
    {
        private readonly Context context;
        TextToSpeech speaker;
        string toSpeak;

        public TextToSpeechImplementation(Context context)
        {
            this.context = context;
        }

        public void Speak(string text)
        {
            toSpeak = text;
            if (speaker == null)
                speaker = new TextToSpeech(context, this);
            else
                speaker.Speak(toSpeak, QueueMode.Flush, null, null); //todo: dará erro com Android 5-
        }

        #region IOnInitListener implementation
        public void OnInit(OperationResult status)
        {
            if (status.Equals(OperationResult.Success))
                speaker.Speak(toSpeak, QueueMode.Flush, null, null); //todo: dará erro com Android 5-
        }
        #endregion
    }
}