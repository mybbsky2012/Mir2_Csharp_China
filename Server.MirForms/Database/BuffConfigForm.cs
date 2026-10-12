using Server.MirDatabase;
using Server.MirEnvir;

namespace Server
{
    /// <summary>
    /// 自定义 BUFF 属性编辑器 —— 维护 Configs\BuffSystem.ini。
    ///
    /// 界面结构（左侧列表 + 右侧属性）：
    ///   · 左侧：已配置的 BUFF 列表。数字槽位 = 自定义 BUFF（Custom1~Custom24）；
    ///           枚举名 = 挂在已有 BUFF 上（如 [隐身术]），只做属性/图标的追加。
    ///   · 右侧上：「基本信息」（名称 / 图标 / 是否可见 / 堆叠方式 / 标记 / 说明）
    ///   · 右侧中：「属性加成」表格（属性 / 基础值 / 每级递增 → 最终 = 基础 + 每级×等级）
    ///   · 底部：保存到文件 / 重新加载 / 应用并刷新在线玩家
    ///
    /// ★这是「配置编辑器」，属性真正生效在服务端 HumanObject.AddBuff 里。
    ///   保存后如果服务器正在运行，会顺手调用 Envir.Main.ReloadBuffConfig() 立刻生效，
    ///   否则（没开服）下次启动服务端自动读取。
    /// </summary>
    public class BuffConfigForm : Form
    {
        private ListBox _defList;
        private NumericUpDown _slotBox;
        private NumericUpDown _iconBox;
        private TextBox _nameBox;
        private TextBox _descBox;
        private CheckBox _visibleBox;
        private ComboBox _stackBox;
        private CheckedListBox _propertyList;
        private DataGridView _statGrid;
        private Label _hintLabel;

        /// <summary>填充控件时置位，避免控件事件把半成品值写回数据。</summary>
        private bool _loading;

        private BuffDef SelectedDef
        {
            get
            {
                int index = _defList.SelectedIndex;

                if (index < 0 || index >= BuffConfig.Defs.Count) return null;

                return BuffConfig.Defs[index];
            }
        }

        public BuffConfigForm()
        {
            BuildUi();

            BuffConfig.Load();
            RefreshList();
        }

        #region 界面搭建

        private void BuildUi()
        {
            Text = "自定义 BUFF 属性";
            ClientSize = new Size(980, 660);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            // ---------- 左侧：BUFF 列表 ----------
            var leftPanel = new Panel
            {
                Location = new Point(12, 12),
                Size = new Size(240, 600),
                BorderStyle = BorderStyle.FixedSingle
            };

            var listLabel = new Label
            {
                Parent = leftPanel,
                Text = "BUFF 列表",
                Location = new Point(8, 6),
                AutoSize = true
            };

            _defList = new ListBox
            {
                Parent = leftPanel,
                Location = new Point(8, 28),
                Size = new Size(222, 520),
                IntegralHeight = false
            };
            _defList.SelectedIndexChanged += (o, e) => LoadSelected();

            var newCustomButton = new Button
            {
                Parent = leftPanel,
                Text = "新建自定义",
                Location = new Point(8, 556),
                Size = new Size(104, 32)
            };
            newCustomButton.Click += NewCustomButton_Click;

            var addExistingButton = new Button
            {
                Parent = leftPanel,
                Text = "挂到已有BUFF",
                Location = new Point(118, 556),
                Size = new Size(112, 32)
            };
            addExistingButton.Click += AddExistingButton_Click;

            var deleteButton = new Button
            {
                Parent = leftPanel,
                Text = "删除选中",
                Location = new Point(8, 592),
                Size = new Size(222, 32)
            };
            deleteButton.Click += DeleteButton_Click;

            Controls.Add(leftPanel);

            // ---------- 右侧上：基本信息 ----------
            var infoGroup = new GroupBox
            {
                Text = "基本信息",
                Location = new Point(264, 12),
                Size = new Size(704, 200)
            };

            var slotLabel = new Label { Parent = infoGroup, Text = "槽位", Location = new Point(16, 30), AutoSize = true };
            _slotBox = new NumericUpDown
            {
                Parent = infoGroup,
                Location = new Point(80, 26),
                Size = new Size(70, 25),
                Minimum = 0,
                Maximum = BuffConfig.CustomSlotCount,
                Enabled = false,
                Value = 0
            };

            var slotHint = new Label
            {
                Parent = infoGroup,
                Text = "0 = 挂在已有 BUFF；1~" + BuffConfig.CustomSlotCount + " = 自定义槽位",
                Location = new Point(160, 30),
                AutoSize = true,
                ForeColor = Color.Gray
            };

            var nameLabel = new Label { Parent = infoGroup, Text = "名称", Location = new Point(16, 66), AutoSize = true };
            _nameBox = new TextBox
            {
                Parent = infoGroup,
                Location = new Point(80, 62),
                Size = new Size(240, 25)
            };
            _nameBox.TextChanged += (o, e) =>
            {
                if (_loading) return;

                BuffDef def = SelectedDef;
                if (def == null) return;

                def.Name = _nameBox.Text;
                def.HasName = true;
                RefreshListKeepSelection();
            };

            var iconLabel = new Label { Parent = infoGroup, Text = "图标", Location = new Point(340, 66), AutoSize = true };
            _iconBox = new NumericUpDown
            {
                Parent = infoGroup,
                Location = new Point(400, 62),
                Size = new Size(100, 25),
                Minimum = 0,
                Maximum = 999999,
                Value = 0
            };
            _iconBox.ValueChanged += (o, e) =>
            {
                if (_loading) return;

                BuffDef def = SelectedDef;
                if (def == null) return;

                def.Icon = (int)_iconBox.Value;
                def.HasIcon = true;
            };

            var iconHint = new Label
            {
                Parent = infoGroup,
                Text = "0=原图标；10000+=Prguse；20000+=Prguse2；30000+=MagIcon；40000+=MagIcon2",
                Location = new Point(508, 66),
                AutoSize = true,
                ForeColor = Color.Gray
            };

            _visibleBox = new CheckBox
            {
                Parent = infoGroup,
                Text = "其他玩家可见",
                Location = new Point(16, 100),
                Size = new Size(130, 24)
            };
            _visibleBox.CheckedChanged += (o, e) =>
            {
                if (_loading) return;

                BuffDef def = SelectedDef;
                if (def == null) return;

                def.Visible = _visibleBox.Checked;
                def.HasVisible = true;
            };

            var stackLabel = new Label { Parent = infoGroup, Text = "堆叠方式", Location = new Point(160, 102), AutoSize = true };
            _stackBox = new ComboBox
            {
                Parent = infoGroup,
                Location = new Point(240, 98),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _stackBox.Items.AddRange(Enum.GetNames(typeof(BuffStackType)));
            _stackBox.SelectedIndexChanged += (o, e) =>
            {
                if (_loading) return;

                BuffDef def = SelectedDef;
                if (def == null || _stackBox.SelectedIndex < 0) return;

                def.Stack = (BuffStackType)_stackBox.SelectedIndex;
                def.HasStack = true;
            };

            var propertyLabel = new Label { Parent = infoGroup, Text = "标记", Location = new Point(460, 102), AutoSize = true };
            _propertyList = new CheckedListBox
            {
                Parent = infoGroup,
                Location = new Point(508, 96),
                Size = new Size(180, 66),
                CheckOnClick = true,
                MultiColumn = false
            };
            _propertyList.Items.AddRange(new object[] { "RemoveOnDeath", "RemoveOnExit", "Debuff", "PauseInSafeZone" });
            _propertyList.ItemCheck += (o, e) =>
            {
                if (_loading) return;

                // ItemCheck 触发时 CheckedItems 还是旧值，延后到消息队列处理
                BeginInvoke(new Action(() =>
                {
                    BuffDef def = SelectedDef;
                    if (def == null) return;

                    BuffProperty result = BuffProperty.None;

                    foreach (object item in _propertyList.CheckedItems)
                    {
                        BuffProperty flag;
                        if (Enum.TryParse(item.ToString(), true, out flag)) result |= flag;
                    }

                    def.Property = result;
                    def.HasProperty = true;
                }));
            };

            var descLabel = new Label { Parent = infoGroup, Text = "说明", Location = new Point(16, 134), AutoSize = true };
            _descBox = new TextBox
            {
                Parent = infoGroup,
                Location = new Point(80, 130),
                Size = new Size(608, 56),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            _descBox.TextChanged += (o, e) =>
            {
                if (_loading) return;

                BuffDef def = SelectedDef;
                if (def == null) return;

                def.Desc = _descBox.Text;
            };

            Controls.Add(infoGroup);

            // ---------- 右侧中：属性表格 ----------
            var statGroup = new GroupBox
            {
                Text = "属性加成（每级递增 = 施法者每升 1 级额外增加的值）",
                Location = new Point(264, 220),
                Size = new Size(704, 350)
            };

            _statGrid = new DataGridView
            {
                Parent = statGroup,
                Location = new Point(12, 24),
                Size = new Size(680, 280),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnEnter,
                RowHeadersWidth = 40
            };

            var statColumn = new DataGridViewComboBoxColumn
            {
                Name = "StatName",
                HeaderText = "属性",
                DataSource = StatNames(),
                FillWeight = 45
            };

            var baseColumn = new DataGridViewTextBoxColumn
            {
                Name = "BaseValue",
                HeaderText = "基础值",
                FillWeight = 25
            };

            var perLevelColumn = new DataGridViewTextBoxColumn
            {
                Name = "PerLevel",
                HeaderText = "每级递增",
                FillWeight = 25
            };

            _statGrid.Columns.Add(statColumn);
            _statGrid.Columns.Add(baseColumn);
            _statGrid.Columns.Add(perLevelColumn);

            _statGrid.CurrentCellDirtyStateChanged += (o, e) =>
            {
                if (_statGrid.IsCurrentCellDirty) _statGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            _statGrid.CellValueChanged += (o, e) =>
            {
                if (_loading) return;

                ReadStatsFromGrid();
            };

            _statGrid.RowsRemoved += (o, e) =>
            {
                if (_loading) return;

                ReadStatsFromGrid();
            };

            // 单元格里填了非数字/清空 → 静默忽略，别弹错误对话框
            _statGrid.DataError += (o, e) => { e.ThrowException = false; };

            var addStatButton = new Button
            {
                Parent = statGroup,
                Text = "添加属性行",
                Location = new Point(12, 310),
                Size = new Size(120, 30)
            };
            addStatButton.Click += (o, e) =>
            {
                _loading = true;
                _statGrid.Rows.Add(Stat.MinAC.ToString(), "0", "0");
                _loading = false;

                ReadStatsFromGrid();
            };

            var removeStatButton = new Button
            {
                Parent = statGroup,
                Text = "删除选中行",
                Location = new Point(140, 310),
                Size = new Size(120, 30)
            };
            removeStatButton.Click += (o, e) =>
            {
                if (_statGrid.CurrentRow == null) return;

                if (!_statGrid.CurrentRow.IsNewRow) _statGrid.Rows.Remove(_statGrid.CurrentRow);
            };

            Controls.Add(statGroup);

            // ---------- 底部按钮 ----------
            var saveButton = new Button
            {
                Text = "保存并生效",
                Location = new Point(264, 590),
                Size = new Size(130, 36)
            };
            saveButton.Click += SaveButton_Click;
            Controls.Add(saveButton);

            var reloadButton = new Button
            {
                Text = "重新加载配置文件",
                Location = new Point(404, 590),
                Size = new Size(140, 36)
            };
            reloadButton.Click += (o, e) =>
            {
                BuffConfig.Load();
                RefreshList();

                _hintLabel.Text = string.IsNullOrEmpty(BuffConfig.LastError)
                    ? "已从文件重新加载"
                    : "加载出错：" + BuffConfig.LastError;
            };
            Controls.Add(reloadButton);

            var closeButton = new Button
            {
                Text = "关闭",
                Location = new Point(878, 590),
                Size = new Size(90, 36)
            };
            closeButton.Click += (o, e) => Close();
            Controls.Add(closeButton);

            _hintLabel = new Label
            {
                Location = new Point(264, 632),
                Size = new Size(704, 20),
                ForeColor = Color.DimGray,
                Text = "改完点「保存并生效」——文件为 Configs\\BuffSystem.ini；"
            };
            Controls.Add(_hintLabel);
        }

        private static string[] StatNames()
        {
            var names = new List<string>();

            foreach (string name in Enum.GetNames(typeof(Stat)))
            {
                if (name == "Unknown") continue;

                names.Add(name);
            }

            return names.ToArray();
        }

        #endregion

        #region 列表与编辑

        private void RefreshList()
        {
            int keep = _defList.SelectedIndex;

            _defList.Items.Clear();

            for (int i = 0; i < BuffConfig.Defs.Count; i++)
            {
                BuffDef def = BuffConfig.Defs[i];

                string name = string.IsNullOrEmpty(def.Name) ? def.Type.ToString() : def.Name;

                _defList.Items.Add(string.Format("{0}. {1}", def.Section, name));
            }

            if (_defList.Items.Count == 0) return;

            if (keep < 0 || keep >= _defList.Items.Count) keep = 0;

            _defList.SelectedIndex = keep;
        }

        private void RefreshListKeepSelection()
        {
            int keep = _defList.SelectedIndex;

            if (keep < 0) return;

            if (keep >= _defList.Items.Count) return;

            BuffDef def = BuffConfig.Defs[keep];
            string name = string.IsNullOrEmpty(def.Name) ? def.Type.ToString() : def.Name;

            _defList.Items[keep] = string.Format("{0}. {1}", def.Section, name);
        }

        private void LoadSelected()
        {
            BuffDef def = SelectedDef;

            _loading = true;

            try
            {
                bool enabled = def != null;

                _slotBox.Enabled = false;
                _nameBox.Enabled = enabled;
                _iconBox.Enabled = enabled;
                _visibleBox.Enabled = enabled;
                _stackBox.Enabled = enabled;
                _propertyList.Enabled = enabled;
                _descBox.Enabled = enabled;
                _statGrid.Enabled = enabled;

                _statGrid.Rows.Clear();

                if (!enabled)
                {
                    _slotBox.Value = 0;
                    _nameBox.Text = string.Empty;
                    _iconBox.Value = 0;
                    _visibleBox.Checked = false;
                    _stackBox.SelectedIndex = -1;
                    _descBox.Text = string.Empty;
                    return;
                }

                _slotBox.Value = def.Slot;
                _nameBox.Text = def.Name ?? string.Empty;
                _iconBox.Value = def.Icon;
                _visibleBox.Checked = def.Visible;

                _stackBox.SelectedIndex = (int)def.Stack;

                for (int i = 0; i < _propertyList.Items.Count; i++)
                {
                    BuffProperty flag;
                    string item = _propertyList.Items[i].ToString();

                    bool check = Enum.TryParse(item, true, out flag) && def.Property.HasFlag(flag);

                    _propertyList.SetItemChecked(i, check);
                }

                _descBox.Text = def.Desc ?? string.Empty;

                for (int i = 0; i < def.Stats.Count; i++)
                {
                    BuffStatDef stat = def.Stats[i];

                    _statGrid.Rows.Add(stat.Stat.ToString(), stat.Base.ToString(), stat.PerLevel.ToString());
                }

                _hintLabel.Text = def.IsCustom
                    ? string.Format("自定义槽位 {0}（BuffType.{1}）—— 内容完全来自本配置表", def.Slot, def.Type)
                    : string.Format("挂在已有 BUFF「{0}」上，只追加属性，不改它原有的技能逻辑", def.Type);
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>把表格里的内容整体回写到当前 BUFF 的属性列表（简单、不会出现半更新状态）。</summary>
        private void ReadStatsFromGrid()
        {
            BuffDef def = SelectedDef;

            if (def == null) return;

            def.Stats.Clear();

            for (int i = 0; i < _statGrid.Rows.Count; i++)
            {
                DataGridViewRow row = _statGrid.Rows[i];

                if (row.IsNewRow) continue;

                string statName = Convert.ToString(row.Cells[0].Value);
                string baseText = Convert.ToString(row.Cells[1].Value);
                string perLevelText = Convert.ToString(row.Cells[2].Value);

                Stat stat;

                if (!Enum.TryParse(statName, false, out stat)) continue;
                if (stat == Stat.Unknown) continue;

                int baseValue, perLevel;

                if (!int.TryParse(baseText, out baseValue)) baseValue = 0;
                if (!int.TryParse(perLevelText, out perLevel)) perLevel = 0;

                if (baseValue == 0 && perLevel == 0) continue;

                def.Stats.Add(new BuffStatDef { Stat = stat, Base = baseValue, PerLevel = perLevel });
            }
        }

        #endregion

        #region 按钮

        private void NewCustomButton_Click(object sender, EventArgs e)
        {
            int slot = FindFreeSlot();

            if (slot == 0)
            {
                MessageBox.Show(this,
                    string.Format("自定义槽位已用满（1~{0}），请先删除不用的", BuffConfig.CustomSlotCount),
                    "无法新建", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            BuffDef def = new BuffDef(BuffConfig.SlotToType(slot), slot)
            {
                Name = "自定义BUFF" + slot,
                HasName = true,
                Visible = true,
                HasVisible = true,
                Stack = BuffStackType.ResetDuration,
                HasStack = true
            };

            BuffConfig.Defs.Add(def);

            RefreshList();
            _defList.SelectedIndex = _defList.Items.Count - 1;
        }

        private void AddExistingButton_Click(object sender, EventArgs e)
        {
            var used = new List<BuffType>();

            for (int i = 0; i < BuffConfig.Defs.Count; i++)
                used.Add(BuffConfig.Defs[i].Type);

            using (var picker = new BuffTypePicker(used))
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;

                if (picker.Selected == BuffType.None) return;

                BuffConfig.Defs.Add(new BuffDef(picker.Selected, 0)
                {
                    Visible = true
                });

                RefreshList();
                _defList.SelectedIndex = _defList.Items.Count - 1;
            }
        }

        private int FindFreeSlot()
        {
            for (int slot = 1; slot <= BuffConfig.CustomSlotCount; slot++)
            {
                BuffType type = BuffConfig.SlotToType(slot);
                bool taken = false;

                for (int i = 0; i < BuffConfig.Defs.Count; i++)
                {
                    if (BuffConfig.Defs[i].Type != type) continue;

                    taken = true;
                    break;
                }

                if (!taken) return slot;
            }

            return 0;
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            BuffDef def = SelectedDef;

            if (def == null) return;

            if (MessageBox.Show(this,
                    string.Format("确定删除 [{0}] 吗？", def.Section),
                    "删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            BuffConfig.Defs.Remove(def);

            RefreshList();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            ReadStatsFromGrid();

            try
            {
                BuffConfig.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string message = "已保存到 Configs\\BuffSystem.ini";

            // 服务器在跑 → 顺手让它立刻生效（不用重启）
            try
            {
                if (Envir.Main != null && Envir.Main.Running)
                {
                    Envir.Main.ReloadBuffConfig();
                    message += "\r\n并已通知运行中的服务器重新加载（玩家身上原有的自定义 BUFF 会被清掉，需要重新加）";
                }
                else
                {
                    message += "\r\n服务器当前未运行，下次启动时自动读取";
                }
            }
            catch (Exception ex)
            {
                message += "\r\n通知服务器重载失败（文件已保存，可在游戏里打 @RELOADBUFF）：" + ex.Message;
            }

            _hintLabel.Text = message.Replace("\r\n", "  ");

            MessageBox.Show(this, message, "保存完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion

        /// <summary>
        /// 选一个「已有 BUFF」挂配置用的小选择框。
        /// 列出 BuffType 里除自定义槽位外的全部枚举（BuffType 是 byte 枚举，索引即枚举值）。
        /// </summary>
        private sealed class BuffTypePicker : Form
        {
            private ListBox _list;
            private TextBox _filter;

            public BuffType Selected = BuffType.None;

            public BuffTypePicker(List<BuffType> exclude)
            {
                Text = "选择已有 BUFF";
                ClientSize = new Size(360, 520);
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MinimizeBox = false;
                MaximizeBox = false;

                var label = new Label
                {
                    Parent = this,
                    Text = "搜索：",
                    Location = new Point(12, 12),
                    AutoSize = true
                };

                _filter = new TextBox
                {
                    Parent = this,
                    Location = new Point(60, 8),
                    Size = new Size(288, 25)
                };
                _filter.TextChanged += (o, e) => Fill(exclude);

                _list = new ListBox
                {
                    Parent = this,
                    Location = new Point(12, 42),
                    Size = new Size(336, 420),
                    IntegralHeight = false
                };
                _list.DoubleClick += (o, e) => Accept();

                var okButton = new Button
                {
                    Parent = this,
                    Text = "确定",
                    Location = new Point(168, 474),
                    Size = new Size(86, 32)
                };
                okButton.Click += (o, e) => Accept();

                var cancelButton = new Button
                {
                    Parent = this,
                    Text = "取消",
                    Location = new Point(262, 474),
                    Size = new Size(86, 32),
                    DialogResult = DialogResult.Cancel
                };

                Fill(exclude);
            }

            private void Fill(List<BuffType> exclude)
            {
                _list.Items.Clear();

                string filter = _filter.Text.Trim();

                foreach (BuffType type in Enum.GetValues(typeof(BuffType)))
                {
                    int value = (int)type;

                    // 跳过 None、自定义槽位，以及已经配过的
                    if (type == BuffType.None) continue;
                    if (value >= BuffConfig.FirstCustomType) continue;
                    if (exclude.Contains(type)) continue;

                    string name = type.ToString();

                    if (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    _list.Items.Add(name);
                }
            }

            private void Accept()
            {
                if (_list.SelectedItem == null) return;

                BuffType type;

                if (!Enum.TryParse(_list.SelectedItem.ToString(), false, out type)) return;

                Selected = type;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
