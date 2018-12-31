using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Widget;
using MusicSelect.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Exception = System.Exception;

namespace MusicSelect
{
    [Activity(Label = "Music Selector", MainLauncher = true, Icon = "@drawable/icon",
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : Activity
    {
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
        private TextView textConnectionStatus;

        private SeekBar seekBarTime;

        MediaQueueControl _mediaQueue;
        MediaSessionService _mediaSessionService;

        private static MainActivity instance;

        private int NextNotificationId()
        {
            _currentNotificationId++;
            return _currentNotificationId;
        }

        internal void SetConnected()
        {
            RunOnUiThread(() =>
            {
                textConnectionStatus.Text = "Conectado!";
                textConnectionStatus.SetTextColor(Color.Green);
            });
        }

        internal void SetDisconnected()
        {
            RunOnUiThread(() =>
            {
                textConnectionStatus.Text = "Desconectado";
                textConnectionStatus.SetTextColor(Color.Red);
            });
        }

        private async Task ScreenOnAsync()
        {
            var powerManager = (PowerManager)GetSystemService(PowerService);
            var wakeLock = powerManager.NewWakeLock(WakeLockFlags.ScreenDim | WakeLockFlags.AcquireCausesWakeup, "StackOverflow");
            wakeLock.Acquire();
            await Task.Delay(2000);
            wakeLock.Release();
        }

        public static bool IsBluetoothHeadsetConnected()
        {
            BluetoothAdapter mBluetoothAdapter = BluetoothAdapter.DefaultAdapter;
            return mBluetoothAdapter != null && mBluetoothAdapter.IsEnabled
                    && mBluetoothAdapter.GetProfileConnectionState(ProfileType.Headset) == ProfileState.Connected;
        }

        private PendingIntent CreateShareAction(string artist, string title, string filePath)
        {
            if (File.Exists(filePath))
            {
                Intent intentShareFile = new Intent(Intent.ActionSend);
                intentShareFile.SetType("audio/mp3");
                intentShareFile.PutExtra(Intent.ExtraStream, Android.Net.Uri.Parse(filePath));
                intentShareFile.PutExtra(Intent.ExtraSubject, "Escute essa música...");
                intentShareFile.PutExtra(Intent.ExtraText, $"{artist} - {title}");
                intentShareFile.AddFlags(ActivityFlags.GrantPersistableUriPermission);
                return PendingIntent.GetActivity(this, 0, intentShareFile,
                                    PendingIntentFlags.CancelCurrent);
            }
            return null;
        }

        public static MainActivity GetInstance()
        {
            return instance;
        }

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            try
            {
                base.OnCreate(savedInstanceState);

                instance = this;

                ActionBar.SetBackgroundDrawable(new ColorDrawable(Color.ParseColor("#80000000")));
                SetContentView(Resource.Layout.Main);
                GetLocationPermission();

                _mediaQueue = new MediaQueueControl(this);
                _mediaSessionService = new MediaSessionService(this, _mediaQueue);
                _mediaQueue.MediaSessionService = _mediaSessionService;
                _mediaSessionService.NewMediaSession();


                _mediaQueue.PlayerAfterPrepare += () =>
                {
                    try
                    {
                        textViewTitle.Text = _mediaQueue.CurrentMusicTitle?.Trim().ToUpperInvariant();
                        textViewArtist.Text = _mediaQueue.CurrentMusicArtist?.Trim().ToUpperInvariant();
                        textViewDuraction.Text = TimeSpan.FromMilliseconds(_mediaQueue.CurrentMusicDuration).ToString("hh':'mm':'ss");
                        textViewBitrate.Text = _mediaQueue.CurrentMusicBitrate;
                        imageViewArt.SetImageBitmap(_mediaQueue.CurrentMusicAlbumArt);

                        string cleanText(string input)
                        {
                            return string.Concat(input.Where(c => char.IsLetter(c) || char.IsNumber(c)))?.ToLowerInvariant();
                        }

                        if (!checkBoxNoVoice.Checked)
                            new TextToSpeechImplementation(this).Speak($"{cleanText(textViewArtist.Text)}! {cleanText(textViewTitle.Text)}!!");

                        new DialogService(this).ShowNotification(
                            NextNotificationId(), textViewArtist.Text, textViewTitle.Text,
                            _mediaQueue.CurrentMusicAlbumArt,
                            new Notification.Action(Resource.Drawable.play_pause, "Compartilhar",
                                CreateShareAction(textViewArtist.Text, textViewTitle.Text, _mediaQueue.CurrentMusicFilename)));
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, $"PlayerPrepared.Error: {e}", ToastLength.Long).Show();
                        new DialogService(this).ShowDialog($"PlayerPrepared.Error: {e}", "Erro");
                    }
                };

                _mediaQueue.AfterMoveFile += (newFolder, newFilename) =>
                {
                    try
                    {
                        new DialogService(this).ShowNotification(
                                        _currentNotificationId, _mediaQueue.CurrentMusicArtist,
                                        $"{_mediaQueue.CurrentMusicTitle} (*{newFolder.ToUpper()}*)",
                                        _mediaQueue.CurrentMusicAlbumArt,
                                        new Notification.Action(Resource.Drawable.play_pause,
                                            "Compartilhar",
                                            CreateShareAction(_mediaQueue.CurrentMusicArtist, _mediaQueue.CurrentMusicTitle,
                                            newFilename)));
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, $"AfterMoveFile.Error: {e}", ToastLength.Long).Show();
                    }
                };

                _mediaQueue.PlayerCompletion += async () =>
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
                        Toast.MakeText(this, $"PlayerCompletion.Error: {e}", ToastLength.Long).Show();
                        new DialogService(this).ShowDialog($"PlayerCompletion.Error: {e}", "Erro");
                    }
                };

                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    _mediaQueue.PlayerTimedMetaDataAvailable += (args) =>
                    {
                        GeneralService.Beep();
                        var id3Array = args.Data.GetMetaData();
                        var id3String = System.Text.Encoding.UTF8.GetString(id3Array);
                        new DialogService(this).ShowDialog($"TimedMetaDataAvailable: {id3String}");

                    };
                }

                _mediaQueue.PlayerTimedText += (args) =>
                {
                    try
                    {
                        GeneralService.Beep();
                        var text = args.Text?.Text;
                        new DialogService(this).ShowDialog($"TimedText: {text}");
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, $"PlayerTimedText.Error: {e}", ToastLength.Long).Show();
                        new DialogService(this).ShowDialog($"PlayerTimedText.Error: {e}", "Erro");
                    }
                };

                _mediaQueue.PlayerError += (args) => new DialogService(this).ShowDialog(args.ToString(), "Erro");

                _mediaQueue.TimerPlaying += (currentPosition) =>
                {
                    RunOnUiThread(() =>
                    {
                        try
                        {
                            textViewDuraction.Text =
                        $"{TimeSpan.FromMilliseconds(currentPosition).ToString("hh':'mm':'ss")} / " +
                        TimeSpan.FromMilliseconds(_mediaQueue.CurrentMusicDuration).ToString("hh':'mm':'ss");
                            seekBarTime.Progress = currentPosition;

                        }
                        catch (Exception exception)
                        {
                            Toast.MakeText(this, exception.Message, ToastLength.Long);
                            new DialogService(this).ShowDialog(exception.Message, "Erro");
                        }
                    });
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
                textConnectionStatus = FindViewById<TextView>(Resource.Id.textConnectionStatus);

                buttonPlayPause.Click += (sender, args) =>
                {
                    _mediaQueue.PlayPause();
                };

                toggleButtonDoubleSpeed.Click += (sender, args) =>
                {
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
                    {
                        var newPlaybackParams = _mediaQueue.PlayerPlaybackParams;
                        newPlaybackParams.SetSpeed((sender as ToggleButton).Checked ? 2f : 1f);
                        _mediaQueue.PlayerPlaybackParams = newPlaybackParams;
                    }
                    else
                        new DialogService(this).ShowDialog("Não disponível nessa versão do Android!", "Erro");
                };

                buttonListenLater.Click += async (sender, args) =>
                {
                    try
                    {
                        await _mediaQueue.StopMoveAndStartPlayerAsync("ListenLater", true);
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                        new DialogService(this).ShowDialog(e.ToString(), "Erro");
                    }
                };

                buttonAnotherVersion.Click += async (sender, args) =>
                {
                    try
                    {
                        await _mediaQueue.StopMoveAndStartPlayerAsync("AnotherVersion", true);
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                        new DialogService(this).ShowDialog(e.ToString(), "Erro");
                    }
                };

                buttonResolve.Click += async (sender, args) =>
                {
                    try
                    {
                        await _mediaQueue.StopMoveAndStartPlayerAsync("Resolver", true);
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                        new DialogService(this).ShowDialog(e.ToString(), "Erro");
                    }
                };

                buttonDelete.Click += async (sender, args) =>
                {
                    try
                    {
                        await _mediaQueue.StopMoveAndStartPlayerAsync("Excluir", true);
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                        new DialogService(this).ShowDialog(e.ToString(), "Erro");
                    }
                };

                buttonSelect.Click += async (sender, args) =>
                {
                    try
                    {
                        await _mediaQueue.StopMoveAndStartPlayerAsync("Selecionadas", false);
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
                        if (args.FromUser && _mediaQueue.PlayerIsPlaying)
                        {
                            _mediaQueue.PlayerSeekTo(args.Progress);
                        }
                    }
                    catch (Exception e)
                    {
                        Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                    }
                };

                _mediaQueue.PlayerBeforeStart += () => seekBarTime.Max = _mediaQueue.CurrentMusicDuration;
                _mediaQueue.PlayerAfterPrepare += () => Title = $"Music Selector {_mediaQueue.MusicCount} música(s))";

                if (IsBluetoothHeadsetConnected())
                    SetConnected();
                else
                    SetDisconnected();

                await _mediaQueue.PrepareNextMusicAsync();
            }
            catch (Exception e)
            {
                Toast.MakeText(this, e.ToString(), ToastLength.Long).Show();
                new DialogService(this).ShowDialog(e.ToString(), "Erro");
            }
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

        ~MainActivity()
        {
            try
            {
                _mediaSessionService.ReleaseMediaSession();
            }
            catch (Exception e)
            {
                Toast.MakeText(this, $"~MediaQueueControl.Error: {e}", ToastLength.Long).Show();
            }
        }
    }
}

