using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes.Dialogs;
using Client.MirSounds;
using Client.Resolution;
using C = ClientPackets;
using S = ServerPackets;
namespace Client.MirScenes
{
    public class SelectScene : MirScene
    {
        public MirImageControl Background, Title;
        private NewCharacterDialog _character;

        public MirLabel ServerLabel;
        public MirAnimatedControl CharacterDisplay;
        public MirButton StartGameButton, NewCharacterButton, DeleteCharacterButton, CreditsButton, ExitGame;
        public CharacterButton[] CharacterButtons;
        public MirLabel LastAccessLabel, LastAccessLabelLabel;
        public List<SelectInfo> Characters = new List<SelectInfo>();
        private int _selected;

        private long _loadingSince;                 // 载入画面出现/进入请求发出的时间（服务器一直没回包时自动撤掉）
        private bool _startPending;                 // 已点「开始游戏」，正在载入流程里（等资源/等回包）
        private bool _startPacketSent;              // 进入请求是否已真正发出（资源加载完才发）

        public SelectScene(List<SelectInfo> characters)
        {
            SoundManager.PlayMusic(SoundList.SelectMusic, true);
            Disposing += (o, e) => SoundManager.StopMusic();

            Characters = characters;
            SortList();

            KeyPress += SelectScene_KeyPress;

            Background = new MirImageControl
            {
                Index = 64,
                Library = Libraries.Prguse,
                Parent = this,
            };

            Title = new MirImageControl
            {
                Index = 40,
                Library = Libraries.Title,
                Parent = this,
                Location = new Point(358, 15)
				//Location = new Point(468, 20)
            };

            ServerLabel = new MirLabel
            {
                Location = new Point(322, 44),
				//Location = new Point(432, 60),
                Parent = Background,
                Size = new Size(155, 17),
                Text = "Legend of Mir 2",
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            };

            var xPoint = ((Settings.ScreenWidth - 200) / 5);

            StartGameButton = new MirButton
            {
                Enabled = false,
                HoverIndex = 341,
                Index = 340,
                Library = Libraries.Title,
                Location = new Point(100 + (xPoint * 1) - (xPoint / 2) - 50, Settings.ScreenHeight - 32),
                Parent = Background,
                PressedIndex = 342
            };
            StartGameButton.Click += (o, e) => StartGame();

            NewCharacterButton = new MirButton
            {
                HoverIndex = 344,
                Index = 343,
                Library = Libraries.Title,
                Location = new Point(100 + (xPoint * 2) - (xPoint / 2) - 50, Settings.ScreenHeight - 32),
                Parent = Background,
                PressedIndex = 345,
            };
            NewCharacterButton.Click += (o, e) => OpenNewCharacterDialog();

            DeleteCharacterButton = new MirButton
            {
                HoverIndex = 347,
                Index = 346,
                Library = Libraries.Title,
                Location = new Point(100 + (xPoint * 3) - (xPoint / 2) - 50, Settings.ScreenHeight - 32),
                Parent = Background,
                PressedIndex = 348
            };
            DeleteCharacterButton.Click += (o, e) => DeleteCharacter();


            CreditsButton = new MirButton
            {
                HoverIndex = 350,
                Index = 349,
                Library = Libraries.Title,
                Location = new Point(100 + (xPoint * 4) - (xPoint / 2) - 50, Settings.ScreenHeight - 32),
                Parent = Background,
                PressedIndex = 351
            };
            CreditsButton.Click += (o, e) =>
            {

            };

            ExitGame = new MirButton
            {
                HoverIndex = 353,
                Index = 352,
                Library = Libraries.Title,
                Location = new Point(100 + (xPoint * 5) - (xPoint / 2) - 50, Settings.ScreenHeight - 32),
                Parent = Background,
                PressedIndex = 354
            };
            ExitGame.Click += (o, e) => Program.Form.Close();


            CharacterDisplay = new MirAnimatedControl
            {
                Animated = true,
                AnimationCount = 16,
                AnimationDelay = 250,
                FadeIn = true,
                FadeInDelay = 75,
                FadeInRate = 0.1F,
                Index = 220,
                Library = Libraries.ChrSel,
                Location = new Point(200, 300),
                Parent = Background,
                UseOffSet = true,
                Visible = false
            };
            CharacterDisplay.AfterDraw += (o, e) =>
            {
                // if (_selected >= 0 && _selected < Characters.Count && characters[_selected].Class == MirClass.Wizard)
                Libraries.ChrSel.DrawBlend(CharacterDisplay.Index + 560, CharacterDisplay.DisplayLocationWithoutOffSet, Color.White, true);
            };

            CharacterButtons = new CharacterButton[4];

            CharacterButtons[0] = new CharacterButton
            {
                Location = new Point(447, 122),
                Parent = Background,
                Sound = SoundList.ButtonA,
            };
            CharacterButtons[0].Click += (o, e) =>
            {
                if (characters.Count <= 0) return;

                _selected = 0;
                UpdateInterface();
            };

            CharacterButtons[1] = new CharacterButton
            {
                Location = new Point(447, 226),
                Parent = Background,
                Sound = SoundList.ButtonA,
            };
            CharacterButtons[1].Click += (o, e) =>
            {
                if (characters.Count <= 1) return;
                _selected = 1;
                UpdateInterface();
            };

            CharacterButtons[2] = new CharacterButton
            {
                Location = new Point(447, 330),
                Parent = Background,
                Sound = SoundList.ButtonA,
            };
            CharacterButtons[2].Click += (o, e) =>
            {
                if (characters.Count <= 2) return;

                _selected = 2;
                UpdateInterface();
            };

            CharacterButtons[3] = new CharacterButton
            {
                Location = new Point(447, 434),
                Parent = Background,
                Sound = SoundList.ButtonA,
            };
            CharacterButtons[3].Click += (o, e) =>
            {
                if (characters.Count <= 3) return;

                _selected = 3;
                UpdateInterface();
            };

            LastAccessLabel = new MirLabel
            {
                Location = new Point(140, 509),
                Parent = Background,
                Size = new Size(180, 21),
                DrawFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter,
                Border = true,
            };
            LastAccessLabelLabel = new MirLabel
            {
                Location = new Point(-80, -1),
                Parent = LastAccessLabel,
                Text = "上次联机:",
                Size = new Size(100, 21),
                DrawFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter,
                Border = true,
            };
            UpdateInterface();
        }

        private void SelectScene_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != (char)Keys.Enter) return;
            if (StartGameButton.Enabled)
                StartGame();
            e.Handled = true;
        }


        public void SortList()
        {
            if (Characters != null)
                Characters.Sort((c1, c2) => c2.LastAccess.CompareTo(c1.LastAccess));
        }

        private void OpenNewCharacterDialog()
        {
            if (_character == null || _character.IsDisposed)
            {
                _character = new NewCharacterDialog { Parent = this };

                _character.OnCreateCharacter += (o, e) =>
                {
                    Network.Enqueue(new C.NewCharacter
                    {
                        Name = _character.NameTextBox.Text,
                        Class = _character.Class,
                        Gender = _character.Gender
                    });
                };
            }

            _character.Show();
        }


        public void StartGame()
        {
            if (_startPending) return;   // 已经在进游戏流程里，防止重复触发

            StartGameButton.Enabled = false;
            _startPending = true;
            _startPacketSent = false;

            // 点「开始游戏」的瞬间就切到载入画面：
            // 此时游戏资源（图库）多半还在后台加载，载入背景从这一刻起就盖住全屏，
            // 资源加载进度直接显示在载入画面上；加载完自动发出进入请求，全程不再露出大厅/黑屏。
            _loadingSince = CMain.Time;
            LoadingScreen.Show();

            TrySendStartGame();
        }

        /// <summary>资源就绪后真正发出进入游戏的请求（资源没加载完时由 Process 轮询后再调）。</summary>
        private void TrySendStartGame()
        {
            if (!_startPending || _startPacketSent) return;
            if (!Libraries.Loaded) return;   // 游戏资源还在加载，等 Process 里的轮询

            _startPacketSent = true;
            _loadingSince = CMain.Time;      // 15 秒无回包保护从这一刻起算
            LoadingScreen.SetStatus("正在进入游戏，请稍候...");

            Network.Enqueue(new C.StartGame
            {
                CharacterIndex = Characters[_selected].Index
            });
        }

        public override void Process()
        {
            // 已点「开始游戏」但游戏资源（图库）还在后台加载：
            // 载入画面保持全屏盖着，底部实时显示加载进度；加载完自动发出进入请求。
            if (_startPending && !_startPacketSent)
            {
                if (!Libraries.Loaded)
                {
                    // 资源加载阶段不设超时：后台线程一定会加载完（缺文件也只是跳过），
                    // 加载完自动发出进入请求；进入请求发出后才有 15 秒无回包保护。
                    LoadingScreen.SetStatus(Libraries.Count > 0
                        ? string.Format("正在加载游戏资源 {0}/{1}...", Libraries.Progress, Libraries.Count)
                        : "正在加载游戏资源，请稍候...");
                    return;
                }

                TrySendStartGame();
                return;
            }

            // 进入请求已发出，但服务器一直没回包（掉线/网络异常）：15 秒后自动退出载入画面，让玩家可以再点一次
            if (_startPending && _startPacketSent && LoadingScreen.Visible && CMain.Time - _loadingSince > 15000)
            {
                _startPending = false;
                LoadingScreen.Hide();
                StartGameButton.Enabled = _selected >= 0 && _selected < Characters.Count;
            }
        }
        public override void ProcessPacket(Packet p)
        {
            switch (p.Index)
            {
                case (short)ServerPacketIds.NewCharacter:
                    NewCharacter((S.NewCharacter)p);
                    break;
                case (short)ServerPacketIds.NewCharacterSuccess:
                    NewCharacter((S.NewCharacterSuccess)p);
                    break;
                case (short)ServerPacketIds.DeleteCharacter:
                    DeleteCharacter((S.DeleteCharacter)p);
                    break;
                case (short)ServerPacketIds.DeleteCharacterSuccess:
                    DeleteCharacter((S.DeleteCharacterSuccess)p);
                    break;
                case (short)ServerPacketIds.StartGame:
                    StartGame((S.StartGame)p);
                    break;
                case (short)ServerPacketIds.StartGameBanned:
                    StartGame((S.StartGameBanned)p);
                    break;
                case (short)ServerPacketIds.StartGameDelay:
                    StartGame((S.StartGameDelay)p);
                    break;
                default:
                    base.ProcessPacket(p);
                    break;
            }
        }

        private void NewCharacter(S.NewCharacter p)
        {
            _character.OKButton.Enabled = true;

            switch (p.Result)
            {
                case 0:
                    MirMessageBox.Show("服务器当前禁止创建账户");
                    _character.Dispose();
                    break;
                case 1:
                    MirMessageBox.Show("角色名不可用");
                    _character.NameTextBox.SetFocus();
                    break;
                case 2:
                    MirMessageBox.Show("选择的性别不存在\n 联系管理员");
                    break;
                case 3:
                    MirMessageBox.Show("选择的职业不存在\n 联系管理员");
                    break;
                case 4:
                    MirMessageBox.Show("每个账户只能创建" + Globals.MaxCharacterCount + "角色");
                    _character.Dispose();
                    break;
                case 5:
                    MirMessageBox.Show("角色名被使用");
                    _character.NameTextBox.SetFocus();
                    break;
            }
        }
        private void NewCharacter(S.NewCharacterSuccess p)
        {
            _character.Dispose();
            MirMessageBox.Show("角色创建成功");

            Characters.Insert(0, p.CharInfo);
            _selected = 0;
            UpdateInterface();
        }

        private void DeleteCharacter()
        {
            if (_selected < 0 || _selected >= Characters.Count) return;

            MirMessageBox message = new MirMessageBox(string.Format("确定要删除角色 {0} 删除后不可恢复", Characters[_selected].Name), MirMessageBoxButtons.YesNo);
            int index = Characters[_selected].Index;

            message.YesButton.Click += (o1, e1) =>
            {
                MirInputBox inputBox = new MirInputBox("请输入删除角色名");
                inputBox.OKButton.Click += (o, e) =>
                {
                    string name = Characters[_selected].Name.ToString();

                    if (inputBox.InputTextBox.Text == name)
                    {
                        DeleteCharacterButton.Enabled = false;
                        Network.Enqueue(new C.DeleteCharacter { CharacterIndex = index });
                    }
                    else
                    {
                        MirMessageBox failedMessage = new MirMessageBox(string.Format("输入错误"), MirMessageBoxButtons.OK);
                        failedMessage.Show();
                    }
                    inputBox.Dispose();
                };
                inputBox.Show();
            };
            message.Show();
        }

        private void DeleteCharacter(S.DeleteCharacter p)
        {
            DeleteCharacterButton.Enabled = true;
            switch (p.Result)
            {
                case 0:
                    MirMessageBox.Show("服务器当前禁止删除账户");
                    break;
                case 1:
                    MirMessageBox.Show("账户不存在\n联系管理员");
                    break;
            }
        }
        private void DeleteCharacter(S.DeleteCharacterSuccess p)
        {
            DeleteCharacterButton.Enabled = true;
            MirMessageBox.Show("角色删除成功");

            for (int i = 0; i < Characters.Count; i++)
                if (Characters[i].Index == p.CharacterIndex)
                {
                    Characters.RemoveAt(i);
                    break;
                }

            UpdateInterface();
        }

        private void StartGame(S.StartGameDelay p)
        {
            _startPending = false;
            StartGameButton.Enabled = true;
            LoadingScreen.Hide();

            long time = CMain.Time + p.Milliseconds;

            MirMessageBox message = new MirMessageBox(string.Format("账户登录失败 再次登录时间 {0} 秒", Math.Ceiling(p.Milliseconds / 1000M)));

            message.BeforeDraw += (o, e) => message.Label.Text = string.Format("账户登录失败 再次登录时间 {0} 秒", Math.Ceiling((time - CMain.Time) / 1000M));


            message.AfterDraw += (o, e) =>
            {
                if (CMain.Time <= time) return;
                message.Dispose();
                StartGame();
            };

            message.Show();
        }
        public void StartGame(S.StartGameBanned p)
        {
            _startPending = false;
            StartGameButton.Enabled = true;
            LoadingScreen.Hide();

            TimeSpan d = p.ExpiryDate - CMain.Now;
            MirMessageBox.Show(string.Format("此账户被禁用\n\n原因{0}\n解禁日期{1}\n倒计时{2:#,##0} 小时, {3} 分钟, {4} 秒", p.Reason,
                                             p.ExpiryDate, Math.Floor(d.TotalHours), d.Minutes, d.Seconds));
        }
        public void StartGame(S.StartGame p)
        {
            StartGameButton.Enabled = true;

            switch (p.Result)
            {
                case 0:
                    _startPending = false;
                    LoadingScreen.Hide();
                    MirMessageBox.Show("服务器维护禁止登录");
                    break;
                case 1:
                    _startPending = false;
                    LoadingScreen.Hide();
                    MirMessageBox.Show("尚未登录");
                    break;
                case 2:
                    _startPending = false;
                    LoadingScreen.Hide();
                    MirMessageBox.Show("没有激活角色");
                    break;
                case 3:
                    _startPending = false;
                    LoadingScreen.Hide();
                    MirMessageBox.Show("无效地图或没有新手出生点");
                    break;
                case 4:
                    _startPending = false;   // 交给 GameScene.Process 接管载入画面（Armed 仍为 true）

                    // 服务端下发的 Resolution 是「允许的最大分辨率」，客户端选择超过上限时回落到上限。
                    if (Settings.Resolution == 0 || p.Resolution < Settings.Resolution)
                        Settings.Resolution = p.Resolution;

                    Settings.Resolution = ResolutionHelper.Normalize(Settings.Resolution);

                    Size gameSize = ResolutionHelper.GetSize(Settings.Resolution);
                    CMain.SetResolution(gameSize.Width, gameSize.Height);

                    ActiveScene = new GameScene();

                    // 重新挂到刚建好的 GameScene 上：从点「进入游戏」到地图+角色画出来之间一直盖着，
                    // 由 GameScene.Process 在世界就绪后撤掉（Armed 状态跨场景保留）。
                    LoadingScreen.Show();

                    Dispose();
                    break;
            }
        }
        private void UpdateInterface()
        {
            for (int i = 0; i < CharacterButtons.Length; i++)
            {
                CharacterButtons[i].Selected = i == _selected;
                CharacterButtons[i].Update(i >= Characters.Count ? null : Characters[i]);
            }

            if (_selected >= 0 && _selected < Characters.Count)
            {
                CharacterDisplay.Visible = true;
                //CharacterDisplay.Index = ((byte)Characters[_selected].Class + 1) * 20 + (byte)Characters[_selected].Gender * 280; 

                switch ((MirClass)Characters[_selected].Class)
                {
                    case MirClass.战士:
                        CharacterDisplay.Index = (byte)Characters[_selected].Gender == 0 ? 20 : 300; //220 : 500;
                        break;
                    case MirClass.法师:
                        CharacterDisplay.Index = (byte)Characters[_selected].Gender == 0 ? 40 : 320; //240 : 520;
                        break;
                    case MirClass.道士:
                        CharacterDisplay.Index = (byte)Characters[_selected].Gender == 0 ? 60 : 340; //260 : 540;
                        break;
                    case MirClass.刺客:
                        CharacterDisplay.Index = (byte)Characters[_selected].Gender == 0 ? 80 : 360; //280 : 560;
                        break;
                    case MirClass.弓箭:
                        CharacterDisplay.Index = (byte)Characters[_selected].Gender == 0 ? 100 : 140; //160 : 180;
                        break;
                }

                LastAccessLabel.Text = Characters[_selected].LastAccess == DateTime.MinValue ? "未曾登录" : Characters[_selected].LastAccess.ToString();
                LastAccessLabel.Visible = true;
                LastAccessLabelLabel.Visible = true;
                StartGameButton.Enabled = true;
            }
            else
            {
                CharacterDisplay.Visible = false;
                LastAccessLabel.Visible = false;
                LastAccessLabelLabel.Visible = false;
                StartGameButton.Enabled = false;
            }
        }


        #region Disposable
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Background = null;
                _character = null;


                ServerLabel = null;
                CharacterDisplay = null;
                StartGameButton = null;
                NewCharacterButton = null;
                DeleteCharacterButton = null;
                CreditsButton = null;
                ExitGame = null;
                CharacterButtons = null;
                LastAccessLabel = null; LastAccessLabelLabel = null;
                Characters = null;
                _selected = 0;
            }

            base.Dispose(disposing);
        }
        #endregion        
        public sealed class CharacterButton : MirImageControl
        {
            public MirLabel NameLabel, LevelLabel, ClassLabel;
            public bool Selected;

            public CharacterButton()
            {
                Index = 44; //45 locked
                Library = Libraries.Prguse;
                Sound = SoundList.ButtonA;

                NameLabel = new MirLabel
                {
                    Location = new Point(107, 9),
                    Parent = this,
                    NotControl = true,
                    Size = new Size(170, 18)
                };

                LevelLabel = new MirLabel
                {
                    Location = new Point(107, 28),
                    Parent = this,
                    NotControl = true,
                    Size = new Size(30, 18)
                };

                ClassLabel = new MirLabel
                {
                    Location = new Point(178, 28),
                    Parent = this,
                    NotControl = true,
                    Size = new Size(100, 18)
                };
            }

            public void Update(SelectInfo info)
            {
                if (info == null)
                {
                    Index = 44;
                    Library = Libraries.Prguse;
                    NameLabel.Text = string.Empty;
                    LevelLabel.Text = string.Empty;
                    ClassLabel.Text = string.Empty;

                    NameLabel.Visible = false;
                    LevelLabel.Visible = false;
                    ClassLabel.Visible = false;

                    return;
                }

                Library = Libraries.Title;

                Index = 660 + (byte)info.Class;

                if (Selected) Index += 5;


                NameLabel.Text = info.Name;
                LevelLabel.Text = info.Level.ToString();
                ClassLabel.Text = info.Class.ToString();

                NameLabel.Visible = true;
                LevelLabel.Visible = true;
                ClassLabel.Visible = true;
            }
        }
    }
}
