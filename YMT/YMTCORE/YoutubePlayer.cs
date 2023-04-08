using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.ConstrainedExecution;
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
        //<this, is clear>
        private Action<YoutubePlayer, bool> ExOnPlaybackStopped;

        public YoutubePlayer(Action<YoutubePlayer, bool> ExOnPlaybackStopped)
        {
            this.ExOnPlaybackStopped = ExOnPlaybackStopped;
        }

        public void Play(string videoUrl)
        {
            lock (m_lock)
            {
                if (m_player != null)
                {
                    RawStop();
                }
                m_player = new WaveOutEvent();
                m_player_reader = new MediaFoundationReader(videoUrl);
                m_player.PlaybackStopped += M_player_PlaybackStopped;
                m_player.Init(m_player_reader);
                m_player.Play();
            }
        }

        private void M_player_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            RawStop(true);
            ExOnPlaybackStopped(this, true);
        }

        public bool IsReady()
        {
            lock (m_lock)
            {
                return m_player == null;
            }
        }

        private void RawStop(bool no_callback = false)
        {
            if (m_player == null) return;

            m_player.PlaybackStopped -= M_player_PlaybackStopped;
            m_player?.Stop();

            if (!no_callback)
            {
                bool clear = false;
                clear = m_player_reader.Position >= m_player_reader.Length;

                Task.Run(() =>
                {
                    ExOnPlaybackStopped(this, clear);
                });
            }

            m_player_reader?.Dispose();
            m_player?.Dispose();

            m_player = null;
            m_player_reader = null;
        }

        public void Stop()
        {
            lock (m_lock)
            {
                RawStop();
            }
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
