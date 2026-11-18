using System.Collections.Concurrent;
using System.Net.Sockets;
using Client.MirControls;
using Client.MirScenes;
using C = ClientPackets;


namespace Client.MirNetwork
{
    static class Network
    {
        private static TcpClient _client;
        public static int ConnectAttempt = 0;
        public static int MaxAttempts = 20;
        public static bool ErrorShown;
        public static bool Connected;
        public static long TimeOutTime, TimeConnected, RetryTime = CMain.Time + 5000;

        /// <summary>上一帧实际处理的包数量（调试面板显示用，超过上限说明在积压）。</summary>
        public static int LastPacketsProcessed;

        // 断线提示框超时兜底（10-29）：丢后台没人点「确定」时，到点自动回登录界面（见 Process 顶端检查）
        private static Action _delayedReturnToLogin;
        private static long _delayedReturnToLoginTime;

        private static ConcurrentQueue<Packet> _receiveList;
        private static ConcurrentQueue<Packet> _sendList;

        static byte[] _rawData = new byte[0];
        static readonly byte[] _rawBytes = new byte[8 * 1024];

        //★地址池轮换（10-25）：连接失败自动换 Settings.ServerAddressPool 里的下一个地址；
        //连上后归零（下次连接默认从第一个地址开始）。单地址配置行为与原来完全一致。
        private static int _poolIndex;

        /// <summary>当前应尝试的连接地址（地址池为空时回落 Settings.IPAddress）</summary>
        private static string CurrentAddress
        {
            get
            {
                if (Settings.ServerAddressPool.Count == 0) return Settings.IPAddress;
                return Settings.ServerAddressPool[_poolIndex % Settings.ServerAddressPool.Count];
            }
        }

        /// <summary>本次尝试失败，下次换池中下一个地址（单地址池不轮换）</summary>
        private static void NextPoolAddress()
        {
            if (Settings.ServerAddressPool.Count > 1)
                _poolIndex = (_poolIndex + 1) % Settings.ServerAddressPool.Count;
        }

        public static void Connect()
        {
            if (_client != null)
                Disconnect();

            if (ConnectAttempt >= MaxAttempts)
            {
                if (ErrorShown)
                {
                    return;
                }

                ErrorShown = true;

                MirMessageBox errorBox = new("连接到服务器时出错", MirMessageBoxButtons.Cancel);
                errorBox.CancelButton.Click += (o, e) => Program.Form.Close();
                errorBox.Label.Text = $"已达最大连接尝试次数： {MaxAttempts}" +
                                      $"{Environment.NewLine}请稍后再试或检查您的连接设置";
                if (Settings.ServerAddressPool.Count > 1)
                    errorBox.Label.Text += $"{Environment.NewLine}已尝试以下地址均未成功： {string.Join(", ", Settings.ServerAddressPool)}";
                errorBox.Show();
                return;
            }

            ConnectAttempt++;

            try
            {
                _client = new TcpClient { NoDelay = true };
                _client?.BeginConnect(CurrentAddress, Settings.Port, Connection, null);
            }
            catch (ObjectDisposedException ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
                Disconnect();
            }
            catch (Exception ex)
            {
                //地址池里可能有解析不了的地址（域名失效等）：记日志、换下一个地址，不让客户端卡死
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
                NextPoolAddress();
                Disconnect();
            }
        }

        private static void Connection(IAsyncResult result)
        {
            try
            {
                _client?.EndConnect(result);

                if ((_client != null &&
                    !_client.Connected) ||
                    _client == null)
                {
                    NextPoolAddress();
                    Connect();
                    return;
                }

                //连上了：归零，下次连接默认从池里第一个地址开始
                _poolIndex = 0;

                _receiveList = new ConcurrentQueue<Packet>();
                _sendList = new ConcurrentQueue<Packet>();
                _rawData = new byte[0];

                TimeOutTime = CMain.Time + Settings.TimeOut;
                TimeConnected = CMain.Time;

                BeginReceive();
            }
            catch (SocketException)
            {
                Thread.Sleep(100);
                NextPoolAddress();
                Connect();
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
                Disconnect();
            }
        }

        private static void BeginReceive()
        {
            if (_client == null || !_client.Connected) return;

            try
            {
                _client.Client.BeginReceive(_rawBytes, 0, _rawBytes.Length, SocketFlags.None, ReceiveData, _rawBytes);
            }
            catch
            {
                Disconnect();
            }
        }
        private static void ReceiveData(IAsyncResult result)
        {
            if (_client == null || !_client.Connected) return;

            int dataRead;

            try
            {
                dataRead = _client.Client.EndReceive(result);
            }
            catch
            {
                Disconnect();
                return;
            }

            if (dataRead == 0)
            {
                Disconnect();
                return;
            }

            byte[] rawBytes = result.AsyncState as byte[];

            // ★收包解码必须兜底：Packet.ReceivePacket 碰到坏数据会抛 InvalidDataException，
            // 这里跑在 IOCP 回调线程上，没人接住 = 整个客户端进程被杀
            //（事件日志实锤：10-20 21:39 两次「玩着玩着客户端直接退出」）。
            // 改为记日志 + 断线，客户端回到掉线处理流程，不再闪退。
            try
            {
                byte[] temp = _rawData;
                _rawData = new byte[dataRead + temp.Length];
                Buffer.BlockCopy(temp, 0, _rawData, 0, temp.Length);
                Buffer.BlockCopy(rawBytes, 0, _rawData, temp.Length, dataRead);

                Packet p;
                List<byte> data = new List<byte>();

                while ((p = Packet.ReceivePacket(_rawData, out _rawData)) != null)
                {
                    data.AddRange(p.GetPacketBytes());
                    _receiveList.Enqueue(p);
                }

                CMain.BytesReceived += data.Count;
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
                Disconnect();
                return;
            }

            BeginReceive();
        }

        private static void BeginSend(List<byte> data)
        {
            if (_client == null || !_client.Connected || data.Count == 0) return;
            
            try
            {
                _client.Client.BeginSend(data.ToArray(), 0, data.Count, SocketFlags.None, SendData, null);
            }
            catch
            {
                Disconnect();
            }
        }
        private static void SendData(IAsyncResult result)
        {
            try
            {
                _client.Client.EndSend(result);
            }
            catch
            { }
        }

        public static void Disconnect()
        {
            if (_client == null) return;

            _client?.Close();

            TimeConnected = 0;
            Connected = false;
            _sendList = null;
            _client = null;

            _receiveList = null;
        }

        public static void Process()
        {
            // 断线提示框超时兜底（10-29）：丢后台没人点确定时，超时后自动回登录界面。
            // 放在 Process 最顶端，保证任何场景状态下每帧都会检查。
            if (_delayedReturnToLogin != null && CMain.Time >= _delayedReturnToLoginTime)
            {
                var action = _delayedReturnToLogin;
                _delayedReturnToLogin = null;
                action();
            }

            if (_client == null || !_client.Connected)
            {
                if (Connected)
                {
                    while (_receiveList != null && !_receiveList.IsEmpty)
                    {
                        if (!_receiveList.TryDequeue(out Packet p) || p == null) continue;
                        if (!(p is ServerPackets.Disconnect) && !(p is ServerPackets.ClientVersion)) continue;

                        MirScene.ActiveScene.ProcessPacket(p);
                        _receiveList = null;
                        return;
                    }

                    Disconnect();

                    // 断线不再退出客户端：游戏场景回登录界面重新连，其他场景留在原地即可重连
                    // ★保留「点确定返回」弹窗；丢后台没人点时 10 秒后自动回登录界面（10-29）。
                    //   不做超时的话，停在游戏场景会导致重连后的握手包（S.Connected 只有登录场景
                    //   处理）没人认领，5 秒握手超时→拆链→重连死循环，服务端每 5 秒刷连接日志。
                    GameScene gameScene = MirScene.ActiveScene as GameScene;
                    if (gameScene != null)
                    {
                        bool returned = false;   // 点确定与超时兜底只允许执行一次

                        MirMessageBox box = new MirMessageBox("与服务器的连接中断，点击确定返回登录界面重新连接。");
                        box.OKButton.Click += (o, e) =>
                        {
                            _delayedReturnToLogin = null;
                            if (returned) return;
                            returned = true;
                            gameScene.ReturnToLoginOnDisconnect();
                        };
                        box.Show();

                        _delayedReturnToLogin = () =>
                        {
                            if (returned) return;
                            returned = true;
                            gameScene.ReturnToLoginOnDisconnect();
                            MirMessageBox.Show("与服务器的连接中断，长时间未确认，已自动返回登录界面。");
                        };
                        _delayedReturnToLoginTime = CMain.Time + 10000;
                    }
                    else
                    {
                        MirMessageBox.Show("与服务器的连接中断");
                    }
                    return;
                }
                else if (CMain.Time >= RetryTime)
                {
                    RetryTime = CMain.Time + 5000;
                    Connect();
                }
                return;
            }

            if (!Connected && TimeConnected > 0 && CMain.Time > TimeConnected + 5000)
            {
                //连上了 TCP 但 5 秒内没握手成功：当失败处理，换下一个地址重连
                NextPoolAddress();
                Disconnect();
                Connect();
                return;
            }



            //★包处理限流（10-31）：满屏怪物同帧齐射时，一帧能涌进几百个包，每个包都要线性扫
            //  Objects、建特效、首次引用时还要读图库、播音效 —— 全做完这一帧就是几百毫秒，
            //  下一帧积压更多，形成正反馈直到客户端「卡死」。
            //  这里限死单帧处理量，多出来的顺延到下一帧：最坏只是画面延迟两三帧，不会拖死主线程。
            //    300 包/帧 × 60 帧 = 1.8 万包/秒，正常玩远达不到；登录/切图时大量对象包会被摊到
            //    几帧里，而那段时间载入画面本来就盖着，玩家无感。
            const int maxPacketsPerFrame = 300;
            int packetsThisFrame = 0;

            while (_receiveList != null && !_receiveList.IsEmpty)
            {
                if (packetsThisFrame++ >= maxPacketsPerFrame) break;
                if (!_receiveList.TryDequeue(out Packet p) || p == null) continue;
                //★兜底（11-10）：单个包处理异常绝不允许炸穿整个 Process——
                //  之前 humup.wav 格式异常就是从 ProcessPacket 一路抛到 UpdateEnviroment，
                //  把本帧的 ActiveScene.Process()（角色动作推进/移动）和发包全部跳过。
                //  现在改为记录后继续处理后面的包，坏包只丢自己的处理。
                try
                {
                    MirScene.ActiveScene.ProcessPacket(p);
                }
                catch (Exception ex)
                {
                    File.AppendAllText(@".\Error.txt",
                        $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] 包处理异常（类型 {p.Index}，已跳过该包）：{ex}{Environment.NewLine}");
                }
            }

            LastPacketsProcessed = packetsThisFrame;


            if (CMain.Time > TimeOutTime && _sendList != null && _sendList.IsEmpty)
                _sendList.Enqueue(new C.KeepAlive());

            if (_sendList == null || _sendList.IsEmpty) return;

            TimeOutTime = CMain.Time + Settings.TimeOut;

            List<byte> data = new List<byte>();
            while (!_sendList.IsEmpty)
            {
                if (!_sendList.TryDequeue(out Packet p)) continue;
                data.AddRange(p.GetPacketBytes());
            }

            CMain.BytesSent += data.Count;

            BeginSend(data);
        }
        
        public static void Enqueue(Packet p)
        {
            if (_sendList != null && p != null)
                _sendList.Enqueue(p);
        }

        /// <summary>
        /// 只把发送队列里积压的包（必要时补一个心跳）发出去，**完全不处理接收包**。
        ///
        /// 用于主线程被阻塞、走不到正常 Process() 的场景（典型：微端切图时同步下载 .map）。
        /// 接收本身由异步回调线程完成，socket 不会积压；但心跳是靠 Process() 里的超时判断补发的，
        /// 主线程一停就永远发不出去 —— 服务端 TimeOut（默认 10 秒）收不到任何包就断开，
        /// 这就是「NPC 传送到没下载的地图时掉线」的真正原因。
        ///
        /// 这里刻意不调 Process()：它会把新地图的对象包派发进来，而调用点正卡在 MapChanged
        /// 中间（新地图 CellInfo 还没建好），派发必然错乱。这些包留在队列里，等主循环恢复后
        /// 按原顺序正常处理即可。
        /// </summary>
        internal static void PumpKeepAlive()
        {
            try
            {
                if (_client == null || !_client.Connected || !Connected) return;
                if (_sendList == null) return;

                if (CMain.Time > TimeOutTime && _sendList.IsEmpty)
                    _sendList.Enqueue(new C.KeepAlive());

                if (_sendList.IsEmpty) return;

                TimeOutTime = CMain.Time + Settings.TimeOut;

                List<byte> data = new List<byte>();
                while (!_sendList.IsEmpty)
                {
                    if (!_sendList.TryDequeue(out Packet p)) continue;
                    data.AddRange(p.GetPacketBytes());
                }

                CMain.BytesSent += data.Count;

                BeginSend(data);
            }
            catch
            {
            }
        }
    }
}
