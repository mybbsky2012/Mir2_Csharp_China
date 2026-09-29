using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using Client.MirSounds;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 内挂（自动挂机）设置面板。
    /// 面板本身只修改本地 Settings，实际动作由 MapControl.ProcessAutoPlay 执行；
    /// 「免蜡 / 穿人 / 免助跑」属于服务器开关，点选后会向服务器申请，最终状态以服务器回执为准。
    /// </summary>
    public sealed class AutoPlayDialog : MirImageControl
    {
        public MirLabel TitleLabel;
        public MirButton CloseButton;

        public MirCheckBox AllBox, AttackBox, MoveBox, PickUpBox, PotHPBox, PotMPBox, NoLampBox, WalkThroughBox, NoRunUpBox;
        public MirLabel HintLabel;

        public AutoPlayDialog()
        {
            Index = 466;
            Library = Libraries.Title;
            Size = new Size(224, 180);
            Movable = true;
            Sort = true;
            Location = new Point((Settings.ScreenWidth - Size.Width) / 2, (Settings.ScreenHeight - Size.Height) / 2);

            TitleLabel = new MirLabel
            {
                Location = new Point(0, 6),
                Size = new Size(224, 20),
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
                Location = new Point(198, 3),
                Library = Libraries.Prguse2,
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            // 左列：本地开关；右列：服务器开关（背景图 224x180 固定，故采用两列布局）
            AllBox = CreateBox(20, 30, "内挂总开关", Settings.AutoPlay);
            AllBox.Click += (o, e) =>
            {
                Settings.AutoPlay = AllBox.Checked;

                if (!Settings.AutoPlay && MapObject.User != null)
                    MapObject.TargetObjectID = 0;

                Settings.Save();
                GameScene.Scene.OutputMessage(Settings.AutoPlay ? "内挂已开启" : "内挂已关闭");
            };

            AttackBox = CreateBox(20, 48, "自动攻击", Settings.AutoAttack);
            AttackBox.Click += (o, e) => { Settings.AutoAttack = AttackBox.Checked; Settings.Save(); };

            MoveBox = CreateBox(20, 66, "自动走位", Settings.AutoMove);
            MoveBox.Click += (o, e) => { Settings.AutoMove = MoveBox.Checked; Settings.Save(); };

            PickUpBox = CreateBox(20, 84, "自动捡物", Settings.AutoPickup);
            PickUpBox.Click += (o, e) => { Settings.AutoPickup = PickUpBox.Checked; Settings.Save(); };

            PotHPBox = CreateBox(20, 102, "自动喝血药", Settings.AutoPotHP);
            PotHPBox.Click += (o, e) => { Settings.AutoPotHP = PotHPBox.Checked; Settings.Save(); };

            PotMPBox = CreateBox(20, 120, "自动喝蓝药", Settings.AutoPotMP);
            PotMPBox.Click += (o, e) => { Settings.AutoPotMP = PotMPBox.Checked; Settings.Save(); };

            NoLampBox = CreateBox(104, 30, "免蜡", Settings.NoLamp);
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

            WalkThroughBox = CreateBox(104, 48, "穿人+穿怪", Settings.WalkThrough);
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

            NoRunUpBox = CreateBox(104, 66, "免助跑", Settings.NoRunUp);
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

            HintLabel = new MirLabel
            {
                Location = new Point(0, 172),
                Size = new Size(224, 8),
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

            NoLampBox.Checked = Settings.NoLamp;
            WalkThroughBox.Checked = Settings.WalkThrough;
            NoRunUpBox.Checked = Settings.NoRunUp;

            NoLampBox.Enabled = GameScene.NoLampAllowed;
            WalkThroughBox.Enabled = GameScene.WalkThroughAllowed;
            NoRunUpBox.Enabled = GameScene.NoRunUpAllowed;

            Redraw();
        }
    }
}
