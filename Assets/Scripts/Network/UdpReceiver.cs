using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TelloViewer
{
    public sealed class UdpReceiver : IDisposable
    {
        private readonly object _stateSync = new object();
        private readonly Action<byte[], int> _onDatagram;
        private readonly Action _onIdle;
        private readonly int _port;
        private readonly int _wakeIntervalMilliseconds;
        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);
        private Thread _thread;
        private Socket _activeSocket;
        private bool _isBound;
        private string _lastError;
        private bool _started;

        public UdpReceiver(int port, int wakeIntervalMilliseconds, Action<byte[], int> onDatagram, Action onIdle)
        {
            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            _port = port;
            _wakeIntervalMilliseconds = Math.Max(1, wakeIntervalMilliseconds);
            _onDatagram = onDatagram ?? throw new ArgumentNullException(nameof(onDatagram));
            _onIdle = onIdle ?? throw new ArgumentNullException(nameof(onIdle));
        }

        public bool IsBound
        {
            get
            {
                lock (_stateSync)
                {
                    return _isBound;
                }
            }
        }

        public string LastError
        {
            get
            {
                lock (_stateSync)
                {
                    return _lastError;
                }
            }
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _stopEvent.Reset();
            _thread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "Tello UDP " + _port
            };
            _thread.Start();
        }

        public bool Stop()
        {
            if (!_started)
            {
                return true;
            }

            _stopEvent.Set();
            Socket socket;
            lock (_stateSync)
            {
                socket = _activeSocket;
                _activeSocket = null;
                _isBound = false;
            }

            if (socket != null)
            {
                socket.Close();
            }

            bool stopped = _thread == null || _thread.Join(1000);
            if (!stopped)
            {
                SetError("UDP受信スレッドが1秒以内に停止しませんでした。");
                return false;
            }

            _started = false;
            _thread = null;
            return true;
        }

        public void Dispose()
        {
            if (Stop())
            {
                _stopEvent.Dispose();
            }
        }

        private void ReceiveLoop()
        {
            byte[] buffer = new byte[65536];
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (!_stopEvent.WaitOne(0))
            {
                Socket socket = null;
                try
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.ReceiveTimeout = _wakeIntervalMilliseconds;
                    socket.Bind(new IPEndPoint(IPAddress.Any, _port));
                    lock (_stateSync)
                    {
                        if (_stopEvent.WaitOne(0))
                        {
                            socket.Close();
                            return;
                        }

                        _activeSocket = socket;
                        _isBound = true;
                        _lastError = null;
                    }

                    while (!_stopEvent.WaitOne(0))
                    {
                        try
                        {
                            int length = socket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref remoteEndPoint);
                            _onDatagram(buffer, length);
                        }
                        catch (SocketException exception)
                        {
                            if (exception.SocketErrorCode == SocketError.TimedOut ||
                                exception.SocketErrorCode == SocketError.WouldBlock)
                            {
                                _onIdle();
                                continue;
                            }

                            if (!_stopEvent.WaitOne(0))
                            {
                                SetError("UDP受信に失敗しました: " + exception.Message);
                            }

                            break;
                        }
                        catch (ObjectDisposedException)
                        {
                            if (!_stopEvent.WaitOne(0))
                            {
                                SetError("受信中にUDPソケットが閉じられました。");
                            }

                            break;
                        }
                        catch (Exception exception)
                        {
                            SetError("UDP受信処理で予期しない例外が発生しました: " + exception);
                            break;
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (!_stopEvent.WaitOne(0))
                    {
                        SetError("UDPポート " + _port + " を待受できません: " + exception.Message);
                    }
                }
                finally
                {
                    lock (_stateSync)
                    {
                        if (_activeSocket == socket)
                        {
                            _activeSocket = null;
                        }

                        _isBound = false;
                    }

                    if (socket != null)
                    {
                        socket.Close();
                    }
                }

                if (!_stopEvent.WaitOne(3000))
                {
                    continue;
                }

                break;
            }
        }

        private void SetError(string message)
        {
            lock (_stateSync)
            {
                _lastError = message;
                _isBound = false;
            }
        }
    }
}
