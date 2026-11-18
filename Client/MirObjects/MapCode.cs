using System.Buffers.Binary;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;
using Client.Utils;

namespace Client.MirObjects
{
    public class CellInfo
    {
        public short BackIndex;
        public int BackImage;
        public short MiddleIndex;
        public int MiddleImage;
        public short FrontIndex;
        public int FrontImage;

        public byte DoorIndex;
        public byte DoorOffset;

        public byte FrontAnimationFrame;
        public byte FrontAnimationTick;

        public byte MiddleAnimationFrame;
        public byte MiddleAnimationTick;

        public short TileAnimationImage;
        public short TileAnimationOffset;
        public byte  TileAnimationFrames;

        public byte Light;
        public byte Unknown;
        public List<MapObject> CellObjects;

        public bool FishingCell;

        public void AddObject(MapObject ob)
        {
            if (CellObjects == null) CellObjects = new List<MapObject>();

            //★同一个对象绝不插两次（11-09 重影修复）：格子列表里重复一个实例 = 每帧多画一遍
            if (CellObjects.Contains(ob)) return;

            CellObjects.Insert(0, ob);
            Sort();
        }
        public void RemoveObject(MapObject ob)
        {
            if (CellObjects == null) return;

            CellObjects.Remove(ob);

            if (CellObjects.Count == 0) CellObjects = null;
            else Sort();
        }
        public MapObject FindObject(uint ObjectID)
        {
            return CellObjects.Find(
                delegate(MapObject mo)
            {
                return mo.ObjectID == ObjectID;
            });
        }
        //★重影/残影根治（11-09）
        //  绘制走的是「格子列表」(`CellObjects`)，而推进逻辑走的是「世界列表」(`MapControl.Objects`)。
        //  这两份数据本来应该同步，但 `MapObject.Remove()` 是按 `ob.MapLocation` 去找格子删的：
        //  一旦对象的 MapLocation 和它实际登记的那一格对不上（例如服务端重下发 ObjectMonster 时
        //  `Load(update:true)` 直接改了 MapLocation 却没重新入格），
        //  `RemoveObject` 就**删不掉任何东西** —— 对象从 Objects 里消失、却永久留在那一格的绘制列表里。
        //  它不再被 Process，`DrawLocation`（屏幕坐标）就冻结在最后一帧，于是：
        //    · 玩家一走动，它在屏幕上纹丝不动 → 看起来「一直跟着人物走」；
        //    · 它那一格滚出视野就不再被绘制 → 「走远消失」；
        //    · 走回那一格附近又被画出来 → 「回来又出现」。
        //  这里加一道「必须仍在世界列表里才画」的闸门，把它连同各种重复入格一起挡掉。
        private static int _orphanLogCount;
        internal static bool InWorld(MapObject ob)
        {
            if (MapControl.Objects.Contains(ob)) return true;

            if (_orphanLogCount < 30)
            {
                _orphanLogCount++;
                CMain.SaveError(string.Format(
                    "[OrphanObject] 已不在世界列表、却仍在格子({0},{1})的绘制列表里：#{2} {3} 类型={4} 动作={5} 位置=({6},{7}){8}",
                    ob.MapLocation.X, ob.MapLocation.Y, ob.ObjectID, ob.Name, ob.Race,
                    ob.CurrentAction, ob.CurrentLocation.X, ob.CurrentLocation.Y,
                    _orphanLogCount == 30 ? "  （后续不再记录）" : ""));
            }
            return false;
        }

        public void DrawObjects()
        {
            if (CellObjects == null) return;

            for (int i = 0; i < CellObjects.Count; i++)
            {
                if (!InWorld(CellObjects[i])) continue;

                if (!CellObjects[i].Dead)
                {
                    CellObjects[i].Draw();
                    //★目标高亮（残影修复 11-06）：紧跟在「本体」后面、在同一个格子里画。
                    //  同一深度 → 后画的前景层/树/墙能正常盖住高亮；
                    //  只有真正被画出来的对象才轮到这里 → 视野外、已死的对象不会再凭空冒出影子。
                    CellObjects[i].DrawHighlight();
                    continue;
                }

                if(CellObjects[i].Race == ObjectType.Monster)
                {
                    switch(((MonsterObject)CellObjects[i]).BaseImage)
                    {
                        case Monster.PalaceWallLeft:
                        case Monster.PalaceWall1:
                        case Monster.PalaceWall2:
                        case Monster.SSabukWall1:
                        case Monster.SSabukWall2:
                        case Monster.SSabukWall3:
                        case Monster.HellLord:
                            CellObjects[i].Draw();
                            break;
                        default:
                            continue;
                    }
                }
            }
        }

        public void DrawDeadObjects()
        {
            if (CellObjects == null) return;
            for (int i = 0; i < CellObjects.Count; i++)
            {
                if (!CellObjects[i].Dead) continue;
                if (!InWorld(CellObjects[i])) continue;   // 同上：已离场的不画

                if (CellObjects[i].Race == ObjectType.Monster)
                {
                    switch (((MonsterObject)CellObjects[i]).BaseImage)
                    {
                        case Monster.PalaceWallLeft:
                        case Monster.PalaceWall1:
                        case Monster.PalaceWall2:
                        case Monster.SSabukWall1:
                        case Monster.SSabukWall2:
                        case Monster.SSabukWall3:
                        case Monster.HellLord:
                            continue;
                    }
                }

                CellObjects[i].Draw();
            }
        }

        public void Sort()
        {
            CellObjects.Sort(delegate(MapObject ob1, MapObject ob2)
            {
                if (ob1.Race == ObjectType.Item && ob2.Race != ObjectType.Item)
                    return -1;
                if (ob2.Race == ObjectType.Item && ob1.Race != ObjectType.Item)
                    return 1;
                if (ob1.Race == ObjectType.Spell && ob2.Race != ObjectType.Spell)
                    return -1;
                if (ob2.Race == ObjectType.Spell && ob1.Race != ObjectType.Spell)
                    return 1;

                int i = ob2.Dead.CompareTo(ob1.Dead);
                return i == 0 ? ob1.ObjectID.CompareTo(ob2.ObjectID) : i;
            });
        }
    }

    class MapReader
    {
        public int Width, Height;
        public CellInfo[,] MapCells;
        private readonly string FileName;
        private byte[] Bytes;

        /// <summary>true = 这是一张占位地图（真实 .map 缺失/损坏/解析失败时兜底用）。</summary>
        internal bool IsPlaceholder;
        
        public MapReader(string FileName)
        {
            this.FileName = FileName;

            Initiate();
        }

        /// <summary>合法地图文件的长度下限。正常地图都在几十 KB 以上，低于这个长度一定是半截/空文件。</summary>
        private const int MinMapFileBytes = 1024;

        /// <summary>占位地图边长（真实地图没下来时用它顶上，保证能进游戏）。</summary>
        private const int PlaceholderSize = 1000;

        private void Initiate()
        {
            try
            {
                // 微端：地图文件缺了就进不去，但「干等」会带来两个后果：
                //   1) 主线程停摆 → 心跳发不出去 → 服务端 TimeOut（默认 10 秒）判定掉线；
                //   2) 画面完全静止，玩家以为客户端卡死了。
                // 所以改成：最多等 Settings.MicroMapTimeout（默认 2 秒），一边等一边泵心跳 + 刷新载入画面；
                // 等不到就立刻放行 → 先进游戏 + 占位地图，后台继续下载，到位后无缝重载真实地图。
                if (!File.Exists(FileName))
                    WaitForMapFile();

                //★11-14 长度校验：0 字节 / 半截文件同样等于「没有地图」。
                //  以前直接 ReadAllBytes 再访问 Bytes[2]，短文件就是 IndexOutOfRangeException，
                //  而这条路在 MapChanged 里跑、外层没有 try/catch —— 表现就是「切图闪退」。
                long length = SafeLength(FileName);

                if (length >= 0 && length < MinMapFileBytes)
                {
                    ResourceDownloader.Log("地图文件长度异常（" + length + " 字节），按缺失处理并重新下载：" + FileName);
                    try { File.Delete(FileName); } catch { }
                    ResourceDownloader.ResetFileState(FileName);
                    ResourceDownloader.EnsureLocalFile(FileName);   // 重新排进后台下载
                }

                if (!File.Exists(FileName) || SafeLength(FileName) < MinMapFileBytes)
                {
                    UsePlaceholderMap("地图文件缺失或不可用");
                    return;
                }

                Bytes = File.ReadAllBytes(FileName);

                // 文件到了：不管这次是首次进图还是后台补齐后的重载，都清掉「正在等文件」标记
                PendingMapFile = null;

                ParseBytes();
            }
            catch (Exception ex)
            {
                //★11-14 这条路绝不允许把异常抛出去：MapChanged 外层没有任何 try/catch，
                //  抛一次就是整个客户端退出。地图再坏也只能退回空地图。
                ResourceDownloader.Log("地图加载异常，已退回空地图：" + FileName + " : " + ex);
                UsePlaceholderMap("地图文件解析异常：" + ex.Message);
            }
        }

        /// <summary>按文件头识别地图格式并解析。任何格式解析失败都由 Initiate 统一兜到空地图。</summary>
        private void ParseBytes()
        {
            //c# custom map format
            if ((Bytes[2] == 0x43) && (Bytes[3] == 0x23))
            {
                LoadMapType100();
            }
            //wemade mir3 maps have no title they just start with blank bytes
            else if (Bytes[0] == 0)
            {
                LoadMapType5();
            }
            //shanda mir3 maps start with title: (C) SNDA, MIR3.
            else if ((Bytes[0] == 0x0F) && (Bytes[5] == 0x53) && (Bytes[14] == 0x33))
            {
                LoadMapType6();
            }
            //wemades antihack map (laby maps) title start with: Mir2 AntiHack
            else if ((Bytes[0] == 0x15) && (Bytes[4] == 0x32) && (Bytes[6] == 0x41) && (Bytes[19] == 0x31))
            {
                LoadMapType4();
            }
            //wemades 2010 map format i guess title starts with: Map 2010 Ver 1.0
            else if ((Bytes[0] == 0x10) && (Bytes[2] == 0x61) && (Bytes[7] == 0x31) && (Bytes[14] == 0x31))
            {
                LoadMapType1();
            }
            //shanda's 2012 format and one of shandas(wemades) older formats share same header info, only difference is the filesize
            else if ((Bytes[4] == 0x0F) || (Bytes[4] == 0x03) && (Bytes[18] == 0x0D) && (Bytes[19] == 0x0A))
            {
                int W = Bytes[0] + (Bytes[1] << 8);
                int H = Bytes[2] + (Bytes[3] << 8);
                if (Bytes.Length > (52 + (W * H * 14)))
                    LoadMapType3();
                else
                    LoadMapType2();
            }
            //3/4 heroes map format (myth/lifcos i guess)
            else if ((Bytes[0] == 0x0D) && (Bytes[1] == 0x4C) && (Bytes[7] == 0x20) && (Bytes[11] == 0x6D))
            {
                LoadMapType7();
            }
            else
            {
                //if it's none of the above load the default old school format
                LoadMapType0();
            }

            //★11-14 验收：各 LoadMapTypeN 内部都有 try/catch，失败时会「填一半就返回」。
            //  只要格子表不完整，后面绘制/寻路照样会崩 —— 这里统一退回空地图。
            if (MapCells == null || Width <= 0 || Height <= 0 ||
                MapCells.GetLength(0) != Width || MapCells.GetLength(1) != Height)
            {
                UsePlaceholderMap("地图解析结果无效（宽高或格子表不完整）");
                return;
            }

            SanitizeCellLibraries();
        }

        // ------------------------------------------------------------------
        // ★11-14 空地图兜底 + 图库编号消毒
        // 这两个函数是「缺地图/缺图库也绝不退出客户端」的最后一道防线。
        // ------------------------------------------------------------------

        /// <summary>
        /// 用一张全空地图顶上（1000×1000，所有格子 BackImage/MiddleImage/FrontImage 都是 0）。
        ///
        /// 目的只有一个：**绝不因为缺地图文件让客户端退出**。玩家能留在世界里、
        /// 能收包能走动，后台把 .map 补下来后 GameScene.ReloadFloor 会无缝换回真实地面。
        /// 同时把这张图重新排进下载队列并启动看护线程（重试到上限会结束本次加载）。
        /// </summary>
        private void UsePlaceholderMap(string reason)
        {
            Bytes = null;
            IsPlaceholder = true;

            Width = PlaceholderSize;
            Height = PlaceholderSize;
            MapCells = new CellInfo[Width, Height];

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    MapCells[x, y] = new CellInfo();

            ResourceDownloader.Log("已用空地图兜底（" + reason + "）：" + FileName);

            // 还在微端模式下：确保后台继续补这张图（文件被判定损坏删掉后必须重新入队）
            if (!Settings.MicroClient) return;
            if (ResourceDownloader.IsGivenUp(FileName)) return;

            PendingMapFile = FileName;
            ResourceDownloader.EnsureLocalFile(FileName);
            StartLateReloadWatcher();
        }

        /// <summary>
        /// 地图里记录的是「图库编号」，绘制时直接 `Libraries.MapLibs[i]` 取值。
        /// 微端缺资源 / 清单还没到手时，客户端可能少登记了一批地图图库，那个位置就是 null，
        /// 一取就 NullReferenceException → 客户端直接退出。这里把取不到的编号统一改成 -1
        /// （绘制循环本来就跳过 -1），位置存在但为 null 的补一个空库（它的 Draw 自己会跳过）。
        /// </summary>
        private void SanitizeCellLibraries()
        {
            MLibrary[] libs = Libraries.MapLibs;
            if (libs == null || MapCells == null) return;

            int fixedCount = 0;

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    CellInfo cell = MapCells[x, y];
                    if (cell == null)
                    {
                        MapCells[x, y] = new CellInfo();
                        continue;
                    }

                    cell.BackIndex = SanitizeLibIndex(cell.BackIndex, libs, ref fixedCount);
                    cell.MiddleIndex = SanitizeLibIndex(cell.MiddleIndex, libs, ref fixedCount);
                    cell.FrontIndex = SanitizeLibIndex(cell.FrontIndex, libs, ref fixedCount);
                }

            if (fixedCount > 0)
                ResourceDownloader.Log("地图引用了客户端未登记的图库，已跳过 " + fixedCount + " 处：" + FileName);
        }

        private static short SanitizeLibIndex(short index, MLibrary[] libs, ref int fixedCount)
        {
            if (index < 0) return -1;

            if (index >= libs.Length)
            {
                fixedCount++;
                return -1;      // 越界：绘制循环看到 -1 会跳过这一层
            }

            if (libs[index] == null)
            {
                // 编号合法但这个图库还没登记：补一个指向不存在文件的空库。
                // 不改成 -1 —— 等清单到手、RemoteIndexReady 重建图库后，这里可能就真的有画了。
                libs[index] = new MLibrary(Settings.DataPath + "Map\\Unregistered" + index);
                fixedCount++;
            }

            return index;
        }

        private static long SafeLength(string path)
        {
            try
            {
                return File.Exists(path) ? new FileInfo(path).Length : -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>等 .map 下载就绪。会阻塞主线程，但期间每 100ms 泵一次心跳、刷新一次载入画面。</summary>
        private void WaitForMapFile()
        {
            // 进游戏那一下本来就有载入画面在跑（Armed=true），不能因为这里 Hide 把它关掉，
            // 否则地图下完之后会留一段黑屏。原样恢复：本来就是亮的就继续亮着，交给 GameScene 收尾。
            bool wasArmed = LoadingScreen.Armed;

            LoadingScreen.Show();
            LoadingScreen.SetStatus("正在下载地图 " + Path.GetFileNameWithoutExtension(FileName) + "，请稍候...");

            //★11-14 等待预算固定 2 秒（可在 Mir2Config.ini 的 [MicroClient] MapTimeout 微调，最大 5 秒）。
            //  这个值同时也是这张图的「连接超时」——2 秒拿不到响应头就放弃本次尝试。
            int budget = Settings.MicroMapTimeout;
            if (budget < 500) budget = 2000;

            try
            {
                ResourceDownloader.EnsureLocalFileBlocking(FileName, budget, PumpWhileWaiting);
            }
            finally
            {
                //★11-14 等满预算还没到 → 立刻撤载入画面，让玩家**先进游戏**。
                //  以前这里「文件没到就不撤」，而 GameScene 每帧又把载入画面重新点亮，
                //  于是玩家被永久按在载入画面上，想动也动不了 —— 这就是「切到没下过的图就卡死」。
                if (!File.Exists(FileName))
                {
                    PendingMapFile = FileName;
                    LoadingScreen.Hide();
                }
                else if (!wasArmed)
                {
                    LoadingScreen.Hide();
                }
            }

            if (!File.Exists(FileName))
            {
                PendingMapFile = FileName;
                ResourceDownloader.Log("地图未就绪（已等 " + (budget / 1000.0).ToString("0.#") +
                                       " 秒），先进游戏，后台继续下载：" + FileName);
                StartLateReloadWatcher();
            }
            else
            {
                PendingMapFile = null;
            }
        }

        /// <summary>
        /// 当前正在等文件的 .map 全路径（null = 没有在等）。
        /// MapControl 靠它决定「要不要在补图时提示玩家」。
        /// </summary>
        internal static string PendingMapFile;

        /// <summary>地图文件在空地图兜底后才在后台补齐完成时触发（参数=地图文件全路径）。
        /// 由 MapControl 挂接，在主循环里自动重载地面。</summary>
        internal static Action<string> MapFileArrivedLate;

        /// <summary>
        /// 本次地图加载被放弃时触发（参数：地图文件全路径、原因）。
        ///
        /// ★11-14 满足任意一条就结束本次加载，不再无限等：
        ///   · 下载重试次数达到 Settings.MicroMaxRetry 上限；
        ///   · 服务端明确回 404（这张图服务端确实没有）；
        ///   · 看护线程盯满 10 分钟仍然是空的。
        /// 由 GameScene 挂接：撤掉载入画面 + 提示玩家。地图仍留在空地图兜底上，**不会退出客户端**。
        /// 回调在后台线程触发，实现方必须自己切回主线程。
        /// </summary>
        internal static Action<string, string> MapLoadAbandoned;

        /// <summary>已在盯着的地图文件，防止重复启动看护线程。</summary>
        private static readonly HashSet<string> _watchers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 后台看护线程：每 500ms 查一次地图文件到了没有；没到就顺手调一次
        /// EnsureLocalFile（偶发失败冷却到期后自动重新入队）。
        /// 三种收尾：文件到了 → 通知重载；重试次数用完 → 结束本次加载；盯满 10 分钟 → 同样结束。
        /// </summary>
        private void StartLateReloadWatcher()
        {
            string file = FileName;

            lock (_watchers)
            {
                if (!_watchers.Add(file)) return;   // 同一张图只盯一份，避免多个线程重复触发
            }

            var thread = new System.Threading.Thread(delegate()
            {
                try
                {
                    DateTime deadline = DateTime.UtcNow.AddMinutes(10);

                    while (DateTime.UtcNow < deadline)
                    {
                        System.Threading.Thread.Sleep(500);

                        // 文件长度不对（0 字节 / 半截）等于没下到：删掉重下，别当成「已就绪」
                        long len = SafeLength(file);
                        if (len >= 0 && len < MinMapFileBytes)
                        {
                            ResourceDownloader.Log("地图文件长度异常（" + len + " 字节），删除后重新下载：" + file);
                            try { File.Delete(file); } catch { }
                            ResourceDownloader.ResetFileState(file);
                            len = -1;
                        }

                        if (len >= MinMapFileBytes)
                        {
                            ResourceDownloader.Log("地图文件后台补齐完成：" + file);

                            var handler = MapFileArrivedLate;
                            if (handler != null)
                            {
                                try { handler(file); } catch { }
                            }
                            return;
                        }

                        //★11-14 重试到上限（或服务端 404）：结束本次加载，别再让玩家无限等
                        if (ResourceDownloader.IsGivenUp(file))
                        {
                            AbandonMapLoad("已重试 " + ResourceDownloader.GetAttemptCount(file) + " 次仍未成功");
                            return;
                        }

                        // 冷却期过后自动重新入队（断线/服务器重启等偶发失败的自愈）
                        ResourceDownloader.EnsureLocalFile(file);
                    }

                    AbandonMapLoad("等待超过 10 分钟仍未能下载");
                }
                catch
                {
                }
                finally
                {
                    lock (_watchers) _watchers.Remove(file);
                }
            })
            { IsBackground = true, Name = "MapLateReload" };

            thread.Start();
        }

        /// <summary>
        /// 结束本次地图加载：清掉「正在等文件」标记，并通知场景收尾（撤载入画面 + 提示）。
        /// 地图本身仍留在占位兜底上 —— **不会因为缺图把玩家踢出游戏**。
        /// </summary>
        private void AbandonMapLoad(string reason)
        {
            if (string.Equals(PendingMapFile, FileName, StringComparison.OrdinalIgnoreCase))
                PendingMapFile = null;

            ResourceDownloader.Log("结束本次地图加载（" + reason + "）：" + FileName);

            var handler = MapLoadAbandoned;
            if (handler != null)
            {
                try { handler(FileName, reason); } catch { }
            }
        }

        /// <summary>
        /// 阻塞等待期间的回调（每 100ms 一次）：推进时间基准 → 泵心跳 → 刷新进度文字和画面。
        /// </summary>
        private void PumpWhileWaiting()
        {
            // 主循环停了，CMain.Time 不会自己往前走；心跳判定完全依赖它，必须手动推进。
            CMain.Time = CMain.Timer.ElapsedMilliseconds;

            // ★只发不收。接收由异步线程完成，socket 不会积压；
            //   这里若走 Network.Process()，会把新地图的对象包派发给正卡在 MapChanged 中间的客户端，
            //   而新地图的格子数据还没建好 —— 内存越界、对象错位的经典来源。
            Network.PumpKeepAlive();

            string relative = ResourceDownloader.ToRelativePath(FileName);
            long bytes = relative != null &&
                         string.Equals(ResourceDownloader.ActiveFile, relative, StringComparison.OrdinalIgnoreCase)
                ? ResourceDownloader.ActiveBytes
                : 0;

            string name = Path.GetFileNameWithoutExtension(FileName);
            LoadingScreen.SetStatus(bytes > 0
                ? string.Format("正在下载地图 {0}（已收到 {1:0.0} MB），请稍候...", name, bytes / 1048576.0)
                : string.Format("正在下载地图 {0}，请稍候...", name));

            CMain.RenderLoadingFrame();
        }

        private void LoadMapType0()
        {
            try
            {
                int offset = 0;
                Width = BitConverter.ToInt16(Bytes, offset);
                offset += 2;
                Height = BitConverter.ToInt16(Bytes, offset);
                MapCells = new CellInfo[Width, Height];
                offset = 52;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {//12
                        MapCells[x, y] = new CellInfo();
                        MapCells[x, y].BackIndex = 0;
                        MapCells[x, y].MiddleIndex = 1;
                        MapCells[x, y].BackImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].MiddleImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].FrontImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].DoorIndex = (byte)(Bytes[offset++] & 0x7F);
                        MapCells[x, y].DoorOffset = Bytes[offset++];
                        MapCells[x, y].FrontAnimationFrame = Bytes[offset++];
                        MapCells[x, y].FrontAnimationTick = Bytes[offset++];
                        MapCells[x, y].FrontIndex = (short)(Bytes[offset++]+ 2);
                        MapCells[x, y].Light = Bytes[offset++];
                        if ((MapCells[x, y].BackImage & 0x8000) != 0)
                            MapCells[x, y].BackImage = (MapCells[x, y].BackImage & 0x7FFF) | 0x20000000;

                        if (MapCells[x, y].Light >= 100 && MapCells[x, y].Light <= 119)
                            MapCells[x, y].FishingCell = true;


                    }
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
            }

        }

        private void LoadMapType1()
        {
            try
            {
                ReadOnlySpan<byte> span = Bytes.AsSpan();

                int initialOffset = 21;

                short w = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(initialOffset, 2));
                initialOffset += 2;

                short xor = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(initialOffset, 2));
                initialOffset += 2;

                short h = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(initialOffset, 2));
                initialOffset += 2;

                Width = (w ^ xor);
                Height = (h ^ xor);
                MapCells = new CellInfo[Width, Height];

                int offSet = 54;
                int mapSize = Width * Height;
                int cellSize = 15;

                ReadOnlySpan<byte> mapSpan = span.Slice(offSet, mapSize * cellSize);

                const int BackImageXor = unchecked((int)0xAA38AA38);

                for (int i = 0; i < mapSize; i++)
                {
                    int x = i / Height;
                    int y = i % Height;

                    int cellOffset = i * cellSize;

                    int backImageRaw = BinaryPrimitives.ReadInt32LittleEndian(mapSpan.Slice(cellOffset, 4));
                    int backImage = backImageRaw ^ BackImageXor;
                    cellOffset += 4;

                    short middleImage = (short)(BinaryPrimitives.ReadInt16LittleEndian(mapSpan.Slice(cellOffset, 2)) ^ xor);
                    cellOffset += 2;

                    short frontImage = (short)(BinaryPrimitives.ReadInt16LittleEndian(mapSpan.Slice(cellOffset, 2)) ^ xor);
                    cellOffset += 2;

                    byte doorIndex = (byte)(mapSpan[cellOffset] & 0x7F);
                    cellOffset += 1;

                    byte doorOffset = mapSpan[cellOffset];
                    cellOffset += 1;

                    byte frontAnimationFrame = mapSpan[cellOffset];
                    cellOffset += 1;

                    byte frontAnimationTick = mapSpan[cellOffset];
                    cellOffset += 1;

                    short frontIndex = (short)(mapSpan[cellOffset] + 2);
                    cellOffset += 1;

                    byte light = mapSpan[cellOffset];
                    cellOffset += 1;

                    byte unknown = mapSpan[cellOffset];
                    cellOffset += 1;

                    var cell = new CellInfo
                    {
                        BackIndex = 0,
                        BackImage = backImage,
                        MiddleIndex = 1,
                        MiddleImage = middleImage,
                        FrontImage = frontImage,
                        DoorIndex = doorIndex,
                        DoorOffset = doorOffset,
                        FrontAnimationFrame = frontAnimationFrame,
                        FrontAnimationTick = frontAnimationTick,
                        FrontIndex = frontIndex,
                        Light = light,
                        Unknown = unknown
                    };

                    if (cell.FrontIndex == 102)
                        cell.FrontIndex = 90;

                    if (cell.FrontIndex >= 255)
                        cell.FrontIndex = -1;

                    if (cell.Light >= 100 && cell.Light <= 119)
                        cell.FishingCell = true;

                    MapCells[x, y] = cell;
                }
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
            }
        }

        private void LoadMapType2()
        {
            try
            {
                int offset = 0;
                Width = BitConverter.ToInt16(Bytes, offset);
                offset += 2;
                Height = BitConverter.ToInt16(Bytes, offset);
                MapCells = new CellInfo[Width, Height];
                offset = 52;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {//14
                        MapCells[x, y] = new CellInfo();
                        MapCells[x, y].BackImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].MiddleImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].FrontImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].DoorIndex = (byte)(Bytes[offset++] & 0x7F);
                        MapCells[x, y].DoorOffset = Bytes[offset++];
                        MapCells[x, y].FrontAnimationFrame = Bytes[offset++];
                        MapCells[x, y].FrontAnimationTick = Bytes[offset++];
                        MapCells[x, y].FrontIndex = (short)(Bytes[offset++] + 120);
                        MapCells[x, y].Light = Bytes[offset++];
                        MapCells[x, y].BackIndex = (short)(Bytes[offset++] + 100);
                        MapCells[x, y].MiddleIndex = (short)(Bytes[offset++] + 110);
                        if ((MapCells[x, y].BackImage & 0x8000) != 0)
                            MapCells[x, y].BackImage = (MapCells[x, y].BackImage & 0x7FFF) | 0x20000000;

                        if (MapCells[x, y].Light >= 100 && MapCells[x, y].Light <= 119)
                            MapCells[x, y].FishingCell = true;
                    }
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
            }

        }

        private void LoadMapType3()
        {
            try
            {
                int offset = 0;
                Width = BitConverter.ToInt16(Bytes, offset);
                offset += 2;
                Height = BitConverter.ToInt16(Bytes, offset);
                MapCells = new CellInfo[Width, Height];
                offset = 52;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {//36
                        MapCells[x, y] = new CellInfo();
                        MapCells[x, y].BackImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].MiddleImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].FrontImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].DoorIndex = (byte)(Bytes[offset++] & 0x7F);
                        MapCells[x, y].DoorOffset = Bytes[offset++];
                        MapCells[x, y].FrontAnimationFrame = Bytes[offset++];
                        MapCells[x, y].FrontAnimationTick = Bytes[offset++];
                        MapCells[x, y].FrontIndex = (short)(Bytes[offset++] + 120);
                        MapCells[x, y].Light = Bytes[offset++];
                        MapCells[x, y].BackIndex = (short)(Bytes[offset++] + 100);
                        MapCells[x, y].MiddleIndex = (short)(Bytes[offset++] + 110);
                        MapCells[x, y].TileAnimationImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 7;//2bytes from tileanimframe, 2 bytes always blank?, 2bytes potentialy 'backtiles index', 1byte fileindex for the backtiles?
                        MapCells[x, y].TileAnimationFrames = Bytes[offset++];
                        MapCells[x, y].TileAnimationOffset = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 14; //tons of light, blending, .. related options i hope
                        if ((MapCells[x, y].BackImage & 0x8000) != 0)
                            MapCells[x, y].BackImage = (MapCells[x, y].BackImage & 0x7FFF) | 0x20000000;

                        if (MapCells[x, y].Light >= 100 && MapCells[x, y].Light <= 119)
                            MapCells[x, y].FishingCell = true;
                    }

            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
            }
        }

        private void LoadMapType4()
        {
            try
            {
                int offset = 31;
                int w = BitConverter.ToInt16(Bytes, offset);
                offset += 2;
                int xor = BitConverter.ToInt16(Bytes, offset);
                offset += 2;
                int h = BitConverter.ToInt16(Bytes, offset);
                Width = w ^ xor;
                Height = h ^ xor;
                MapCells = new CellInfo[Width, Height];
                offset = 64;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {//12
                        MapCells[x, y] = new CellInfo();
                        MapCells[x, y].BackIndex = 0;
                        MapCells[x, y].MiddleIndex = 1;
                        MapCells[x, y].BackImage = (short)(BitConverter.ToInt16(Bytes, offset) ^ xor);
                        offset += 2;
                        MapCells[x, y].MiddleImage = (short)(BitConverter.ToInt16(Bytes, offset) ^ xor);
                        offset += 2;
                        MapCells[x, y].FrontImage = (short)(BitConverter.ToInt16(Bytes, offset) ^xor);
                        offset += 2;
                        MapCells[x, y].DoorIndex = (byte)(Bytes[offset++] & 0x7F);
                        MapCells[x, y].DoorOffset = Bytes[offset++];
                        MapCells[x, y].FrontAnimationFrame = Bytes[offset++];
                        MapCells[x, y].FrontAnimationTick = Bytes[offset++];
                        MapCells[x, y].FrontIndex = (short)(Bytes[offset++] + 2);
                        MapCells[x, y].Light = Bytes[offset++];
                        if ((MapCells[x, y].BackImage & 0x8000) != 0)
                            MapCells[x, y].BackImage = (MapCells[x, y].BackImage & 0x7FFF) | 0x20000000;

                        if (MapCells[x, y].Light >= 100 && MapCells[x, y].Light <= 119)
                            MapCells[x, y].FishingCell = true;
                    }
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
            }
        }

        private void LoadMapType5()
        {
            try
            {
                byte flag = 0;
                int offset = 20;
                short Attribute = (short)(BitConverter.ToInt16(Bytes,offset));
                Width = (int)(BitConverter.ToInt16(Bytes,offset+=2));
                Height = (int)(BitConverter.ToInt16(Bytes, offset += 2));
                //ignoring eventfile and fogcolor for now (seems unused in maps i checked)
                offset = 28;
                //initiate all cells
                MapCells = new CellInfo[Width, Height];
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        MapCells[x, y] = new CellInfo();
                //read all back tiles
                for (int x = 0; x < (Width/2); x++)
                    for (int y = 0; y < (Height/2); y++)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            MapCells[(x * 2) + (i % 2), (y * 2) + (i / 2)].BackIndex = (short)(Bytes[offset] != 255? Bytes[offset]+200 : -1);
                            MapCells[(x*2) + (i % 2), (y*2) + (i / 2)].BackImage = (int)(BitConverter.ToUInt16(Bytes, offset + 1)+1);
                        }
                        offset += 3;
                    }
                //read rest of data
                offset = 28 + (3 * ((Width /2) + (Width %2)) * (Height / 2));
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {
                        
                        flag = Bytes[offset++];
                        MapCells[x, y].MiddleAnimationFrame = Bytes[offset++];

                        MapCells[x, y].FrontAnimationFrame = Bytes[offset] == 255? (byte)0 : Bytes[offset];
                        MapCells[x, y].FrontAnimationFrame &= 0x8F;
                        offset++;
                        MapCells[x, y].MiddleAnimationTick = 0;
                        MapCells[x, y].FrontAnimationTick = 0;
                        MapCells[x,y].FrontIndex = (short)(Bytes[offset] != 255 ? Bytes[offset] + 200 : -1);
                        offset++;
                        MapCells[x,y].MiddleIndex = (short)(Bytes[offset] != 255 ? Bytes[offset] + 200 : -1);
                        offset++;
                        MapCells[x,y].MiddleImage = (ushort)(BitConverter.ToUInt16(Bytes,offset)+1);
                        offset += 2;
                        MapCells[x, y].FrontImage = (ushort)(BitConverter.ToUInt16(Bytes, offset)+1);
                        if ((MapCells[x, y].FrontImage == 1) && (MapCells[x, y].FrontIndex == 200))
                            MapCells[x, y].FrontIndex = -1;
                        offset += 2;
                        offset += 3;//mir3 maps dont have doors so dont bother reading the info
                        MapCells[x, y].Light = (byte)(Bytes[offset] & 0x0F);
                        offset += 2;
                        if ((flag & 0x01) != 1) MapCells[x, y].BackImage |= 0x20000000;
                        if ((flag & 0x02) != 2) MapCells[x, y].FrontImage = (ushort)((UInt16)MapCells[x, y].FrontImage | 0x8000);

                        if (MapCells[x, y].Light >= 100 && MapCells[x, y].Light <= 119)
                            MapCells[x, y].FishingCell = true;
                        else
                            MapCells[x, y].Light *= 2;//expand general mir3 lighting as default range is small. Might break new colour lights.
                    }
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
            }
        }

        private void LoadMapType6()
        {
            try
            {
                byte flag = 0;
                int offset = 16;
                Width = BitConverter.ToInt16(Bytes, offset);
                offset += 2;
                Height = BitConverter.ToInt16(Bytes, offset);
                MapCells = new CellInfo[Width, Height];
                offset = 40;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {
                        MapCells[x, y] = new CellInfo();
                        flag = Bytes[offset++];
                        MapCells[x,y].BackIndex = (short)(Bytes[offset] != 255 ? Bytes[offset]+ 300 : -1);
                        offset++;
                        MapCells[x, y].MiddleIndex = (short)(Bytes[offset] != 255 ? Bytes[offset] + 300 : -1);
                        offset++;
                        MapCells[x, y].FrontIndex = (short)(Bytes[offset] != 255 ? Bytes[offset] + 300 : -1);
                        offset++;
                        MapCells[x, y].BackImage = (short)(BitConverter.ToInt16(Bytes, offset) + 1);
                        offset += 2;
                        MapCells[x, y].MiddleImage = (short)(BitConverter.ToInt16(Bytes, offset) + 1);
                        offset += 2;
                        MapCells[x, y].FrontImage = (short)(BitConverter.ToInt16(Bytes, offset) + 1);
                        offset += 2;
                        if ((MapCells[x, y].FrontImage == 1) && (MapCells[x, y].FrontIndex == 200))
                            MapCells[x, y].FrontIndex = -1;
                        MapCells[x, y].MiddleAnimationFrame = Bytes[offset++];
                        MapCells[x, y].FrontAnimationFrame = Bytes[offset] == 255 ? (byte)0 : Bytes[offset];
                        if (MapCells[x, y].FrontAnimationFrame > 0x0F)//assuming shanda used same value not sure
                            MapCells[x, y].FrontAnimationFrame = (byte)(/*0x80 ^*/ (MapCells[x, y].FrontAnimationFrame & 0x0F));
                        offset++;
                        MapCells[x, y].MiddleAnimationTick = 1;
                        MapCells[x, y].FrontAnimationTick = 1;
                        MapCells[x, y].Light = (byte)(Bytes[offset] & 0x0F);
                        MapCells[x, y].Light *= 4;//far wants all light on mir3 maps to be maxed :p
                        offset += 8;
                        if ((flag & 0x01) != 1) MapCells[x, y].BackImage |= 0x20000000;
                        if ((flag & 0x02) != 2) MapCells[x, y].FrontImage = (short)((UInt16)MapCells[x, y].FrontImage | 0x8000);

                    }
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
            }

        }

        private void LoadMapType7()
        {
            try
            {
                int offset = 21;
                Width = BitConverter.ToInt16(Bytes, offset);
                offset += 4;
                Height = BitConverter.ToInt16(Bytes, offset);
                MapCells = new CellInfo[Width, Height];

                offset = 54;

                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {//total 15
                        MapCells[x, y] = new CellInfo
                        {
                            BackIndex = 0,
                            BackImage = (int)BitConverter.ToInt32(Bytes, offset),
                            MiddleIndex = 1,
                            MiddleImage = (short)BitConverter.ToInt16(Bytes, offset += 4),
                            FrontImage = (short)BitConverter.ToInt16(Bytes, offset += 2),
                            DoorIndex = (byte)(Bytes[offset += 2] & 0x7F),
                            DoorOffset = Bytes[++offset],
                            FrontAnimationFrame = Bytes[++offset],
                            FrontAnimationTick = Bytes[++offset],
                            FrontIndex = (short)(Bytes[++offset] + 2),
                            Light = Bytes[++offset],
                            Unknown = Bytes[++offset],
                        };
                        if ((MapCells[x, y].BackImage & 0x8000) != 0)
                            MapCells[x, y].BackImage = (MapCells[x, y].BackImage & 0x7FFF) | 0x20000000;
                        offset++;

                        if (MapCells[x, y].Light >= 100 && MapCells[x, y].Light <= 119)
                            MapCells[x, y].FishingCell = true;
                    }
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
            }
        }

        private void LoadMapType100()
        {
            try 
            { 
                int offset = 4;
                if ((Bytes[0]!= 1) || (Bytes[1] != 0)) return;//only support version 1 atm
                Width = BitConverter.ToInt16(Bytes, offset);
                offset += 2;
                Height = BitConverter.ToInt16(Bytes, offset);
                MapCells = new CellInfo[Width, Height];
                offset = 8;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {
                        MapCells[x, y] = new CellInfo();
                        MapCells[x, y].BackIndex = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].BackImage = (int)BitConverter.ToInt32(Bytes, offset);
                        offset += 4;
                        MapCells[x, y].MiddleIndex = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].MiddleImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].FrontIndex = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].FrontImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].DoorIndex = (byte)(Bytes[offset++] & 0x7F);
                        MapCells[x, y].DoorOffset = Bytes[offset++];
                        MapCells[x, y].FrontAnimationFrame = Bytes[offset++];
                        MapCells[x, y].FrontAnimationTick = Bytes[offset++];
                        MapCells[x, y].MiddleAnimationFrame = Bytes[offset++];
                        MapCells[x, y].MiddleAnimationTick = Bytes[offset++];
                        MapCells[x, y].TileAnimationImage = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].TileAnimationOffset = (short)BitConverter.ToInt16(Bytes, offset);
                        offset += 2;
                        MapCells[x, y].TileAnimationFrames = Bytes[offset++];
                        MapCells[x, y].Light = Bytes[offset++];

                        if (MapCells[x, y].Light >= 100 && MapCells[x, y].Light <= 119)
                            MapCells[x, y].FishingCell = true;
                    }
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
            }
        }

    }

}
