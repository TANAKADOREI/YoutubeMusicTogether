using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace YMTCORE
{
    public class YoutubePlayer : IDisposable
    {
        private readonly YoutubeClient _youtubeClient;
        private IWavePlayer _wavePlayer;
        private MediaFoundationReader _mediaReader;
        private Action<YoutubePlayer> ExOnPlaybackStopped;

        public YoutubePlayer(Action<YoutubePlayer> ExOnPlaybackStopped)
        {
            _youtubeClient = new YoutubeClient();
            this.ExOnPlaybackStopped = ExOnPlaybackStopped;
        }

        public void Play(string videoUrl)
        {
            if (_wavePlayer != null)
            {
                Stop();
            }

            _wavePlayer = new WaveOutEvent();
            _mediaReader = new MediaFoundationReader(videoUrl);
            _wavePlayer.Init(_mediaReader);
            _wavePlayer.PlaybackStopped += OnPlaybackStopped;
            _wavePlayer.Play();
        }

        public void Stop()
        {
            _wavePlayer?.Stop();
            _mediaReader?.Dispose();
            _wavePlayer?.Dispose();

            ExOnPlaybackStopped(this);
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            Stop();
        }

        public void Dispose()
        {
            Stop();
        }

        public void Seek(TimeSpan interval)
        {
            _mediaReader.Position = _mediaReader.WaveFormat.AverageBytesPerSecond * (int)interval.TotalSeconds;
        }
    }
}
