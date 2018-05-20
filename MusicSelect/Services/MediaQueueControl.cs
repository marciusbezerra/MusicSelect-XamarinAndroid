using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Widget;
using static Android.Media.MediaPlayer;

namespace MusicSelect.Services
{
    public class MediaQueueControl
    {
        private readonly MediaPlayer player;
        private Timer playTimer;
        private string MusicDirectory;

        public delegate void PlayerPreparedDelegate();
        public delegate void PlayerCompletionDelegate();
        public delegate void PlayerTimedMetaDataAvailableDelegate(TimedMetaDataAvailableEventArgs args);
        public delegate void PlayerTimedTextDelegate(TimedTextEventArgs args);
        public delegate void PlayerErrorDelegate(MediaPlayer.ErrorEventArgs args);
        public delegate void TimerPlayingDelegate(int currentPosition);
        public delegate void PlayerBeforeStartDelegate();
        public delegate void PlayerAfterPrepareDelegate();

        public event PlayerPreparedDelegate PlayerPrepared;
        public event PlayerCompletionDelegate PlayerCompletion;
        public event PlayerTimedMetaDataAvailableDelegate PlayerTimedMetaDataAvailable;
        public event PlayerTimedTextDelegate PlayerTimedText;
        public event PlayerErrorDelegate PlayerError;
        public event TimerPlayingDelegate TimerPlaying;
        public event PlayerBeforeStartDelegate PlayerBeforeStart;
        public event PlayerAfterPrepareDelegate PlayerAfterPrepare;

        private readonly string[] MusicDirs =
        {
            "/sdcard/Documents/Music",
            "/storage/external_SD/Music",
            "/storage/5800-E221/music",
        };
        private readonly Context _context;
        public MediaSessionService MediaSessionService { get; set; }

        public PlaybackParams PlayerPlaybackParams
        {
            get => player.PlaybackParams;
            set => player.PlaybackParams = value;
        }

        public bool PlayerIsPlaying => player.IsPlaying;
        public int CurrentMusicDuration => player.Duration;
        public int PlayerPosition => player.CurrentPosition;
        public int MusicCount { get; set; }
        public string CurrentMusicFilename { get; internal set; }
        public string CurrentMusicTitle { get; set; }
        public string CurrentMusicArtist { get; set; }
        public Bitmap CurrentMusicAlbumArt { get; set; }
        public string CurrentMusicBitrate { get; private set; }

        public MediaQueueControl(Context context)
        {
            _context = context;
            player = new MediaPlayer();
            player.Prepared += (sender, args) => PlayerPrepared?.Invoke();
            player.Completion += (sender, args) => PlayerCompletion?.Invoke();
            player.TimedMetaDataAvailable += (sender, args) => PlayerTimedMetaDataAvailable?.Invoke(args);
            player.TimedText += (sender, args) => PlayerTimedText?.Invoke(args);
            player.Error += (sender, args) => PlayerError?.Invoke(args);
        }

        public void PlayerSeekTo(int milliseconds)
        {
            player.SeekTo(milliseconds);
        }

        public void StartPlayer()
        {
            if (CurrentMusicFilename != null)
            {

                player.Start();
                MediaSessionService.SetState(PlaybackStateCode.Playing, PlayerPosition);
                playTimer?.Dispose();
                PlayerBeforeStart?.Invoke();
                playTimer = new Timer(state =>
                {
                    if (player.IsPlaying && PlayerPosition > 0)
                    {
                        TimerPlaying?.Invoke(PlayerPosition);
                        playTimer.Change(500, Timeout.Infinite);
                    }
                }, null, 500, Timeout.Infinite);
            }
        }

        public void Pause()
        {
            if (player.IsPlaying)
            {
                player.Pause();
                MediaSessionService.SetState(PlaybackStateCode.Paused, PlayerPosition);
            }
            else
            {
                StartPlayer();
            }
        }

        public void PlayPause()
        {
            if (!player.IsPlaying)
                StartPlayer();
            else
                player.Pause();
        }

        public async Task StopMoveAndStartPlayerAsync(string newFolder, bool hiddenFolder)
        {
            StopPlayer();
            MoveCurrentMusic(newFolder, hiddenFolder);
            await PrepareNextMusicAsync();
            StartPlayer();
        }

        private void StopPlayer()
        {
            playTimer?.Dispose();
            player.Stop();
            MediaSessionService.SetState(PlaybackStateCode.Stopped, PlayerPosition);
            player.Reset();
        }

        private void MoveCurrentMusic(string newFolder, bool hiddenFolder)
        {
            if (CurrentMusicFilename != null && File.Exists(CurrentMusicFilename))
            {
                var toFolder = System.IO.Path.Combine(MusicDirectory, newFolder);
                var toFilename = System.IO.Path.Combine(toFolder, System.IO.Path.GetFileName(CurrentMusicFilename));
                Directory.CreateDirectory(toFolder);
                if (File.Exists(toFilename))
                    toFilename = System.IO.Path.Combine(toFolder, $"{Guid.NewGuid()}_{System.IO.Path.GetFileName(CurrentMusicFilename)}");
                File.Move(CurrentMusicFilename, toFilename);
                if (hiddenFolder)
                    CreateNoMediafile(toFolder);
                return;
            }
        }

        private void CreateNoMediafile(string folder)
        {
            var hiddenFile = System.IO.Path.Combine(folder, ".nomedia");
            if (!File.Exists(hiddenFile))
                File.Create(hiddenFile).Dispose();
        }

        public async Task PrepareNextMusicAsync()
        {
            MusicDirectory = MusicDirs.FirstOrDefault(HasMusicIn) ?? MusicDirs.FirstOrDefault();

            var musics = Directory.GetFiles(MusicDirectory, "*.mp3").ToList();

            musics.AddRange(Directory.GetFiles(MusicDirectory, "*.MP3"));

            if (!musics.Any())
            {
                Toast.MakeText(_context, "Nenhuma música foi localizada em: " + MusicDirectory, ToastLength.Long).Show();
                CurrentMusicFilename = null;
                var bitmapArt = await BitmapFactory.DecodeResourceAsync(_context.Resources, Resource.Drawable.technics0);
                await FillMusicInfosAsync(bitmapArt);
                return;
            }

            MusicCount = musics.Count;
            CurrentMusicFilename = musics.FirstOrDefault();
            try
            {
                player.Reset();
                player.SetDataSource(CurrentMusicFilename);
                player.SetAudioAttributes(new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Media)
                    .SetContentType(AudioContentType.Music)
                    .SetLegacyStreamType(Android.Media.Stream.Music)
                    .Build());
                player.Prepare();
            }
            catch (Exception e)
            {
                Toast.MakeText(_context, e.ToString(), ToastLength.Long).Show();
                new DialogService(_context).ShowDialog(e.ToString(), "Erro");
                StopPlayer();
                MoveCurrentMusic("Resolver", true);
                await PrepareNextMusicAsync();
            }

            var retriever = new MediaMetadataRetriever();
            retriever.SetDataSource(CurrentMusicFilename);

            var data = retriever.GetEmbeddedPicture();


            if (data != null)
            {
                var bitmap = BitmapFactory.DecodeByteArray(data, 0, data.Length);
                await FillMusicInfosAsync(bitmap);
            }
            else
            {
                await FillMusicInfosAsync(null);
            }
            PlayerAfterPrepare?.Invoke();
        }

        private bool HasMusicIn(string diretory)
        {
            return Directory.Exists(diretory) &&
                (Directory.GetFiles(diretory, "*.mp3").Any() || Directory.GetFiles(diretory, "*.MP3").Any());
        }

        private async Task FillMusicInfosAsync(Bitmap albumArt)
        {
            if (CurrentMusicFilename == null)
            {
                CurrentMusicTitle = string.Empty;
                CurrentMusicArtist = string.Empty;
                CurrentMusicBitrate = string.Empty;
            }
            else
            {
                var title = "";
                var artist = "";
                var bitrate = "";

                if (Build.VERSION.SdkInt > BuildVersionCodes.Kitkat)
                {
                    var retriever = new MediaMetadataRetriever();
                    await retriever.SetDataSourceAsync(CurrentMusicFilename);

                    title = retriever.ExtractMetadata(MetadataKey.Title);
                    artist = retriever.ExtractMetadata(MetadataKey.Artist);
                    bitrate = retriever.ExtractMetadata(MetadataKey.Bitrate)?.Substring(0, 3);
                }

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
                {
                    var musicParts = System.IO.Path.GetFileNameWithoutExtension(CurrentMusicFilename).Split('-');
                    title = musicParts.FirstOrDefault();
                    artist = musicParts.LastOrDefault();
                }

                var mex = new MediaExtractor();
                mex.SetDataSource(CurrentMusicFilename);
                var mediaFormat = mex.GetTrackFormat(0);
                var sampleRate = mediaFormat.ContainsKey(MediaFormat.KeySampleRate) ? mediaFormat.GetInteger(MediaFormat.KeySampleRate) : 0;
                var channelCount = mediaFormat.ContainsKey(MediaFormat.KeyChannelCount) ? mediaFormat.GetInteger(MediaFormat.KeyChannelCount) : 0;

                CurrentMusicTitle = title;
                CurrentMusicArtist = artist;
                CurrentMusicBitrate = $"{bitrate}kbps {sampleRate}Hz {channelCount} canais";
            }
            if (albumArt != null)
                CurrentMusicAlbumArt = albumArt;
            else
                CurrentMusicAlbumArt = await GenerateRadomBitmapAsync();
        }

        private Task<Bitmap> GenerateRadomBitmapAsync()
        {
            var radomInt = new Random();
            var imageId = radomInt.Next(1, 15);
            var resourceId = _context.Resources.GetIdentifier($"technics{imageId}", "drawable", _context.PackageName);
            return BitmapFactory.DecodeResourceAsync(_context.Resources, resourceId);
        }

        ~MediaQueueControl()
        {
            try
            {
                player?.Release();
            }
            catch (Exception e)
            {
                Toast.MakeText(_context, $"~MediaQueueControl.Error: {e}", ToastLength.Long).Show();
            }
        }
    }
}