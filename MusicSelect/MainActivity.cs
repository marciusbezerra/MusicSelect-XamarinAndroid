using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Media;
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
        private Button buttonPlayPause;
        private Button buttonResolve;
        private Button buttonDelete;
        private Button buttonSelect;

        private SeekBar seekBarTime;
        private Timer playTimer;
        private AudioManager appAudioService;
        private ComponentName appComponentName;

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
                    await UpdateScreenInfoAsync();
                };

                player.Completion += (sender, args) =>
                {
                    try
                    {
                        Beep();
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

                buttonPlayPause = FindViewById<Button>(Resource.Id.buttonPlayPause);
                buttonResolve = FindViewById<Button>(Resource.Id.buttonResolve);
                buttonDelete = FindViewById<Button>(Resource.Id.buttonDelete);
                buttonSelect = FindViewById<Button>(Resource.Id.buttonSelect);

                imageViewArt = FindViewById<ImageView>(Resource.Id.imageViewArt);
                textViewTitle = FindViewById<TextView>(Resource.Id.textViewTitle);
                textViewArtist = FindViewById<TextView>(Resource.Id.textViewArtist);
                textViewDuraction = FindViewById<TextView>(Resource.Id.textViewDuraction);
                textViewBitrate = FindViewById<TextView>(Resource.Id.textViewBitrate);
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

                buttonResolve.Click += (sender, args) =>
                {
                    try
                    {
                        StopPlayer();
                        MoveCurrentMusic("Resolver");
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
                        MoveCurrentMusic("Excluir");
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
                        MoveCurrentMusic("Selecionadas");
                        UpdateCurrentMusic();
                        StartPlayer();
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                        ShowDialog(e.ToString(), "Erro");
                    }
                };

                if (Directory.Exists(@"/sdcard/Documents/Music"))
                    MusicDirectory = @"/sdcard/Documents/Music";
                else if (Directory.Exists(@"/storage/external_SD/Music"))
                    MusicDirectory = @"/storage/external_SD/Music";
                else
                    MusicDirectory = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "Music");

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

        private static bool IsBluetoothHeadsetConnected()
        {
            var bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
            return bluetoothAdapter != null && bluetoothAdapter.IsEnabled
                   && bluetoothAdapter.GetProfileConnectionState(ProfileType.Headset) == ProfileState.Connected;
        }

        private static void Beep()
        {
            var toneG = new ToneGenerator(Stream.Alarm, 100);
            toneG.StartTone(Tone.CdmaAlertCallGuard, 200);
        }

        public void RegisterMediaButtonEvents()
        {
            try
            {
                var bluetoothReceiverClassName = Class.FromType(typeof(BluetoothReceiver)).Name;
                appAudioService = (AudioManager)GetSystemService(AudioService);
                appComponentName = new ComponentName(PackageName, bluetoothReceiverClassName);

                appAudioService.RegisterMediaButtonEventReceiver(appComponentName);
                ShowNotification($"{Title}:{_currentNotificationId}", "BLUETOOTH CONNECTED AND MEDIA BUTTON EVENT REGISTERED!");
                Beep();

            }
            catch (Exception e)
            {
                Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                ShowDialog(e.ToString(), "Erro");
            }
        }

        public void UnregisterMediaButtonEvents()
        {
            try
            {
                appAudioService.UnregisterMediaButtonEventReceiver(appComponentName);
                ShowNotification($"{Title}:{_currentNotificationId}", "BLUETOOTH DESCONNECTED AND MEDIA BUTTON EVENT UNREGISTERED!");
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
            var firstMusic = musics.FirstOrDefault();
            player.SetDataSource(firstMusic);
            player.SetAudioStreamType(Stream.Music);
            player.Prepare();
            CurrentMusic = firstMusic;
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
                var retriever = new MediaMetadataRetriever();
                retriever.SetDataSource(CurrentMusic);

                var title = retriever.ExtractMetadata(MetadataKey.Title);
                var artist = retriever.ExtractMetadata(MetadataKey.Artist);
                var bitrate = retriever.ExtractMetadata(MetadataKey.Bitrate)?.Substring(0, 3);

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
                {
                    var musicParts = Path.GetFileNameWithoutExtension(CurrentMusic).Split('-');
                    title = musicParts.FirstOrDefault();
                    artist = musicParts.LastOrDefault();
                }

                new TextToSpeechImplementation(this).Speak($"{artist}! {title}!!");

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
                imageViewArt.SetAdjustViewBounds(true);
            }
            else
            {

                var retriever = new MediaMetadataRetriever();
                retriever.SetDataSource(CurrentMusic);

                var data = retriever.GetEmbeddedPicture();

                if (data != null)
                {
                    var bitmap = BitmapFactory.DecodeByteArray(data, 0, data.Length);
                    imageViewArt.SetImageBitmap(bitmap);
                    imageViewArt.SetAdjustViewBounds(true);
                }
                else
                {
                    var radomInt = new Random();
                    var imageId = radomInt.Next(1, 15);
                    var resourceId = Resources.GetIdentifier($"technics{imageId}", "drawable", PackageName);
                    imageViewArt.SetImageResource(resourceId);
                    imageViewArt.SetAdjustViewBounds(true);
                }
            }
        }

        private void MoveCurrentMusic(string newFolder)
        {
            if (CurrentMusic != null && File.Exists(CurrentMusic))
            {
                var toFolder = Path.Combine(MusicDirectory, newFolder);
                var toFilename = Path.Combine(toFolder, Path.GetFileName(CurrentMusic));
                Directory.CreateDirectory(toFolder);
                if (File.Exists(toFilename))
                    toFilename = Path.Combine(toFolder, $"{Guid.NewGuid()}_{Path.GetFileName(CurrentMusic)}");
                File.Move(CurrentMusic, toFilename);
                return;
            }
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
}

