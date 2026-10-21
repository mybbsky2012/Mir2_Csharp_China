using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

namespace Server
{
    /// <summary>
    /// 发送邮件奖励：选一件物品 + 写邮件内容 + 选收件人，直接塞进角色的邮件箱。
    /// 走的是服务端原生 MailInfo.Send()：离线角色进其存档，在线角色会收到新邮件提示。
    /// 只发 1 件（与界面一致），不扣任何人金币。
    /// </summary>
    public partial class SendAwardForm : Form
    {
        private const string TargetAll = "★ 全部角色（含离线）";
        private const string TargetOnline = "★ 仅在线玩家";
        private const string MailSender = "系统";

        // 注意：必须用服务端运行实例 Envir.Main（SMain.Envir）。
        // EditEnvir 是编辑器实例，只有物品表，LoadAccounts 只跑在运行实例上 —— 用它的话「全部角色」永远是 0 个。
        public Envir Envir => SMain.Envir;

        public SendAwardForm()
        {
            InitializeComponent();

            LoadCategories();
            LoadTargets();
        }

        private void LoadCategories()
        {
            cmbCategory.Items.Clear();
            cmbCategory.Items.Add("全部");
            foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
            {
                if (Envir == null) break;
                if (Envir.ItemInfoList.Any(x => x.Type == type)) cmbCategory.Items.Add(type);
            }

            if (cmbCategory.Items.Count > 0) cmbCategory.SelectedIndex = 0;
        }

        private void LoadTargets()
        {
            cmbTarget.Items.Clear();
            cmbTarget.Items.Add(TargetAll);
            cmbTarget.Items.Add(TargetOnline);

            if (Envir != null)
            {
                foreach (PlayerObject player in Envir.Players)
                {
                    if (player?.Info == null) continue;
                    if (!cmbTarget.Items.Contains(player.Name)) cmbTarget.Items.Add(player.Name);
                }
            }

            cmbTarget.SelectedIndex = 0;
        }

        private void RefreshItems()
        {
            if (Envir == null) return;

            object selected = cmbCategory.SelectedItem;
            lstItems.BeginUpdate();
            lstItems.Items.Clear();

            IEnumerable<ItemInfo> source = selected is ItemType type
                ? Envir.ItemInfoList.Where(x => x.Type == type)
                : Envir.ItemInfoList;

            foreach (ItemInfo info in source.OrderBy(x => x.Index))
                lstItems.Items.Add(new ItemEntry(info));

            lstItems.EndUpdate();
        }

        private void cmbCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshItems();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (Envir == null || !Envir.Running)
            {
                MessageBox.Show("请先启动服务器（服务端运行时才能发邮件）。", "发送邮件奖励", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!(lstItems.SelectedItem is ItemEntry entry))
            {
                MessageBox.Show("请先在左侧选择要发送的物品。", "发送邮件奖励", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string message = txtMessage.Text.Trim();
            if (message.Length == 0)
            {
                MessageBox.Show("请填写邮件内容。", "发送邮件奖励", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string target = (cmbTarget.Text ?? string.Empty).Trim();
            if (target.Length == 0)
            {
                MessageBox.Show("请选择发送人员。", "发送邮件奖励", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<CharacterInfo> recipients = new List<CharacterInfo>();

            if (target == TargetAll)
            {
                recipients.AddRange(Envir.CharacterList);
            }
            else if (target == TargetOnline)
            {
                foreach (PlayerObject player in Envir.Players)
                {
                    if (player?.Info != null) recipients.Add(player.Info);
                }
            }
            else
            {
                CharacterInfo one = Envir.GetCharacterInfo(target);
                if (one == null)
                {
                    MessageBox.Show($"找不到角色「{target}」，请检查名字是否正确。", "发送邮件奖励", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                recipients.Add(one);
            }

            if (recipients.Count == 0)
            {
                MessageBox.Show("没有可发送的角色。", "发送邮件奖励", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show($"将把「{entry.Info.Name}」发送给 {recipients.Count} 个角色，确认发送？",
                                "发送邮件奖励", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            int sent = 0;
            foreach (CharacterInfo character in recipients)
            {
                // 每个角色都要独立的物品实例，否则多封邮件共用同一个 UniqueID 会互相覆盖
                UserItem item = Envir.CreateFreshItem(entry.Info);
                if (item == null) continue;

                MailInfo mail = new MailInfo(character.Index)
                {
                    Sender = MailSender,
                    Message = message,
                };
                mail.Items.Add(item);

                mail.Send();
                sent++;
            }

            // 立刻落盘，避免服务端意外关闭时这几封邮件丢失
            try { Envir.SaveDB(); }
            catch (Exception ex) { MessageBox.Show("邮件已发出，但保存数据库失败：" + ex.Message, "发送邮件奖励"); }

            MessageBox.Show($"已发送 {sent} 封邮件。\n\n在线玩家会看到新邮件提示，打开邮件即可领取。",
                            "发送邮件奖励", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private sealed class ItemEntry
        {
            public readonly ItemInfo Info;

            public ItemEntry(ItemInfo info)
            {
                Info = info;
            }

            public override string ToString()
            {
                return $"{Info.Index}: {Info.Name}";
            }
        }

        private void InitializeComponent()
        {
            this.lblItem = new Label();
            this.cmbCategory = new ComboBox();
            this.lstItems = new ListBox();
            this.lblMessage = new Label();
            this.txtMessage = new TextBox();
            this.lblTarget = new Label();
            this.cmbTarget = new ComboBox();
            this.btnSend = new Button();
            this.SuspendLayout();
            // 
            // lblItem
            // 
            this.lblItem.AutoSize = true;
            this.lblItem.Location = new Point(13, 23);
            this.lblItem.Name = "lblItem";
            this.lblItem.Size = new Size(32, 17);
            this.lblItem.TabIndex = 0;
            this.lblItem.Text = "物品";
            // 
            // cmbCategory
            // 
            this.cmbCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbCategory.FormattingEnabled = true;
            this.cmbCategory.Location = new Point(13, 45);
            this.cmbCategory.Name = "cmbCategory";
            this.cmbCategory.Size = new Size(195, 25);
            this.cmbCategory.TabIndex = 1;
            this.cmbCategory.SelectedIndexChanged += new EventHandler(this.cmbCategory_SelectedIndexChanged);
            // 
            // lstItems
            // 
            this.lstItems.FormattingEnabled = true;
            this.lstItems.IntegralHeight = false;
            this.lstItems.ItemHeight = 17;
            this.lstItems.Location = new Point(13, 76);
            this.lstItems.Name = "lstItems";
            this.lstItems.Size = new Size(195, 436);
            this.lstItems.TabIndex = 2;
            // 
            // lblMessage
            // 
            this.lblMessage.AutoSize = true;
            this.lblMessage.Location = new Point(222, 23);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Size = new Size(60, 17);
            this.lblMessage.TabIndex = 3;
            this.lblMessage.Text = "邮件内容";
            // 
            // txtMessage
            // 
            this.txtMessage.Location = new Point(222, 45);
            this.txtMessage.Multiline = true;
            this.txtMessage.Name = "txtMessage";
            this.txtMessage.ScrollBars = ScrollBars.Vertical;
            this.txtMessage.Size = new Size(213, 355);
            this.txtMessage.TabIndex = 4;
            // 
            // lblTarget
            // 
            this.lblTarget.AutoSize = true;
            this.lblTarget.Location = new Point(222, 416);
            this.lblTarget.Name = "lblTarget";
            this.lblTarget.Size = new Size(60, 17);
            this.lblTarget.TabIndex = 5;
            this.lblTarget.Text = "发送人员";
            // 
            // cmbTarget
            // 
            this.cmbTarget.FormattingEnabled = true;
            this.cmbTarget.Location = new Point(222, 438);
            this.cmbTarget.Name = "cmbTarget";
            this.cmbTarget.Size = new Size(213, 25);
            this.cmbTarget.TabIndex = 6;
            // 
            // btnSend
            // 
            this.btnSend.Location = new Point(222, 478);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new Size(213, 50);
            this.btnSend.TabIndex = 7;
            this.btnSend.Text = "发送";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new EventHandler(this.btnSend_Click);
            // 
            // SendAwardForm
            // 
            this.ClientSize = new Size(448, 546);
            this.Controls.Add(this.btnSend);
            this.Controls.Add(this.cmbTarget);
            this.Controls.Add(this.lblTarget);
            this.Controls.Add(this.txtMessage);
            this.Controls.Add(this.lblMessage);
            this.Controls.Add(this.lstItems);
            this.Controls.Add(this.cmbCategory);
            this.Controls.Add(this.lblItem);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SendAwardForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "发送邮件奖励";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private Label lblItem;
        private ComboBox cmbCategory;
        private ListBox lstItems;
        private Label lblMessage;
        private TextBox txtMessage;
        private Label lblTarget;
        private ComboBox cmbTarget;
        private Button btnSend;
    }
}
