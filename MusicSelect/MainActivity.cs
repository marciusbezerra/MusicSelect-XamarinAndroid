using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Media;
using Android.OS;
using Android.Widget;
using MusicSelect.Services;
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
    [Activity(Label = "Music Selector", MainLauncher = true, Icon = "@drawable/icon",
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
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

        MediaSessionService mediaSessionService;


        private readonly string[] MusicDirs =
        {
            "/sdcard/Documents/Music",
            "/storage/external_SD/Music",
            "/storage/5800-E221/music",
        };
        private int NextNotificationId()
        {
            _currentNotificationId++;
            return _currentNotificationId;
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

        protected override void OnCreate(Bundle savedInstanceState)
        {
            try
            {
                base.OnCreate(savedInstanceState);

                CurrentActivity = this;

                ActionBar.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));

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
                        new DialogService(this).ShowDialog($"player.Prepared.Error: {e}", "Erro");
                    }
                };

                player.Completion += async (sender, args) =>
                {
                    try
                    {
                        if (!checkBoxNoVoice.Checked)
                            GeneralService.Beep();
                        if (checkBoxAutoSkip.Checked)
                            buttonListenLater.PerformClick();
                        await ScreenOnAsync();
                    }
                    catch (Exception e)
                    {
                        //TODO: APAGAR ISSO CASO NÃO ESTEJA DANDO ERRO!
                        Toast.MakeText(this, $"player.Completion.Error: {e}", ToastLength.Long).Show();
                        new DialogService(this).ShowDialog($"player.Completion.Error: {e}", "Erro");
                    }
                };

                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    player.TimedMetaDataAvailable += (sender, args) =>
                    {
                        try
                        {
                            GeneralService.Beep();
                            var id3Array = args.Data.GetMetaData();
                            var id3String = System.Text.Encoding.UTF8.GetString(id3Array);
                            new DialogService(this).ShowNotification($"{Title}:{_currentNotificationId}", $"TimedMetaDataAvailable: {id3String}", NextNotificationId());
                        }
                        catch (Exception e)
                        {
                            //TODO: APAGAR ISSO CASO NÃO ESTEJA DANDO ERRO!
                            Toast.MakeText(this, $"player.TimedMetaDataAvailable.Error: {e}", ToastLength.Long).Show();
                            new DialogService(this).ShowDialog($"player.TimedMetaDataAvailable.Error: {e}", "Erro");
                        }
                    };
                }

                player.TimedText += (sender, args) =>
                {
                    try
                    {
                        GeneralService.Beep();
                        var text = args.Text?.Text;
                        new DialogService(this).ShowNotification($"{Title}:{_currentNotificationId}", $"TimedText: {text}", NextNotificationId());
                    }
                    catch (Exception e)
                    {
                        //TODO: APAGAR ISSO CASO NÃO ESTEJA DANDO ERRO!
                        Toast.MakeText(this, $"player.TimedText.Error: {e}", ToastLength.Long).Show();
                        new DialogService(this).ShowDialog($"player.TimedText.Error: {e}", "Erro");
                    }
                };

                player.Error += (sender, args) =>
                {
                    new DialogService(this).ShowDialog(args.ToString(), "Erro");
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
                        new DialogService(this).ShowDialog("Não disponível nessa versão do Android!", "Erro");
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
                        new DialogService(this).ShowDialog(e.ToString(), "Erro");
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
                        new DialogService(this).ShowDialog(e.ToString(), "Erro");
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
                        new DialogService(this).ShowDialog(e.ToString(), "Erro");
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
                        new DialogService(this).ShowDialog(e.ToString(), "Erro");
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
                        new DialogService(this).ShowDialog(e.ToString(), "Erro");
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

                mediaSessionService = new MediaSessionService(this);
                mediaSessionService.RegisterMediaButtonEvents();
            }
            catch (Exception e)
            {
                Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                new DialogService(this).ShowDialog(e.ToString(), "Erro");
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

        public void SelectAndNext()
        {
            buttonSelect.PerformClick();
        }

        internal void DeleteAndNext()
        {
            buttonDelete.PerformClick();
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
                                new DialogService(this).ShowDialog(exception.Message, "Erro");
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
                player.SetAudioAttributes(new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Media)
                    .SetContentType(AudioContentType.Music)
                    .SetLegacyStreamType(Stream.Music)
                    .Build());
                player.Prepare();
            }
            catch (Exception e)
            {
                Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                new DialogService(this).ShowDialog(e.ToString(), "Erro");
                StopPlayer();
                MoveCurrentMusic("Resolver", true);
                UpdateCurrentMusic();
            }

            UpdatePictureArt(false);
            Title = "Music Selector (" + musicCount + " música(s))";
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
                    return string.Concat(input.Where(c => char.IsLetter(c) || char.IsNumber(c)))?.ToLowerInvariant();
                }

                if (!checkBoxNoVoice.Checked)
                    new TextToSpeechImplementation(this).Speak($"{cleanText(artist)}! {cleanText(title)}!!");

                var moreInfo = await MoreInfoAsync();

                textViewTitle.Text = title?.Trim().ToUpperInvariant();
                textViewArtist.Text = artist?.Trim().ToUpperInvariant();
                textViewDuraction.Text = TimeSpan.FromMilliseconds(player.Duration).ToString("hh':'mm':'ss");
                textViewBitrate.Text = $"{bitrate}kbps {moreInfo}";

                SendBluetoothInfo(textViewArtist.Text, textViewTitle.Text);

                new DialogService(this).ShowNotification($"{Title}:{_currentNotificationId}", $"{textViewTitle.Text} - {textViewArtist.Text}", NextNotificationId());
            }
        }

        private void SendBluetoothInfo(string artist, string title)
        {
            try
            {
                var audioManager = (AudioManager)GetSystemService(AudioService);

                var metadata = new MediaMetadata.Builder();

                metadata.PutString(MediaMetadata.MetadataKeyTitle, $"DEBUG_TEST1: {title}");
                metadata.PutString(MediaMetadata.MetadataKeyDisplayTitle, $"DEBUG_TEST2: {title}");
                metadata.PutString(MediaMetadata.MetadataKeyArtist, $"DEBUG_TEST1: {artist}");
                metadata.PutString(MediaMetadata.MetadataKeyAlbumArtist, $"DEBUG_TEST2: {artist}");
            }
            catch (Exception e)
            {
                Toast.MakeText(this, e.Message, ToastLength.Long).Show();
            }
        }

        private Task<string> MoreInfoAsync()
        {
            return Task.Factory.StartNew(() =>
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
                     new DialogService(this).ShowDialog(e.Message, "Erro");
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
                mediaSessionService.UnregisterMediaButtonEvents();
            }
            catch (Exception e)
            {
                //TODO: APAGAR ISSO CASO NÃO ESTEJA DANDO ERRO!
                Toast.MakeText(this, $"~MainActivity.Error: {e}", ToastLength.Long).Show();
                new DialogService(this).ShowDialog($"~MainActivity.Error: {e}", "Erro");
            }
        }
    }
}

