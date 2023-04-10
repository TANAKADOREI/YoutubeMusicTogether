using AngleSharp.Dom;
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
        private string m_url;
        //<this, is clear>
        private Action<YoutubePlayer, bool, string> ExOnPlaybackStopped;

        public YoutubePlayer(Action<YoutubePlayer, bool, string> ExOnPlaybackStopped)
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
                m_url = videoUrl;
                m_player.PlaybackStopped += M_player_PlaybackStopped;
                m_player.Init(m_player_reader);
                m_player.Play();
            }
        }

        private void M_player_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            string url = m_url;
            lock (m_lock)
            {
                RawStop(true);
            }
            ExOnPlaybackStopped(this, true, url);
        }

        public bool IsReady()
        {
            lock (m_lock)
            {
                return m_player == null;
            }
        }

        public float SetVolume(bool up)
        {
            lock (m_lock)
            {
                if (m_player != null)
                {
                    var vol = m_player.Volume + ((up) ? 0.1 : -0.1);
                    if (vol <= 0) m_player.Volume = 0;
                    else if (vol >= 1) m_player.Volume = 1;
                    else throw null;
                }
                return m_player.Volume*100;
            }
        }

        private void RawStop(bool no_callback = false)
        {
            if (m_player == null) return;

            //콜백 제거. 중복처리 방지
            m_player.PlaybackStopped -= M_player_PlaybackStopped;
            m_player?.Stop();

            if (!no_callback)
            {
                bool clear = false;
                clear = m_player_reader.Position >= m_player_reader.Length;
                string url = m_url;

                Task.Run(() =>
                {
                    ExOnPlaybackStopped(this, clear, url);
                });
            }

            m_url = null;
            m_player_reader?.Dispose();
            m_player?.Dispose();

            m_player = null;
            m_player_reader = null;
        }

        //노래가 진행될때 중지 하는 함수
        public void Stop(bool no_MusicEndCallback)
        {
            lock (m_lock)
            {
                RawStop(no_MusicEndCallback);
            }
        }

        public void Dispose()
        {
            Stop(true);
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
