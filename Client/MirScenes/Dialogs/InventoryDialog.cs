using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{
    public sealed class InventoryDialog : MirImageControl
    {
        public MirImageControl WeightBar;
        public MirImageControl[] LockBar = new MirImageControl[10];
        public MirItemCell[] Grid;
        public MirItemCell[] QuestGrid;

        public MirButton CloseButton, ItemButton, ItemButton2, QuestButton, AddButton;
        public MirLabel GoldLabel, WeightLabel;
        public MirLabel RecycleLabel;
        public MirButton SortButton;

        public InventoryDialog()
        {
            Index = 196;
            Library = Libraries.Title;
            Movable = true;
            Sort = true;
            Visible = false;

            WeightBar = new MirImageControl
            {
                Index = 24,
                Library = Libraries.Prguse,
                Location = new Point(182, 217),
                Parent = this,
                DrawImage = false,
                NotControl = true,
            };

            ItemButton = new MirButton
            {
                Index = 197,
                Library = Libraries.Title,
                Location = new Point(6, 7),
                Parent = this,
                Size = new Size(72, 23),
                Sound = SoundList.ButtonA,
            };
            ItemButton.Click += Button_Click;

            ItemButton2 = new MirButton
            {
                Index = 738,
                Library = Libraries.Title,
                Location = new Point(76, 7),
                Parent = this,
                Size = new Size(72, 23),
                Sound = SoundList.ButtonA,
            };
            ItemButton2.Click += Button_Click;

            QuestButton = new MirButton
            {
                Index = 739,
                Library = Libraries.Title,
                Location = new Point(146, 7),
                Parent = this,
                Size = new Size(72, 23),
                Sound = SoundList.ButtonA,
            };
            QuestButton.Click += Button_Click;

            AddButton = new MirButton
            {
                Index = 483,
                HoverIndex = 484,
                PressedIndex = 485,
                Library = Libraries.Title,
                Location = new Point(235, 5),
                Parent = this,
                Size = new Size(72, 23),
                Sound = SoundList.ButtonA,
                Visible = false,
            };
            AddButton.Click += (o1, e) =>
            {
                int openLevel = (GameScene.User.Inventory.Length - 46) / 4;
                int openGold = (1000000 + openLevel * 1000000);
                MirMessageBox messageBox = new MirMessageBox(string.Format(GameLanguage.ExtraSlots4, openGold), MirMessageBoxButtons.OKCancel);

                messageBox.OKButton.Click += (o, a) =>
                {
                    Network.Enqueue(new C.Chat { Message = "@ADDINVENTORY" });
                };
                messageBox.Show();
            };

            CloseButton = new MirButton
            {
                HoverIndex = 361,
                Index = 360,
                Location = new Point(289, 3),
                Library = Libraries.Prguse2,
                Parent = this,
                PressedIndex = 362,
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            // 一键回收入口：点击向服务器发 @一键回收，弹出按套装分类的回收菜单。
            // 86 格背包后扩展槽按钮（AddButton）永远隐藏，这里复用它留出的凹槽底座；
            // 若某角色还是 46 格旧背包（AddButton 会显示），RefreshInventory2 里会把本按钮隐藏，避免重叠。
            RecycleLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(231, 7),
                Size = new Size(54, 20),
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                ForeColour = Color.FromArgb(255, 216, 150),
                OutLine = true,
                OutLineColour = Color.Black,
                Text = "一键回收",
                Sound = SoundList.ButtonA,
            };
            RecycleLabel.Click += (o, e) =>
            {
                Network.Enqueue(new C.Chat { Message = "@一键回收" });
            };

            GoldLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(40, 212),
                Size = new Size(111, 14),
                Sound = SoundList.Gold,
            };
            GoldLabel.Click += (o, e) =>
            {
                if (GameScene.SelectedCell == null)
                    GameScene.PickedUpGold = !GameScene.PickedUpGold && GameScene.Gold > 0;
            };


            Grid = new MirItemCell[8 * 10];

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    int idx = 8 * y + x;
                    Grid[idx] = new MirItemCell
                    {
                        ItemSlot = 6 + idx,
                        GridType = MirGridType.Inventory,
                        Library = Libraries.Items,
                        Parent = this,
                        Location = new Point(x * 36 + 9 + x, y % 5 * 32 + 37 + y % 5),
                    };

                    if (idx >= 40)
                        Grid[idx].Visible = false;
                }
            }

            QuestGrid = new MirItemCell[8 * 5];

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    QuestGrid[8 * y + x] = new MirItemCell
                    {
                        ItemSlot = 8 * y + x,
                        GridType = MirGridType.QuestInventory,
                        Library = Libraries.Items,
                        Parent = this,
                        Location = new Point(x * 36 + 9 + x, y * 32 + 37 + y),
                        Visible = false
                    };
                }
            }

            WeightLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(268, 212),
                Size = new Size(26, 14)
            };

            // 整理背包按钮（Prguse 969 = 橙色循环箭头图标）：请求服务端整理背包（腰带格不动）
            SortButton = new MirButton
            {
                Index = 969,
                Library = Libraries.Prguse,
                Location = new Point(297, 207),
                Parent = this,
                Size = new Size(18, 16),
                Sound = SoundList.ButtonA,
                Hint = "整理背包",
            };
            SortButton.Click += (o, e) => Network.Enqueue(new C.SortBag());
            WeightBar.BeforeDraw += WeightBar_BeforeDraw;

            for (int i = 0; i < LockBar.Length; i++)
            {
                LockBar[i] = new MirImageControl
                {
                    Index = 307,
                    Library = Libraries.Prguse2,
                    Location = new Point(9 + i % 2 * 148, 37 + i / 2 * 33),
                    Parent = this,
                    DrawImage = true,
                    NotControl = true,
                    Visible = false,
                };
            }

        }

        void Button_Click(object sender, EventArgs e)
        {
            if (GameScene.User.Inventory.Length == 46 && sender == ItemButton2)
            {
                MirMessageBox messageBox = new MirMessageBox(GameLanguage.ExtraSlots8, MirMessageBoxButtons.OKCancel);

                messageBox.OKButton.Click += (o, a) =>
                {
                    Network.Enqueue(new C.Chat { Message = "@ADDINVENTORY" });
                };
                messageBox.Show();
            }
            else
            {
                if (sender == ItemButton)
                {
                    RefreshInventory();
                }
                else if (sender == ItemButton2)
                {
                    RefreshInventory2();
                }
                else if (sender == QuestButton)
                {
                    Reset();

                    ItemButton.Index = 737;
                    ItemButton2.Index = 738;
                    QuestButton.Index = 198;

                    if (GameScene.User.Inventory.Length == 46)
                    {
                        ItemButton2.Index = 169;
                    }

                    foreach (var grid in QuestGrid)
                    {
                        grid.Visible = true;
                    }
                }
            }
        }

        void Reset()
        {
            foreach (MirItemCell grid in QuestGrid)
            {
                grid.Visible = false;
            }

            foreach (MirItemCell grid in Grid)
            {
                grid.Visible = false;
            }

            for (int i = 0; i < LockBar.Length; i++)
            {
                LockBar[i].Visible = false;
            }

            AddButton.Visible = false;
        }



        public void RefreshInventory()
        {
            Reset();

            ItemButton.Index = 197;
            ItemButton2.Index = 738;
            QuestButton.Index = 739;

            if (GameScene.User.Inventory.Length == 46)
            {
                ItemButton2.Index = 169;
            }

            foreach (var grid in Grid)
            {
                if (grid.ItemSlot < 46)
                    grid.Visible = true;
                else
                    grid.Visible = false;
            }
        }

        public void RefreshInventory2()
        {
            Reset();

            ItemButton.Index = 737;
            ItemButton2.Index = 168;
            QuestButton.Index = 739;

            foreach (var grid in Grid)
            {
                if (grid.ItemSlot < 46 || grid.ItemSlot >= GameScene.User.Inventory.Length)
                    grid.Visible = false;
                else
                    grid.Visible = true;
            }

            int openLevel = (GameScene.User.Inventory.Length - 46) / 4;
            for (int i = 0; i < LockBar.Length; i++)
            {
                LockBar[i].Visible = (i < openLevel) ? false : true;
            }

            AddButton.Visible = openLevel >= 10 ? false : true;

            // 扩展槽按钮要显示时（46 格旧背包），让一键回收按钮让位，避免两者叠在同一凹槽里
            RecycleLabel.Visible = !AddButton.Visible;
        }

        public void Process()
        {
            WeightLabel.Text = GameScene.User.Inventory.Count(t => t == null).ToString();
            //WeightLabel.Text = (MapObject.User.MaxBagWeight - MapObject.User.CurrentBagWeight).ToString();
            GoldLabel.Text = GameScene.Gold.ToString("###,###,##0");
        }


        private void WeightBar_BeforeDraw(object sender, EventArgs e)
        {
            if (WeightBar.Library == null) return;

            // 背包负重为 0 时（等级属性未加载/异常配置）直接相除会得到 ∞/NaN，
            // (int)NaN 会变成 int.MinValue，画出异常色块。此处直接不绘制。
            int maxBagWeight = MapObject.User.Stats[Stat.背包负重];
            if (maxBagWeight <= 0) return;

            double percent = MapObject.User.CurrentBagWeight / (double)maxBagWeight;
            if (percent > 1) percent = 1;
            if (percent <= 0) return;

            Rectangle section = new Rectangle
            {
                Size = new Size((int)((WeightBar.Size.Width - 3) * percent), WeightBar.Size.Height)
            };

            WeightBar.Library.Draw(WeightBar.Index, section, WeightBar.DisplayLocation, Color.White, false);
        }


        public MirItemCell GetCell(ulong id)
        {
            for (int i = 0; i < Grid.Length; i++)
            {
                if (Grid[i].Item == null || Grid[i].Item.UniqueID != id) continue;
                return Grid[i];
            }
            return null;
        }

        public MirItemCell GetQuestCell(ulong id)
        {
            return QuestGrid.FirstOrDefault(t => t.Item != null && t.Item.UniqueID == id);
        }

        public void DisplayItemGridEffect(ulong id, int type = 0)
        {
            MirItemCell cell = GetCell(id);

            if (cell.Item == null) return;

            MirAnimatedControl animEffect = null;

            switch (type)
            {
                case 0:
                    animEffect = new MirAnimatedControl
                    {
                        Animated = true,
                        AnimationCount = 9,
                        AnimationDelay = 150,
                        Index = 410,
                        Library = Libraries.Prguse,
                        Location = cell.Location,
                        Parent = this,
                        Loop = false,
                        NotControl = true,
                        UseOffSet = true,
                        Blending = true,
                        BlendingRate = 1F
                    };
                    animEffect.AfterAnimation += (o, e) => animEffect.Dispose();
                    SoundManager.PlaySound(20000 + (ushort)Spell.MagicShield * 10);
                    break;
            }
        }
    }
    public sealed class BeltDialog : MirImageControl
    {
        public MirLabel[] Key = new MirLabel[6];
        public MirButton CloseButton, RotateButton;
        public MirItemCell[] Grid;

        // ★可选皮肤（Data 里有 UI_32bit.Lib 时）：六个格子用皮肤索引 17 的六格面板（240×50）。
        //   格子和 Prguse 1932 一样从 12 起、步距 35（最后一格 187..219），
        //   面板右侧烘死了 ↻ / × 两个小图标 → 按钮只当热区，行为和 Prguse 版一致。
        private const int SkinPanelIndex = 17;
        private const int SkinPanelWidth = 240;
        private const int SkinPanelHeight = 50;
        private const int SkinCellX = 12;
        private const int SkinCellY = 12;
        private const int SkinCellPitch = 35;
        public bool UseUISkin { get; private set; }
        private bool _skinVertical;

        public BeltDialog()
        {
            Index = 1932;
            Library = Libraries.Prguse;
            Movable = true;
            Sort = true;
            Visible = true;
            Location = new Point(GameScene.Scene.MainDialog.Location.X + 230, Settings.ScreenHeight - 150);

            BeforeDraw += BeltPanel_BeforeDraw;

            for (int i = 0; i < Key.Length; i++)
            {
                Key[i] = new MirLabel
                {
                    Parent = this,
                    Size = new Size(26, 14),
                    Location = new Point(8 + i * 35, 2),
                    Text = (i + 1).ToString()
                };
            }

            RotateButton = new MirButton
            {
                HoverIndex = 1927,
                Index = 1926,
                Location = new Point(222, 3),
                Library = Libraries.Prguse,
                Parent = this,
                PressedIndex = 1928,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.Rotate
            };
            RotateButton.Click += (o, e) => Flip();

            CloseButton = new MirButton
            {
                HoverIndex = 1924,
                Index = 1923,
                Location = new Point(222, 19),
                Library = Libraries.Prguse,
                Parent = this,
                PressedIndex = 1925,
                Sound = SoundList.ButtonA,
                Hint = string.Format(GameLanguage.Close, CMain.InputKeys.GetKey(KeybindOptions.Belt))
            };
            CloseButton.Click += (o, e) => Hide();

            Grid = new MirItemCell[6];

            for (int x = 0; x < 6; x++)
            {
                Grid[x] = new MirItemCell
                {
                    ItemSlot = x,
                    Size = new Size(32, 32),
                    GridType = MirGridType.Inventory,
                    Library = Libraries.Items,
                    Parent = this,
                    Location = new Point(x * 35 + 12, 3),
                };
            }

            // ★皮肤模式：换成索引 17 的六格面板（240×50，格子 12/12 起、步距 35）
            if (Libraries.HasUISkin)
            {
                UseUISkin = true;
                Library = Libraries.UISkin;
                Index = SkinPanelIndex;      // 会自动把 Size 更新成 240×50

                for (int x = 0; x < 6; x++)
                    Key[x].Location = new Point(SkinCellX + x * SkinCellPitch + 2, SkinCellY);

                ApplySkinLayout(false);
            }
        }

        /// <summary>
        /// 皮肤模式的横/竖两套摆位：
        ///   横排 = 皮肤索引 17（240×50），两个按钮只当热区盖在烘好的 ↻/× 上；
        ///   竖排 = 皮肤库里没有独立素材，按用户要求直接用 Prguse 的竖排（1944），按钮也是 Prguse 那套。
        /// </summary>
        private void ApplySkinLayout(bool vertical)
        {
            _skinVertical = vertical;

            if (vertical)
            {
                Library = Libraries.Prguse;
                Index = 1944;                       // Prguse 竖排 40×240
                DrawImage = true;
                AutoSize = true;
                Size = new Size(40, 240);
                Location = new Point(0, 200);       // 和 Prguse 版 Flip 一致

                for (int x = 0; x < 6; x++)
                {
                    Grid[x].Location = new Point(3, x * 35 + 12);
                    Key[x].Location = new Point(-1, 11 + x * 35);
                }

                CloseButton.Location = new Point(3, 222);
                RotateButton.Location = new Point(19, 222);
                MakePrguseButton(CloseButton, 1935, 1936, 1937);
                MakePrguseButton(RotateButton, 1938, 1939, 1940);
            }
            else
            {
                Library = Libraries.UISkin;
                Index = SkinPanelIndex;             // 240×50
                DrawImage = true;
                AutoSize = true;
                Size = new Size(SkinPanelWidth, SkinPanelHeight);

                for (int x = 0; x < 6; x++)
                {
                    Grid[x].Location = new Point(SkinCellX + x * SkinCellPitch, SkinCellY);
                    Key[x].Location = new Point(SkinCellX + x * SkinCellPitch + 2, SkinCellY);
                }

                CloseButton.Location = new Point(218, 33);
                RotateButton.Location = new Point(218, 12);
                MakeHotzone(CloseButton);
                MakeHotzone(RotateButton);

                // 从竖排切回来：恢复 Prguse 版的原始停靠位
                Location = new Point(GameScene.Scene.MainDialog.Location.X + 230, Settings.ScreenHeight - 150);
            }

            ClampToScreen();
        }

        /// <summary>按钮改成「透明热区」：不画图，只负责点中烘在皮肤画面里的那个小图标。</summary>
        private static void MakeHotzone(MirButton button)
        {
            button.Library = Libraries.UISkin;
            button.Index = -1;
            button.HoverIndex = -1;
            button.PressedIndex = -1;
            button.DrawImage = false;
            button.AutoSize = false;
            button.Size = new Size(18, 16);
            button.Visible = true;
        }

        /// <summary>恢复成 Prguse 的可见按钮（竖排用）。</summary>
        private static void MakePrguseButton(MirButton button, int normal, int hover, int pressed)
        {
            button.Library = Libraries.Prguse;
            button.Index = normal;
            button.HoverIndex = hover;
            button.PressedIndex = pressed;
            button.DrawImage = true;
            button.AutoSize = true;
            button.Visible = true;
        }

        /// <summary>竖排是 50×240，切换后如果会顶出屏幕就拉回来（不然下面的按钮点不到）。</summary>
        private void ClampToScreen()
        {
            int x = Location.X;
            int y = Location.Y;

            if (x + Size.Width > Settings.ScreenWidth) x = Settings.ScreenWidth - Size.Width;
            if (y + Size.Height > Settings.ScreenHeight) y = Settings.ScreenHeight - Size.Height;
            if (x < 0) x = 0;
            if (y < 0) y = 0;

            Location = new Point(x, y);
        }

        private void BeltPanel_BeforeDraw(object sender, EventArgs e)
        {
            if (!UseUISkin)
            {
                if (Libraries.Prguse != null)
                    Libraries.Prguse.Draw(Index + 1, DisplayLocation, Color.White, false, 0.5F);
                return;
            }

            // 竖排借用了 Prguse 的素材，原版的半透明底图（1945）也要一起补上
            if (_skinVertical && Libraries.Prguse != null)
                Libraries.Prguse.Draw(1945, DisplayLocation, Color.White, false, 0.5F);
        }

        public void Flip()
        {
            if (UseUISkin)
            {
                ApplySkinLayout(!_skinVertical);
                return;
            }

            //0,70 LOCATION
            if (Index == 1932)
            {
                Index = 1944;
                Location = new Point(0, 200);

                for (int x = 0; x < 6; x++)
                    Grid[x].Location = new Point(3, x * 35 + 12);

                CloseButton.Index = 1935;
                CloseButton.HoverIndex = 1936;
                CloseButton.Location = new Point(3, 222);
                CloseButton.PressedIndex = 1937;

                RotateButton.Index = 1938;
                RotateButton.HoverIndex = 1939;
                RotateButton.Location = new Point(19, 222);
                RotateButton.PressedIndex = 1940;

            }
            else
            {
                Index = 1932;
                Location = new Point(GameScene.Scene.MainDialog.Location.X + 230, Settings.ScreenHeight - 150);

                for (int x = 0; x < 6; x++)
                    Grid[x].Location = new Point(x * 35 + 12, 3);

                CloseButton.Index = 1923;
                CloseButton.HoverIndex = 1924;
                CloseButton.Location = new Point(222, 19);
                CloseButton.PressedIndex = 1925;

                RotateButton.Index = 1926;
                RotateButton.HoverIndex = 1927;
                RotateButton.Location = new Point(222, 3);
                RotateButton.PressedIndex = 1928;
            }

            for (int i = 0; i < Key.Length; i++)
            {
                Key[i].Location = (Index != 1932) ? new Point(-1, 11 + i * 35) : new Point(8 + i * 35, 2);
            }
        }


        public MirItemCell GetCell(ulong id)
        {
            for (int i = 0; i < Grid.Length; i++)
            {
                if (Grid[i].Item == null || Grid[i].Item.UniqueID != id) continue;
                return Grid[i];
            }
            return null;
        }
    }
}
