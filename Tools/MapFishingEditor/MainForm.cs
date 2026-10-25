using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace MapFishingEditor
{
    internal sealed class MainForm : Form
    {
        private MapDoc _doc;
        private MapCanvas _canvas;
        private Panel _mag;
        private Label _info, _title;
        private StatusStrip _status;
        private ToolStripStatusLabel _stLeft, _stRight;
        private NumericUpDown _numBrush, _numGX, _numGY;
        private ComboBox _cmbMode, _cmbGroup;
        private CheckBox _chkWater, _chkStand;
        private Label _lblZoom;

        private readonly Stack<List<KeyValuePair<int, byte>>> _undo = new Stack<List<KeyValuePair<int, byte>>>();
        private List<KeyValuePair<int, byte>> _stroke;
        private bool _dirty;
        private long _fishCount;
        private Point _rectStart = new Point(-1, -1);

        private int BrushValue { get { return 100 + Math.Max(0, _cmbGroup.SelectedIndex); } }
        private bool IsRectMode { get { return _cmbMode.SelectedIndex == 1; } }
        private bool IsPickMode { get { return _cmbMode.SelectedIndex == 2; } }

        public MainForm(string initialFile)
        {
            Text = "Mir2 钓场编辑器";
            Font = new Font("Microsoft YaHei UI", 9f);
            Size = new Size(1400, 860);
            MinimumSize = new Size(1120, 620);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            BuildUi();

            KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.S) { SaveMap(); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.O) { OpenDialog(); e.Handled = true; }
            };

            if (!string.IsNullOrEmpty(initialFile) && File.Exists(initialFile))
                OpenMap(initialFile);
        }

        // ---------------- UI ----------------

        private void BuildUi()
        {
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(6, 4, 6, 0),
                BackColor = Color.FromArgb(246, 246, 248),
                WrapContents = false,
                AutoScroll = true
            };

            bar.Controls.Add(MakeBtn("打开", (s, e) => OpenDialog(), 64));
            var btnSave = MakeBtn("保存", (s, e) => SaveMap(), 62);
            bar.Controls.Add(btnSave);
            bar.Controls.Add(MakeBtn("撤销", (s, e) => Undo(), 54));
            bar.Controls.Add(MakeBtn("还原备份", (s, e) => RestoreBackup(), 76));

            bar.Controls.Add(MakeGap());
            bar.Controls.Add(MakeLabel("模式"));
            _cmbMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 72,
                Margin = new Padding(2, 6, 2, 0),
                Height = 26
            };
            _cmbMode.Items.AddRange(new object[] { "画笔", "矩形", "吸管" });
            _cmbMode.SelectedIndex = 0;
            bar.Controls.Add(_cmbMode);

            bar.Controls.Add(MakeLabel("笔刷"));
            _numBrush = new NumericUpDown
            {
                Minimum = 1, Maximum = 64, Value = 4, Width = 52,
                Margin = new Padding(2, 6, 6, 0)
            };
            bar.Controls.Add(_numBrush);

            bar.Controls.Add(MakeLabel("掉落组"));
            _cmbGroup = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 138,
                Margin = new Padding(2, 6, 6, 0)
            };
            for (int i = 0; i <= 18; i++)
                _cmbGroup.Items.Add(string.Format("{0} → {1:00}Fishing.txt", 100 + i, i));
            _cmbGroup.SelectedIndex = 0;
            bar.Controls.Add(_cmbGroup);

            _chkWater = new CheckBox
            {
                Text = "只刷水面",
                Checked = true,
                AutoSize = false,
                Width = 78,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(4, 5, 0, 0)
            };
            bar.Controls.Add(_chkWater);

            _chkStand = new CheckBox
            {
                Text = "可站位置",
                AutoSize = false,
                Width = 84,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(4, 5, 0, 0)
            };
            _chkStand.CheckedChanged += (s, e) =>
            {
                _canvas.ShowStand = _chkStand.Checked;
                if (_chkStand.Checked) BuildStandCache();
                _canvas.Invalidate();
            };
            bar.Controls.Add(_chkStand);

            bar.Controls.Add(MakeGap());
            bar.Controls.Add(MakeBtn("－", (s, e) => SetZoom(_canvas.Zoom - 1), 34));
            _lblZoom = MakeLabel("2x");
            _lblZoom.Width = 40;
            _lblZoom.TextAlign = ContentAlignment.MiddleCenter;
            bar.Controls.Add(_lblZoom);
            bar.Controls.Add(MakeBtn("＋", (s, e) => SetZoom(_canvas.Zoom + 1), 34));
            bar.Controls.Add(MakeBtn("适应窗口", (s, e) => FitZoom(), 82));

            bar.Controls.Add(MakeGap());
            bar.Controls.Add(MakeLabel("跳到"));
            _numGX = new NumericUpDown { Minimum = 0, Maximum = 7999, Width = 64, Margin = new Padding(2, 6, 2, 0) };
            _numGY = new NumericUpDown { Minimum = 0, Maximum = 7999, Width = 64, Margin = new Padding(2, 6, 2, 0) };
            bar.Controls.Add(_numGX);
            bar.Controls.Add(_numGY);
            bar.Controls.Add(MakeBtn("定位", (s, e) => GotoCell(), 54));

            // 主画布
            _canvas = new MapCanvas { Dock = DockStyle.Fill };
            _canvas.StrokeBegin += Canvas_StrokeBegin;
            _canvas.StrokeMove += Canvas_StrokeMove;
            _canvas.StrokeEnd += Canvas_StrokeEnd;
            _canvas.Hovered += Canvas_Hovered;
            _canvas.ZoomChanged += () => { _lblZoom.Text = _canvas.Zoom + "x"; };

            // 右侧面板
            var side = new Panel
            {
                Dock = DockStyle.Right,
                Width = 268,
                BackColor = Color.FromArgb(250, 250, 252),
                Padding = new Padding(8)
            };

            _mag = new Panel
            {
                Location = new Point(8, 8),
                Size = new Size(250, 250),
                BackColor = Color.FromArgb(30, 30, 34),
                BorderStyle = BorderStyle.FixedSingle
            };
            _mag.Paint += Mag_Paint;
            side.Controls.Add(_mag);

            _title = new Label
            {
                Location = new Point(8, 266),
                Size = new Size(250, 42),
                AutoSize = false,
                Text = "未载入地图",
                ForeColor = Color.FromArgb(70, 70, 80)
            };
            side.Controls.Add(_title);

            _info = new Label
            {
                Location = new Point(8, 308),
                Size = new Size(250, 130),
                AutoSize = false,
                Text = "",
                ForeColor = Color.FromArgb(60, 60, 70)
            };
            side.Controls.Add(_info);

            side.Controls.Add(new Label
            {
                Location = new Point(8, 442),
                Size = new Size(250, 150),
                Text = "图例\r\n" +
                       "■ 深蓝  水面（可刷）\r\n" +
                       "■ 灰褐  墙体/其他阻挡\r\n" +
                       "■ 浅灰  陆地（可站）\r\n" +
                       "■ 亮青  钓鱼点 light 100~119\r\n" +
                       "■ 淡黄  可抛竿站位（勾选后显示）\r\n\r\n" +
                       "操作\r\n" +
                       "左键涂抹 / 右键擦除\r\n" +
                       "滚轮缩放 · Ctrl+Z 撤销\r\n" +
                       "Ctrl+S 保存",
                ForeColor = Color.FromArgb(110, 110, 120)
            });

            _status = new StatusStrip();
            _stLeft = new ToolStripStatusLabel("就绪") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _stRight = new ToolStripStatusLabel("");
            _status.Items.Add(_stLeft);
            _status.Items.Add(_stRight);

            var center = new Panel { Dock = DockStyle.Fill };
            center.Controls.Add(_canvas);
            center.Controls.Add(side);
            _canvas.BringToFront();

            Controls.Add(center);
            Controls.Add(bar);
            Controls.Add(_status);
        }

        private static Button MakeBtn(string text, EventHandler onClick, int w)
        {
            var b = new Button
            {
                Text = text,
                Width = w,
                Height = 30,
                Margin = new Padding(3, 5, 0, 0),
                FlatStyle = FlatStyle.System
            };
            b.Click += onClick;
            return b;
        }

        private static Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Width = 42,
                Height = 30,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(4, 5, 2, 0)
            };
        }

        private static Control MakeGap()
        {
            return new Label { Width = 10, Height = 30, Margin = new Padding(0) };
        }

        // ---------------- 文件 ----------------

        private void OpenDialog()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "打开地图文件";
                dlg.Filter = "Mir2 地图 (*.map)|*.map|所有文件 (*.*)|*.*";
                string dir = @"D:\123\Maps";
                if (Directory.Exists(dir)) dlg.InitialDirectory = dir;
                else if (_doc != null) dlg.InitialDirectory = Path.GetDirectoryName(_doc.FilePath);

                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (!ConfirmDiscard()) return;
                OpenMap(dlg.FileName);
            }
        }

        private void OpenMap(string path)
        {
            try
            {
                var doc = MapDoc.Load(path);
                _doc = doc;
                _undo.Clear();
                _stroke = null;
                _dirty = false;

                _canvas.Doc = doc;
                _canvas.StandCache = null;
                _canvas.Zoom = FitZoomValue();
                _canvas.UpdateScrollSize();
                _canvas.RenderAll();
                _lblZoom.Text = _canvas.Zoom + "x";

                _fishCount = doc.CountFish();
                _numGX.Maximum = doc.Width - 1;
                _numGY.Maximum = doc.Height - 1;

                if (_chkStand.Checked) BuildStandCache();

                UpdateCaption();
                _stLeft.Text = string.Format("已载入 {0}   {1}x{2}   已有钓鱼点 {3} 格",
                    Path.GetFileName(path), doc.Width, doc.Height, _fishCount);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SaveMap()
        {
            if (_doc == null) return;
            try
            {
                string bak = _doc.Save(true);
                _doc.Save(false);
                _dirty = false;
                UpdateCaption();
                string msg = "已保存：" + _doc.FilePath;
                if (bak != null) msg += "\r\n原文件已备份为：" + Path.GetFileName(bak);
                _stLeft.Text = msg.Replace("\r\n", "   ");
                MessageBox.Show(this, msg, "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreBackup()
        {
            if (_doc == null) return;
            if (!MapDoc.HasBackup(_doc.FilePath))
            {
                MessageBox.Show(this, "这个地图没有备份文件（" + Path.GetFileName(_doc.FilePath) + ".bak_fishing）。",
                    "无法还原", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "用备份覆盖当前内容？未保存的改动会丢失。", "还原备份",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            try
            {
                _doc.RestoreBackup();
                _undo.Clear();
                _dirty = true;
                _canvas.RenderAll();
                _fishCount = _doc.CountFish();
                if (_chkStand.Checked) BuildStandCache();
                UpdateCaption();
                _stLeft.Text = "已从备份还原（内存中），记得再按一次「保存」写回文件";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "还原失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool ConfirmDiscard()
        {
            if (!_dirty) return true;
            return MessageBox.Show(this, "当前地图有未保存的修改，确定放弃？", "确认",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
        }

        // ---------------- 绘制交互 ----------------

        private void Canvas_Hovered(Point c)
        {
            if (_doc == null) return;
            if (_doc.InRange(c.X, c.Y))
            {
                int r = (int)_numBrush.Value;
                int half = r / 2;
                _canvas.BrushCell = IsPickMode ? new Rectangle(c.X, c.Y, 1, 1)
                                               : new Rectangle(c.X - half, c.Y - half, r, r);
                byte l = _doc.GetLight(c.X, c.Y);
                _info.Text = string.Format(
                    "坐标  X {0}   Y {1}\r\n地形  {2}\r\nlight  {3}{4}\r\n\r\n钓鱼点总数  {5} 格",
                    c.X, c.Y, _doc.TerrainName(c.X, c.Y), l,
                    (l >= 100 && l <= 119) ? "  （掉落组 " + (l - 100) + "）" : "",
                    _fishCount);
            }
            else
            {
                _canvas.BrushCell = Rectangle.Empty;
            }
            _canvas.Invalidate();
            _mag.Invalidate();
        }

        private void Canvas_StrokeBegin(Point c, MouseButtons btn)
        {
            if (_doc == null) return;
            bool erase = btn == MouseButtons.Right;

            if (IsPickMode)
            {
                byte l = _doc.GetLight(c.X, c.Y);
                _cmbGroup.SelectedIndex = (l >= 100 && l <= 118) ? l - 100 : 0;
                _stLeft.Text = string.Format("吸取：({0},{1}) light={2}", c.X, c.Y, l);
                return;
            }

            _stroke = new List<KeyValuePair<int, byte>>();

            if (IsRectMode)
            {
                _rectStart = c;
                _canvas.RectSelect = new Rectangle(c.X, c.Y, 1, 1);
                _canvas.Invalidate();
                return;
            }

            ApplyBrush(c, erase);
        }

        private void Canvas_StrokeMove(Point c, MouseButtons btn)
        {
            if (_doc == null || _stroke == null) return;
            bool erase = btn == MouseButtons.Right;

            if (IsRectMode)
            {
                int x0 = Math.Min(_rectStart.X, c.X), y0 = Math.Min(_rectStart.Y, c.Y);
                int x1 = Math.Max(_rectStart.X, c.X), y1 = Math.Max(_rectStart.Y, c.Y);
                _canvas.RectSelect = new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                _canvas.Invalidate();
                return;
            }

            ApplyBrush(c, erase);
        }

        private void Canvas_StrokeEnd()
        {
            if (_doc == null) return;

            var sel = _canvas.RectSelect; // 值类型，先取本地副本
            if (IsRectMode && sel.Width > 0 && sel.Height > 0)
            {
                FillRect(sel, false);
                _canvas.RectSelect = Rectangle.Empty;
                _canvas.Invalidate();
            }

            if (_stroke != null && _stroke.Count > 0)
                _undo.Push(_stroke);
            _stroke = null;

            if (_chkStand.Checked) BuildStandCache();
            UpdateCaption();
        }

        private void ApplyBrush(Point c, bool erase)
        {
            int r = (int)_numBrush.Value;
            int half = r / 2;
            var area = new Rectangle(c.X - half, c.Y - half, r, r);
            PaintArea(area, erase);
            _canvas.RenderArea(area.X, area.Y, area.Width, area.Height);
            _canvas.Invalidate();
            _mag.Invalidate();
        }

        private void FillRect(Rectangle r, bool erase)
        {
            PaintArea(r, erase);
            _canvas.RenderArea(r.X, r.Y, r.Width, r.Height);
            _canvas.Invalidate();
        }

        private void PaintArea(Rectangle area, bool erase)
        {
            byte v = erase ? (byte)0 : (byte)BrushValue;
            bool onlyWater = _chkWater.Checked;
            int x1 = area.Right, y1 = area.Bottom;
            for (int y = area.Y; y < y1; y++)
                for (int x = area.X; x < x1; x++)
                {
                    if (!_doc.InRange(x, y)) continue;
                    if (onlyWater && !_doc.IsWater(x, y)) continue;
                    byte old = _doc.GetLight(x, y);
                    if (old == v) continue;

                    if (old >= 100 && old <= 119 && !(v >= 100 && v <= 119)) _fishCount--;
                    else if (!(old >= 100 && old <= 119) && v >= 100 && v <= 119) _fishCount++;

                    _stroke.Add(new KeyValuePair<int, byte>(_doc.Offset(x, y) + MapDoc.LightOffset, old));
                    _doc.SetLight(x, y, v);
                    _dirty = true;
                }
        }

        private void Undo()
        {
            if (_doc == null || _undo.Count == 0) { _stLeft.Text = "没有可撤销的操作"; return; }
            var list = _undo.Pop();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                int off = list[i].Key;
                byte old = list[i].Value;
                byte cur = _doc.Raw[off];
                if (cur >= 100 && cur <= 119 && !(old >= 100 && old <= 119)) _fishCount--;
                else if (!(cur >= 100 && cur <= 119) && old >= 100 && old <= 119) _fishCount++;
                _doc.Raw[off] = old;
            }
            _dirty = true;
            _canvas.RenderAll();
            if (_chkStand.Checked) BuildStandCache();
            UpdateCaption();
            _stLeft.Text = string.Format("已撤销一步（{0} 格）", list.Count);
        }

        private void BuildStandCache()
        {
            if (_doc == null) return;
            var doc = _doc;
            var cache = new bool[doc.Width * doc.Height];
            for (int y = 0; y < doc.Height; y++)
                for (int x = 0; x < doc.Width; x++)
                {
                    if (doc.IsBlocked(x, y)) continue;
                    bool ok = false;
                    for (int d = 0; d < 4 && !ok; d++)
                    {
                        int fx = x, fy = y;
                        if (d == 0) fy -= 3;
                        else if (d == 1) fx += 3;
                        else if (d == 2) fy += 3;
                        else fx -= 3;
                        if (!doc.InRange(fx, fy)) continue;
                        if (doc.IsFish(fx, fy)) ok = true;
                    }
                    cache[y * doc.Width + x] = ok;
                }
            _canvas.StandCache = cache;
            _canvas.Invalidate();
        }

        // ---------------- 缩放 / 定位 ----------------

        private int FitZoomValue()
        {
            if (_doc == null) return 2;
            int availW = Math.Max(200, _canvas.ClientSize.Width - 24);
            int availH = Math.Max(200, _canvas.ClientSize.Height - 24);
            int z = Math.Min(availW / Math.Max(1, _doc.Width), availH / Math.Max(1, _doc.Height));
            return Math.Max(1, Math.Min(4, z));
        }

        private void FitZoom()
        {
            SetZoom(FitZoomValue());
        }

        private void SetZoom(int z)
        {
            if (_doc == null) return;
            z = Math.Max(1, Math.Min(12, z));
            _canvas.Zoom = z;
            _canvas.UpdateScrollSize();
            _lblZoom.Text = z + "x";
            _canvas.Invalidate();
        }

        private void GotoCell()
        {
            if (_doc == null) return;
            int x = (int)_numGX.Value, y = (int)_numGY.Value;
            if (x >= _doc.Width || y >= _doc.Height)
            {
                MessageBox.Show(this, "坐标超出地图范围。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int cx = x * _canvas.Zoom - _canvas.ClientSize.Width / 2;
            int cy = y * _canvas.Zoom - _canvas.ClientSize.Height / 2;
            _canvas.AutoScrollPosition = new Point(Math.Max(0, cx), Math.Max(0, cy));
            _canvas.Invalidate();
        }

        // ---------------- 放大镜 ----------------

        private void Mag_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.FromArgb(24, 24, 28));
            if (_doc == null || _canvas.BaseBitmap == null) return;

            const int cells = 40, scale = 6;
            Point p = _canvas.Hover;
            if (!_doc.InRange(p.X, p.Y)) p = new Point(_doc.Width / 2, _doc.Height / 2);

            int sx = p.X - cells / 2;
            int sy = p.Y - cells / 2;

            using (var b = new SolidBrush(Color.Black))
            {
                for (int j = 0; j < cells; j++)
                    for (int i = 0; i < cells; i++)
                    {
                        int gx = sx + i, gy = sy + j;
                        if (!_doc.InRange(gx, gy)) continue;
                        b.Color = Color.FromArgb(_canvas.ColorOf(gx, gy));
                        g.FillRectangle(b, i * scale, j * scale, scale, scale);
                    }
            }

            // 网格
            using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255)))
            {
                for (int i = 0; i <= cells; i++)
                {
                    g.DrawLine(pen, i * scale, 0, i * scale, cells * scale);
                    g.DrawLine(pen, 0, i * scale, cells * scale, i * scale);
                }
            }

            // 笔刷范围
            var br = _canvas.BrushCell;
            if (br.Width > 0)
            {
                var rr = new Rectangle((br.X - sx) * scale, (br.Y - sy) * scale, br.Width * scale, br.Height * scale);
                using (var pen = new Pen(Color.FromArgb(255, 255, 220, 40), 2f))
                    g.DrawRectangle(pen, rr);
            }

            // 中心十字
            int cxp = (p.X - sx) * scale + scale / 2;
            int cyp = (p.Y - sy) * scale + scale / 2;
            using (var pen = new Pen(Color.FromArgb(220, 255, 80, 80), 1f))
            {
                g.DrawLine(pen, cxp - 6, cyp, cxp + 6, cyp);
                g.DrawLine(pen, cxp, cyp - 6, cxp, cyp + 6);
            }

            using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255)))
                g.DrawRectangle(pen, 0, 0, _mag.Width - 1, _mag.Height - 1);
        }

        // ---------------- 标题 ----------------

        private void UpdateCaption()
        {
            if (_doc == null)
            {
                Text = "Mir2 钓场编辑器";
                _title.Text = "未载入地图";
                return;
            }
            string name = Path.GetFileName(_doc.FilePath);
            Text = string.Format("Mir2 钓场编辑器 - {0}{1}", name, _dirty ? " *" : "");
            _title.Text = string.Format("{0}\r\n{1} x {2}   钓鱼点 {3} 格{4}",
                name, _doc.Width, _doc.Height, _fishCount, _dirty ? "   (未保存)" : "");
            if (_stRight.Text.Length == 0)
                _stRight.Text = _doc.FilePath;
        }
    }
}
