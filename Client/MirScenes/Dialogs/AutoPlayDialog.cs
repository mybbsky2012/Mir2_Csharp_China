using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using Client.MirSounds;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 内挂（自动挂机）设置面板 —— 三页标签：战斗 / 保护 / 其他（10-26 改版）。
    ///
    /// 面板本身只修改本地 Settings，实际动作由 MapControl 的内挂逻辑执行；
    /// 「免蜡 / 穿人 / 免助跑 / 超负重 / 泰山」属于服务器开关，点选后向服务器申请，最终状态以服务器回执为准。
    ///
    /// 版面（面板 308x483，底图 Prguse 256）：标题 y=5，标签牌 y=36，内容区 y=60~430，装饰区 434 以下。
    /// 「内挂总开关」只放在第 1 页（战斗），不随页切换重复出现。
    /// </summary>
    public sealed class AutoPlayDialog : MirImageControl
    {
        public MirLabel TitleLabel;
        public MirButton CloseButton;

        // ── 标签页 ──────────────────────────────────────────
        public MirButton[] TabButton = new MirButton[3];
        public MirLabel[] TabLabel = new MirLabel[3];
        private readonly List<MirControl>[] _pages = new List<MirControl>[3];
        private readonly List<MirEditableDropDown> _inputs = new List<MirEditableDropDown>();
        private int _activePage;

        private static readonly string[] TabNames = new string[] { "战斗", "保护", "其他" };
        private static readonly int[] TabX = new int[] { 10, 106, 202 };

        // ── 第 1 页：战斗 ───────────────────────────────────
        public MirCheckBox AllBox, AttackBox, MoveBox, AutoMoveRunBox, AutoSkillBox, AutoDodgeBox,
                           ThrustingGapBox, MagicLockBox;

        // ── 第 2 页：保护 ───────────────────────────────────
        public MirCheckBox PotHPBox, PotMPBox;
        public MirCheckBox[] PotRowBox = new MirCheckBox[4];
        public MirEditableDropDown[] PotRowName = new MirEditableDropDown[4];
        public MirEditableDropDown[] PotRowPct = new MirEditableDropDown[4];
        public MirEditableDropDown[] PotRowItv = new MirEditableDropDown[4];

        public MirCheckBox ScrollBox;
        public MirEditableDropDown ScrollPct;
        public MirEditableDropDown ScrollItem;

        public MirCheckBox RepairBox;
        public MirEditableDropDown RepairPct;
        public MirEditableDropDown RepairItem;

        // 自动续气血石（宝石位）
        public MirCheckBox RenewStoneBox;

        // ── 第 3 页：其他 ───────────────────────────────────
        public MirCheckBox PickUpBox, ItemFilterBox, HealthNumberBox, PoisonSwapBox,
                           NoLampBox, WalkThroughBox, NoRunUpBox, OverWeightBox, MountTaiBox;

        // 简装（纯本地渲染替换，不发包、不影响服务端判定）
        public MirCheckBox SimplePlayerBox, SimpleMonsterBox;

        // 免shift（纯本地输入判定：点玩家/宝宝不再需要按住 Shift）
        public MirCheckBox NoShiftBox;

        // 只显示身边 1 格内的玩家（纯本地渲染过滤，防同屏大量玩家卡屏）
        public MirCheckBox NearPlayerBox;

        public MirLabel HintLabel;

        public AutoPlayDialog()
        {
            Index = 256;
            Library = Libraries.Prguse;
            AutoSize = false;
            Size = new Size(308, 483);
            Movable = true;
            Sort = true;
            Location = new Point((Settings.ScreenWidth - Size.Width) / 2, (Settings.ScreenHeight - Size.Height) / 2);

            for (int i = 0; i < 3; i++) _pages[i] = new List<MirControl>();

            TitleLabel = new MirLabel
            {
                Location = new Point(8, 5),
                Size = new Size(292, 20),
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                Parent = this,
                NotControl = true,
                Text = "内挂设置"
            };

            CloseButton = new MirButton
            {
                HoverIndex = 361,
                Index = 360,
                PressedIndex = 362,
                Location = new Point(279, 4),
                Library = Libraries.Prguse2,
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            // Hide() 里已经会 Save()，这里不要再存一次（重复落盘纯属浪费）
            CloseButton.Click += (o, e) => Hide();

            BuildTabs();
            BuildPageBattle();
            BuildPageProtect();
            BuildPageOther();

            HintLabel = new MirLabel
            {
                Location = new Point(0, 460),
                Size = new Size(308, 14),
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                Parent = this,
                NotControl = true,
                Text = "",
            };

            SwitchPage(0);
        }

        // ══════════════════════════════════════════════════
        // 标签
        // ══════════════════════════════════════════════════
        private void BuildTabs()
        {
            for (int i = 0; i < 3; i++)
            {
                int index = i;

                TabButton[i] = new MirButton
                {
                    Index = 2214,
                    HoverIndex = 2215,
                    PressedIndex = 2215,
                    Library = Libraries.Prguse,
                    Location = new Point(TabX[i], 36),
                    Parent = this,
                    Sound = SoundList.ButtonA,
                };
                TabButton[i].Click += (o, e) => SwitchPage(index);

                TabLabel[i] = new MirLabel
                {
                    Location = new Point(TabX[i], 40),
                    Size = new Size(96, 20),
                    DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                    Parent = this,
                    NotControl = true,
                    Text = TabNames[i],
                };
            }
        }

        public void SwitchPage(int page)
        {
            _activePage = page;

            for (int p = 0; p < 3; p++)
            {
                bool on = p == page;

                foreach (MirControl c in _pages[p])
                {
                    if (c == null || c.IsDisposed) continue;

                    c.Visible = on;

                    MirEditableDropDown dd = c as MirEditableDropDown;
                    if (dd != null) dd.SetHostVisible(on);
                }

                if (TabButton[p] != null && !TabButton[p].IsDisposed)
                    TabButton[p].Index = on ? 2215 : 2214;

                if (TabLabel[p] != null && !TabLabel[p].IsDisposed)
                    TabLabel[p].ForeColour = on ? Color.FromArgb(255, 255, 244, 210) : Color.FromArgb(255, 172, 162, 144);
            }

            Redraw();
        }

        private T Reg<T>(int page, T c) where T : MirControl
        {
            _pages[page].Add(c);

            MirEditableDropDown dd = c as MirEditableDropDown;
            if (dd != null) _inputs.Add(dd);

            return c;
        }

        // ══════════════════════════════════════════════════
        // 第 1 页：战斗
        // ══════════════════════════════════════════════════
        private void BuildPageBattle()
        {
            const int P = 0;

            AllBox = Reg(P, CreateBox(26, 76, "内挂总开关", Settings.AutoPlay));
            AllBox.Click += (o, e) =>
            {
                Settings.AutoPlay = AllBox.Checked;

                if (!Settings.AutoPlay && MapObject.User != null)
                    MapObject.TargetObjectID = 0;

                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoPlay ? "内挂已开启" : "内挂已关闭");
            };

            AttackBox = Reg(P, CreateBox(26, 112, "自动攻击", Settings.AutoAttack));
            AttackBox.Click += (o, e) => { Settings.AutoAttack = AttackBox.Checked; Settings.Save(); };

            MoveBox = Reg(P, CreateBox(26, 166, "自动走位", Settings.AutoMove));
            MoveBox.Click += (o, e) => { Settings.AutoMove = MoveBox.Checked; Settings.Save(); };

            AutoSkillBox = Reg(P, CreateBox(26, 220, "自动技能", Settings.AutoSkill));
            AutoSkillBox.Click += (o, e) =>
            {
                Settings.AutoSkill = AutoSkillBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoSkill ? "自动技能已开启：远怪用远程、近怪用近攻、围攻用群攻" : "自动技能已关闭");
            };

            AutoDodgeBox = Reg(P, CreateBox(26, 274, "自动躲避", Settings.AutoDodge));
            AutoDodgeBox.Click += (o, e) =>
            {
                Settings.AutoDodge = AutoDodgeBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoDodge ? "自动躲避已开启：被围攻时自动走开" : "自动躲避已关闭");
            };

            ThrustingGapBox = Reg(P, CreateBox(162, 112, "隔位刺杀", Settings.AutoThrustingGap));
            ThrustingGapBox.Click += (o, e) =>
            {
                Settings.AutoThrustingGap = ThrustingGapBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoThrustingGap
                    ? "隔位刺杀已开启：战士自动走到与目标隔一格的位置，刀刀用刺杀剑气打（勾选即生效，不必开内挂总开关）"
                    : "隔位刺杀已关闭");
            };

            MagicLockBox = Reg(P, CreateBox(162, 166, "魔法锁定", Settings.MagicLock));
            MagicLockBox.Click += (o, e) =>
            {
                Settings.MagicLock = MagicLockBox.Checked;
                Settings.Save();

                if (!Settings.MagicLock)
                    MapObject.LockObjectID = 0;

                GameScene.Scene.OutputMessage(Settings.MagicLock
                    ? "魔法锁定已开启：锁定鼠标指向的玩家/怪物/NPC，鼠标移开不丢目标，指向新对象自动换锁"
                    : "魔法锁定已关闭");
            };

            AutoMoveRunBox = Reg(P, CreateBox(162, 220, "走位跑步", Settings.AutoMoveRun));
            AutoMoveRunBox.Click += (o, e) => { Settings.AutoMoveRun = AutoMoveRunBox.Checked; Settings.Save(); };

            Reg(P, CreateHint(360, "总开关开启后自动打怪：自动攻击最近目标、被围攻自动走位躲避、按距离放技能。"));
        }

        // ══════════════════════════════════════════════════
        // 第 2 页：保护
        // ══════════════════════════════════════════════════
        private void BuildPageProtect()
        {
            const int P = 1;

            // ── 两个喝药总闸（自动打怪时生效：必须开内挂总开关）──
            PotHPBox = Reg(P, CreateBox(14, 64, "自动喝血药（自动打怪）", Settings.AutoPotHP));
            PotHPBox.Click += (o, e) =>
            {
                Settings.AutoPotHP = PotHPBox.Checked;
                Settings.Save();

                if (!Settings.AutoPotHP)
                {
                    GameScene.Scene.OutputMessage("自动喝血药已关闭");
                    return;
                }

                // 防呆：总闸开了但下面一行「启用」都没勾，照样一口不喝
                bool anyRow = Settings.PotRowOn[0] || Settings.PotRowOn[2];
                GameScene.Scene.OutputMessage(anyRow
                    ? "自动喝血药已开启：只在自动打怪（第 1 页总开关）开启时生效，血量低于明细表百分比就喝"
                    : "自动喝血药已开启，但下面的明细行一个都没启用——请至少勾上第 1 行，并填好百分比和药品名");
            };

            PotMPBox = Reg(P, CreateBox(14, 86, "自动喝蓝药（自动打怪）", Settings.AutoPotMP));
            PotMPBox.Click += (o, e) =>
            {
                Settings.AutoPotMP = PotMPBox.Checked;
                Settings.Save();

                if (!Settings.AutoPotMP)
                {
                    GameScene.Scene.OutputMessage("自动喝蓝药已关闭");
                    return;
                }

                bool anyRow = Settings.PotRowOn[1] || Settings.PotRowOn[3];
                GameScene.Scene.OutputMessage(anyRow
                    ? "自动喝蓝药已开启：只在自动打怪（第 1 页总开关）开启时生效，魔法低于明细表百分比就喝"
                    : "自动喝蓝药已开启，但下面的明细行一个都没启用——请至少勾上第 2 行，并填好百分比和药品名");
            };

            Reg(P, CreateSection("药水明细（打怪看总闸）", 108));
            Reg(P, CreateHeader(14, 124, "启用"));
            Reg(P, CreateHeader(84, 124, "百分比"));
            Reg(P, CreateHeader(120, 124, "使用物品(可输入)"));
            Reg(P, CreateHeader(228, 124, "间隔(毫秒)"));

            for (int i = 0; i < 4; i++)
            {
                int row = i;
                int y = 142 + i * 24;
                bool isHP = (i == 0 || i == 2);

                PotRowBox[i] = Reg(P, CreateBox(14, y, isHP ? "血量低于" : "魔法低于", Settings.PotRowOn[i]));
                PotRowBox[i].Click += (o, e) =>
                {
                    Settings.PotRowOn[row] = PotRowBox[row].Checked;
                    Settings.Save();

                    // 勾上这一行就给个明确反馈：不打怪也生效，免得又觉得「勾了没反应」
                    if (Settings.PotRowOn[row])
                    {
                        string itemName = (Settings.PotRowItem[row] ?? string.Empty).Trim();
                        if (itemName.Length == 0) itemName = "背包里的对应药水";

                        GameScene.Scene.OutputMessage(string.Format(
                            "第 {0} 行已启用：{1}低于 {2}% 自动使用「{3}」，不开自动打怪也生效",
                            row + 1, isHP ? "血量" : "魔法", Settings.PotRowPercent[row], itemName));
                    }
                };

                PotRowPct[i] = Reg(P, CreateNum(84, y - 3, 30, Settings.PotRowPercent[i].ToString()));
                PotRowPct[i].TextChanged += (o, e) =>
                {
                    Settings.PotRowPercent[row] = Clamp(ParseInt(PotRowPct[row].Text, 50), 1, 99);
                };

                PotRowName[i] = Reg(P, CreateItemDrop(120, y - 3, 96, Settings.PotRowItem[i]));
                PotRowName[i].TextChanged += (o, e) =>
                {
                    Settings.PotRowItem[row] = PotRowName[row].Text;
                };

                PotRowItv[i] = Reg(P, CreateNum(228, y - 3, 52, Settings.PotRowInterval[i].ToString()));
                PotRowItv[i].TextChanged += (o, e) =>
                {
                    Settings.PotRowInterval[row] = Math.Max(100, ParseInt(PotRowItv[row].Text, 1000));
                };
            }

            // ── 传送保护（不受内挂总开关限制，勾选即生效）──
            Reg(P, CreateSection("传送保护（勾选即生效）", 240));
            Reg(P, CreateHeader(14, 256, "启用"));
            Reg(P, CreateHeader(84, 256, "百分比"));
            Reg(P, CreateHeader(120, 256, "使用物品(可输入)"));

            ScrollBox = Reg(P, CreateBox(14, 274, "血量低于", Settings.AutoScrollOn));
            ScrollBox.Click += (o, e) =>
            {
                Settings.AutoScrollOn = ScrollBox.Checked;
                Settings.Save();
            };

            ScrollPct = Reg(P, CreateNum(84, 271, 30, Settings.AutoScrollPercent.ToString()));
            ScrollPct.TextChanged += (o, e) =>
            {
                Settings.AutoScrollPercent = Clamp(ParseInt(ScrollPct.Text, 15), 1, 99);
            };

            ScrollItem = Reg(P, CreateItemDrop(120, 271, 96, Settings.AutoScrollItem));
            ScrollItem.TextChanged += (o, e) =>
            {
                Settings.AutoScrollItem = string.IsNullOrWhiteSpace(ScrollItem.Text) ? "小退" : ScrollItem.Text.Trim();
            };

            // ── 装备保护 ──
            Reg(P, CreateSection("装备保护", 300));
            Reg(P, CreateHeader(14, 316, "启用"));
            Reg(P, CreateHeader(82, 316, "持久低于"));
            Reg(P, CreateHeader(130, 316, "使用物品(可输入)"));

            RepairBox = Reg(P, CreateBox(14, 334, "全部装备", Settings.AutoRepairOn));
            RepairBox.Click += (o, e) =>
            {
                Settings.AutoRepairOn = RepairBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoRepairOn
                    ? "装备保护已开启：装备持久低于 " + Settings.AutoRepairPercent + "%（或掉到 1）自动使用修复物品，勾选即生效，不需要内挂总开关"
                    : "装备保护已关闭");
            };

            RepairPct = Reg(P, CreateNum(84, 331, 26, Settings.AutoRepairPercent.ToString()));
            RepairPct.TextChanged += (o, e) =>
            {
                Settings.AutoRepairPercent = Clamp(ParseInt(RepairPct.Text, 5), 1, 99);
            };

            RepairItem = Reg(P, CreateItemDrop(126, 331, 100, Settings.AutoRepairItem));
            RepairItem.TextChanged += (o, e) =>
            {
                Settings.AutoRepairItem = RepairItem.Text;
            };

            // ── 自动续气血石（10-26）：只认宝石位（守护石槽）──
            RenewStoneBox = Reg(P, CreateBox(14, 358, "自动续气血石(气血/魔血/幻血)", Settings.AutoRenewStone));
            RenewStoneBox.Click += (o, e) =>
            {
                Settings.AutoRenewStone = RenewStoneBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoRenewStone
                    ? "自动续气血石已开启：宝石位（守护石槽）的气血石/魔血石/幻血石电量用完后，自动从背包换一块新的穿上，勾选即生效"
                    : "自动续气血石已关闭");
            };

            Reg(P, new MirLabel
            {
                Location = new Point(113, 333),
                Size = new Size(12, 14),
                Parent = this,
                NotControl = true,
                Text = "%",
                ForeColour = Color.FromArgb(255, 245, 238, 220),
            });

            Reg(P, CreateHint(386, "喝药两开关只在打怪时生效，其余勾了即生效。装备耐久低自动修，宝石位石头用完自动换（玩家/英雄都管）。", 40));
        }

        // ══════════════════════════════════════════════════
        // 第 3 页：其他
        // ══════════════════════════════════════════════════
        private void BuildPageOther()
        {
            const int P = 2;

            PickUpBox = Reg(P, CreateBox(26, 76, "自动捡物", Settings.AutoPickup));
            PickUpBox.Click += (o, e) =>
            {
                Settings.AutoPickup = PickUpBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoPickup
                    ? "自动捡物已开启：未开总开关时只捡脚下的物品；开了总开关会主动去捡，且只捡物品过滤列表里的物品"
                    : "自动捡物已关闭");
            };

            ItemFilterBox = Reg(P, CreateBox(26, 122, "物品过滤", Settings.ItemFilterOn));
            ItemFilterBox.Click += (o, e) =>
            {
                Settings.ItemFilterOn = ItemFilterBox.Checked;
                Settings.Save();

                ItemFilter.Reload();

                if (!Settings.ItemFilterOn)
                {
                    GameScene.Scene.OutputMessage("物品过滤已关闭：恢复显示全部地面物品");
                    return;
                }

                if (ItemFilter.NameCount == 0 && ItemFilter.MinGrade == ItemGrade.None)
                {
                    GameScene.Scene.OutputMessage("物品过滤已开启，但列表为空（Data\\ItemFilter.txt 不存在或没有有效行），暂不生效");
                    return;
                }

                GameScene.Scene.OutputMessage(string.Format("物品过滤已开启：只显示列表内的物品（名字 {0} 条{1}）",
                    ItemFilter.NameCount,
                    ItemFilter.MinGrade != ItemGrade.None ? "，品质≥" + ItemFilter.MinGrade : string.Empty));
            };

            HealthNumberBox = Reg(P, CreateBox(26, 168, "数字显血", Settings.HealthNumber));
            HealthNumberBox.Click += (o, e) =>
            {
                Settings.HealthNumber = HealthNumberBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.HealthNumber
                    ? "数字显血已开启：血条下方显示 Lv.等级 + 血量（自己/宝宝、队友，以及刚被你打到的怪物）"
                    : "数字显血已关闭");
            };

            PoisonSwapBox = Reg(P, CreateBox(26, 214, "毒符互换", Settings.AutoSwapPoison));
            PoisonSwapBox.Click += (o, e) =>
            {
                Settings.AutoSwapPoison = PoisonSwapBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoSwapPoison
                    ? "毒符互换已开启：吃符的技能自动换护身符（含血龙兽）、施毒术/毒雾换毒粉（黄/灰交替，毒雾要 5 个；灰毒粉=群毒掉血、黄毒粉=群毒掉防）、苏生术换苏生符（不必开内挂总开关）"
                    : "毒符互换已关闭");
            };

            NoLampBox = Reg(P, CreateBox(26, 260, "免蜡", Settings.NoLamp));
            NoLampBox.Click += (o, e) =>
            {
                if (!GameScene.NoLampAllowed)
                {
                    NoLampBox.Checked = false;
                    GameScene.Scene.OutputMessage("本服务器未开放免蜡功能");
                    return;
                }

                GameScene.RequestPlayerOption(PlayerOptionType.NoLamp, NoLampBox.Checked);
            };

            WalkThroughBox = Reg(P, CreateBox(162, 76, "穿人/怪/NPC", Settings.WalkThrough));
            WalkThroughBox.Click += (o, e) =>
            {
                if (!GameScene.WalkThroughAllowed && !GameScene.WalkThroughMonsterAllowed
                    && !GameScene.WalkThroughNPCAllowed && !GameScene.WalkThroughWallAllowed)
                {
                    WalkThroughBox.Checked = false;
                    GameScene.Scene.OutputMessage("本服务器未开放穿人功能");
                    return;
                }

                if (WalkThroughBox.Checked)
                {
                    string can = "";
                    if (GameScene.WalkThroughAllowed) can += "玩家 ";
                    if (GameScene.WalkThroughMonsterAllowed) can += "怪物 ";
                    if (GameScene.WalkThroughNPCAllowed) can += "NPC ";
                    if (GameScene.WalkThroughWallAllowed) can += "城门城墙 ";
                    if (can.Length == 0) can = "（无）";

                    GameScene.Scene.OutputMessage("穿人已开启，可穿过：" + can.Trim()
                        + (GameScene.WalkThroughWallAllowed ? "" : "；城门、城墙不可穿"));
                }

                GameScene.RequestPlayerOption(PlayerOptionType.WalkThrough, WalkThroughBox.Checked);
            };

            NoRunUpBox = Reg(P, CreateBox(162, 122, "免助跑", Settings.NoRunUp));
            NoRunUpBox.Click += (o, e) =>
            {
                if (!GameScene.NoRunUpAllowed)
                {
                    NoRunUpBox.Checked = false;
                    GameScene.Scene.OutputMessage("本服务器未开放免助跑功能");
                    return;
                }

                GameScene.RequestPlayerOption(PlayerOptionType.NoRunUp, NoRunUpBox.Checked);
            };

            OverWeightBox = Reg(P, CreateBox(162, 168, "超负重", Settings.OverWeight));
            OverWeightBox.Click += (o, e) =>
            {
                if (!GameScene.OverWeightAllowed)
                {
                    OverWeightBox.Checked = false;
                    GameScene.Scene.OutputMessage("本服务器未开放超负重功能");
                    return;
                }

                GameScene.RequestPlayerOption(PlayerOptionType.OverWeight, OverWeightBox.Checked);
            };

            MountTaiBox = Reg(P, CreateBox(162, 214, "泰山(不后仰)", Settings.MountTai));
            MountTaiBox.Click += (o, e) =>
            {
                if (!GameScene.MountTaiAllowed)
                {
                    MountTaiBox.Checked = false;
                    GameScene.Scene.OutputMessage("本服务器未开放泰山功能");
                    return;
                }

                GameScene.RequestPlayerOption(PlayerOptionType.MountTai, MountTaiBox.Checked);
            };

            // ── 免shift（第 3 行右列）──────────────────────────
            // 纯本地输入判定，不发包：勾上后左键点「别的玩家 / 宝宝」直接攻击，不用再按住 Shift。
            // 点地面不触发（仍是正常走路），所以不会出现原地乱挥。
            NoShiftBox = Reg(P, CreateBox(162, 260, "免shift", Settings.NoShift));
            NoShiftBox.Click += (o, e) =>
            {
                Settings.NoShift = NoShiftBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.NoShift
                    ? "免shift已开启：左键点玩家、宝宝直接攻击，不必再按住 Shift（点地面仍正常走路；只想交易或看装备时请先关掉）"
                    : "免shift已关闭：攻击玩家、宝宝需要按住 Shift");
            };

            // ── 简装（第 4 行）────────────────────────────────
            // 纯本地渲染替换：只改「画哪本图集」，不发包、不碰服务端，别人看到的你还是原样。
            SimplePlayerBox = Reg(P, CreateBox(26, 306, "人物简装", Settings.SimplePlayer));
            SimplePlayerBox.Click += (o, e) =>
            {
                Settings.SimplePlayer = SimplePlayerBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.SimplePlayer
                    ? "人物简装已开启：视野内所有玩家统一显示为「布衣 + 木剑」（含自己、英雄、宝宝），仅本地显示，不影响别人"
                    : "人物简装已关闭");
            };

            SimpleMonsterBox = Reg(P, CreateBox(162, 306, "怪物简装", Settings.SimpleMonster));
            SimpleMonsterBox.Click += (o, e) =>
            {
                Settings.SimpleMonster = SimpleMonsterBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.SimpleMonster
                    ? "怪物简装已开启：除卫士、弓箭手、练功师外，所有怪物统一显示为稻草人（仅本地显示，打怪判定不变）"
                    : "怪物简装已关闭");
            };

            // ── 只显示 1 格内玩家（第 5 行）────────────────────
            // 纯本地渲染过滤：勾上后，超出自己身边 1 格（含斜向 8 格）的其他玩家整条不画，
            // 怪物/NPC/掉落/宝宝/英雄照常显示。防同屏大量玩家（尤其假人）把帧数拖垮。
            // 常显白名单（MapObject.NearPlayerKeepVisible）：队友、同会、鼠标指向、当前目标/魔法锁定。
            NearPlayerBox = Reg(P, CreateBox(26, 340, "只显示1格内玩家", Settings.NearPlayerOnly));
            NearPlayerBox.Click += (o, e) =>
            {
                Settings.NearPlayerOnly = NearPlayerBox.Checked;
                Settings.Save();

                // 立刻生效：世界层（MapControl）是整屏缓存到 RenderTarget 的，只 Redraw 场景不重画地图，
                // 勾上后会「看着没反应」，要等下次有人走动才刷新。
                GameScene.Scene.Redraw();
                if (GameScene.Scene.MapControl != null && !GameScene.Scene.MapControl.IsDisposed)
                    GameScene.Scene.MapControl.TextureValid = false;

                GameScene.Scene.OutputMessage(Settings.NearPlayerOnly
                    ? "只显示1格内玩家已开启：1 格以外的玩家不显示；队友、同会、鼠标指向、当前目标（含魔法锁定）仍然常显（怪物、NPC、掉落、宝宝、英雄照常；仅本地显示，不影响别人和战斗判定）"
                    : "只显示1格内玩家已关闭：恢复正常显示视野内所有玩家");
            };

            Reg(P, CreateHint(360, "自动捡物：未开总开关只捡脚下；开了主动去捡但只捡过滤表内物品。右列需服务器开放。", 28));
            Reg(P, CreateHint(392, "1格内玩家：队友/行会/指向/目标常显。简装、免shift只改本地：攻击掉落照常，点玩家照打。", 32));
        }
        // ══════════════════════════════════════════════════
        // 控件工厂
        // ══════════════════════════════════════════════════
        private MirCheckBox CreateBox(int x, int y, string text, bool value)
        {
            MirCheckBox box = new MirCheckBox
            {
                Index = 2086,
                UnTickedIndex = 2086,
                TickedIndex = 2087,
                Library = Libraries.Prguse,
                Parent = this,
                Location = new Point(x, y),
                Size = new Size(16, 12),
                LabelText = text
            };

            box.Checked = value;

            return box;
        }

        private MirLabel CreateSection(string text, int y)
        {
            return new MirLabel
            {
                Location = new Point(0, y),
                Size = new Size(308, 14),
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                Parent = this,
                NotControl = true,
                Text = text,
                ForeColour = Color.FromArgb(255, 238, 214, 150),
            };
        }

        private MirLabel CreateHeader(int x, int y, string text)
        {
            return new MirLabel
            {
                Location = new Point(x, y),
                Size = new Size(120, 14),
                Parent = this,
                NotControl = true,
                Text = text,
                ForeColour = Color.FromArgb(255, 188, 178, 158),
            };
        }

        private MirLabel CreateHint(int y, string text, int h = 32)
        {
            return new MirLabel
            {
                Location = new Point(0, y),
                Size = new Size(308, h),
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak,
                Parent = this,
                NotControl = true,
                Text = text,
                ForeColour = Color.FromArgb(255, 155, 145, 128),
            };
        }

        private MirEditableDropDown CreateNum(int x, int y, int w, string text)
        {
            MirEditableDropDown dd = new MirEditableDropDown
            {
                Parent = this,
                Location = new Point(x, y),
                Size = new Size(w, 17),
                NumbersOnly = true,     // 纯数字：只认 0-9，不挂任何候选列表
                ShowArrow = false,      // 不显示下拉箭头，避免弹出无关候选（右键也不用）
                MaxLength = 5,
            };

            dd.Text = text;

            return dd;
        }

        private MirEditableDropDown CreateItemDrop(int x, int y, int w, string text)
        {
            MirEditableDropDown dd = new MirEditableDropDown
            {
                Parent = this,
                Location = new Point(x, y),
                Size = new Size(w, 17),
                MaxLength = 24,
            };

            dd.Text = text;
            dd.SetItems(AutoPlayDialog.BagItemNames());

            return dd;
        }

        /// <summary>背包里可用的物品名（药水 / 卷轴 / 特殊消耗品），供「使用物品」下拉挑选</summary>
        public static List<string> BagItemNames()
        {
            List<string> list = new List<string>();

            if (MapObject.User == null || MapObject.User.Inventory == null) return list;

            for (int i = 0; i < MapObject.User.Inventory.Length; i++)
            {
                UserItem item = MapObject.User.Inventory[i];
                if (item == null || item.Info == null) continue;

                ItemType t = item.Info.Type;
                if (t != ItemType.药水 && t != ItemType.卷轴 && t != ItemType.特殊消耗品) continue;
                if (!list.Contains(item.Info.Name)) list.Add(item.Info.Name);
            }

            return list;
        }

        /// <summary>打开时刷新下拉候选（背包可能变了）</summary>
        public void RefreshItemLists()
        {
            List<string> names = BagItemNames();

            foreach (MirEditableDropDown dd in _inputs)
            {
                if (dd == null || dd.IsDisposed) continue;

                // 数字框（百分比 / 间隔）绝不能塞物品名，否则会显示成「体力」「魔力」这种残字
                if (dd.NumbersOnly)
                {
                    dd.SetItems(null);
                    continue;
                }

                dd.SetItems(names);
            }

            // 传送保护的「使用物品」多一项「小退」（默认行为）
            if (ScrollItem != null && !ScrollItem.IsDisposed)
            {
                List<string> scrollNames = new List<string>(names);
                scrollNames.Insert(0, "小退");
                ScrollItem.SetItems(scrollNames);
            }
        }

        private static int ParseInt(string s, int def)
        {
            int v;
            if (int.TryParse((s ?? string.Empty).Trim(), out v)) return v;
            return def;
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        // ══════════════════════════════════════════════════
        // 显示 / 隐藏：同步浮在 Form 上的输入框
        // ══════════════════════════════════════════════════
        public override void Show()
        {
            base.Show();

            RefreshItemLists();
            SwitchPage(_activePage);

            foreach (MirControl c in _pages[_activePage])
            {
                MirEditableDropDown dd = c as MirEditableDropDown;
                if (dd != null && !dd.IsDisposed) dd.SetHostVisible(true);
            }
        }

        public override void Hide()
        {
            foreach (MirEditableDropDown dd in _inputs)
                if (dd != null && !dd.IsDisposed)
                    dd.SetHostVisible(false);

            Settings.Save();
            base.Hide();
        }

        /// <summary>根据 Settings 与服务器回执刷新界面状态</summary>
        public void UpdateState()
        {
            if (AllBox == null || AllBox.IsDisposed) return;

            AllBox.Checked = Settings.AutoPlay;
            AttackBox.Checked = Settings.AutoAttack;
            MoveBox.Checked = Settings.AutoMove;
            AutoSkillBox.Checked = Settings.AutoSkill;
            AutoDodgeBox.Checked = Settings.AutoDodge;
            AutoMoveRunBox.Checked = Settings.AutoMoveRun;
            ThrustingGapBox.Checked = Settings.AutoThrustingGap;
            MagicLockBox.Checked = Settings.MagicLock;

            PotHPBox.Checked = Settings.AutoPotHP;
            PotMPBox.Checked = Settings.AutoPotMP;

            for (int i = 0; i < 4; i++)
            {
                if (PotRowBox[i] != null && !PotRowBox[i].IsDisposed) PotRowBox[i].Checked = Settings.PotRowOn[i];
                if (PotRowPct[i] != null && !PotRowPct[i].IsDisposed) PotRowPct[i].Text = Settings.PotRowPercent[i].ToString();
                if (PotRowName[i] != null && !PotRowName[i].IsDisposed) PotRowName[i].Text = Settings.PotRowItem[i];
                if (PotRowItv[i] != null && !PotRowItv[i].IsDisposed) PotRowItv[i].Text = Settings.PotRowInterval[i].ToString();
            }

            if (ScrollBox != null && !ScrollBox.IsDisposed) ScrollBox.Checked = Settings.AutoScrollOn;
            if (ScrollPct != null && !ScrollPct.IsDisposed) ScrollPct.Text = Settings.AutoScrollPercent.ToString();

            if (RepairBox != null && !RepairBox.IsDisposed) RepairBox.Checked = Settings.AutoRepairOn;
            if (RepairPct != null && !RepairPct.IsDisposed) RepairPct.Text = Settings.AutoRepairPercent.ToString();
            if (RepairItem != null && !RepairItem.IsDisposed) RepairItem.Text = Settings.AutoRepairItem;
            if (RenewStoneBox != null && !RenewStoneBox.IsDisposed) RenewStoneBox.Checked = Settings.AutoRenewStone;

            PickUpBox.Checked = Settings.AutoPickup;
            ItemFilterBox.Checked = Settings.ItemFilterOn;
            HealthNumberBox.Checked = Settings.HealthNumber;
            PoisonSwapBox.Checked = Settings.AutoSwapPoison;

            NoLampBox.Checked = Settings.NoLamp;
            WalkThroughBox.Checked = Settings.WalkThrough;
            NoRunUpBox.Checked = Settings.NoRunUp;
            OverWeightBox.Checked = Settings.OverWeight;
            MountTaiBox.Checked = Settings.MountTai;

            // 免shift 是纯本地开关，打开面板时按配置回填（配置被手改过也能对上）
            if (NoShiftBox != null && !NoShiftBox.IsDisposed) NoShiftBox.Checked = Settings.NoShift;
            if (SimplePlayerBox != null && !SimplePlayerBox.IsDisposed) SimplePlayerBox.Checked = Settings.SimplePlayer;
            if (SimpleMonsterBox != null && !SimpleMonsterBox.IsDisposed) SimpleMonsterBox.Checked = Settings.SimpleMonster;
            if (NearPlayerBox != null && !NearPlayerBox.IsDisposed) NearPlayerBox.Checked = Settings.NearPlayerOnly;

            NoLampBox.Enabled = GameScene.NoLampAllowed;
            WalkThroughBox.Enabled = GameScene.WalkThroughAllowed || GameScene.WalkThroughMonsterAllowed
                                     || GameScene.WalkThroughNPCAllowed || GameScene.WalkThroughWallAllowed;
            NoRunUpBox.Enabled = GameScene.NoRunUpAllowed;
            OverWeightBox.Enabled = GameScene.OverWeightAllowed;
            MountTaiBox.Enabled = GameScene.MountTaiAllowed;

            Redraw();
        }
    }
}
