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
        private object m_lock = new object();
        private IWavePlayer m_player;
        private MediaFoundationReader m_player_reader;
        private Action<YoutubePlayer> ExOnPlaybackStopped;

        public YoutubePlayer(Action<YoutubePlayer> ExOnPlaybackStopped)
        {
            this.ExOnPlaybackStopped = ExOnPlaybackStopped;
        }

        public void Play(string videoUrl)
        {
            IWavePlayer temp = null;

            lock (m_lock)
            {
                temp = m_player;
            }

            if (m_player != null)
            {
                Stop();
            }

            lock (m_lock)
            {
                m_player = new WaveOutEvent();
                m_player_reader = new MediaFoundationReader(videoUrl);
                m_player.Init(m_player_reader);
                m_player.PlaybackStopped += OnPlaybackStopped;
                m_player.Play();
            }
        }

        public void Stop()
        {
            lock (m_lock)
            {
                if (m_player == null) return;
                m_player?.Stop();
                m_player_reader?.Dispose();
                m_player?.Dispose();
                m_player = null;
                m_player_reader = null;
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            ExOnPlaybackStopped(this);
        }

        public void Dispose()
        {
            Stop();
        }

        public void Seek(TimeSpan interval)
        {
            lock (m_lock)
            {
                m_player_reader.Position = m_player_reader.WaveFormat.AverageBytesPerSecond * (int)interval.TotalSeconds;
            }
        }
    }
}
