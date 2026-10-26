using Client.MirGraphics;
using Client.MirSounds;

namespace Client.MirControls
{
    /// <summary>
    /// 可编辑下拉框：平时显示一行文字（纯自绘，不占键盘焦点），
    /// 点文字 → 出现真输入框可直接键入，点别处（失焦）→ 自动收起；点右侧箭头 → 弹出候选列表挑选。
    /// 内挂面板的「使用物品 / 百分比 / 间隔」都用它。
    ///
    /// 为什么不一直显示输入框：MirTextBox 内部是 WinForms TextBox，会抢键盘焦点，
    /// 面板一打开人物就走不动了。所以默认隐藏，按需唤出。
    /// </summary>
    public sealed class MirEditableDropDown : MirControl
    {
        private readonly MirLabel _display;
        private readonly MirTextBox _input;
        private readonly MirButton _arrow;
        private readonly MirLabel[] _option = new MirLabel[8];
        private readonly List<string> _items = new List<string>();
        private readonly int _origHeight;

        private bool _open;
        private bool _numbersOnly;
        private bool _showArrow = true;

        /// <summary>边框色：常态暗金，编辑态提亮（给玩家「点进去了」的反馈）</summary>
        private static readonly Color BorderColourNormal = Color.FromArgb(255, 146, 132, 100);
        private static readonly Color BorderColourActive = Color.FromArgb(255, 232, 208, 150);

        /// <summary>框底色：面板底图接近纯黑（RGB≈6,4,10），输入槽用略暖的深色才不会与背景糊成一片</summary>
        private static readonly Color FieldBack = Color.FromArgb(255, 18, 16, 12);

        /// <summary>候选列表底色 / 悬停色</summary>
        private static readonly Color OptionBack = Color.FromArgb(255, 34, 30, 24);
        private static readonly Color OptionHover = Color.FromArgb(255, 140, 70, 0);

        public event EventHandler TextChanged;

        public string Text
        {
            get { return _display == null || _display.IsDisposed ? string.Empty : (_display.Text ?? string.Empty); }
            set
            {
                string v = value ?? string.Empty;

                // 正在编辑时不要覆盖玩家正在敲的内容
                if (_input != null && !_input.IsDisposed && !_input.Visible) _input.Text = v;
                if (_display != null && !_display.IsDisposed) _display.Text = v;

                if (TextChanged != null) TextChanged(this, EventArgs.Empty);
            }
        }

        public List<string> Items { get { return _items; } }

        public bool NumbersOnly
        {
            get { return _numbersOnly; }
            set
            {
                _numbersOnly = value;

                // 数字框不需要候选列表，顺手清空（防止被外部灌入物品名）
                if (value) SetItems(null);
            }
        }

        /// <summary>是否显示右侧下拉箭头。纯数字输入框设 false，箭头隐藏、文字区吃满整行</summary>
        public bool ShowArrow
        {
            get { return _showArrow; }
            set
            {
                if (_showArrow == value) return;

                _showArrow = value;

                if (_arrow != null && !_arrow.IsDisposed) _arrow.Visible = value;

                OnSizeChanged();
                Redraw();
            }
        }

        public int MaxLength
        {
            get { return _input == null || _input.IsDisposed ? 0 : _input.MaxLength; }
            set { if (_input != null && !_input.IsDisposed) _input.MaxLength = value; }
        }

        public void SetItems(IEnumerable<string> list)
        {
            _items.Clear();

            if (list != null)
                foreach (string s in list)
                    if (!string.IsNullOrWhiteSpace(s) && !_items.Contains(s))
                        _items.Add(s);

            CloseList();
        }

        public MirEditableDropDown()
        {
            Size = new Size(100, 17);
            _origHeight = 17;
            BackColour = FieldBack;

            // ★必须有底色 + 边框，否则在深色面板上这些框看着像悬空的文字
            DrawControlTexture = true;
            Border = true;
            BorderColour = BorderColourNormal;

            // 平时显示的文字层（自绘，不抢焦点）
            _display = new MirLabel
            {
                Parent = this,
                Location = new Point(2, 1),
                Size = new Size(76, 15),
                ForeColour = Color.FromArgb(255, 245, 238, 220),
                BackColour = FieldBack,
                Font = new Font(Settings.FontName, 8F),
                DrawFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter,
                Text = string.Empty,
            };
            _display.Click += (o, e) => BeginEdit();

            // 真正的输入框（默认隐藏，点文字才出现）
            _input = new MirTextBox
            {
                Parent = this,
                Location = new Point(2, 1),
                Size = new Size(76, 15),
                BackColour = FieldBack,
                ForeColour = Color.White,
                Font = new Font(Settings.FontName, 8F),
                MaxLength = 24,
                Visible = false,
            };
            _input.TextBox.TextChanged += (o, e) =>
            {
                if (_display != null && !_display.IsDisposed) _display.Text = _input.Text;
                if (TextChanged != null) TextChanged(this, EventArgs.Empty);
            };
            _input.TextBox.KeyPress += (o, e) =>
            {
                if (e.KeyChar == (char)Keys.Enter)
                {
                    EndEdit();
                    e.Handled = true;
                    return;
                }

                if (_numbersOnly && !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    e.Handled = true;
            };
            _input.TextBox.LostFocus += (o, e) => EndEdit();

            _arrow = new MirButton
            {
                Index = 207,
                HoverIndex = 208,
                PressedIndex = 209,
                Library = Libraries.Prguse2,
                Parent = this,
                Location = new Point(81, 2),
                Sound = SoundList.ButtonA,
            };
            _arrow.Click += (o, e) => ToggleList();

            for (int i = 0; i < _option.Length; i++)
            {
                int index = i;

                _option[i] = new MirLabel
                {
                    Parent = this,
                    Visible = false,
                    Location = new Point(1, 17 + i * 15),
                    Size = new Size(98, 14),
                    ForeColour = Color.White,
                    BackColour = OptionBack,
                    Font = new Font(Settings.FontName, 8F),
                    DrawFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter,
                };
                _option[i].MouseEnter += (o, e) => _option[index].BackColour = OptionHover;
                _option[i].MouseLeave += (o, e) => _option[index].BackColour = OptionBack;
                _option[i].Click += (o, e) => PickItem(index);
            }

            BeforeDraw += MirEditableDropDown_BeforeDraw;

            // 构造后统一排一次：控制器初始 Size 恰好等于默认 100 时不会触发 OnSizeChanged，
            // 会导致文字区停留在默认 76 宽（物品名被截短）
            OnSizeChanged();
        }

        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();

            // 有箭头时给箭头留 24px，没有箭头就吃满（数字框更容易看清）
            int textW = Math.Max(12, Size.Width - (_showArrow ? 24 : 8));

            if (_display != null && !_display.IsDisposed)
            {
                _display.Location = new Point(2, 1);
                _display.Size = new Size(textW, 15);
            }

            if (_input != null && !_input.IsDisposed)
            {
                _input.Location = new Point(2, 1);
                _input.Size = new Size(textW, 15);
            }

            if (_arrow != null && !_arrow.IsDisposed)
                _arrow.Location = new Point(Math.Max(4, Size.Width - 19), 2);

            for (int i = 0; i < _option.Length; i++)
                if (_option[i] != null && !_option[i].IsDisposed)
                {
                    _option[i].Location = new Point(1, _origHeight + i * 15);
                    _option[i].Size = new Size(Math.Max(20, Size.Width - 2), 14);
                }
        }

        protected override void OnVisibleChanged()
        {
            base.OnVisibleChanged();

            if (!Visible)
            {
                CloseList();
                EndEdit();
            }
        }

        /// <summary>面板显示/隐藏时统一收起</summary>
        public void SetHostVisible(bool visible)
        {
            if (!visible)
            {
                CloseList();
                EndEdit();
            }
        }

        /// <summary>进入编辑态：显示真输入框并聚焦</summary>
        public void BeginEdit()
        {
            if (_input == null || _input.IsDisposed) return;

            CloseList();

            _input.Text = _display.Text;
            _input.Visible = true;

            BorderColour = BorderColourActive;

            // MirTextBox 内部是真 WinForms TextBox，首次显示才挂到 Form 上（Shown 事件）。
            // 这里直接挂好，避免第一次点击时输入框不出现（要再点一次才出来）。
            if (_input.TextBox != null && !_input.TextBox.IsDisposed)
            {
                if (_input.TextBox.Parent == null) _input.TextBox.Parent = Program.Form;
                if (!_input.TextBox.Visible) _input.TextBox.Visible = true;
            }

            _input.SetFocus();
        }

        /// <summary>退出编辑态：收起输入框，把内容写回显示层</summary>
        public void EndEdit()
        {
            if (_input == null || _input.IsDisposed) return;

            if (!_input.Visible)
            {
                BorderColour = BorderColourNormal;
                return;
            }

            string v = _input.Text ?? string.Empty;

            _input.Visible = false;
            BorderColour = BorderColourNormal;

            if (_display != null && !_display.IsDisposed) _display.Text = v;
            if (TextChanged != null) TextChanged(this, EventArgs.Empty);
        }

        private void MirEditableDropDown_BeforeDraw(object sender, EventArgs e)
        {
            if (!_open) return;
            if (IsMouseOver(CMain.MPoint)) return;

            foreach (MirControl c in Controls)
                if (c != null && !c.IsDisposed && c.Visible && c.IsMouseOver(CMain.MPoint))
                    return;

            CloseList();
        }

        private void ToggleList()
        {
            if (_open) CloseList();
            else OpenList();
        }

        public void OpenList()
        {
            if (_items.Count == 0) return;

            EndEdit();

            // 先把自己提到兄弟控件最上层，否则展开的候选列表会被下一行的控件盖住
            BringToFront();

            int n = Math.Min(_items.Count, _option.Length);

            // 再按父面板剩余高度收一收，避免列表压出面板底部
            if (Parent != null)
            {
                int room = (Parent.Size.Height - Location.Y - _origHeight) / 15;
                if (room < n) n = Math.Max(1, room);
            }

            for (int i = 0; i < _option.Length; i++)
            {
                if (i < n)
                {
                    _option[i].Text = _items[i];
                    _option[i].Visible = true;
                }
                else
                {
                    _option[i].Visible = false;
                }
            }

            Size = new Size(Size.Width, _origHeight + n * 15);
            _open = true;
        }

        public void CloseList()
        {
            if (_option[0] != null)
                for (int i = 0; i < _option.Length; i++)
                    if (_option[i] != null && !_option[i].IsDisposed)
                        _option[i].Visible = false;

            if (_origHeight > 0 && Size.Height != _origHeight)
                Size = new Size(Size.Width, _origHeight);

            _open = false;
        }

        private void PickItem(int index)
        {
            if (index >= 0 && index < _items.Count)
                Text = _items[index];

            CloseList();
        }

        public override void Draw()
        {
            // 面板拖动时，浮在 Form 上的真实输入框要跟着走（它只在 Location 变化时才同步）
            if (_input != null && !_input.IsDisposed && _input.TextBox != null && _input.TextBox.Visible)
            {
                Point want = _input.DisplayLocation;
                if (_input.TextBox.Location != want)
                    _input.TextBox.Location = want;
            }

            base.Draw();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing) return;

            if (_input != null && !_input.IsDisposed) _input.Dispose();
            if (_arrow != null && !_arrow.IsDisposed) _arrow.Dispose();
            if (_display != null && !_display.IsDisposed) _display.Dispose();

            for (int i = 0; i < _option.Length; i++)
                if (_option[i] != null && !_option[i].IsDisposed)
                    _option[i].Dispose();
        }
    }
}
