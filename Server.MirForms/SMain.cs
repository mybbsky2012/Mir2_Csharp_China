using CustomFormControl;
using Server.Account;
using Server.Database;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirForms.Systems;
using Server.MirObjects;
using Server.Systems;

namespace Server
{
    public partial class SMain : Form
    {
        public static Envir Envir => Envir.Main;

        public static Envir EditEnvir => Envir.Edit;

        protected static MessageQueue MessageQueue => MessageQueue.Instance;

        public SMain()
        {
            InitializeComponent();

            AutoResize();

            // ★状态列（10-25）：按单元格上色需要 OwnerDraw（WinForms ListView 不支持单独给某一列变色）
            PlayersOnlineListView.OwnerDraw = true;
            PlayersOnlineListView.DrawColumnHeader += PlayersOnlineListView_DrawColumnHeader;
            PlayersOnlineListView.DrawSubItem += PlayersOnlineListView_DrawSubItem;

            // ★在线列表改虚拟模式（20261106）：数据源在 _onlineSnapshot，控件只记行数，
            //   滚到哪才向 RetrieveVirtualItem 要哪几行 —— 2 万在线也不会全量建行卡 UI。
            //   注意：虚拟模式不支持控件排序（Sorting 必须 None、点表头 Sort() 会抛异常），
            //   行顺序由快照自己保证（按角色 Index 升序）。
            PlayersOnlineListView.Sorting = SortOrder.None;
            PlayersOnlineListView.VirtualMode = true;
            PlayersOnlineListView.RetrieveVirtualItem += PlayersOnlineListView_RetrieveVirtualItem;
        }

        private void AutoResize()
        {
            int columnCount = PlayersOnlineListView.Columns.Count;

            foreach (ColumnHeader column in PlayersOnlineListView.Columns)
            {
                column.Width = PlayersOnlineListView.Width / (columnCount - 1) - 1;
            }

            indexHeader.Width = 2;
        }

        public static void Enqueue(Exception ex)
        {
            MessageQueue.Enqueue(ex);
        }

        public static void EnqueueDebugging(string msg)
        {
            MessageQueue.EnqueueDebugging(msg);
        }

        public static void EnqueueChat(string msg)
        {
            MessageQueue.EnqueueChat(msg);
        }

        public static void Enqueue(string msg)
        {
            MessageQueue.Enqueue(msg);
        }

        private void configToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void InterfaceTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                Text = $"总计: {Envir.LastCount}, 实计: {Envir.LastRealCount}";
                PlayersLabel.Text = $"在线角色: {Envir.Players.Count}";
                MonsterLabel.Text = $"刷新怪物: {Envir.MonsterCount}";
                ConnectionsLabel.Text = $"连线登录: {Envir.Connections.Count}";
                BlockedIPsLabel.Text = $"屏蔽锁定 IPs: {Envir.IPBlocks.Count(x => x.Value > Envir.Now)}";
                UpTimeLabel.Text = $"运行时间: {Envir.Stopwatch.ElapsedMilliseconds / 1000 / 60 / 60 / 24}日:{Envir.Stopwatch.ElapsedMilliseconds / 1000 / 60 / 60 % 24}小时:{Envir.Stopwatch.ElapsedMilliseconds / 1000 / 60 % 60}分钟:{Envir.Stopwatch.ElapsedMilliseconds / 1000 % 60}秒";

                if (Settings.Multithreaded && (Envir.MobThreads != null))
                {
                    CycleDelayLabel.Text = $"延迟周期: {Envir.LastRunTime:0000}";
                    for (int i = 0; i < Envir.MobThreads.Length; i++)
                    {
                        if (Envir.MobThreads[i] == null) break;
                        CycleDelayLabel.Text = CycleDelayLabel.Text + $"|{Envir.MobThreads[i].LastRunTime:0000}";

                    }
                }
                else
                    CycleDelayLabel.Text = $"延迟周期: {Envir.LastRunTime}";

                while (!MessageQueue.MessageLog.IsEmpty)
                {
                    string message;

                    if (!MessageQueue.MessageLog.TryDequeue(out message)) continue;

                    AppendLog(LogTextBox, message);
                }

                while (!MessageQueue.DebugLog.IsEmpty)
                {
                    string message;

                    if (!MessageQueue.DebugLog.TryDequeue(out message)) continue;

                    AppendLog(DebugLogTextBox, message);
                }

                while (!MessageQueue.ChatLog.IsEmpty)
                {
                    string message;

                    if (!MessageQueue.ChatLog.TryDequeue(out message)) continue;

                    AppendLog(ChatLogTextBox, message);
                }

                ProcessPlayersOnlineTab(false);
                ProcessGuildViewTab(false);
            }
            catch (Exception ex)
            {
                // ★11-16：定时器 100ms 一拍。一旦某处持续抛异常，模态框自己的消息循环
                //   还在派发 Timer → 弹窗一个接一个堆叠，界面彻底锁死。
                //   同一条异常 30 秒内只弹一次，其余只写日志。
                ReportUiError(ex);
            }
        }

        private static string _lastUiError;
        private static long _lastUiErrorTick;

        /// <summary>界面刷新异常：同一条 30 秒内只弹一次，避免模态框堵塞消息循环</summary>
        private static void ReportUiError(Exception ex)
        {
            string msg = ex?.ToString() ?? "未知错误";
            long now = Environment.TickCount64;

            if (string.Equals(msg, _lastUiError, StringComparison.Ordinal) && now - _lastUiErrorTick < 30000)
                return;

            _lastUiError = msg;
            _lastUiErrorTick = now;

            try { Enqueue("界面刷新异常：" + ex.Message); } catch { }

            MessageBox.Show(msg, "服务器错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>在线状态文本：假人 → 挂机 → 在线（顺序即优先级，假人永不误判为挂机）</summary>
        private static string GetStatusText(PlayerObject player)
        {
            if (player is FakePlayerObject) return "假人";
            if (player.OfflineAFK) return "挂机";
            return "在线";
        }

        private ListViewItem CreateListView(PlayerObject player)
        {
            CharacterInfo character = player?.Info;

            // 玩家正在登录/下线时 Info 可能是空的，别让整个在线列表刷新炸掉
            if (character == null) return CreatePlaceholderItem();

            ListViewItem ListItem = new ListViewItem(character.Index.ToString()) { Tag = character };

            ListItem.SubItems.Add(character.Name);
            ListItem.SubItems.Add(character.Level.ToString());
            ListItem.SubItems.Add(character.Class.ToString());
            ListItem.SubItems.Add(character.Gender.ToString());
            ListItem.SubItems.Add(GetStatusText(player));

            return EnsureItemColumns(ListItem);
        }

        /// <summary>
        /// ★虚拟模式的占位行（11-16）：子项数必须**等于**控件列数。
        /// 以前这里直接 `new ListViewItem(string.Empty)` —— 它只有 1 个子项，而在线列表有 6 列，
        /// WinForms 在 VirtualMode 下会当场抛
        /// 「处于 VirtualMode 时，ListView RetrieveVirtualListItem 事件需要每个 ListView 列的列表视图子项」，
        /// 表现就是服务端每 0.7 秒弹一次「服务器错误」。
        /// 触发点：玩家下线把 Info 置空的那一刻，正好刷到这一行 → CreateListView 返回 null → 走兜底。
        /// </summary>
        private ListViewItem CreatePlaceholderItem()
        {
            ListViewItem item = new ListViewItem(string.Empty);

            int columns = PlayersOnlineListView.Columns.Count;

            while (item.SubItems.Count < columns)
                item.SubItems.Add(string.Empty);

            return item;
        }

        /// <summary>把子项补齐到列数（列数以后再加也不用改这里）</summary>
        private ListViewItem EnsureItemColumns(ListViewItem item)
        {
            if (item == null) return CreatePlaceholderItem();

            int columns = PlayersOnlineListView.Columns.Count;

            while (item.SubItems.Count < columns)
                item.SubItems.Add(string.Empty);

            return item;
        }

        private void PlayersOnlineListView_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;   // 表头走系统默认绘制
        }

        private void PlayersOnlineListView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            const int StatusColumn = 5;   // 状态列下标（序号/名称/等级/职业/性别/状态）

            // 背景：选中行高亮，其余白底
            Color back = e.Item.Selected ? SystemColors.Highlight : SystemColors.Window;
            using (var b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);

            // 文字颜色：状态列按内容上色，其他列选中行用高亮白字
            string text = e.SubItem?.Text ?? string.Empty;
            Color fore;
            if (e.ColumnIndex == StatusColumn)
            {
                switch (text)
                {
                    case "挂机": fore = Color.FromArgb(200, 110, 0); break;   // 橙
                    case "假人": fore = Color.FromArgb(120, 120, 120); break; // 灰
                    case "在线": fore = Color.FromArgb(0, 128, 0); break;     // 绿
                    default: fore = SystemColors.WindowText; break;
                }
            }
            else
                fore = e.Item.Selected ? SystemColors.HighlightText : SystemColors.WindowText;

            var rect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, Math.Max(e.Bounds.Width - 6, 0), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, Font, rect, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // 网格线（OwnerDraw 下自绘，保证与原样式一致）
            using (var pen = new Pen(Color.FromArgb(240, 240, 240)))
            {
                e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
            }
        }

        private void AppendLog(TextBox box, string message)
        {
            box.AppendText(message);

            // 防止长期运行日志无限增长：超过 3 万字符时修剪掉老的一半
            if (box.TextLength <= 30000) return;

            int cut = box.TextLength / 2;
            string text = box.Text;
            while (cut < text.Length && text[cut] != '\n') cut++;
            if (cut < text.Length) cut++;
            box.Text = text.Substring(Math.Min(cut, text.Length));
        }

        // ★在线列表刷新节流（11-04）：InterfaceTimer 是 100ms 一拍，原来每拍都调用本方法，
        //   300 个假人在线时等于每秒把 300 行删光重建 10 次 —— 列表一直在闪、滚动条一直被顶回去，
        //   看起来就是「在线列表自己在循环刷新」。现在非强制时最多 0.7 秒刷一次。
        private const int OnlineRefreshMs = 700;
        private long _lastOnlineRefresh;

        // ★虚拟模式数据源（20261106）：与列表行一一对应的在线玩家快照（已按角色 Index 升序）。
        //   RetrieveVirtualItem / 双击取行都从这里拿，控件里不再常驻任何 ListViewItem。
        private List<PlayerObject> _onlineSnapshot = new List<PlayerObject>();
        private string[] _onlineStatusCache = Array.Empty<string>();   // 状态列缓存：哪行变了才重取哪行

        private void ProcessPlayersOnlineTab(bool forced = false)
        {
            if (!forced)
            {
                // 不是「在线玩家」页时不刷（看不见的列表没必要刷新）
                if (MainTabs.SelectedTab != tabPage4) return;
                if (Environment.TickCount64 - _lastOnlineRefresh < OnlineRefreshMs) return;
                _lastOnlineRefresh = Environment.TickCount64;
            }

            // 服务端线程同时在校 Envir.Players，这里先做本地快照，避免"集合已修改/越界"
            List<PlayerObject> players;
            try
            {
                players = new List<PlayerObject>(Envir.Players);
            }
            catch
            {
                return;
            }

            // 过滤掉 Info 还空着的（正在登录/下线的交界）、去重、按角色 Index 排序（顺序固定不跳动）
            List<PlayerObject> visible = new List<PlayerObject>(players.Count);
            HashSet<int> seen = new HashSet<int>();
            foreach (PlayerObject p in players)
            {
                CharacterInfo info = p?.Info;
                if (info == null) continue;      // 登录/下线中途，这一拍先不显示
                if (!seen.Add(info.Index)) continue;
                visible.Add(p);
            }
            // ★竞态修复（11-10）：Sort 比较进行中，下线线程可能把某个玩家的 Info 置空，
            //   比较器里 a.Info.Index 二次解引用就会 NRE（线上弹窗栈：ProcessPlayersOnlineTab.<b__23_0>）。
            //   过滤通过后立刻把 Index 快照进字典，排序只查快照、不再访问 Info；
            //   快照时 Info 已空的兜底排到最后，下线玩家这一拍先显示、下一拍自然消失。
            Dictionary<PlayerObject, int> idxCache = new Dictionary<PlayerObject, int>(visible.Count);
            foreach (PlayerObject p in visible)
                idxCache[p] = p.Info != null ? p.Info.Index : int.MaxValue;
            visible.Sort((a, b) =>
            {
                int ia, ib;
                idxCache.TryGetValue(a, out ia);
                idxCache.TryGetValue(b, out ib);
                return ia.CompareTo(ib);
            });

            // 记住当前选中角色的 Index（上下线会让行号整体位移，按身份恢复选中）
            int selChar = 0;
            if (PlayersOnlineListView.SelectedIndices.Count > 0)
            {
                int ri = PlayersOnlineListView.SelectedIndices[0];
                if (ri >= 0 && ri < _onlineSnapshot.Count && _onlineSnapshot[ri]?.Info != null)
                    selChar = _onlineSnapshot[ri].Info.Index;
            }

            // ★虚拟模式刷新：先换数据源、再把行数改成新值 —— 不删行不建行。
            //   之前是全量删光重建，2 万在线时每 0.7 秒卡一次 UI；现在只是一次赋值。
            _onlineSnapshot = visible;

            if (PlayersOnlineListView.VirtualListSize != visible.Count)
                PlayersOnlineListView.VirtualListSize = visible.Count;

            // 状态列差异刷新：行内容是 RetrieveVirtualItem 现取的（永远反映快照最新状态），
            // 哪行的状态变了就只重取重画那一行。
            if (_onlineStatusCache.Length != visible.Count)
                _onlineStatusCache = new string[visible.Count];

            for (int i = 0; i < visible.Count; i++)
            {
                string status = GetStatusText(visible[i]);

                if (!string.Equals(_onlineStatusCache[i], status, StringComparison.Ordinal))
                {
                    _onlineStatusCache[i] = status;

                    try { PlayersOnlineListView.RedrawItems(i, i, true); } catch { }
                }
            }

            // 行整体位移后按角色 Index 恢复选中（位置没变就不动，避免每拍折腾选中状态）
            if (selChar != 0)
            {
                bool keep = PlayersOnlineListView.SelectedIndices.Count > 0 &&
                            PlayersOnlineListView.SelectedIndices[0] < visible.Count &&
                            visible[PlayersOnlineListView.SelectedIndices[0]]?.Info?.Index == selChar;

                if (!keep)
                {
                    int idx = visible.FindIndex(p => p?.Info != null && p.Info.Index == selChar);

                    PlayersOnlineListView.SelectedIndices.Clear();
                    if (idx >= 0) PlayersOnlineListView.SelectedIndices.Add(idx);
                }
            }
        }

        /// <summary>虚拟模式取行：控件只对屏幕内可见的行回调这里，从快照现造 ListViewItem</summary>
        private void PlayersOnlineListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            List<PlayerObject> snapshot = _onlineSnapshot;

            // ★注意：兜底行必须补齐到列数（CreatePlaceholderItem），
            //   裸 new ListViewItem("") 只有 1 个子项，VirtualMode 下会直接抛异常。
            if (e.ItemIndex < 0 || e.ItemIndex >= snapshot.Count)
            {
                e.Item = CreatePlaceholderItem();
                return;
            }

            e.Item = EnsureItemColumns(CreateListView(snapshot[e.ItemIndex]));   // 快照已过滤 Info，兜底防炸
        }

        private void startServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.Start();
        }

        private void stopServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.Stop();
            Envir.MonsterCount = 0;
        }

        private void SMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            Envir.Stop();

            // 日志是异步落盘的（AsyncLog），停服前把队列尽量写光，
            // 否则最后那几条「已退出游戏 / 服务已停止」会丢。
            AsyncLog.Flush(1500);
        }

        private void closeServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 20261106 非模态打开子窗口：不锁主界面，多个工具窗口可以同时开着互不干扰。
        /// 挂 Owner 让子窗口始终跟在主窗口之上、主窗口最小化时一起收起来。
        /// 注意：同一种窗口现在允许重复开（比如两个怪物编辑器），改同一份数据前注意别互相覆盖。
        /// </summary>
        private void ShowToolForm(Form form)
        {
            form.Owner = this;
            form.Show();
        }

        private void itemInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ItemInfoForm form = new ItemInfoForm();

            ShowToolForm(form);
        }

        private void monsterInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MonsterInfoForm form = new MonsterInfoForm();

            ShowToolForm(form);
        }

        private void nPCInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NPCInfoForm form = new NPCInfoForm();

            ShowToolForm(form);
        }

        private void balanceConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BalanceConfigForm form = new BalanceConfigForm();

            ShowToolForm(form);
        }

        private void questInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            QuestInfoForm form = new QuestInfoForm();

            ShowToolForm(form);
        }

        private void serverToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConfigForm form = new ConfigForm();

            ShowToolForm(form);
        }

        private void sendAwardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SendAwardForm form = new SendAwardForm();

            ShowToolForm(form);
        }

        private void balanceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BalanceConfigForm form = new BalanceConfigForm();

            ShowToolForm(form);
        }

        private void accountToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AccountInfoForm form = new AccountInfoForm();

            ShowToolForm(form);
        }

        private void mapInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MapInfoForm form = new MapInfoForm();

            ShowToolForm(form);
        }

        //★ 自定义 BUFF 属性编辑器（Configs\BuffSystem.ini）
        private void buffConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BuffConfigForm form = new BuffConfigForm();

            ShowToolForm(form);
        }

        private void itemInfoToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ItemInfoForm form = new ItemInfoForm();

            ShowToolForm(form);
        }

        private void monsterInfoToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            MonsterInfoForm form = new MonsterInfoForm();

            ShowToolForm(form);
        }

        private void nPCInfoToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            NPCInfoForm form = new NPCInfoForm();

            ShowToolForm(form);
        }

        private void questInfoToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            QuestInfoForm form = new QuestInfoForm();

            ShowToolForm(form);
        }

        private void dragonSystemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DragonInfoForm form = new DragonInfoForm();

            ShowToolForm(form);
        }

        private void miningToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MiningInfoForm form = new MiningInfoForm();

            ShowToolForm(form);
        }

        private void guildsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GuildInfoForm form = new GuildInfoForm();

            ShowToolForm(form);
        }

        private void fishingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(0);

            ShowToolForm(form);
        }

        private void GlobalMessageButton_Click(object sender, EventArgs e)
        {
            if (GlobalMessageTextBox.Text.Length < 1) return;

            foreach (var player in Envir.Players)
            {
                player.ReceiveChat(GlobalMessageTextBox.Text, ChatType.Announcement);
            }

            EnqueueChat(GlobalMessageTextBox.Text);
            GlobalMessageTextBox.Text = string.Empty;
        }

        private void PlayersOnlineListView_DoubleClick(object sender, EventArgs e)
        {
            CustomFormControl.ListViewNF list = (CustomFormControl.ListViewNF)sender;

            if (list.SelectedIndices.Count <= 0) return;

            int row = list.SelectedIndices[0];

            // ★虚拟模式（20261106）：按行号去快照里取身份，不要碰 Items/SelectedItems 的 Tag
            //   （控件里没有常驻行，行内容都是 RetrieveVirtualItem 现造的）
            if (row < 0 || row >= _onlineSnapshot.Count || _onlineSnapshot[row]?.Info == null)
            {
                MessageBox.Show("这一行的数据已过期，列表已重新刷新。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ProcessPlayersOnlineTab(true);
                return;
            }

            CharacterInfo info = _onlineSnapshot[row].Info;

            PlayerObject player = null;
            try { player = Envir.GetPlayer((uint)info.Index); }
            catch { }

            if (player == null)
            {
                MessageBox.Show($"角色「{info.Name}」已经不在线了（可能刚下线），列表已刷新。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                ProcessPlayersOnlineTab(true);
                return;
            }

            PlayerInfoForm form = new PlayerInfoForm((uint)info.Index);

            ShowToolForm(form);
        }

        private void PlayersOnlineListView_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            e.Cancel = true;
            e.NewWidth = PlayersOnlineListView.Columns[e.ColumnIndex].Width;
        }

        private void mailToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(1);

            ShowToolForm(form);
        }

        private void goodsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(2);

            ShowToolForm(form);
        }

        private void relationshipToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(4);

            ShowToolForm(form);
        }

        private void refiningToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(3);

            ShowToolForm(form);
        }

        private void mentorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(5);

            ShowToolForm(form);
        }

        private void magicInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MagicInfoForm form = new MagicInfoForm();
            ShowToolForm(form);
        }

        private void SMain_Load(object sender, EventArgs e)
        {
            var loaded = EditEnvir.LoadDB();

            if (loaded)
            {
                Envir.Start();
            }

            AutoResize();
        }

        private void gemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(6);

            ShowToolForm(form);
        }

        private void conquestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConquestInfoForm form = new ConquestInfoForm();

            ShowToolForm(form);
        }

        private void rebootServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.Reboot();
        }

        private void respawnsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(7);

            ShowToolForm(form);
        }

        private void monsterTunerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!SMain.Envir.Running)
            {
                MessageBox.Show("服务器运行必须调整怪物", "注意",
                MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            MonsterTunerForm form = new MonsterTunerForm();

            ShowToolForm(form);
        }

        private void gameshopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GameShop form = new GameShop();
            ShowToolForm(form);
        }

        private void itemNEWToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ItemInfoFormNew form = new ItemInfoFormNew();

            ShowToolForm(form);
        }

        private void monsterExperimentalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MonsterInfoFormNew form = new MonsterInfoFormNew();

            ShowToolForm(form);
        }

        private void dropBuilderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MirForms.DropBuilder.DropGenForm GenForm = new MirForms.DropBuilder.DropGenForm();

            ShowToolForm(GenForm);
        }

        private void clearBlockedIPsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.IPBlocks.Clear();
        }

        private void nPCsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.ReloadNPCs();
        }

        private void allNPCsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.ReloadAllNPCs();
        }

        private void gameShopToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Envir.ReloadGameShop();
        }

        private void itemDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.ReloadItems();
        }

        private void monsterDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.ReloadMonsters();
        }

        private void magicDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.ReloadMagics();
        }

        private void reloadRespawnsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.ReloadRespawns();
        }

        private void dropsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.ReloadDrops();
        }

        private void lineMessageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.ReloadLineMessages();
        }

        private void reloadMapsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 新图免重启上线 + 老图（安全区/传送点/矿区/开关/显示参数）配置刷新
            Envir.ReloadMaps();
        }

        #region Guild View Tab
        public void ProcessGuildViewTab(bool forced = false)
        {
            if (GuildListView.Items.Count != Envir.GuildList.Count || forced == true)
            {
                GuildListView.Items.Clear();

                foreach (GuildInfo guild in Envir.GuildList)
                {
                    ListViewItem tempItem = new ListViewItem(guild.GuildIndex.ToString()) { Tag = this };

                    tempItem.SubItems.Add(guild.Name);

                    if (guild.Ranks.Count > 0 && guild.Ranks[0].Members.Count > 0)
                    {
                        tempItem.SubItems.Add(guild.Ranks[0].Members[0].Name);
                    }
                    else
                    {
                        tempItem.SubItems.Add("DELETED");
                        tempItem.ForeColor = Color.Red;
                    }

                    tempItem.SubItems.Add(guild.Membercount.ToString());
                    tempItem.SubItems.Add(guild.Level.ToString());
                    tempItem.SubItems.Add($"{guild.Gold}");

                    GuildListView.Items.Add(tempItem);
                }
            }
        }

        private void GuildListView_DoubleClick(object sender, EventArgs e)
        {
            ListViewNF list = (ListViewNF)sender;

            if (list.SelectedItems.Count <= 0) return;

            ListViewItem item = list.SelectedItems[0];
            int index = Int32.Parse(item.Text);

            GuildObject Guild = Envir.GetGuild(index);

            GuildItemForm form = new GuildItemForm
            {
                GuildName = Guild.Name,
                main = this,
            };

            if (Guild == null) return;

            foreach (var i in Guild.StoredItems)
            {
                if (i == null) continue;
                ListViewItem tempItem = new ListViewItem(i.Item.UniqueID.ToString()) { Tag = this };

                CharacterInfo character = Envir.GetCharacterInfo((int)i.UserId);
                if (character != null)
                    tempItem.SubItems.Add(character.Name);
                else if (i.UserId == -1)
                    tempItem.SubItems.Add("Server");
                else
                    tempItem.SubItems.Add("Unknown");

                tempItem.SubItems.Add(i.Item.FriendlyName);
                tempItem.SubItems.Add(i.Item.Count.ToString());
                tempItem.SubItems.Add(i.Item.CurrentDura + "/" + i.Item.MaxDura);

                form.GuildItemListView.Items.Add(tempItem);
            }

            foreach (var r in Guild.Ranks)
                foreach (var m in r.Members)
                {
                    ListViewItem tempItem = new ListViewItem(m.Name) { Tag = this };
                    tempItem.SubItems.Add(r.Name);
                    form.MemberListView.Items.Add(tempItem);
                }

            ShowToolForm(form);
        }
        #endregion

        private void MainTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 只有切到「在线玩家」页才强制重建；切到别的页不再白白重建一次
            // （以前每切一次都把列表删光重建，选中和滚动位置全被重置）
            ProcessPlayersOnlineTab(MainTabs.SelectedTab == tabPage4);
            ProcessGuildViewTab(true);
        }

        private void GuildListView_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void heroesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(8);

            ShowToolForm(form);
        }

        private void CharacterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CharacterInfoForm form = new CharacterInfoForm();

            ShowToolForm(form);
        }

        private void recipeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RecipeInfoForm form = new RecipeInfoForm();

            ShowToolForm(form);
        }

        #region Monsters Tab
        private void LoadMonstersButton_Click(object sender, EventArgs e)
        {
            MonsterListView.Items.Clear();
            for (int i = 0; i < Envir.MapList.Count; i++)
            {
                var map = Envir.MapList[i];
                ListViewItem ListItem = new ListViewItem(i.ToString()) { Tag = this };

                ListItem.SubItems.Add(map.Info.Title);
                ListItem.SubItems.Add(map.Info.FileName);
                ListItem.SubItems.Add(map.GetAllMonstersObjectsCount().ToString());
                int totalSpawnsCount = 0;
                int errorCount = 0;

                foreach (var spawn in map.Respawns)
                {
                    totalSpawnsCount += spawn.Info.Count;
                    errorCount += spawn.ErrorCount;
                }

                ListItem.SubItems.Add(totalSpawnsCount.ToString());
                ListItem.SubItems.Add(errorCount.ToString());

                MonsterListView.Items.Add(ListItem);
            }
        }
        #endregion
    }
}