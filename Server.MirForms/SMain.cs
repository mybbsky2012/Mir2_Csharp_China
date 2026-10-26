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
                MessageBox.Show(ex.ToString());
            }
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
            CharacterInfo character = player.Info;

            ListViewItem ListItem = new ListViewItem(character.Index.ToString()) { Tag = character };

            ListItem.SubItems.Add(character.Name);
            ListItem.SubItems.Add(character.Level.ToString());
            ListItem.SubItems.Add(character.Class.ToString());
            ListItem.SubItems.Add(character.Gender.ToString());
            ListItem.SubItems.Add(GetStatusText(player));

            return ListItem;
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

        private void ProcessPlayersOnlineTab(bool forced = false)
        {
            if (PlayersOnlineListView.Items.Count != Envir.Players.Count || forced == true)
            {
                PlayersOnlineListView.BeginUpdate();
                PlayersOnlineListView.Items.Clear();

                for (int i = 0; i < Envir.Players.Count; i++)
                    PlayersOnlineListView.Items.Add(CreateListView(Envir.Players[i]));

                PlayersOnlineListView.EndUpdate();
            }
            else
            {
                // ★数量没变也要刷新状态列：真人→挂机、挂机→真人重登 等切换不会触发重建
                bool changed = false;
                for (int i = 0; i < Envir.Players.Count && i < PlayersOnlineListView.Items.Count; i++)
                {
                    string status = GetStatusText(Envir.Players[i]);
                    var sub = PlayersOnlineListView.Items[i].SubItems[5];
                    if (sub == null || sub.Text != status)
                    {
                        if (sub != null) sub.Text = status;
                        changed = true;
                    }
                }

                if (changed) PlayersOnlineListView.Invalidate();
            }
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
        }

        private void closeServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void itemInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ItemInfoForm form = new ItemInfoForm();

            form.ShowDialog();
        }

        private void monsterInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MonsterInfoForm form = new MonsterInfoForm();

            form.ShowDialog();
        }

        private void nPCInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NPCInfoForm form = new NPCInfoForm();

            form.ShowDialog();
        }

        private void balanceConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BalanceConfigForm form = new BalanceConfigForm();

            form.ShowDialog();
        }

        private void questInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            QuestInfoForm form = new QuestInfoForm();

            form.ShowDialog();
        }

        private void serverToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConfigForm form = new ConfigForm();

            form.ShowDialog();
        }

        private void sendAwardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SendAwardForm form = new SendAwardForm();

            form.ShowDialog();
        }

        private void balanceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BalanceConfigForm form = new BalanceConfigForm();

            form.ShowDialog();
        }

        private void accountToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AccountInfoForm form = new AccountInfoForm();

            form.ShowDialog();
        }

        private void mapInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MapInfoForm form = new MapInfoForm();

            form.ShowDialog();
        }

        //★ 自定义 BUFF 属性编辑器（Configs\BuffSystem.ini）
        private void buffConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BuffConfigForm form = new BuffConfigForm();

            form.ShowDialog();
        }

        private void itemInfoToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ItemInfoForm form = new ItemInfoForm();

            form.ShowDialog();
        }

        private void monsterInfoToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            MonsterInfoForm form = new MonsterInfoForm();

            form.ShowDialog();
        }

        private void nPCInfoToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            NPCInfoForm form = new NPCInfoForm();

            form.ShowDialog();
        }

        private void questInfoToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            QuestInfoForm form = new QuestInfoForm();

            form.ShowDialog();
        }

        private void dragonSystemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DragonInfoForm form = new DragonInfoForm();

            form.ShowDialog();
        }

        private void miningToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MiningInfoForm form = new MiningInfoForm();

            form.ShowDialog();
        }

        private void guildsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GuildInfoForm form = new GuildInfoForm();

            form.ShowDialog();
        }

        private void fishingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(0);

            form.ShowDialog();
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

            if (list.SelectedItems.Count > 0)
            {
                ListViewItem item = list.SelectedItems[0];
                string index = item.SubItems[0].Text;

                PlayerInfoForm form = new PlayerInfoForm(Convert.ToUInt32(index));

                form.ShowDialog();
            }
        }

        private void PlayersOnlineListView_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            e.Cancel = true;
            e.NewWidth = PlayersOnlineListView.Columns[e.ColumnIndex].Width;
        }

        private void mailToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(1);

            form.ShowDialog();
        }

        private void goodsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(2);

            form.ShowDialog();
        }

        private void relationshipToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(4);

            form.ShowDialog();
        }

        private void refiningToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(3);

            form.ShowDialog();
        }

        private void mentorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(5);

            form.ShowDialog();
        }

        private void magicInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MagicInfoForm form = new MagicInfoForm();
            form.ShowDialog();
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

            form.ShowDialog();
        }

        private void conquestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConquestInfoForm form = new ConquestInfoForm();

            form.ShowDialog();
        }

        private void rebootServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.Reboot();
        }

        private void respawnsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(7);

            form.ShowDialog();
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

            form.ShowDialog();
        }

        private void gameshopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GameShop form = new GameShop();
            form.ShowDialog();
        }

        private void itemNEWToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ItemInfoFormNew form = new ItemInfoFormNew();

            form.ShowDialog();
        }

        private void monsterExperimentalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MonsterInfoFormNew form = new MonsterInfoFormNew();

            form.ShowDialog();
        }

        private void dropBuilderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MirForms.DropBuilder.DropGenForm GenForm = new MirForms.DropBuilder.DropGenForm();

            GenForm.ShowDialog();
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

            form.ShowDialog();
        }
        #endregion

        private void MainTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            ProcessPlayersOnlineTab(true);
            ProcessGuildViewTab(true);
        }

        private void GuildListView_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void heroesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(8);

            form.ShowDialog();
        }

        private void CharacterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CharacterInfoForm form = new CharacterInfoForm();

            form.ShowDialog();
        }

        private void recipeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RecipeInfoForm form = new RecipeInfoForm();

            form.ShowDialog();
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