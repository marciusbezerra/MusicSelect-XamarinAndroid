using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Widget;
using Java.Lang;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exception = System.Exception;
using Path = System.IO.Path;
using Stream = Android.Media.Stream;

namespace MusicSelect
{
    [Activity(Label = "MusicSelect", MainLauncher = true, Icon = "@drawable/icon", ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : Activity
    {
        private string MusicDirectory;
        private MediaPlayer player;
        public string CurrentMusic;
        public static MainActivity CurrentActivity { get; set; }
        private static int _currentNotificationId;

        private ImageView imageViewArt;
        private TextView textViewTitle;
        private TextView textViewArtist;
        private TextView textViewDuraction;
        private TextView textViewBitrate;
        private CheckBox checkBoxAutoSkip;
        private CheckBox checkBoxNoVoice;
        private Button buttonPlayPause;
        private ToggleButton toggleButtonDoubleSpeed;
        private Button buttonListenLater;
        private Button buttonAnotherVersion;
        private Button buttonResolve;
        private Button buttonDelete;
        private Button buttonSelect;

        private SeekBar seekBarTime;
        private Timer playTimer;
        //private AudioManager appAudioService;
        //private ComponentName appComponentName;


        private readonly string[] MusicDirs =
        {
            "/sdcard/Documents/Music",
            "/storage/external_SD/Music",
            "/storage/5800-E221/music",
        };

        private MediaSession mediaSession;

        private int NextNotificationId()
        {
            _currentNotificationId++;
            return _currentNotificationId;
        }

        private void ShowDialog(string message, string title = "Aviso")
        {
            AlertDialog[] dialog = { null };
            var builder = new AlertDialog.Builder(this);
            builder.SetMessage(message)
                .SetTitle(title)
                .SetCancelable(false)
                .SetPositiveButton("OK", (sender, args) =>
                {
                    dialog[0]?.Cancel();
                });
            dialog[0] = builder.Create();
            dialog[0].Show();
        }

        private void ShowNotification(string title, string message)
        {
            var mBuilder =
                new Notification.Builder(this)
                .SetSmallIcon(Resource.Drawable.Icon)
                .SetContentTitle(title)
                .SetContentText(message);

            var resultIntent = new Intent(this, typeof(MainActivity));
            var stackBuilder = TaskStackBuilder.Create(this);
            stackBuilder.AddParentStack(this);
            stackBuilder.AddNextIntent(resultIntent);
            var resultPendingIntent = stackBuilder.GetPendingIntent(1, PendingIntentFlags.UpdateCurrent);
            mBuilder.SetContentIntent(resultPendingIntent);
            var mNotificationManager = (NotificationManager)GetSystemService(NotificationService);
            mNotificationManager.Notify(NextNotificationId(), mBuilder.Build());
        }

        private async Task ScreenOnAsync()
        {
            try
            {
                var powerManager = (PowerManager)GetSystemService(PowerService);
                var wakeLock = powerManager.NewWakeLock(WakeLockFlags.ScreenDim | WakeLockFlags.AcquireCausesWakeup, "StackOverflow");
                wakeLock.Acquire();
                await Task.Delay(2000);
                wakeLock.Release();
            }
            catch (Exception e) //TODO: Retirar, caso dê certo!
            {
                Toast.MakeText(this, $"{nameof(ScreenOnAsync)}.error: {e}", ToastLength.Long).Show();
                throw;
            }
        }

        protected override void OnCreate(Bundle bundle)
        {
            try
            {
                base.OnCreate(bundle);

                CurrentActivity = this;

                SetContentView(Resource.Layout.Main);

                GetLocationPermission();

                player = new MediaPlayer();

                player.Prepared += async (sender, args) =>
                {
                    try
                    {
                        await UpdateScreenInfoAsync();
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, $"player.Prepared.Error: {e}", ToastLength.Long).Show();
                        ShowDialog($"player.Prepared.Error: {e}", "Erro");
                    }
                };

                player.Completion += async (sender, args) =>
                {
                    try
                    {
                        if (!checkBoxNoVoice.Checked)
                            Beep();
                        if (checkBoxAutoSkip.Checked)
                            buttonListenLater.PerformClick();
                        await ScreenOnAsync();
                    }
                    catch (Exception e)
                    {
                        //TODO: APAGAR ISSO CASO NÃO ESTEJA DANDO ERRO!
                        Toast.MakeText(this, $"player.Completion.Error: {e}", ToastLength.Long).Show();
                        ShowDialog($"player.Completion.Error: {e}", "Erro");
                    }
                };

                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    player.TimedMetaDataAvailable += (sender, args) =>
                    {
                        try
                        {
                            Beep();
                            var id3Array = args.Data.GetMetaData();
                            var id3String = System.Text.Encoding.UTF8.GetString(id3Array);
                            ShowNotification($"{Title}:{_currentNotificationId}", $"TimedMetaDataAvailable: {id3String}");
                        }
                        catch (Exception e)
                        {
                            //TODO: APAGAR ISSO CASO NÃO ESTEJA DANDO ERRO!
                            Toast.MakeText(this, $"player.TimedMetaDataAvailable.Error: {e}", ToastLength.Long).Show();
                            ShowDialog($"player.TimedMetaDataAvailable.Error: {e}", "Erro");
                        }
                    };
                }

                player.TimedText += (sender, args) =>
                {
                    try
                    {
                        Beep();
                        var text = args.Text?.Text;
                        ShowNotification($"{Title}:{_currentNotificationId}", $"TimedText: {text}");
                    }
                    catch (Exception e)
                    {
                        //TODO: APAGAR ISSO CASO NÃO ESTEJA DANDO ERRO!
                        Toast.MakeText(this, $"player.TimedText.Error: {e}", ToastLength.Long).Show();
                        ShowDialog($"player.TimedText.Error: {e}", "Erro");
                    }
                };

                player.Error += (sender, args) =>
                {
                    ShowDialog(args.ToString(), "Erro");
                };

                buttonPlayPause = FindViewById<Button>(Resource.Id.buttonPlayPause);
                toggleButtonDoubleSpeed = FindViewById<ToggleButton>(Resource.Id.toggleButtonDoubleSpeed);
                buttonListenLater = FindViewById<Button>(Resource.Id.buttonListenLater);
                buttonAnotherVersion = FindViewById<Button>(Resource.Id.buttonAnotherVersion);
                buttonResolve = FindViewById<Button>(Resource.Id.buttonResolve);
                buttonDelete = FindViewById<Button>(Resource.Id.buttonDelete);
                buttonSelect = FindViewById<Button>(Resource.Id.buttonSelect);

                imageViewArt = FindViewById<ImageView>(Resource.Id.imageViewArt);
                textViewTitle = FindViewById<TextView>(Resource.Id.textViewTitle);
                textViewArtist = FindViewById<TextView>(Resource.Id.textViewArtist);
                textViewDuraction = FindViewById<TextView>(Resource.Id.textViewDuraction);
                textViewBitrate = FindViewById<TextView>(Resource.Id.textViewBitrate);
                checkBoxAutoSkip = FindViewById<CheckBox>(Resource.Id.checkBoxAutoSkip);
                checkBoxNoVoice = FindViewById<CheckBox>(Resource.Id.checkBoxNoVoice);
                seekBarTime = FindViewById<SeekBar>(Resource.Id.seekBarTime);

                buttonPlayPause.Click += (sender, args) =>
                {
                    if (player.IsPlaying)
                        player.Pause();
                    else
                    {
                        StartPlayer();
                    }
                };

                toggleButtonDoubleSpeed.Click += (sender, args) =>
                {
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
                    {
                        var newPlaybackParams = player.PlaybackParams;
                        newPlaybackParams.SetSpeed((sender as ToggleButton).Checked ? 2f : 1f);
                        player.PlaybackParams = newPlaybackParams;
                    }
                    else
                        ShowDialog("Não disponível nessa versão do Android!", "Erro");
                };

                buttonListenLater.Click += (sender, args) =>
                {
                    try
                    {
                        StopPlayer();
                        MoveCurrentMusic("ListenLater", true);
                        UpdateCurrentMusic();
                        StartPlayer();
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                        ShowDialog(e.ToString(), "Erro");
                    }
                };

                buttonAnotherVersion.Click += (sender, args) =>
                {
                    try
                    {
                        StopPlayer();
                        MoveCurrentMusic("AnotherVersion", true);
                        UpdateCurrentMusic();
                        StartPlayer();
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                        ShowDialog(e.ToString(), "Erro");
                    }
                };

                buttonResolve.Click += (sender, args) =>
                {
                    try
                    {
                        StopPlayer();
                        MoveCurrentMusic("Resolver", true);
                        UpdateCurrentMusic();
                        StartPlayer();
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                        ShowDialog(e.ToString(), "Erro");
                    }
                };

                buttonDelete.Click += (sender, args) =>
                {
                    try
                    {
                        StopPlayer();
                        MoveCurrentMusic("Excluir", true);
                        UpdateCurrentMusic();
                        StartPlayer();
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                        ShowDialog(e.ToString(), "Erro");
                    }
                };

                buttonSelect.Click += (sender, args) =>
                {
                    try
                    {
                        StopPlayer();
                        MoveCurrentMusic("Selecionadas", false);
                        UpdateCurrentMusic();
                        StartPlayer();
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                        ShowDialog(e.ToString(), "Erro");
                    }
                };

                seekBarTime.ProgressChanged += (sender, args) =>
                {
                    try
                    {
                        if (args.FromUser && player.IsPlaying)
                        {
                            player.SeekTo(args.Progress);
                        }
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                    }
                };

                UpdateCurrentMusic();

                if (IsBluetoothHeadsetConnected())
                    RegisterMediaButtonEvents();
            }
            catch (Exception e)
            {
                Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                ShowDialog(e.ToString(), "Erro");
            }
        }

        private bool HasMusicIn(string diretory)
        {
            return Directory.Exists(diretory) &&
                (Directory.GetFiles(diretory, "*.mp3").Any() || Directory.GetFiles(diretory, "*.MP3").Any());
        }

        private static bool IsBluetoothHeadsetConnected()
        {
            var bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
            return bluetoothAdapter != null && bluetoothAdapter.IsEnabled
                   && bluetoothAdapter.GetProfileConnectionState(ProfileType.Headset) == ProfileState.Connected;
        }

        public static string GetBaseFolderPath(bool getSDPath = false)
        {
            string baseFolderPath = "";

            try
            {
                Context context = Application.Context;
                Java.IO.File[] dirs = context.GetExternalFilesDirs(null);

                foreach (Java.IO.File folder in dirs)
                {
                    bool IsRemovable = Android.OS.Environment.InvokeIsExternalStorageRemovable(folder);
                    bool IsEmulated = Android.OS.Environment.InvokeIsExternalStorageEmulated(folder);

                    if (getSDPath ? IsRemovable && !IsEmulated : !IsRemovable && IsEmulated)
                        baseFolderPath = folder.Path;
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine("GetBaseFolderPath caused the follwing exception: {0}", ex);
            }

            return baseFolderPath;
        }

        private static void Beep()
        {
            var toneG = new ToneGenerator(Stream.Alarm, 100);
            toneG.StartTone(Tone.CdmaAlertCallGuard, 200);
        }

        //public void RegisterMediaButtonEvents()
        //{
        //    try
        //    {
        //        var bluetoothReceiverClassName = Class.FromType(typeof(BluetoothReceiver)).Name;
        //        appAudioService = (AudioManager)GetSystemService(AudioService);
        //        appComponentName = new ComponentName(PackageName, bluetoothReceiverClassName);

        //        appAudioService.RegisterMediaButtonEventReceiver(appComponentName);

        //        Beep();

        //    }
        //    catch (Exception e)
        //    {
        //        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
        //        ShowDialog(e.ToString(), "Erro");
        //    }
        //}

        public void RegisterMediaButtonEvents()
        {
            try
            {
                mediaSession = new MediaSession(this, PackageName);

                if (mediaSession == null)
                {
                    Toast.MakeText(this, "initMediaSession: _mediaSession = null", ToastLength.Long);
                    return;
                }

                var intent = new Intent(this, Class.FromType(typeof(BluetoothReceiver)));
                var pendingIntent = PendingIntent.GetBroadcast(this, 0, intent, PendingIntentFlags.UpdateCurrent);
                mediaSession.SetMediaButtonReceiver(pendingIntent);

                var mediaSessionToken = mediaSession.SessionToken;

                mediaSession.SetCallback(new MediaSessionCallback(this));

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
                Beep();
                //https://code.tutsplus.com/tutorials/background-audio-in-android-with-mediasessioncompat--cms-27030
                //https://www.programcreek.com/java-api-examples/?api=android.media.session.MediaSession

            }
            catch (Exception e)
            {
                Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                ShowDialog(e.ToString(), "Erro");
            }
        }

        //public void UnregisterMediaButtonEvents()
        //{
        //    try
        //    {
        //        //appAudioService.UnregisterMediaButtonEventReceiver(appComponentName);
        //    }
        //    catch (Exception e)
        //    {
        //        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
        //        ShowDialog(e.ToString(), "Erro");
        //    }
        //}

        public void UnregisterMediaButtonEvents()
        {
            try
            {
                mediaSession.Active = false;
            }
            catch (Exception e)
            {
                Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                ShowDialog(e.ToString(), "Erro");
            }
        }

        public void SelectAndNext()
        {
            buttonSelect.PerformClick();
        }

        internal void DeleteAndNext()
        {
            buttonDelete.PerformClick();
        }

        internal void TooglePlayPause()
        {
            buttonPlayPause.PerformClick();
        }

        internal void Play()
        {
            if (!player.IsPlaying)
                buttonPlayPause.PerformClick();
        }

        internal void Pause()
        {
            if (player.IsPlaying)
                buttonPlayPause.PerformClick();
        }

        private void StartPlayer()
        {
            if (CurrentMusic != null)
            {

                player.Start();
                seekBarTime.Max = player.Duration;
                playTimer?.Dispose();
                playTimer = new Timer(state =>
                {
                    if (player.IsPlaying && player.CurrentPosition > 0)
                    {
                        RunOnUiThread(() =>
                        {
                            try
                            {
                                textViewDuraction.Text =
                            $"{TimeSpan.FromMilliseconds(player.CurrentPosition).ToString("hh':'mm':'ss")} / " +
                            $"{TimeSpan.FromMilliseconds(player.Duration).ToString("hh':'mm':'ss")}";
                                seekBarTime.Progress = player.CurrentPosition;

                            }
                            catch (Exception exception)
                            {
                                Toast.MakeText(this, exception.Message, ToastLength.Long);
                                ShowDialog(exception.Message, "Erro");
                            }
                        });
                        playTimer.Change(500, Timeout.Infinite);
                    }
                }, null, 500, Timeout.Infinite);
            }
        }

        private void StopPlayer()
        {
            playTimer?.Dispose();
            player.Stop();
            player.Reset();
        }

        private void GetLocationPermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                if (CheckSelfPermission(Manifest.Permission.ManageDocuments) == Permission.Granted)
                    return;
                if (CheckSelfPermission(Manifest.Permission.Internet) == Permission.Granted)
                    return;
                if (CheckSelfPermission(Manifest.Permission.WriteExternalStorage) == Permission.Granted)
                    return;
                RequestPermissions(
                    new[]
                    {
                    Manifest.Permission.ManageDocuments, Manifest.Permission.Internet,
                    Manifest.Permission.WriteExternalStorage
                    }, 0);
            }
        }

        private void UpdateCurrentMusic()
        {
            MusicDirectory = MusicDirs.FirstOrDefault(d => HasMusicIn(d)) ?? MusicDirs.FirstOrDefault();

            var musics = Directory.GetFiles(MusicDirectory, "*.mp3").ToList();

            musics.AddRange(Directory.GetFiles(MusicDirectory, "*.MP3"));

            if (!musics.Any())
            {
                Toast.MakeText(this, "Nenhuma música foi localizada em: " + MusicDirectory, ToastLength.Long).Show();
                CurrentMusic = null;
                UpdatePictureArt(true);
                return;
            }

            var musicCount = musics.Count;
            CurrentMusic = musics.FirstOrDefault();
            try
            {
                player.Reset();
                player.SetDataSource(CurrentMusic);
                player.SetAudioStreamType(Stream.Music);
                player.Prepare();
            }
            catch (Exception e)
            {
                Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                ShowDialog(e.ToString(), "Erro");
                StopPlayer();
                MoveCurrentMusic("Resolver", true);
                UpdateCurrentMusic();
            }

            UpdatePictureArt(false);
            Title = "Musics (" + musicCount + ")";
        }

        private async Task UpdateScreenInfoAsync()
        {
            if (CurrentMusic == null)
            {
                textViewTitle.Text = string.Empty;
                textViewArtist.Text = string.Empty;
                textViewDuraction.Text = string.Empty;
            }
            else
            {
                var title = "";
                var artist = "";
                var bitrate = "";

                if (Build.VERSION.SdkInt > BuildVersionCodes.Kitkat)
                {
                    var retriever = new MediaMetadataRetriever();
                    await retriever.SetDataSourceAsync(CurrentMusic);

                    title = retriever.ExtractMetadata(MetadataKey.Title);
                    artist = retriever.ExtractMetadata(MetadataKey.Artist);
                    bitrate = retriever.ExtractMetadata(MetadataKey.Bitrate)?.Substring(0, 3);
                }

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
                {
                    var musicParts = Path.GetFileNameWithoutExtension(CurrentMusic).Split('-');
                    title = musicParts.FirstOrDefault();
                    artist = musicParts.LastOrDefault();
                }

                string cleanText(string input)
                {
                    var output = string.Concat(input.Where(c => char.IsLetter(c) || char.IsNumber(c)));
                    return output;
                }

                if (!checkBoxNoVoice.Checked)
                    new TextToSpeechImplementation(this).Speak($"{cleanText(artist)}! {cleanText(title)}!!");

                var moreInfo = await MoreInfoAsync();

                textViewTitle.Text = title?.Trim().ToUpperInvariant();
                textViewArtist.Text = artist?.Trim().ToUpperInvariant();
                textViewDuraction.Text = TimeSpan.FromMilliseconds(player.Duration).ToString("hh':'mm':'ss");
                textViewBitrate.Text = $"{bitrate}kbps {moreInfo}";

                ShowNotification($"{Title}:{_currentNotificationId}", $"{textViewTitle.Text} - {textViewArtist.Text}");
            }
        }

        private async Task<string> MoreInfoAsync()
        {
            return await Task.Factory.StartNew(() =>
             {
                 try
                 {
                     var mex = new MediaExtractor();
                     mex.SetDataSource(CurrentMusic);
                     var mediaFormat = mex.GetTrackFormat(0);
                     var sampleRate = mediaFormat.ContainsKey(MediaFormat.KeySampleRate) ? mediaFormat.GetInteger(MediaFormat.KeySampleRate) : 0;
                     var channelCount = mediaFormat.ContainsKey(MediaFormat.KeyChannelCount) ? mediaFormat.GetInteger(MediaFormat.KeyChannelCount) : 0;
                     return $"{sampleRate}Hz {channelCount} canais";
                 }
                 catch (Exception e)
                 {
                     ShowDialog(e.Message, "Erro");
                     return "";
                 }
             });
        }

        private void UpdatePictureArt(bool noMusic)
        {
            if (noMusic)
            {
                imageViewArt.SetImageResource(Resource.Drawable.technics0);
            }
            else
            {

                var retriever = new MediaMetadataRetriever();
                retriever.SetDataSource(CurrentMusic);

                var data = retriever.GetEmbeddedPicture();

                if (data != null)
                {
                    var bitmap = BitmapFactory.DecodeByteArray(data, 0, data.Length);
                    if (bitmap != null)
                    {
                        imageViewArt.SetImageBitmap(bitmap);
                    }
                    else
                        ShowRandomAlbumArt();
                }
                else
                    ShowRandomAlbumArt();
            }
        }

        private void ShowRandomAlbumArt()
        {
            var radomInt = new Random();
            var imageId = radomInt.Next(1, 15);
            var resourceId = Resources.GetIdentifier($"technics{imageId}", "drawable", PackageName);
            imageViewArt.SetImageResource(resourceId);
        }

        private void MoveCurrentMusic(string newFolder, bool hiddenFolder)
        {
            if (CurrentMusic != null && File.Exists(CurrentMusic))
            {
                var toFolder = Path.Combine(MusicDirectory, newFolder);
                var toFilename = Path.Combine(toFolder, Path.GetFileName(CurrentMusic));
                Directory.CreateDirectory(toFolder);
                if (File.Exists(toFilename))
                    toFilename = Path.Combine(toFolder, $"{Guid.NewGuid()}_{Path.GetFileName(CurrentMusic)}");
                File.Move(CurrentMusic, toFilename);
                if (hiddenFolder)
                    CreateNoMediafile(toFolder);
                return;
            }
        }

        private void CreateNoMediafile(string folder)
        {
            var hiddenFile = Path.Combine(folder, ".nomedia");
            if (!File.Exists(hiddenFile))
                File.Create(hiddenFile).Dispose();
        }

        ~MainActivity()
        {
            try
            {
                player?.Release();
            }
            catch (Exception e)
            {
                //TODO: APAGAR ISSO CASO NÃO ESTEJA DANDO ERRO!
                Toast.MakeText(this, $"~MainActivity.Error: {e}", ToastLength.Long).Show();
                ShowDialog($"~MainActivity.Error: {e}", "Erro");
            }
        }
    }

    public class MediaSessionCallback : MediaSession.Callback
    {
        private MainActivity mainActivity;

        public MediaSessionCallback(MainActivity mainActivity)
        {
            this.mainActivity = mainActivity;
        }

        public override bool OnMediaButtonEvent(Intent mediaButtonIntent)
        {
            Toast.MakeText(mainActivity, $"OnMediaButtonEvent: {mediaButtonIntent}", ToastLength.Long).Show();
            return false;
        }

        public override void OnPause()
        {
            Toast.MakeText(mainActivity, "OnPause", ToastLength.Long).Show();
            base.OnPlay();
        }

        public override void OnPlay()
        {
            Toast.MakeText(mainActivity, "OnPlay", ToastLength.Long).Show();
            base.OnPlay();
        }

        public override void OnStop()
        {
            Toast.MakeText(mainActivity, "OnStop", ToastLength.Long).Show();
            base.OnStop();
        }

        public override void OnSkipToNext()
        {
            Toast.MakeText(mainActivity, "OnSkipToNext", ToastLength.Long).Show();
            base.OnSkipToNext();
        }

        public override void OnSkipToPrevious()
        {
            Toast.MakeText(mainActivity, "OnSkipToPrevious", ToastLength.Long).Show();
            base.OnSkipToPrevious();
        }

        public override void OnCustomAction(string action, Bundle extras)
        {
            Toast.MakeText(mainActivity, $"OnCustomAction: {action}", ToastLength.Long).Show();
            base.OnCustomAction(action, extras);
        }

        public override void OnSetRating(Rating rating)
        {
            Toast.MakeText(mainActivity, $"OnSetRating: {rating}", ToastLength.Long).Show();
            base.OnSetRating(rating);
        }

    }
}

