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

        public MirCheckBox AllBox, AttackBox, MoveBox, PickUpBox, PotHPBox, PotMPBox,
                           AutoMoveRunBox, AutoSkillBox, AutoDodgeBox, PoisonSwapBox, ThrustingGapBox,
                           NoLampBox, WalkThroughBox, NoRunUpBox, OverWeightBox, MountTaiBox;
        public MirLabel HintLabel;

        public AutoPlayDialog()
        {
            // 背景图：Title.Lib 索引 199（第200张，264x272 通用黑底金边面板，右上自带关闭叉）
            // 注意：代码里的 Index 是 0 基索引，比素材查看器里的「第N张」小 1
            Index = 199;
            Library = Libraries.Title;
            AutoSize = false;
            Size = new Size(264, 272);
            Movable = true;
            Sort = true;
            Location = new Point((Settings.ScreenWidth - Size.Width) / 2, (Settings.ScreenHeight - Size.Height) / 2);

            // 版面：y=28 为金色分隔线（标题栏），内容区 y=29~238，y=239 以下为底部装饰
            TitleLabel = new MirLabel
            {
                Location = new Point(8, 5),
                Size = new Size(216, 20),
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
                Location = new Point(235, 3),   // 盖在背景图自带的关闭叉（约 243~252, 9~18）上
                Library = Libraries.Prguse2,
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            // 左列：本地开关；右列：服务器开关
            // 每列最多 8 项，行距 24（内容区 y=29~238）
            AllBox = CreateBox(28, 42, "内挂总开关", Settings.AutoPlay);
            AllBox.Click += (o, e) =>
            {
                Settings.AutoPlay = AllBox.Checked;

                if (!Settings.AutoPlay && MapObject.User != null)
                    MapObject.TargetObjectID = 0;

                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoPlay ? "内挂已开启" : "内挂已关闭");
            };

            AttackBox = CreateBox(28, 66, "自动攻击", Settings.AutoAttack);
            AttackBox.Click += (o, e) => { Settings.AutoAttack = AttackBox.Checked; Settings.Save(); };

            MoveBox = CreateBox(28, 90, "自动走位", Settings.AutoMove);
            MoveBox.Click += (o, e) => { Settings.AutoMove = MoveBox.Checked; Settings.Save(); };

            PickUpBox = CreateBox(28, 114, "自动捡物", Settings.AutoPickup);
            PickUpBox.Click += (o, e) => { Settings.AutoPickup = PickUpBox.Checked; Settings.Save(); };

            PotHPBox = CreateBox(28, 138, "自动喝血药", Settings.AutoPotHP);
            PotHPBox.Click += (o, e) => { Settings.AutoPotHP = PotHPBox.Checked; Settings.Save(); };

            PotMPBox = CreateBox(28, 162, "自动喝蓝药", Settings.AutoPotMP);
            PotMPBox.Click += (o, e) => { Settings.AutoPotMP = PotMPBox.Checked; Settings.Save(); };

            AutoSkillBox = CreateBox(28, 186, "自动技能", Settings.AutoSkill);
            AutoSkillBox.Click += (o, e) =>
            {
                Settings.AutoSkill = AutoSkillBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoSkill ? "自动技能已开启：远怪用远程、近怪用近攻、围攻用群攻" : "自动技能已关闭");
            };

            PoisonSwapBox = CreateBox(28, 210, "毒符互换", Settings.AutoSwapPoison);
            PoisonSwapBox.Click += (o, e) =>
            {
                Settings.AutoSwapPoison = PoisonSwapBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoSwapPoison
                    ? "毒符互换已开启：用吃符的技能自动换符、用施毒术自动换毒粉（黄色/灰色毒粉一次使用交替一次，不必开内挂总开关）"
                    : "毒符互换已关闭");
            };

            AutoMoveRunBox = CreateBox(132, 162, "走位跑步", Settings.AutoMoveRun);
            AutoMoveRunBox.Click += (o, e) => { Settings.AutoMoveRun = AutoMoveRunBox.Checked; Settings.Save(); };

            // 战士隔位刺杀：勾选即生效（手动打怪也生效），不需要开内挂总开关
            ThrustingGapBox = CreateBox(132, 210, "隔位刺杀", Settings.AutoThrustingGap);
            ThrustingGapBox.Click += (o, e) =>
            {
                Settings.AutoThrustingGap = ThrustingGapBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoThrustingGap
                    ? "隔位刺杀已开启：战士自动走到与目标隔一格的位置，刀刀用刺杀剑气打（勾选即生效，不必开内挂总开关）"
                    : "隔位刺杀已关闭");
            };

            AutoDodgeBox = CreateBox(132, 186, "自动躲避", Settings.AutoDodge);
            AutoDodgeBox.Click += (o, e) =>
            {
                Settings.AutoDodge = AutoDodgeBox.Checked;
                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoDodge ? "自动躲避已开启：被围攻时自动走开" : "自动躲避已关闭");
            };

            NoLampBox = CreateBox(132, 42, "免蜡", Settings.NoLamp);
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

            WalkThroughBox = CreateBox(132, 66, "穿人/怪/NPC", Settings.WalkThrough);
            WalkThroughBox.Click += (o, e) =>
            {
                if (!GameScene.WalkThroughAllowed)
                {
                    WalkThroughBox.Checked = false;
                    GameScene.Scene.OutputMessage("本服务器未开放穿人功能");
                    return;
                }

                GameScene.RequestPlayerOption(PlayerOptionType.WalkThrough, WalkThroughBox.Checked);
            };

            NoRunUpBox = CreateBox(132, 90, "免助跑", Settings.NoRunUp);
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

            OverWeightBox = CreateBox(132, 114, "超负重", Settings.OverWeight);
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

            MountTaiBox = CreateBox(132, 138, "泰山(不后仰)", Settings.MountTai);
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

            HintLabel = new MirLabel
            {
                Location = new Point(0, 240),
                Size = new Size(264, 14),
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                Parent = this,
                NotControl = true,
                Text = "右列开关需服务器开放，未开放时置灰",
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
            PotHPBox.Checked = Settings.AutoPotHP;
            PotMPBox.Checked = Settings.AutoPotMP;
            AutoMoveRunBox.Checked = Settings.AutoMoveRun;
            AutoSkillBox.Checked = Settings.AutoSkill;
            AutoDodgeBox.Checked = Settings.AutoDodge;
            ThrustingGapBox.Checked = Settings.AutoThrustingGap;
            PoisonSwapBox.Checked = Settings.AutoSwapPoison;

            NoLampBox.Checked = Settings.NoLamp;
            WalkThroughBox.Checked = Settings.WalkThrough;
            NoRunUpBox.Checked = Settings.NoRunUp;
            OverWeightBox.Checked = Settings.OverWeight;
            MountTaiBox.Checked = Settings.MountTai;

            NoLampBox.Enabled = GameScene.NoLampAllowed;
            WalkThroughBox.Enabled = GameScene.WalkThroughAllowed;
            NoRunUpBox.Enabled = GameScene.NoRunUpAllowed;
            OverWeightBox.Enabled = GameScene.OverWeightAllowed;
            MountTaiBox.Enabled = GameScene.MountTaiAllowed;

            Redraw();
        }
    }
}
