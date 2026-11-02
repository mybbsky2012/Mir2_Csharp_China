using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using SlimDX;
using Font = System.Drawing.Font;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{
    public sealed class BigMapDialog : MirImageControl
    {
        MirLabel CoordinateLabel, TitleLabel;
        public MirButton CloseButton, ScrollUpButton, ScrollDownButton, ScrollBar, WorldButton, MyLocationButton, TeleportToButton, SearchButton;
        public MirTextBox SearchTextBox;

        private float GapPerRow;
        const int MaximumRows = 18;
        public int TargetMapIndex;
        private DateTime NextSearchTime;

        public BigMapViewPort ViewPort;
        public WorldMapImage WorldMap;

        private int scrollOffset;
        private int ScrollOffset
        {
            get { return scrollOffset; }
            set
            {
                scrollOffset = value;
                ScrollBar.Location = new Point(ScrollUpButton.Location.X, (int)(ScrollUpButton.Location.Y + 13 + scrollOffset * GapPerRow));
            }
        }

        private Point mouseLocation = Point.Empty;
        public Point MouseLocation
        {
            get { return mouseLocation; }
            set
            {
                if (mouseLocation == value) return;

                mouseLocation = value;
                MakeCoordinateLabel();
            }
        }

        private BigMapNPCRow selectedNPC;
        public BigMapNPCRow SelectedNPC
        {
            get { return selectedNPC; }
            set
            {
                if (value == selectedNPC) return;

                if (selectedNPC != null)
                    selectedNPC.NameLabel.ForeColour = Color.White;

                selectedNPC = value;
                if (value != null)
                {
                    selectedNPC.NameLabel.ForeColour = Color.Brown;
                    ViewPort.SelectedNPCIcon.Index = selectedNPC.Info.Icon;
                    ViewPort.SelectedNPCIcon.Visible = true;
                    var map = GameScene.Scene.MapControl;
                    TeleportToButton.Enabled = TargetMapIndex == map.Index && selectedNPC.Info.CanTeleportTo;
                }
                else
                {
                    ViewPort.SelectedNPCIcon.Visible = false;
                    TeleportToButton.Enabled = false;
                }
            }
        }

        private BigMapRecord currentRecord;
        public BigMapRecord CurrentRecord
        {
            get { return currentRecord; }
            set
            {
                if (currentRecord == value) return;
                SetButtonsVisibility(false);
                currentRecord = value;
                TitleLabel.Text = currentRecord != null ? currentRecord.MapInfo.Title : string.Empty;
                ViewPort.UserRadarDot.Visible = currentRecord != null && currentRecord.Index == GameScene.Scene.MapControl.Index;
                SetButtonsVisibility(true);
            }
        }

        public BigMapDialog()
        {
            Index = 820;
            Library = Libraries.Title;
            Movable = true;
            Sort = true;
            Location = Center;
            NotControl = false;

            ScrollUpButton = new MirButton
            {
                Index = 197,
                HoverIndex = 198,
                PressedIndex = 199,
                Location = new Point(Size.Width - 21, 48),
                Library = Libraries.Prguse2,
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            ScrollUpButton.Click += (o, e) => ScrollUp();

            ScrollDownButton = new MirButton
            {
                Index = 207,
                HoverIndex = 208,
                PressedIndex = 209,
                Location = new Point(Size.Width - 21, 417),
                Library = Libraries.Prguse2,
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            ScrollDownButton.Click += (o, e) => ScrollDown();

            ScrollBar = new MirButton
            {
                Index = 205,
                HoverIndex = 206,
                PressedIndex = 206,
                Location = new Point(Size.Width - 21, ScrollUpButton.Location.Y + 13),
                Library = Libraries.Prguse2,
                Parent = this,
                Movable = true,
                Sound = SoundList.ButtonA,
            };
            ScrollBar.OnMoving += (o, e) =>
            {
                var y = ScrollBar.Location.Y;
                if (y < 61)
                    y = 61;
                if (y > 398)
                    y = 398;

                var row = (y - ScrollUpButton.Location.Y + 13) / GapPerRow;
                ScrollOffset = (int)row;
                SetNPCButtonVisibility(true);
            };

            WorldButton = new MirButton
            {
                Index = 827,
                HoverIndex = 828,
                PressedIndex = 829,
                Location = new Point(250, Size.Height - 33),
                Library = Libraries.Title,
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            WorldButton.Click += (o, e) => OpenWorldMap();

            MyLocationButton = new MirButton
            {
                Index = 824,
                HoverIndex = 825,
                PressedIndex = 826,
                Location = new Point(400, Size.Height - 33),
                Library = Libraries.Title,
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            MyLocationButton.Click += (o, e) => TargetMyLocation();


            TeleportToButton = new MirButton
            {
                Index = 821,
                HoverIndex = 822,
                PressedIndex = 823,
                DisabledIndex = 823,
                Enabled = false,
                Location = new Point(Size.Width - 122, 432),
                Library = Libraries.Title,
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            TeleportToButton.Click += (o, e) => TeleportToNPC();

            SearchButton = new MirButton
            {
                Index = 1340,
                HoverIndex = 1341,
                PressedIndex = 1342,
                Enabled = true,
                Location = new Point(23, Size.Height - 36),
                Library = Libraries.Prguse2,
                Parent = this,
                Sound = SoundList.ButtonA,
                Hint = "搜索NPC"
            };
            SearchButton.Click += (o, e) => Search();

            SearchTextBox = new MirTextBox
            {
                Location = new Point(59, Size.Height - 27),
                Parent = this,
                Font = new Font(Settings.FontName, 8F),
                Size = new Size(130, 10),
                MaxLength = Globals.MaxChatLength
            };
            SearchTextBox.TextBox.KeyPress += SearchTextBox_KeyPress;

            ViewPort = new BigMapViewPort()
            {
                Parent = this
            };

            CoordinateLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.White,
                Location = new Point(519, 435),
                Parent = this,
            };

            TitleLabel = new MirLabel
            {
                ForeColour = Color.White,
                Parent = this,
                AutoSize = false,
                Size = new Size(699, 20),
                Location = new Point(19, 6),
                Font = new Font(Settings.FontName, 9F, FontStyle.Bold),
                DrawFormat = TextFormatFlags.HorizontalCenter
            };

            WorldMap = new WorldMapImage()
            {
                Parent = this,
                Location = new Point(10, 0)
            };

            CloseButton = new MirButton
            {
                HoverIndex = 361,
                Index = 360,
                Location = new Point(Size.Width - 25, 3),
                Library = Libraries.Prguse2,
                Parent = this,
                PressedIndex = 362,
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            SearchTextBox.Enabled = false;
        }

        private void MakeCoordinateLabel()
        {
            CoordinateLabel.Text = $"[ {MouseLocation.X}, {MouseLocation.Y} ]";
        }

        public void ShowCoordinateLabel()
        {
            CoordinateLabel.Visible = true;
        }

        public void HideCoordinateLabel()
        {
            CoordinateLabel.Visible = false;
        }

        public void WorldMapSetup(WorldMapSetup setup)
        {
            WorldButton.Visible = setup.Enabled;
            WorldMap.MakeButtons(setup.Icons);
        }

        public void Toggle()
        {
            if (Visible)
                Hide();
            else
                Show();
        }

        public override void Show()
        {
            var map = GameScene.Scene.MapControl;
            if (map.BigMap <= 0) return;

            SearchTextBox.Enabled = false;

            base.Show();
            TargetMyLocation();
        }

        private void TargetMyLocation()
        {
            var map = GameScene.Scene.MapControl;
            if (map == null) return;
            SetTargetMap(map.Index);
        }

        public void SetTargetMap(int MapIndex)
        {
            TargetMapIndex = MapIndex;
            CurrentRecord = null;
            ScrollOffset = 0;
            ScrollBar.Visible = false;
            WorldMap.Visible = false;
            SearchTextBox.Visible = true;
            SelectedNPC = null;
            if (!GameScene.MapInfoList.ContainsKey(MapIndex))
                Network.Enqueue(new C.RequestMapInfo() { MapIndex = MapIndex });
            else
                CurrentRecord = GameScene.MapInfoList[MapIndex];
            Redraw();
        }

        public void SetTargetNPC(uint NPCIndex)
        {
            if (CurrentRecord?.NPCButtons == null)
                return;

            foreach (BigMapNPCRow row in CurrentRecord.NPCButtons)
            {
                if (row.Info.ObjectID != NPCIndex) continue;

                SelectedNPC = row;
                return;
            }
        }

        private void SetButtonsVisibility(bool visible)
        {
            if (currentRecord == null) return;
            SetMovementButtonsVisibility(visible);
            SetNPCButtonVisibility(visible);
        }

        private void SetMovementButtonsVisibility(bool visible)
        {
            foreach (var button in currentRecord.MovementButtons.Values)
                button.Visible = visible;
        }
        private void SetNPCButtonVisibility(bool visible)
        {
            foreach (var row in currentRecord.NPCButtons)
                row.Visible = false;

            if (!visible) return;

            for (int i = ScrollOffset; i < ScrollOffset + MaximumRows; i++)
            {
                if (i >= currentRecord.NPCButtons.Count) return;
                currentRecord.NPCButtons[i].Location = new Point(590, 50 + (i - ScrollOffset) * 21);
                currentRecord.NPCButtons[i].Visible = true;
            }

            if (currentRecord.NPCButtons.Count <= MaximumRows) return;

            ScrollBar.Visible = true;
            var scrollHeight = ScrollDownButton.Location.Y - ScrollUpButton.Location.Y - 32;
            var extraRows = currentRecord.NPCButtons.Count - MaximumRows;
            GapPerRow = scrollHeight / (float)extraRows;
        }

        public void BigMap_MouseWheel(object sender, MouseEventArgs e)
        {
            int count = e.Delta / SystemInformation.MouseWheelScrollDelta;

            if (count > 0)
                ScrollUp();
            else if (count < 0)
                ScrollDown();
        }

        private void ScrollUp()
        {
            if (ScrollOffset == 0) return;
            ScrollOffset--;
            SetNPCButtonVisibility(true);
        }

        private void ScrollDown()
        {
            if (ScrollOffset >= currentRecord.NPCButtons.Count - MaximumRows) return;
            ScrollOffset++;
            SetNPCButtonVisibility(true);
        }

        private void OpenWorldMap()
        {
            WorldMap.Visible = true;
            SearchTextBox.Visible = false;
            SetButtonsVisibility(false);
        }

        private void TeleportToNPC()
        {
            if (SelectedNPC == null || !SelectedNPC.Info.CanTeleportTo) return;

            MirMessageBox messageBox = new MirMessageBox($" 花费{GameScene.TeleportToNPCCost}金币移动到此NPC ", MirMessageBoxButtons.YesNo);
            messageBox.YesButton.Click += (o, e) =>
            {
                if (GameScene.Gold < GameScene.TeleportToNPCCost)
                {
                    MirMessageBox messageBox2 = new MirMessageBox("金币不足", MirMessageBoxButtons.OK);
                    messageBox2.Show();
                    return;
                }
                Network.Enqueue(new C.TeleportToNPC { ObjectID = SelectedNPC.Info.ObjectID });
            };

            messageBox.Show();
        }

        private void Search()
        {
            if (!SearchTextBox.Enabled)
            {
                SearchTextBox.Enabled = true;
                SearchTextBox.SetFocus();
            }

            if (!string.IsNullOrWhiteSpace(SearchTextBox.Text) && SearchTextBox.Text.Length > 2) return;

            if (CMain.Now < NextSearchTime) return;

            NextSearchTime = CMain.Now.AddSeconds(1);
            Network.Enqueue(new C.SearchMap { Text = SearchTextBox.Text });
        }

        public void SearchTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (sender == null || e.KeyChar != (char)Keys.Enter) return;

            e.Handled = true;

            if (SearchButton.Enabled)
                SearchButton.InvokeMouseClick(null);
        }

        #region Disposable
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CoordinateLabel = null;
                CloseButton = null;
                ScrollUpButton = null;
                ScrollDownButton = null;
                ScrollBar = null;
                WorldButton = null;
                MyLocationButton = null;
                TeleportToButton = null;
                SearchButton = null;
                SearchTextBox = null;
                TitleLabel = null;
            }

            base.Dispose(disposing);
        }

        #endregion
    }

    public class WorldMapImage : MirImageControl
    {
        private new MirImageControl Border;
        private MirImageControl Clouds;
        private List<MirButton> ButtonList = new List<MirButton>();
        private MirLabel TitleLabel;
        public WorldMapImage()
        {
            Library = Libraries.Prguse2;
            Index = 1360;
            NotControl = false;
            Visible = false;

            Clouds = new MirImageControl()
            {
                Parent = this,
                Library = Libraries.Prguse2,
                Index = 1365,
                NotControl = true,
                Visible = true,
                Blending = true

            };

            Border = new MirImageControl()
            {
                Parent = this,
                Library = Libraries.Prguse2,
                Index = 1366,
                NotControl = true,
                Visible = true
            };

            TitleLabel = new MirLabel()
            {
                AutoSize = true,
                BackColour = Color.Black,
                ForeColour = Color.White,
                Parent = this,
                Visible = true
            };
        }

        public void MakeButtons(List<WorldMapIcon> icons)
        {
            foreach (WorldMapIcon icon in icons)
            {
                MirButton button = new MirButton()
                {
                    Index = icon.ImageIndex,
                    UseOffSet = true,
                    Library = Libraries.MapLinkIcon,
                    Parent = this,
                    Sound = SoundList.ButtonA,
                    OnlyDrawWhenActive = true
                };

                button.Click += (o, e) =>
                {
                    GameScene.Scene.BigMapDialog.SetTargetMap(icon.MapIndex);
                };
                button.MouseEnter += (o, e) =>
                {
                    TitleLabel.Text = icon.Title;
                    TitleLabel.Location = new Point(Size.Width / 2 - TitleLabel.Size.Width / 2, 10);
                };
                button.MouseLeave += (o, e) =>
                {
                    TitleLabel.Text = string.Empty;
                };

                ButtonList.Add(button);
            }
        }
    }

    public sealed class BigMapViewPort : MirControl
    {
        BigMapDialog ParentDialog;
        float ScaleX;
        float ScaleY;
        public MirImageControl SelectedNPCIcon, UserRadarDot;

        int BigMap_MouseCoordsProcessing_OffsetX, BigMap_MouseCoordsProcessing_OffsetY;

        public MirImageControl[] Players;
        public static Dictionary<string, Point> PlayerLocations = new Dictionary<string, Point>();

        public BigMapViewPort()
        {
            NotControl = false;
            Size = new Size(568, 380);

            SelectedNPCIcon = new MirImageControl
            {
                Library = Libraries.MapLinkIcon,
                NotControl = false,
                Parent = this,
                Visible = false
            };
            SelectedNPCIcon.MouseEnter += (o, e) => ParentDialog.MouseLocation = ParentDialog.SelectedNPC.Info.Location;

            UserRadarDot = new MirImageControl
            {
                Library = Libraries.Prguse2,
                Index = 1350,
                Parent = this,
                Visible = false,
                NotControl = true
            };

            Players = new MirImageControl[Globals.MaxGroup];
            for (int i = 0; i < Players.Length; i++)
            {
                Players[i] = new MirImageControl
                {
                    Index = 1350,
                    Library = Libraries.Prguse2,
                    Parent = this,
                    NotControl = false,
                    Visible = false,
                };
            }

            BeforeDraw += (o, e) => OnBeforeDraw();
            MouseMove += UpdateBigMapCoordinates;
            ParentChanged += (o, e) => SetParent();
            MouseDown += OnMouseClick;
        }

        private void SetParent()
        {
            ParentDialog = (BigMapDialog)Parent;
        }

        private void UpdateBigMapCoordinates(object sender, MouseEventArgs e)
        {
            int MouseCoordsOnBigMap_MapValue_X = (int)((e.Location.X - BigMap_MouseCoordsProcessing_OffsetX) / ScaleX);
            int MouseCoordsOnBigMap_MapValue_Y = (int)((e.Location.Y - BigMap_MouseCoordsProcessing_OffsetY) / ScaleY);

            ParentDialog.MouseLocation = new Point(MouseCoordsOnBigMap_MapValue_X, MouseCoordsOnBigMap_MapValue_Y);
        }


        private void OnMouseClick(object sender, MouseEventArgs e)
        {
            //★11-03：只有右键才是「走到这里」。原来不分左右键，左键点到地图也会触发一次
            //  整图 A*（大地图上就是几秒定格），这也是「卡一下但什么都没发生」的来源之一。
            if (e.Button != MouseButtons.Right) return;

            if (ParentDialog == null || GameScene.Scene == null) return;
            if (GameScene.Scene.MapControl == null || MapObject.User == null) return;

            var map = GameScene.Scene.MapControl;

            //大地图可以翻到别的地图去看（世界地图/其它地图页签），那种坐标没法寻路
            if (ParentDialog.TargetMapIndex != map.Index)
            {
                GameScene.Scene.ChatDialog.ReceiveChat("只能在当前所在地图使用自动寻路", ChatType.System);
                return;
            }

            if (ScaleX <= 0 || ScaleY <= 0) return;

            int X = (int)Math.Round((e.Location.X - BigMap_MouseCoordsProcessing_OffsetX) / ScaleX);
            int Y = (int)Math.Round((e.Location.Y - BigMap_MouseCoordsProcessing_OffsetY) / ScaleY);

            //点到缩略图边距上会算出负数/越界坐标 —— 原来直接丢给 GetNode，越界就抛异常（寻路没反应）
            X = Math.Max(0, Math.Min(map.Width - 1, X));
            Y = Math.Max(0, Math.Min(map.Height - 1, Y));

            var path = map.PathFinder.FindPath(MapObject.User.CurrentLocation, new Point(X, Y));

            if (path == null || path.Count == 0)
            {
                map.AutoPath = false;
                GameScene.Scene.ChatDialog.ReceiveChat("目标地点不可用,无适宜的路线", ChatType.System);
            }
            else
            {
                map.CurrentPath = path;
                map.AutoPath = true;
            }
        }

        /// <summary>
        /// 把 MapControl.CurrentPath 画成一条青色点线，终点打个黄色方框（11-03 新增）。
        /// 原来寻路只把路径存进 CurrentPath，界面上一个像素都不画，玩家完全看不到路线。
        /// </summary>
        private void DrawAutoPath(MapControl map)
        {
            if (map == null || ParentDialog == null) return;
            if (ParentDialog.TargetMapIndex != map.Index) return;
            if (!map.AutoPath || map.CurrentPath == null) return;
            if (ScaleX <= 0 || ScaleY <= 0) return;

            var path = map.CurrentPath;

            if (path.Count < 2) return;

            Color line = Color.FromArgb(120, 255, 255);   // 青
            Color head = Color.FromArgb(255, 255, 0);     // 黄

            //采样间隔：路很长时放大间隔，避免每帧画几千次拖慢大地图
            float step = 3f;
            float approx = (path.Count * Math.Max(ScaleX, ScaleY)) / 500f;
            if (approx > step) step = approx;

            float lastX = 0f, lastY = 0f;

            for (int i = 0; i < path.Count; i++)
            {
                Node n = path[i];

                float tx = (n.Location.X * ScaleX) + DisplayLocation.X;
                float ty = (n.Location.Y * ScaleY) + DisplayLocation.Y;

                if (i == 0)
                {
                    lastX = tx;
                    lastY = ty;
                    continue;
                }

                float dx = tx - lastX;
                float dy = ty - lastY;
                float len = (float)Math.Sqrt((dx * dx) + (dy * dy));
                int steps = Math.Max(1, (int)(len / step));

                for (int s = 1; s <= steps; s++)
                {
                    float px = lastX + (dx * s / steps);
                    float py = lastY + (dy * s / steps);

                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 2, 2), new Vector3(px, py, 0f), line);
                }

                lastX = tx;
                lastY = ty;
            }

            //终点标记：用 2×2 的雷达点拼一个 6×6 的空心小方框
            Node end = path[path.Count - 1];
            float ex = (end.Location.X * ScaleX) + DisplayLocation.X;
            float ey = (end.Location.Y * ScaleY) + DisplayLocation.Y;

            for (int i = 0; i <= 3; i++)
            {
                DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 2, 2), new Vector3(ex - 3 + (i * 2), ey - 3, 0f), head);
                DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 2, 2), new Vector3(ex - 3 + (i * 2), ey + 3, 0f), head);
                DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 2, 2), new Vector3(ex - 3, ey - 3 + (i * 2), 0f), head);
                DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 2, 2), new Vector3(ex + 3, ey - 3 + (i * 2), 0f), head);
            }
        }


        private void OnBeforeDraw()
        {
            if (!Parent.Visible) return;

            MouseMove -= UpdateBigMapCoordinates;

            BigMapRecord currentRecord;
            if (!GameScene.MapInfoList.TryGetValue(ParentDialog.TargetMapIndex, out currentRecord))
                return;

            ParentDialog.CurrentRecord = currentRecord;
            int index = currentRecord.MapInfo.BigMap;

            if (index <= 0)
                return;

            MouseMove += UpdateBigMapCoordinates;

            Size = Libraries.MiniMap.GetSize(index);
            Rectangle viewRect = new Rectangle(0, 0, Math.Min(568, Size.Width), Math.Min(380, Size.Height));

            viewRect.X = 14 + (568 - viewRect.Width) / 2;
            viewRect.Y = 52 + (380 - viewRect.Height) / 2;

            Location = viewRect.Location;
            Size = viewRect.Size;

            BigMap_MouseCoordsProcessing_OffsetX = DisplayLocation.X;
            BigMap_MouseCoordsProcessing_OffsetY = DisplayLocation.Y;

            ScaleX = Size.Width / (float)currentRecord.MapInfo.Width;
            ScaleY = Size.Height / (float)currentRecord.MapInfo.Height;

            viewRect.Location = new Point(
                (int)(ScaleX * MapObject.User.CurrentLocation.X) - viewRect.Width / 2,
                (int)(ScaleY * MapObject.User.CurrentLocation.Y) - viewRect.Height / 2);

            if (viewRect.Right >= Size.Width)
                viewRect.X = Size.Width - viewRect.Width;
            if (viewRect.Bottom >= Size.Height)
                viewRect.Y = Size.Height - viewRect.Height;

            if (viewRect.X < 0) viewRect.X = 0;
            if (viewRect.Y < 0) viewRect.Y = 0;

            Libraries.MiniMap.Draw(index, DisplayLocation, Size, Color.FromArgb(255, 255, 255));

            var map = GameScene.Scene.MapControl;

            //★11-03：把自动寻路的路线画在大地图上（原来只存了路径，一个像素都没画，玩家看不到）。
            //  必须画在 MiniMap 之后，否则被整张缩略图盖住。
            DrawAutoPath(map);

            if (ParentDialog.TargetMapIndex == map.Index)
            {
                float x;
                float y;
                for (int i = MapControl.Objects.Count - 1; i >= 0; i--)
                {
                    MapObject ob = MapControl.Objects[i];

                    if (ob.Race == ObjectType.Item || ob.Dead || ob.Race == ObjectType.Spell || ob.ObjectID == MapObject.User.ObjectID) continue;
                    //★11-03：坐标换算原来多减了一个 startPointX/Y（视图居中偏移被当成地图坐标用），
                    //  整张图上的怪点都比真实位置偏十几像素 —— 和雷达点、点击换算都对不上。统一成 mapX*Scale。
                    x = (ob.CurrentLocation.X * ScaleX) + DisplayLocation.X;
                    y = (ob.CurrentLocation.Y * ScaleY) + DisplayLocation.Y;

                    Color colour;

                    if (GroupDialog.GroupList.Contains(ob.Name) || ob.Name.EndsWith(string.Format("({0})", MapObject.User.Name)))
                        colour = Color.FromArgb(0, 0, 255);
                    else
                        if (ob is PlayerObject)
                        colour = Color.FromArgb(255, 255, 255);
                    else if (ob is NPCObject || ob.AI == 980)
                        colour = Color.FromArgb(0, 255, 50);
                    else
                        colour = Color.FromArgb(255, 0, 0);

                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 2, 2), new Vector3((float)(x - 0.5), (float)(y - 0.5), 0.0F), colour);
                }

                x = MapObject.User.CurrentLocation.X * ScaleX;
                y = MapObject.User.CurrentLocation.Y * ScaleY;
                var s = UserRadarDot.Size;
                UserRadarDot.Location = new Point((int)x - s.Width / 2, (int)y - s.Height / 2);

                if (GroupDialog.GroupList.Count > 0)
                {
                    for (int i = 0; i < GameScene.Scene.GroupDialog.GroupMembers.Length; i++)
                    {
                        string groupMembersName = GameScene.Scene.GroupDialog.GroupMembers[i].Text;
                        Players[i].Visible = false;

                        foreach (var groupMembersMap in GroupDialog.GroupMembersMap.Where(x => x.Key == groupMembersName && x.Value == map.Title))
                        {
                            foreach (var groupMemberLocation in PlayerLocations.Where(x => x.Key == groupMembersMap.Key))
                            {

                                float alteredX = (groupMemberLocation.Value.X * ScaleX);
                                float alteredY = (groupMemberLocation.Value.Y * ScaleY);

                                if (groupMembersName != MapObject.User.Name)
                                    Players[i].Visible = true;

                                Players[i].Hint = groupMemberLocation.Key;
                                Players[i].Location = new Point((int)(alteredX - 0.5F) - 3, (int)(alteredY - 0.5F) - 3);
                            }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < Players.Length; i++)
                    {
                        Players[i].Visible = false;
                    }
                }





            }
            else
            {
                for (int i = 0; i < Players.Length; i++)
                {
                    Players[i].Visible = false;
                }
            }

            foreach (var record in ParentDialog.CurrentRecord.MovementButtons)
            {
                var movementInfo = record.Key;
                var button = record.Value;

                float x = movementInfo.Location.X * ScaleX;
                float y = movementInfo.Location.Y * ScaleY;

                var s = Libraries.MapLinkIcon.GetSize(button.Index);

                button.Location = new Point((int)x - s.Width / 2, (int)y - s.Height / 2);
            }

            if (ParentDialog.SelectedNPC != null && SelectedNPCIcon.Visible)
            {
                float x = ParentDialog.SelectedNPC.Info.Location.X * ScaleX;
                float y = ParentDialog.SelectedNPC.Info.Location.Y * ScaleY;

                var s = Libraries.MapLinkIcon.GetSize(SelectedNPCIcon.Index);

                SelectedNPCIcon.Location = new Point((int)x - s.Width / 2, (int)y - s.Height / 2);
            }
        }
    }

    public class BigMapNPCRow : MirButton
    {
        BigMapDialog ParentDialog;
        public ClientNPCInfo Info;
        public MirImageControl Icon;
        public MirLabel NameLabel;

        public BigMapNPCRow(ClientNPCInfo info)
        {
            Info = info;
            NotControl = false;
            Size = new Size(140, 25);
            Visible = false;
            Sound = SoundList.ButtonA;

            string name = string.Empty;
            if (Info.Name.Contains("_"))
            {
                string[] splitName = Info.Name.Split('_');

                for (int s = 0; s < splitName.Count(); s++)
                {
                    if (splitName[s] == string.Empty) continue;
                    if (s == splitName.Count() - 1)
                        name += splitName[s];
                    else name += $"『{splitName[s]}』";
                }
            }

            Icon = new MirImageControl
            {
                Index = info.Icon,
                Library = Libraries.MapLinkIcon,
                Location = new Point(1, 1),
                NotControl = true,
                Parent = this,
                Sound = SoundList.ButtonA
            };

            NameLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.White,
                Location = new Point(23, 2),
                Parent = this,
                NotControl = true,
                Text = name,
                Sound = SoundList.ButtonA
            };

            MouseWheel += (o, e) => ParentDialog.BigMap_MouseWheel(o, e);
            ParentChanged += (o, e) => SetParent();
            Click += (o, e) => Select();
        }

        private void SetParent()
        {
            ParentDialog = (BigMapDialog)Parent;
        }

        private void Select()
        {
            ParentDialog.SelectedNPC = ParentDialog.SelectedNPC == this ? null : this;
        }
    }

    public class BigMapRecord
    {
        public int Index;
        public ClientMapInfo MapInfo;
        public Dictionary<ClientMovementInfo, MirButton> MovementButtons = new Dictionary<ClientMovementInfo, MirButton>();
        public List<BigMapNPCRow> NPCButtons = new List<BigMapNPCRow>();

        public BigMapRecord() { }
    }

}
