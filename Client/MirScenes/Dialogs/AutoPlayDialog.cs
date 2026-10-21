using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using Client.MirSounds;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 内挂（自动挂机）设置面板。
    /// 面板本身只修改本地 Settings，实际动作由 MapControl.ProcessAutoPlay 执行；
    /// 「免蜡 / 穿人 / 免助跑 / 超负重 / 泰山」属于服务器开关，点选后会向服务器申请，最终状态以服务器回执为准。
    /// </summary>
    public sealed class AutoPlayDialog : MirImageControl
    {
        public MirLabel TitleLabel;
        public MirButton CloseButton;

        public MirCheckBox AllBox, AttackBox, MoveBox, PickUpBox, MagicLockBox, PotHPBox, PotMPBox,
                           AutoMoveRunBox, AutoSkillBox, AutoDodgeBox, PoisonSwapBox, ThrustingGapBox,
                           NoLampBox, WalkThroughBox, NoRunUpBox, OverWeightBox, MountTaiBox,
                           ItemFilterBox, HealthNumberBox;
        public MirLabel HintLabel;

        public AutoPlayDialog()
        {
            // 背景图：Prguse.Lib 索引 256（308x483 大面板，上标题栏 + 大片黑色内容区 + 底部装饰）
            // 注意：代码里的 Index 是 0 基索引，比素材查看器里的「第N张」小 1
            Index = 256;
            Library = Libraries.Prguse;
            AutoSize = false;
            Size = new Size(308, 483);
            Movable = true;
            Sort = true;
            Location = new Point((Settings.ScreenWidth - Size.Width) / 2, (Settings.ScreenHeight - Size.Height) / 2);

            // 版面：y=0~29 顶部标题栏，y=30~33 金色分隔线，内容区 y=40~430，y=434 以下为底部装饰
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
                Location = new Point(279, 4),   // 顶部标题栏右上角
                Library = Libraries.Prguse2,
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            // 两列布局：左列 x=28（10 项），右列 x=160（9 项），行距 36（内容区 y=40~430）
            AllBox = CreateBox(28, 48, "内挂总开关", Settings.AutoPlay);
            AllBox.Click += (o, e) =>
            {
                Settings.AutoPlay = AllBox.Checked;

                if (!Settings.AutoPlay && MapObject.User != null)
                    MapObject.TargetObjectID = 0;

                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoPlay ? "内挂已开启" : "内挂已关闭");
            };

            AttackBox = CreateBox(28, 84, "自动攻击", Settings.AutoAttack);
            AttackBox.Click += (o, e) => { Settings.AutoAttack = AttackBox.Checked; Settings.Save(); };

            MoveBox = CreateBox(28, 120, "自动走位", Settings.AutoMove);
            MoveBox.Click += (o, e) => { Settings.AutoMove = MoveBox.Checked; Settings.Save(); };

            PickUpBox = CreateBox(28, 156, "自动捡物", Settings.AutoPickup);
            PickUpBox.Click += (o, e) =>
            {
                Settings.AutoPickup = PickUpBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoPickup
                    ? "自动捡物已开启：未开总开关时只捡脚下的物品；开了总开关会主动去捡，且只捡物品过滤列表里的物品"
                    : "自动捡物已关闭");
            };

            // 魔法锁定：锁定鼠标指向的对象（玩家/怪物/NPC），指向新对象自动换锁，鼠标移开不丢目标
            MagicLockBox = CreateBox(28, 192, "魔法锁定", Settings.MagicLock);
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

            PotHPBox = CreateBox(28, 228, "自动喝血药", Settings.AutoPotHP);
            PotHPBox.Click += (o, e) => { Settings.AutoPotHP = PotHPBox.Checked; Settings.Save(); };

            PotMPBox = CreateBox(28, 264, "自动喝蓝药", Settings.AutoPotMP);
            PotMPBox.Click += (o, e) => { Settings.AutoPotMP = PotMPBox.Checked; Settings.Save(); };

            AutoSkillBox = CreateBox(28, 300, "自动技能", Settings.AutoSkill);
            AutoSkillBox.Click += (o, e) =>
            {
                Settings.AutoSkill = AutoSkillBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoSkill ? "自动技能已开启：远怪用远程、近怪用近攻、围攻用群攻" : "自动技能已关闭");
            };

            PoisonSwapBox = CreateBox(28, 336, "毒符互换", Settings.AutoSwapPoison);
            PoisonSwapBox.Click += (o, e) =>
            {
                Settings.AutoSwapPoison = PoisonSwapBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoSwapPoison
                    ? "毒符互换已开启：吃符的技能自动换护身符（含血龙兽）、施毒术/毒雾换毒粉（黄/灰交替，毒雾要 5 个；灰毒粉=群毒掉血、黄毒粉=群毒掉防）、苏生术换苏生符（不必开内挂总开关）"
                    : "毒符互换已关闭");
            };

            // 物品过滤：列表文件 Data\ItemFilter.txt，只有列表里匹配到的地面物品才显示名字
            ItemFilterBox = CreateBox(28, 372, "物品过滤", Settings.ItemFilterOn);
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

            AutoMoveRunBox = CreateBox(160, 48, "走位跑步", Settings.AutoMoveRun);
            AutoMoveRunBox.Click += (o, e) => { Settings.AutoMoveRun = AutoMoveRunBox.Checked; Settings.Save(); };

            // 战士隔位刺杀：勾选即生效（手动打怪也生效），不需要开内挂总开关
            ThrustingGapBox = CreateBox(160, 84, "隔位刺杀", Settings.AutoThrustingGap);
            ThrustingGapBox.Click += (o, e) =>
            {
                Settings.AutoThrustingGap = ThrustingGapBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoThrustingGap
                    ? "隔位刺杀已开启：战士自动走到与目标隔一格的位置，刀刀用刺杀剑气打（勾选即生效，不必开内挂总开关）"
                    : "隔位刺杀已关闭");
            };

            AutoDodgeBox = CreateBox(160, 120, "自动躲避", Settings.AutoDodge);
            AutoDodgeBox.Click += (o, e) =>
            {
                Settings.AutoDodge = AutoDodgeBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoDodge ? "自动躲避已开启：被围攻时自动走开" : "自动躲避已关闭");
            };

            NoLampBox = CreateBox(160, 156, "免蜡", Settings.NoLamp);
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

            WalkThroughBox = CreateBox(160, 192, "穿人/怪/NPC", Settings.WalkThrough);
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

            NoRunUpBox = CreateBox(160, 228, "免助跑", Settings.NoRunUp);
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

            OverWeightBox = CreateBox(160, 264, "超负重", Settings.OverWeight);
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

            MountTaiBox = CreateBox(160, 300, "泰山(不后仰)", Settings.MountTai);
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

            // 数字显血：纯本地显示开关，血条下方显示血量百分比（不影响服务器）
            HealthNumberBox = CreateBox(160, 336, "数字显血", Settings.HealthNumber);
            HealthNumberBox.Click += (o, e) =>
            {
                Settings.HealthNumber = HealthNumberBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.HealthNumber
                    ? "数字显血已开启：血条下方显示血量百分比（自己/宝宝、队友，以及刚被你打到的怪物）"
                    : "数字显血已关闭");
            };

            HintLabel = new MirLabel
            {
                Location = new Point(0, 400),
                Size = new Size(308, 14),
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                Parent = this,
                NotControl = true,
                Text = "右列开关需服务器开放，其余为本地设置",
            };
        }

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

        /// <summary>根据 Settings 与服务器回执刷新界面状态</summary>
        public void UpdateState()
        {
            if (AllBox == null || AllBox.IsDisposed) return;

            AllBox.Checked = Settings.AutoPlay;
            AttackBox.Checked = Settings.AutoAttack;
            MoveBox.Checked = Settings.AutoMove;
            PickUpBox.Checked = Settings.AutoPickup;
            MagicLockBox.Checked = Settings.MagicLock;
            PotHPBox.Checked = Settings.AutoPotHP;
            PotMPBox.Checked = Settings.AutoPotMP;
            AutoMoveRunBox.Checked = Settings.AutoMoveRun;
            AutoSkillBox.Checked = Settings.AutoSkill;
            AutoDodgeBox.Checked = Settings.AutoDodge;
            ThrustingGapBox.Checked = Settings.AutoThrustingGap;
            PoisonSwapBox.Checked = Settings.AutoSwapPoison;
            ItemFilterBox.Checked = Settings.ItemFilterOn;
            HealthNumberBox.Checked = Settings.HealthNumber;

            NoLampBox.Checked = Settings.NoLamp;
            WalkThroughBox.Checked = Settings.WalkThrough;
            NoRunUpBox.Checked = Settings.NoRunUp;
            OverWeightBox.Checked = Settings.OverWeight;
            MountTaiBox.Checked = Settings.MountTai;

            NoLampBox.Enabled = GameScene.NoLampAllowed;
            //4 档里任意一档开放，玩家就能勾选「穿人」（具体能穿什么由服务器按档位判定）
            WalkThroughBox.Enabled = GameScene.WalkThroughAllowed || GameScene.WalkThroughMonsterAllowed
                                     || GameScene.WalkThroughNPCAllowed || GameScene.WalkThroughWallAllowed;
            NoRunUpBox.Enabled = GameScene.NoRunUpAllowed;
            OverWeightBox.Enabled = GameScene.OverWeightAllowed;
            MountTaiBox.Enabled = GameScene.MountTaiAllowed;

            Redraw();
        }
    }
}
