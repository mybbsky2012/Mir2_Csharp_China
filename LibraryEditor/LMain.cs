using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using LibraryEditor.Formats;
using Shared.Extensions;

namespace LibraryEditor
{
    public partial class LMain : Form
    {
        private readonly Dictionary<int, int> _indexList = new Dictionary<int, int>();
        private ILibrary _library, _referenceLibrary, _shadowLibrary;
        private bool _opening;
        /// <summary>程序化同步 X/Y 输入框时抑制回写，避免把界面值当成用户编辑写进帧数据。</summary>
        private bool _syncingOffsetBoxes;
        private MLibraryV2.MImage _selectedImage, _exportImage;
        private Image _originalImage;
        public Bitmap _referenceImage;

        protected bool ImageTabActive = true;
        protected bool MaskTabActive = false;
        protected bool FrameTabActive = false;

        public bool ApplyOffsets => checkBox1.Checked;

        protected string ViewMode = "Image";

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public LMain()
        {
            InitializeComponent();

            this.FrameAction.ValueType = typeof(MirAction);
            this.FrameAction.DataSource = Enum.GetValues(typeof(MirAction));


            SendMessage(PreviewListView.Handle, 4149, 0, 5242946); //80 x 66

            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);

            if (Program.openFileWith.Length > 0 && File.Exists(Program.openFileWith))
            {
                OpenLibrary(Program.openFileWith);
            }
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            string dropExt = Path.GetExtension(files[0]).ToUpperInvariant();
            // Mir3 的 .MIZ/.MIX 不能被当成 Mir2 盛大格式转换，必须按魔数嗅探后直接打开
            bool mir3Drop = (dropExt == ".MIZ" || dropExt == ".MIX")
                            && File.Exists(files[0]) && IsMir3File(files[0]);

            if (!mir3Drop && (dropExt == ".WIL" || dropExt == ".WZL" || dropExt == ".MIZ"))
            {
                toolStripProgressBar.Maximum = files.Length;
                toolStripProgressBar.Value = 0;

                new Action(() =>
                {
                    try
                    {
                        ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = 8 };
                        Parallel.For(0, files.Length, options, i =>
                        {
                            if (Path.GetExtension(files[i]) == ".wtl")
                            {
                                WTLLibrary WTLlib = new WTLLibrary(files[i]);
                                WTLlib.ToMLibrary();
                            }
                            else
                            {
                                WeMadeLibrary WILlib = new WeMadeLibrary(files[i]);
                                WILlib.ToMLibrary();
                            }

                            Invoke(new Action(() =>
                            {
                                toolStripProgressBar.Value++;
                            }));

                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }

                    Invoke(new Action(() =>
                    {
                        toolStripProgressBar.Value = 0;
                    }));

                    MessageBox.Show(
                        string.Format("已成功转换 {0} {1}",
                            (files.Length).ToString(),
                            (files.Length > 1) ? "libraries" : "library"));
                }).BeginInvoke(null, null);
            }
            else if (dropExt == ".LIB" ||
                     dropExt == ".ELITE" ||
                     dropExt == ".ALIB" ||
                     dropExt == ".EURE" ||
                     dropExt == ".WIL" ||
                     dropExt == ".WZL" ||
                     dropExt == ".MIZ" ||
                     dropExt == ".MIX" ||
                     dropExt == ".WTL")
            {
                // 统一走后台载入，避免大文件把界面卡住
                OpenLibrary(files[0]);
            }
            else
            {
                return;
            }
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void ClearInterface()
        {
            _selectedImage = null;
            ImageBox.Image = null;
            ZoomTrackBar.Value = 1;

            WidthLabel.Text = "<空>";
            HeightLabel.Text = "<空>";
            SyncOffsetBoxes(0, 0);
        }

        public static Bitmap AddPaddingToBitmap(Bitmap originalBitmap, int padding)
        {
            int newWidth = originalBitmap.Width + 2 * padding;
            int newHeight = originalBitmap.Height + 2 * padding;

            Bitmap paddedBitmap = new Bitmap(newWidth, newHeight);

            using (Graphics g = Graphics.FromImage(paddedBitmap))
            {
                g.Clear(Color.Transparent);

                int x = (paddedBitmap.Width - originalBitmap.Width) / 2;
                int y = (paddedBitmap.Height - originalBitmap.Height) / 2;

                g.DrawImage(originalBitmap, x, y);
            }

            return paddedBitmap;
        }

        /// <summary>
        /// 把帧的 X/Y 同步到界面输入框。帧偏移是 short，理论上始终落在输入框范围内；
        /// 这里仍做一次夹取，并抑制回写事件——否则界面值会被当作用户编辑写回帧数据，
        /// 且会递归触发 SelectedIndexChanged（旧崩溃点：X=9703 超出 Maximum 抛 ArgumentOutOfRangeException）。
        /// </summary>
        private void SyncOffsetBoxes(int x, int y)
        {
            if (_syncingOffsetBoxes) return;

            _syncingOffsetBoxes = true;
            try
            {
                decimal vx = ClampToBox(numericUpDownX, x);
                decimal vy = ClampToBox(numericUpDownY, y);

                if (numericUpDownX.Value != vx) numericUpDownX.Value = vx;
                if (numericUpDownY.Value != vy) numericUpDownY.Value = vy;
            }
            finally
            {
                _syncingOffsetBoxes = false;
            }
        }

        private static decimal ClampToBox(NumericUpDown box, decimal value)
        {
            if (value < box.Minimum) return box.Minimum;
            if (value > box.Maximum) return box.Maximum;
            return value;
        }

        /// <summary>供“跳转到指定帧”等入口安全赋值，避免帧数超过输入框上限时抛异常。</summary>
        private static void SafeSetValue(NumericUpDown box, decimal value)
        {
            decimal v = ClampToBox(box, value);
            if (box.Value != v) box.Value = v;
        }

        private void PreviewListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (PreviewListView.SelectedIndices.Count == 0)
            {
                ClearInterface();
                return;
            }

            _selectedImage = _library.GetMImage(PreviewListView.SelectedIndices[0]);

            if (_selectedImage == null)
            {
                ClearInterface();
                return;
            }

            WidthLabel.Text = _selectedImage.Width.ToString();
            HeightLabel.Text = _selectedImage.Height.ToString();

            SyncOffsetBoxes(_selectedImage.X, _selectedImage.Y);

            Bitmap referenceImage = null;
            MLibraryV2.MImage referenceMImage = null;
            if (_referenceLibrary != null)
            {
                referenceMImage = _referenceLibrary.GetMImage(PreviewListView.SelectedIndices[0]);
                if (referenceMImage != null)
                {
                    referenceImage = referenceMImage.Image;
                }
            }

            Bitmap image = null;
            if (ViewMode == "Image")
                image = _selectedImage.Image;
            else
                image = _selectedImage.MaskImage;

            if (image == null)
            {
                ImageBox.Image = null;
                return;
            }

            Bitmap newImage = null;
            if (!ApplyOffsets)
            {
                newImage = new Bitmap(Math.Max(_referenceImage?.Width ?? 0, Math.Max(image.Width, referenceImage?.Width ?? 0)), Math.Max(_referenceImage?.Height ?? 0, Math.Max(image.Height, referenceImage?.Height ?? 0)));
                using (var g = Graphics.FromImage(newImage))
                {
                    if (_referenceImage != null)
                        g.DrawImage(_referenceImage, Point.Empty);
                    if (referenceImage != null)
                        g.DrawImage(referenceImage, Point.Empty);
                    g.DrawImage(image, Point.Empty);
                }
            }
            else
            {
                var maxWidth = Math.Max(image.Width, referenceImage?.Width ?? 0);
                var maxHeight = Math.Max(image.Height, referenceImage?.Height ?? 0);

                int offsetX = 0;
                int offsetY = 0;
                if (referenceImage != null)
                {
                    offsetX = -_selectedImage.X + referenceMImage.X;
                    offsetY = -_selectedImage.Y + referenceMImage.Y;
                }
                maxWidth += Math.Abs(offsetX);
                maxHeight += Math.Abs(offsetY);

                newImage = new Bitmap(maxWidth, maxHeight);
                using (var g = Graphics.FromImage(newImage))
                {
                    if (referenceImage != null)
                        g.DrawImage(referenceImage, new Point(offsetX > 0 ? offsetX : 0, offsetY > 0 ? offsetY : 0));
                    g.DrawImage(image, new Point(offsetX < 0 ? Math.Abs(offsetX) : 0, offsetY < 0 ? Math.Abs(offsetY) : 0));
                }

                if (_referenceImage != null)
                {
                    var newMaxWidth = Math.Max(_referenceImage.Width, newImage.Width + Math.Abs(_selectedImage.X));
                    var newMaxHeight = Math.Max(_referenceImage.Height, newImage.Height + Math.Abs(_selectedImage.Y));

                    var anotherNewBitmap = new Bitmap(newMaxWidth, newMaxHeight);
                    using (var g = Graphics.FromImage(anotherNewBitmap))
                    {
                        g.DrawImage(_referenceImage, new Point(_selectedImage.X < 0 ? Math.Abs(_selectedImage.X) : 0, _selectedImage.Y < 0 ? Math.Abs(_selectedImage.Y) : 0));
                        g.DrawImage(image, new Point(_selectedImage.X > 0 ? _selectedImage.X : 0, _selectedImage.Y > 0 ? _selectedImage.Y : 0));
                    }
                    newImage = anotherNewBitmap;
                }
            }

            ImageBox.Image = newImage;
            int globalOffsetX = _referenceImage != null ? 0 : referenceMImage?.X ?? _selectedImage.X;
            int globalOffsetY = _referenceImage != null ? 0 : referenceMImage?.Y ?? _selectedImage.Y;

            ImageBox.Location = ApplyOffsets ? new Point(100 + globalOffsetX, 100 + globalOffsetY) : Point.Empty;

            // Keep track of what image/s are selected.
            if (PreviewListView.SelectedIndices.Count > 1)
            {
                toolStripStatusLabel.ForeColor = Color.Red;
                toolStripStatusLabel.Text = "选择多个图像";
            }
            else
            {
                toolStripStatusLabel.ForeColor = SystemColors.ControlText;
                toolStripStatusLabel.Text = "选定的图像: " + string.Format("{0} / {1}",
                PreviewListView.SelectedIndices[0].ToString(),
                (PreviewListView.Items.Count - 1).ToString());
            }

            SafeSetValue(nudJump, PreviewListView.SelectedIndices[0]);
        }

        private void PreviewListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            int index;

            if (_indexList.TryGetValue(e.ItemIndex, out index))
            {
                e.Item = new ListViewItem { ImageIndex = index, Text = e.ItemIndex.ToString() };
                return;
            }

            _indexList.Add(e.ItemIndex, ImageList.Images.Count);
            ImageList.Images.Add(_library.GetPreview(e.ItemIndex));
            e.Item = new ListViewItem { ImageIndex = index, Text = e.ItemIndex.ToString() };
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            if (_library == null) return;
            if (_library.FileName == null) return;

            if (ImportImageDialog.ShowDialog() != DialogResult.OK) return;

            List<string> fileNames = new List<string>(ImportImageDialog.FileNames);

            //fileNames.Sort();
            toolStripProgressBar.Value = 0;
            toolStripProgressBar.Maximum = fileNames.Count;

            for (int i = 0; i < fileNames.Count; i++)
            {
                string fileName = fileNames[i];
                Bitmap image;

                try
                {
                    image = new Bitmap(fileName);
                }
                catch
                {
                    continue;
                }

                fileName = Path.Combine(Path.GetDirectoryName(fileName), "Placements", Path.GetFileNameWithoutExtension(fileName));
                fileName = Path.ChangeExtension(fileName, ".txt");

                short x = 0;
                short y = 0;

                if (File.Exists(fileName))
                {
                    string[] placements = File.ReadAllLines(fileName);

                    if (placements.Length > 0)
                        short.TryParse(placements[0], out x);
                    if (placements.Length > 1)
                        short.TryParse(placements[1], out y);
                }

                _library.AddImage(image, x, y, checkboxRemoveBlackOnImport.Checked);
                toolStripProgressBar.Value++;
                //image.Dispose();
            }

            PreviewListView.VirtualListSize = _library.Images.Count;
            toolStripProgressBar.Value = 0;
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SaveLibraryDialog.ShowDialog() != DialogResult.OK) return;

            if (_library != null) _library.Close();
            _library = CreateLibrary(SaveLibraryDialog.FileName);
            PreviewListView.VirtualListSize = 0;
            _library.Save();

            UpdateFrameGridView();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (OpenLibraryDialog.ShowDialog() != DialogResult.OK) return;

            _referenceLibrary = null;
            _referenceImage = null;

            // 对话框里选了「WeMade/Shanda 格式（打开时自动转换为 Lib）」这一项 → 走转换；
            // 选其它项（资源库 / WTL 库 / WeMade 库 …）→ 按原格式就地编辑，不产出 .Lib。
            bool forceConvert = (OpenLibraryDialog.FilterIndex == ConvertFolderFilterIndex);
            OpenLibrary(OpenLibraryDialog.FileName, false, forceConvert);
        }

        /// <summary>「打开」对话框中「打开时自动转换为 Lib」那一项在过滤列表里的序号（从 1 开始）。</summary>
        private const int ConvertFolderFilterIndex = 8;

        /// <summary>
        /// 「直接打开」：不生成 .Lib，按原格式在原文件里就地查看/编辑，保存（含另存）写回源文件
        /// 及其配套的索引/数据文件（.WIL+.wix、.WZL+.wzx、.MIZ+.mix、.WTL）。
        /// </summary>
        private void openDirectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (OpenDirectDialog.ShowDialog() != DialogResult.OK) return;

            _referenceLibrary = null;
            _referenceImage = null;
            OpenLibrary(OpenDirectDialog.FileName, true);
        }

        /// <summary>
        /// 资源库工厂：按扩展名 / 魔数选择具体实现。
        ///   .Elite → EliteLibrary（Elite Origins 客户端）
        ///   .ALib  → ALibLibrary （domino 客户端）
        ///   其它   → MLibraryV2 （Mir2 Library V2/V3，即 .Lib）
        /// 文件不存在时（“新建空库”）同样按扩展名选择；完全无法识别则回退 MLibraryV2。
        /// </summary>
        public static ILibrary CreateLibrary(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return null;

            string ext = Path.GetExtension(filename);
            if (ext != null && ext.Equals(".Elite", StringComparison.OrdinalIgnoreCase))
                return new EliteLibrary(filename);
            if (ext != null && ext.Equals(".ALib", StringComparison.OrdinalIgnoreCase))
                return new ALibLibrary(filename);
            if (ext != null && ext.Equals(".EuRe", StringComparison.OrdinalIgnoreCase))
                return new EuReLibrary(filename);

            // Mir3（传奇3）用 .MIX/.MIZ，扩展名与 Mir2 盛大 .MIZ 冲突 —— 必须按魔数嗅探
            if (IsMir3IndexedName(ext) && File.Exists(filename) && IsMir3File(filename))
                return new Mir3Library(filename);

            // 扩展名不可信时按魔数嗅探：偏移 4 处的 "FEIH"（Elite 独有；ALib 无魔数只能靠扩展名）
            if (File.Exists(filename))
            {
                try
                {
                    byte[] head = new byte[8];
                    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {
                        if (fs.Read(head, 0, 8) == 8 &&
                            head[4] == 0x46 && head[5] == 0x45 && head[6] == 0x49 && head[7] == 0x48)
                        {
                            return new EliteLibrary(filename);
                        }
                    }
                }
                catch
                {
                    // 嗅探失败（权限/占用等），回退到默认实现
                }
            }

            // WeMade / WTL 原格式直连（不转换）：只在对应扩展名下尝试，避免把其它格式误判进来
            if (File.Exists(filename))
            {
                string directExt = Path.GetExtension(filename).ToUpperInvariant();

                if (directExt == ".WIL" || directExt == ".WZL" || directExt == ".MIZ")
                {
                    if (WeMadeDirectLibrary.CanOpen(filename)) return new WeMadeDirectLibrary(filename);
                }
                else if (directExt == ".WTL")
                {
                    if (WtlDirectLibrary.CanOpen(filename)) return new WtlDirectLibrary(filename);
                }
            }

            // 回退 Mir2 Library V2/V3（.Lib）
            try
            {
                return new MLibraryV2(filename);
            }
            catch (Exception ex)
            {
                string headHex = "?";
                try
                {
                    byte[] h = new byte[8];
                    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {
                        int n = fs.Read(h, 0, 8);
                        headHex = BitConverter.ToString(h, 0, n).Replace("-", " ");
                    }
                }
                catch
                {
                    // 文件不可读时头信息留空
                }

                throw new InvalidDataException(
                    "无法识别的资源库格式：" + filename + Environment.NewLine +
                    "文件头 8 字节：" + headHex + Environment.NewLine + Environment.NewLine +
                    "本编辑器支持：.Lib（Mir2 Library V2/V3）、.Elite、.ALib、.EuRe、Mir3 .MIX/.MIZ。" + Environment.NewLine +
                    "WeMade / Shanda 的 .WIL / .WZL / .MIZ / .WTL 默认按原格式就地查看/编辑（保存写回原文件）；" + Environment.NewLine +
                    "需要 .Lib 副本时，在「打开」对话框里选「WeMade/Shanda 格式（打开时自动转换为 Lib）」，或用「功能 → 转换为LIB」。",
                    ex);
            }
        }

        private void OpenLibrary(string filename)
        {
            OpenLibrary(filename, false, false);
        }

        /// <summary>
        /// forceDirect = true 时不转换、不生成 .Lib，直接按原容器格式打开（保存写回原文件）。
        /// </summary>
        private void OpenLibrary(string filename, bool forceDirect)
        {
            OpenLibrary(filename, forceDirect, false);
        }

        /// <summary>
        /// forceConvert = true 时对 .WIL/.WZL/.MIZ/.WTL 强制走「转换为 Lib」的老路径
        /// （对应「打开」对话框里显式选择的那一项）。
        /// </summary>
        private void OpenLibrary(string filename, bool forceDirect, bool forceConvert)
        {
            if (_opening) return;
            _opening = true;

            try
            {
                OpenLibraryAsync(filename, forceDirect, forceConvert);
            }
            catch (Exception ex)
            {
                _opening = false;
                ShowOpenError(filename, ex);
            }
        }

        /// <summary>
        /// 载入/转换全部放到后台线程，避免大文件（几十 MB 的 WIL/Elite/ALib）在解码时把界面卡成"无响应"。
        /// 转换、解析、位图解码都在 Task.Run 里完成，只有刷新控件回到 UI 线程。
        /// </summary>
        private async void OpenLibraryAsync(string filename, bool forceDirect, bool forceConvert)
        {
            try
            {
                string ext = Path.GetExtension(filename);
                ext = ext == null ? string.Empty : ext.ToUpperInvariant();

                // Mir3 的 .MIZ 与 Mir2 盛大 .MIZ 扩展名相同：先按魔数排除 Mir3，再判断是否需走转换
                bool isMir3 = (ext == ".MIZ" || ext == ".MIX")
                              && File.Exists(filename) && IsMir3File(filename);

                bool isWemade = !forceDirect && !isMir3 &&
                                (ext == ".WIL" || ext == ".WZL" || ext == ".MIZ" || ext == ".WTL");

                // 直接打开：不转换，只做存在性检查
                if (forceDirect && !File.Exists(filename))
                {
                    MessageBox.Show("文件不存在：" + filename, "直接打开失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (isWemade)
                {
                    if (!File.Exists(filename))
                    {
                        MessageBox.Show("文件不存在：" + filename, "打开失败",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (ext != ".WTL" && !HasWemadeIndex(filename, ext))
                    {
                        MessageBox.Show(
                            "缺少配套索引文件 " + Path.GetFileName(Path.ChangeExtension(filename, null) + WemadeIndexExt(ext)) +
                            "，无法转换。" + Environment.NewLine +
                            "WeMade 格式的 .WIL/.WZL/.MIZ 必须与同名索引文件放在一起。",
                            "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                ShowBusy(true);

                KeyValuePair<string, ILibrary> loaded;
                try
                {
                    loaded = await Task.Run(() =>
                    {
                        string target = filename;

                        if (forceDirect)
                        {
                            // 不转换：直接按原容器格式打开，保存时写回源文件
                            ILibrary direct = CreateLibrary(filename);
                            if (!IsOriginalFormatLibrary(direct))
                            {
                                if (direct != null) direct.Close();
                                throw new InvalidDataException(
                                    "「" + Path.GetFileName(filename) + "」不支持直接打开（不转换）。" + Environment.NewLine +
                                    "可直接打开的原格式：WeMade .WIL / .WZL / .MIZ（需配套 .wix / .wzx / .mix）、.WTL、.EuRe。" + Environment.NewLine +
                                    "其它格式请用「文件 → 打开」并转换为 .Lib。");
                            }
                            return new KeyValuePair<string, ILibrary>(target, direct);
                        }

                        if (isWemade)
                        {
                            if (!forceConvert)
                            {
                                // 默认：WIL/WZL/MIZ/WTL 都按原格式就地打开编辑，不产出 .Lib。
                                // 只有直连不支持（缺索引、子类型不支持、内容损坏）时才退回「转换为 Lib」。
                                ILibrary direct = null;
                                try { direct = CreateLibrary(filename); }
                                catch { direct = null; }

                                if (direct != null && IsOriginalFormatLibrary(direct))
                                    return new KeyValuePair<string, ILibrary>(filename, direct);

                                if (direct != null) direct.Close();
                            }

                            ConvertWemadeToLib(filename, ext, ReportConvertProgress);

                            target = Path.ChangeExtension(filename, ".Lib");
                            if (!File.Exists(target))
                                throw new InvalidDataException("转换已执行，但未生成 " + target + "。");
                        }

                        return new KeyValuePair<string, ILibrary>(target, CreateLibrary(target));
                    });
                }
                catch (Exception ex)
                {
                    ShowOpenError(filename, ex);
                    return;
                }
                finally
                {
                    ShowBusy(false);
                }

                ClearInterface();
                ImageList.Images.Clear();
                PreviewListView.Items.Clear();
                _indexList.Clear();

                if (_library != null) _library.Close();
                _library = loaded.Value;

                PreviewListView.VirtualListSize = _library.Images.Count;

                if (PreviewListView.Items.Count > 0)
                    PreviewListView.RedrawItems(0, PreviewListView.Items.Count - 1, true);

                // Show .Lib path in application title.
                this.Text = loaded.Key;

                PreviewListView.SelectedIndices.Clear();

                if (PreviewListView.Items.Count > 0)
                    PreviewListView.Items[0].Selected = true;

                UpdateFrameGridView();
            }
            finally
            {
                _opening = false;
            }
        }

        /// <summary>
        /// 把当前选中帧显示到 ImageBox。
        /// 走 GetMImage 以便按需解码位图（直接读 Images[i].Image 会拿到未解码的 null）。
        /// </summary>
        private void ShowSelectedImageInBox()
        {
            if (_library == null || PreviewListView.SelectedIndices.Count == 0) return;

            MLibraryV2.MImage mi = _library.GetMImage(PreviewListView.SelectedIndices[0]);
            if (mi == null) return;

            ImageBox.Image = (ViewMode == "Image") ? mi.Image : mi.MaskImage;
        }

        private void ShowBusy(bool busy)
        {
            UseWaitCursor = busy;
            toolStripProgressBar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            toolStripProgressBar.Value = 0;
        }

        /// <summary>后台转换的进度回调（从工作线程调用，内部切回 UI 线程）。</summary>
        private void ReportConvertProgress(int done, int total)
        {
            if (IsDisposed || !IsHandleCreated) return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (toolStripProgressBar.Style != ProgressBarStyle.Continuous)
                        toolStripProgressBar.Style = ProgressBarStyle.Continuous;

                    if (toolStripProgressBar.Maximum != total)
                        toolStripProgressBar.Maximum = Math.Max(total, 1);

                    toolStripProgressBar.Value = Math.Max(0, Math.Min(done, toolStripProgressBar.Maximum));
                }));
            }
            catch
            {
                // 窗口已关闭等情况直接忽略
            }
        }

        /// <summary>扩展名是否属于 Mir3 可能的索引/数据名（.MIX / .MIZ）。</summary>
        private static bool IsMir3IndexedName(string ext)
        {
            return ext != null &&
                   (ext.Equals(".MIX", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".MIZ", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>按魔数判断文件是否为 Mir3 资源库（前 16 字节或前 5 字节签名）。</summary>
        private static bool IsMir3File(string filename)
        {
            try
            {
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] head = new byte[16];
                    int n = fs.Read(head, 0, head.Length);
                    if (n < 5) return false;
                    byte[] h = new byte[n];
                    Array.Copy(head, h, n);
                    return Mir3Container.IsMir3(h);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 是否为「按原容器格式就地保存」的库：保存/另存会写回源文件（或同名配套文件），不产出 .Lib。
        /// </summary>
        private static bool IsOriginalFormatLibrary(ILibrary library)
        {
            return library != null &&
                   (library is Mir3Library || library is WeMadeDirectLibrary || library is WtlDirectLibrary ||
                    library is EuReLibrary);
        }

        /// <summary>
        /// 原格式库另存时必须沿用的扩展名；返回 null 表示是普通 Lib 系列（可自由改扩展名）。
        /// </summary>
        private static string OriginalFormatSaveExt(ILibrary library)
        {
            if (library is Mir3Library) return ".MIZ";
            if (library is WtlDirectLibrary) return ".WTL";
            if (library is EuReLibrary) return ".EuRe";
            if (library is WeMadeDirectLibrary)
            {
                string e = Path.GetExtension(library.FileName ?? string.Empty);
                return string.IsNullOrEmpty(e) ? ".WIL" : e.ToUpperInvariant();
            }
            return null;
        }

        /// <summary>WeMade/Shanda 系列的配套索引扩展名。</summary>
        private static string WemadeIndexExt(string upperExt)        {
            if (upperExt == ".WIL") return ".wix";
            if (upperExt == ".WZL") return ".wzx";
            if (upperExt == ".MIZ") return ".mix";
            return string.Empty;
        }

        private static bool HasWemadeIndex(string filename, string upperExt)
        {
            string stem = Path.ChangeExtension(filename, null);
            string idx = WemadeIndexExt(upperExt);
            return File.Exists(stem + idx) || File.Exists(stem + idx.ToUpperInvariant());
        }

        /// <summary>把 .WIL/.WZL/.MIZ/.WTL 转换成同名 .Lib（耗时操作，只在后台线程调用）。</summary>
        private static void ConvertWemadeToLib(string filename, string upperExt, Action<int, int> onProgress)
        {
            if (upperExt == ".WTL")
            {
                WTLLibrary wtl = new WTLLibrary(filename);
                wtl.ToMLibrary(onProgress);
            }
            else
            {
                WeMadeLibrary wil = new WeMadeLibrary(filename);
                wil.ToMLibrary(onProgress);
            }
        }

        private static void ShowOpenError(string filename, Exception ex)
        {
            MessageBox.Show(
                "无法打开：" + filename + Environment.NewLine + Environment.NewLine +
                ex.GetType().Name + ": " + ex.Message,
                "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void OpenReferenceLibrary(string filename)
        {
            if (_referenceLibrary != null) _referenceLibrary.Close();

            try
            {
                _referenceLibrary = CreateLibrary(filename);
            }
            catch (Exception ex)
            {
                _referenceLibrary = null;
                ShowOpenError(filename, ex);
            }
        }

        private void OpenShadowLibraryAndImport(string filename)
        {
            if (_library == null) return;

            if (_shadowLibrary != null) _shadowLibrary.Close();

            try
            {
                _shadowLibrary = CreateLibrary(filename);
            }
            catch (Exception ex)
            {
                _shadowLibrary = null;
                ShowOpenError(filename, ex);
                return;
            }

            ImageList.Images.Clear();
            _indexList.Clear();

            for (int i = 0; i < _library.Images.Count; i++)
            {
                var mImage = _library.GetMImage(i);
                if (mImage == null || mImage.Image == null) continue;

                var shadowImage = _shadowLibrary.GetMImage(i);
                if (shadowImage == null || shadowImage.Image == null) continue;

                var offSetX = -mImage.X + shadowImage.X;
                var offSetY = -mImage.Y + shadowImage.Y;

                var maxWidth = Math.Max(mImage.Width, shadowImage.Width + Math.Abs(offSetX));
                var maxHeight = Math.Max(mImage.Height, shadowImage.Height + Math.Abs(offSetY));

                var newBitmap = new Bitmap(maxWidth, maxHeight);
                using (var g = Graphics.FromImage(newBitmap))
                {
                    g.DrawImage(mImage.Image, new Point(offSetX < 0 ? Math.Abs(offSetX) : 0, offSetY < 0 ? Math.Abs(offSetY) : 0));
                    g.DrawImage(shadowImage.Image, new Point(offSetX > 0 ? offSetX : 0, offSetY > 0 ? offSetY : 0));
                }

                _library.ReplaceImage(i, newBitmap, mImage.X, mImage.Y, checkboxRemoveBlackOnImport.Checked);
            }

            PreviewListView.VirtualListSize = _library.Images.Count;

            try
            {
                PreviewListView.RedrawItems(0, PreviewListView.Items.Count - 1, true);

                ShowSelectedImageInBox();
            }
            catch (Exception)
            {
                return;
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_library == null) return;

            UpdateFrameGridData();

            _library.Save();
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_library == null) return;

            // 原格式库（Mir3 / WeMade 直连 / WTL 直连）写回的是成对或同格式文件，
            // 另存必须沿用该格式的扩展名，否则会凭空生成一对扩展名不对的容器文件。
            string forcedExt = OriginalFormatSaveExt(_library);
            string oldFilter = SaveLibraryDialog.Filter;
            string oldTitle = SaveLibraryDialog.Title;

            try
            {
                if (forcedExt != null)
                {
                    SaveLibraryDialog.Filter = "原格式 (*" + forcedExt + ")|*" + forcedExt + "|所有文件 (*.*)|*.*";
                    SaveLibraryDialog.Title = "另存为（保持原格式）";
                }

                if (SaveLibraryDialog.ShowDialog() != DialogResult.OK) return;

                UpdateFrameGridData();

                string target = SaveLibraryDialog.FileName;
                if (forcedExt != null &&
                    !string.Equals(Path.GetExtension(target), forcedExt, StringComparison.OrdinalIgnoreCase))
                {
                    target = Path.ChangeExtension(target, forcedExt);
                }

                _library.FileName = target;
                _library.Save();
            }
            finally
            {
                SaveLibraryDialog.Filter = oldFilter;
                SaveLibraryDialog.Title = oldTitle;
            }
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (_library == null) return;
            if (_library.FileName == null) return;
            if (PreviewListView.SelectedIndices.Count == 0) return;

            if (MessageBox.Show("确定要删除所选图像？",
                "删除所选内容",
                MessageBoxButtons.YesNoCancel) != DialogResult.Yes) return;

            List<int> removeList = new List<int>();

            for (int i = 0; i < PreviewListView.SelectedIndices.Count; i++)
                removeList.Add(PreviewListView.SelectedIndices[i]);

            removeList.Sort();

            for (int i = removeList.Count - 1; i >= 0; i--)
                _library.RemoveImage(removeList[i]);

            ImageList.Images.Clear();
            _indexList.Clear();
            PreviewListView.VirtualListSize -= removeList.Count;
        }

        private void convertToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (OpenWeMadeDialog.ShowDialog() != DialogResult.OK) return;

            toolStripProgressBar.Maximum = OpenWeMadeDialog.FileNames.Length;
            toolStripProgressBar.Value = 0;

            try
            {
                Task.Factory.StartNew(() =>
                {
                    ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = 8 };
                    Parallel.For(0, OpenWeMadeDialog.FileNames.Length, options, i =>
                            {
                                var fileName = OpenWeMadeDialog.FileNames[i];
                                var ext = Path.GetExtension(fileName).ToUpper();
                                if (ext == ".WTL")
                                {
                                    WTLLibrary WTLlib = new WTLLibrary(fileName);
                                    WTLlib.ToMLibrary();
                                }
                                else if (ext == ".LIB")
                                {
                                    MLibraryV1 v1Lib = new MLibraryV1(fileName);
                                    v1Lib.ToMLibrary();
                                }
                                else
                                {
                                    WeMadeLibrary WILlib = new WeMadeLibrary(fileName);
                                    WILlib.ToMLibrary();
                                }
                                Invoke(new Action(() => { toolStripProgressBar.Value++; }));
                            });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            toolStripProgressBar.Value = 0;

            MessageBox.Show(string.Format("已成功转换 {0} {1}",
                (OpenWeMadeDialog.FileNames.Length).ToString(),
                (OpenWeMadeDialog.FileNames.Length > 1) ? "libraries" : "library"));
        }

        private void copyToToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (PreviewListView.SelectedIndices.Count == 0) return;
            if (SaveLibraryDialog.ShowDialog() != DialogResult.OK) return;

            ILibrary tempLibrary = CreateLibrary(SaveLibraryDialog.FileName);

            List<int> copyList = new List<int>();

            for (int i = 0; i < PreviewListView.SelectedIndices.Count; i++)
                copyList.Add(PreviewListView.SelectedIndices[i]);

            copyList.Sort();

            for (int i = 0; i < copyList.Count; i++)
            {
                MLibraryV2.MImage image = _library.GetMImage(copyList[i]);
                tempLibrary.AddImage(image.Image, image.MaskImage, image.X, image.Y, checkboxRemoveBlackOnImport.Checked);
            }

            tempLibrary.Save();
        }

        private void removeBlanksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定要删除空白图像？",
                "删除空白图像",
                MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            _library.RemoveBlanks();
            ImageList.Images.Clear();
            _indexList.Clear();
            PreviewListView.VirtualListSize = _library.Count;
        }

        private void countBlanksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenLibraryDialog.Multiselect = true;

            if (OpenLibraryDialog.ShowDialog() != DialogResult.OK)
            {
                OpenLibraryDialog.Multiselect = false;
                return;
            }

            OpenLibraryDialog.Multiselect = false;

            MLibraryV2.Load = false;

            int count = 0;

            for (int i = 0; i < OpenLibraryDialog.FileNames.Length; i++)
            {
                try
                {
                    ILibrary library = CreateLibrary(OpenLibraryDialog.FileNames[i]);

                    for (int x = 0; x < library.Count; x++)
                    {
                        if (library.Images[x].Length <= 8)
                            count++;
                    }

                    library.Close();
                }
                catch
                {
                    // 单个文件损坏不应中断整批统计
                }
            }

            MLibraryV2.Load = true;
            MessageBox.Show(count.ToString());
        }

        private void InsertImageButton_Click(object sender, EventArgs e)
        {
            if (_library == null) return;
            if (_library.FileName == null) return;
            if (PreviewListView.SelectedIndices.Count == 0) return;
            if (ImportImageDialog.ShowDialog() != DialogResult.OK) return;

            List<string> fileNames = new List<string>(ImportImageDialog.FileNames);

            //fileNames.Sort();

            int index = PreviewListView.SelectedIndices[0];

            toolStripProgressBar.Value = 0;
            toolStripProgressBar.Maximum = fileNames.Count;

            for (int i = fileNames.Count - 1; i >= 0; i--)
            {
                string fileName = fileNames[i];

                Bitmap image;

                try
                {
                    image = new Bitmap(fileName);
                }
                catch
                {
                    continue;
                }

                fileName = Path.Combine(Path.GetDirectoryName(fileName), "Placements", Path.GetFileNameWithoutExtension(fileName));
                fileName = Path.ChangeExtension(fileName, ".txt");

                short x = 0;
                short y = 0;

                if (File.Exists(fileName))
                {
                    string[] placements = File.ReadAllLines(fileName);

                    if (placements.Length > 0)
                        short.TryParse(placements[0], out x);
                    if (placements.Length > 1)
                        short.TryParse(placements[1], out y);
                }

                _library.InsertImage(index, image, x, y, checkboxRemoveBlackOnImport.Checked);

                toolStripProgressBar.Value++;
            }

            ImageList.Images.Clear();
            _indexList.Clear();
            PreviewListView.VirtualListSize = _library.Images.Count;
            toolStripProgressBar.Value = 0;
            _library.Save();
        }

        private void safeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定要删除空白图像？",
                "删除空白图像", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            _library.RemoveBlanks(true);
            ImageList.Images.Clear();
            _indexList.Clear();
            PreviewListView.VirtualListSize = _library.Count;
        }

        private const int HowDeepToScan = 6;

        public static void ProcessDir(string sourceDir, int recursionLvl, string outputDir)
        {
            if (recursionLvl <= HowDeepToScan)
            {
                // Process the list of files found in the directory.
                string[] fileEntries = Directory.GetFiles(sourceDir);
                foreach (string fileName in fileEntries)
                {
                    if (Directory.Exists(outputDir) != true) Directory.CreateDirectory(outputDir);
                    MLibraryV0 OldLibrary = new MLibraryV0(fileName);
                    MLibraryV2 NewLibrary = new MLibraryV2(outputDir + Path.GetFileName(fileName)) { Images = new List<MLibraryV2.MImage>(), IndexList = new List<int>(), Count = OldLibrary.Images.Count }; ;
                    for (int i = 0; i < OldLibrary.Images.Count; i++)
                        NewLibrary.Images.Add(null);
                    for (int j = 0; j < OldLibrary.Images.Count; j++)
                    {
                        MLibraryV0.MImage oldimage = OldLibrary.GetMImage(j);
                        NewLibrary.Images[j] = new MLibraryV2.MImage(oldimage.FBytes, oldimage.Width, oldimage.Height) { X = oldimage.X, Y = oldimage.Y };
                    }
                    NewLibrary.Save();
                    for (int i = 0; i < NewLibrary.Images.Count; i++)
                    {
                        if (NewLibrary.Images[i].Preview != null)
                            NewLibrary.Images[i].Preview.Dispose();
                        if (NewLibrary.Images[i].Image != null)
                            NewLibrary.Images[i].Image.Dispose();
                        if (NewLibrary.Images[i].MaskImage != null)
                            NewLibrary.Images[i].MaskImage.Dispose();
                    }
                    for (int i = 0; i < OldLibrary.Images.Count; i++)
                    {
                        if (OldLibrary.Images[i].Preview != null)
                            OldLibrary.Images[i].Preview.Dispose();
                        if (OldLibrary.Images[i].Image != null)
                            OldLibrary.Images[i].Image.Dispose();
                    }
                    NewLibrary.Images.Clear();
                    NewLibrary.IndexList.Clear();
                    OldLibrary.Images.Clear();
                    OldLibrary.IndexList.Clear();
                    NewLibrary.Close();
                    OldLibrary.Close();
                    NewLibrary = null;
                    OldLibrary = null;
                }

                // Recurse into subdirectories of this directory.
                string[] subdirEntries = Directory.GetDirectories(sourceDir);
                foreach (string subdir in subdirEntries)
                {
                    // Do not iterate through re-parse points.
                    if (Path.GetFileName(Path.GetFullPath(subdir).TrimEnd(Path.DirectorySeparatorChar)) == Path.GetFileName(Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar))) continue;
                    if ((File.GetAttributes(subdir) &
                         FileAttributes.ReparsePoint) !=
                             FileAttributes.ReparsePoint)
                        ProcessDir(subdir, recursionLvl + 1, outputDir + " \\" + Path.GetFileName(Path.GetFullPath(subdir).TrimEnd(Path.DirectorySeparatorChar)) + "\\");
                }
            }
        }

        // Export a single image.
        private void ExportButton_Click(object sender, EventArgs e)
        {
            if (_library == null || _library.FileName == null || PreviewListView.SelectedIndices.Count == 0)
                return;

            string _fileName = Path.GetFileName(OpenLibraryDialog.FileName);
            string _newName = _fileName.Remove(_fileName.IndexOf('.'));
            string _folder = Application.StartupPath + "\\Exported\\" + _newName + "\\";

            Bitmap blank = new Bitmap(1, 1);

            // Create the folder if it doesn't exist.
            (new FileInfo(_folder)).Directory.Create();

            ListView.SelectedIndexCollection _col = PreviewListView.SelectedIndices;

            toolStripProgressBar.Value = 0;
            toolStripProgressBar.Maximum = _col.Count;

            DialogResult result = MessageBox.Show("是保存为BMP格式、否保存PNG格式？或取消操作", "选择保存格式", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel)
            {
                toolStripProgressBar.Value = 0;
                return;
            }
            string fileExtension = (result == DialogResult.Yes) ? ".bmp" : ".png";
            ImageFormat imageFormat = (result == DialogResult.Yes) ? ImageFormat.Bmp : ImageFormat.Png;

            for (int i = _col[0]; i < (_col[0] + _col.Count); i++)
            {
                _exportImage = _library.GetMImage(i);
                if (_exportImage.Image == null)
                {
                    blank.Save(_folder + i.ToString() + fileExtension, imageFormat);
                }
                else
                {
                    _exportImage.Image.Save(_folder + i.ToString() + fileExtension, imageFormat);
                }

                toolStripProgressBar.Value++;

                if (!Directory.Exists(_folder + "/Placements/"))
                    Directory.CreateDirectory(_folder + "/Placements/");

                File.WriteAllLines(_folder + "/Placements/" + i.ToString() + ".txt", new string[] { _exportImage.X.ToString(), _exportImage.Y.ToString() });
            }

            toolStripProgressBar.Value = 0;
            MessageBox.Show("图像保存到 " + _folder + "...", "已完成", MessageBoxButtons.OK);
        }

        // Don't let the splitter go out of sight on resizing.
        private void LMain_Resize(object sender, EventArgs e)
        {
            if (splitContainer1.SplitterDistance <= this.Height - 150) return;
            if (this.Height - 150 > 0)
            {
                splitContainer1.SplitterDistance = this.Height - 150;
            }
        }

        // Resize the image(Zoom).
        private Image ImageBoxZoom(Image image, Size size)
        {
            _originalImage = _selectedImage.Image;
            Bitmap _bmp = new Bitmap(_originalImage, Convert.ToInt32(_originalImage.Width * size.Width), Convert.ToInt32(_originalImage.Height * size.Height));
            Graphics _gfx = Graphics.FromImage(_bmp);
            return _bmp;
        }

        // Zoom in and out.
        private void ZoomTrackBar_Scroll(object sender, EventArgs e)
        {
            if (ImageBox.Image == null)
            {
                ZoomTrackBar.Value = 1;
            }
            if (ZoomTrackBar.Value > 0)
            {
                try
                {
                    PreviewListView.Items[(int)nudJump.Value].EnsureVisible();

                    Bitmap _newBMP = new Bitmap(_selectedImage.Width * ZoomTrackBar.Value, _selectedImage.Height * ZoomTrackBar.Value);
                    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(_newBMP))
                    {
                        if (checkBoxPreventAntiAliasing.Checked == true)
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.CompositingMode = CompositingMode.SourceCopy;
                        }

                        if (checkBoxQuality.Checked == true)
                        {
                            g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        }

                        g.DrawImage(_selectedImage.Image, new Rectangle(0, 0, _newBMP.Width, _newBMP.Height));
                    }
                    ImageBox.Image = _newBMP;

                    toolStripStatusLabel.ForeColor = SystemColors.ControlText;
                    toolStripStatusLabel.Text = "选定的图像: " + string.Format("{0} / {1}",
                        PreviewListView.SelectedIndices[0].ToString(),
                        (PreviewListView.Items.Count - 1).ToString());
                }
                catch
                {
                    return;
                }
            }
        }

        // Swap the image panel background colour Black/White.
        private void pictureBox_Click(object sender, EventArgs e)
        {
            if (panel.BackColor == Color.Black)
            {
                panel.BackColor = Color.GhostWhite;
            }
            else
            {
                panel.BackColor = Color.Black;
            }
        }

        private void PreviewListView_VirtualItemsSelectionRangeChanged(object sender, ListViewVirtualItemsSelectionRangeChangedEventArgs e)
        {
            // Keep track of what image/s are selected.
            ListView.SelectedIndexCollection _col = PreviewListView.SelectedIndices;

            if (_col.Count > 1)
            {
                toolStripStatusLabel.ForeColor = Color.Red;
                toolStripStatusLabel.Text = "选择了多个图像";
            }
        }

        private void buttonReplace_Click(object sender, EventArgs e)
        {
            if (_library == null) return;
            if (_library.FileName == null) return;
            if (PreviewListView.SelectedIndices.Count == 0) return;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.ShowDialog();

            if (ofd.FileName == "") return;

            Bitmap newBmp = new Bitmap(ofd.FileName);

            ImageList.Images.Clear();
            _indexList.Clear();
            _library.ReplaceImage(PreviewListView.SelectedIndices[0], newBmp, 0, 0, checkboxRemoveBlackOnImport.Checked);
            PreviewListView.VirtualListSize = _library.Images.Count;

            try
            {
                PreviewListView.RedrawItems(0, PreviewListView.Items.Count - 1, true);

                ShowSelectedImageInBox();
            }
            catch (Exception)
            {
                return;
            }
        }

        private void previousImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (PreviewListView.Visible && PreviewListView.Items.Count > 0)
                {
                    int index = PreviewListView.SelectedIndices[0];
                    index = index - 1;
                    PreviewListView.SelectedIndices.Clear();
                    this.PreviewListView.Items[index].Selected = true;
                    PreviewListView.Items[index].EnsureVisible();

                    if (_selectedImage.Height == 1 && _selectedImage.Width == 1 && PreviewListView.SelectedIndices[0] != 0)
                    {
                        previousImageToolStripMenuItem_Click(null, null);
                    }
                }
            }
            catch (Exception)
            {
                PreviewListView.SelectedIndices.Clear();
                this.PreviewListView.Items[PreviewListView.Items.Count - 1].Selected = true;
            }
        }

        private void nextImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (PreviewListView.Visible && PreviewListView.Items.Count > 0)
                {
                    int index = PreviewListView.SelectedIndices[0];
                    index = index + 1;
                    PreviewListView.SelectedIndices.Clear();
                    this.PreviewListView.Items[index].Selected = true;
                    PreviewListView.Items[index].EnsureVisible();

                    if (_selectedImage.Height == 1 && _selectedImage.Width == 1 && PreviewListView.SelectedIndices[0] != 0)
                    {
                        nextImageToolStripMenuItem_Click(null, null);
                    }
                }
            }
            catch (Exception)
            {
                PreviewListView.SelectedIndices.Clear();
                this.PreviewListView.Items[0].Selected = true;
            }
        }

        // Move Left and Right through images.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!ImageTabActive) return false;

            if (keyData == Keys.Left)
            {
                previousImageToolStripMenuItem_Click(null, null);
                return true;
            }

            if (keyData == Keys.Right)
            {
                nextImageToolStripMenuItem_Click(null, null);
                return true;
            }

            if (keyData == Keys.Up) //Not 100% accurate but works for now.
            {
                double d = Math.Floor((double)(PreviewListView.Width / 67));
                int index = PreviewListView.SelectedIndices[0] - (int)d;

                PreviewListView.SelectedIndices.Clear();
                if (index < 0)
                    index = 0;

                this.PreviewListView.Items[index].Selected = true;

                return true;
            }

            if (keyData == Keys.Down) //Not 100% accurate but works for now.
            {
                double d = Math.Floor((double)(PreviewListView.Width / 67));
                int index = PreviewListView.SelectedIndices[0] + (int)d;

                PreviewListView.SelectedIndices.Clear();
                if (index > PreviewListView.Items.Count - 1)
                    index = PreviewListView.Items.Count - 1;

                this.PreviewListView.Items[index].Selected = true;

                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void buttonSkipNext_Click(object sender, EventArgs e)
        {
            nextImageToolStripMenuItem_Click(null, null);
        }

        private void buttonSkipPrevious_Click(object sender, EventArgs e)
        {
            previousImageToolStripMenuItem_Click(null, null);
        }

        private void checkBoxQuality_CheckedChanged(object sender, EventArgs e)
        {
            ZoomTrackBar_Scroll(null, null);
        }

        private void checkBoxPreventAntiAliasing_CheckedChanged(object sender, EventArgs e)
        {
            ZoomTrackBar_Scroll(null, null);
        }

        private void nudJump_ValueChanged(object sender, EventArgs e)
        {
            if (PreviewListView.Items.Count - 1 >= nudJump.Value)
            {
                PreviewListView.SelectedIndices.Clear();
                PreviewListView.Items[(int)nudJump.Value].Selected = true;
                PreviewListView.Items[(int)nudJump.Value].EnsureVisible();
            }
        }

        private void nudJump_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                //Enter key is down.
                if (PreviewListView.Items.Count - 1 >= nudJump.Value)
                {
                    PreviewListView.SelectedIndices.Clear();
                    PreviewListView.Items[(int)nudJump.Value].Selected = true;
                    PreviewListView.Items[(int)nudJump.Value].EnsureVisible();
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        #region Frames

        private void defaultMonsterFramesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _library.Frames.Clear();
            _library.Frames = new FrameSet(FrameSet.DefaultMonsterFrameSet);

            UpdateFrameGridView();
        }

        private void defaultNPCFramesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _library.Frames.Clear();
            _library.Frames = new FrameSet(FrameSet.DefaultNPCFrameSet);

            UpdateFrameGridView();
        }

        private void defaultPlayerFramesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _library.Frames.Clear();

            UpdateFrameGridView();
        }

        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (tabControl.SelectedIndex)
            {
                case 0: //Images
                    ImageTabActive = true;
                    MaskTabActive = false;
                    FrameTabActive = false;
                    ImageBox.Location = new Point(0, 0);
                    FrameAnimTimer.Stop();
                    break;
                case 1: //Masks
                    ImageTabActive = false;
                    MaskTabActive = true;
                    FrameTabActive = false;
                    ImageBox.Location = new Point(0, 0);
                    FrameAnimTimer.Stop();
                    break;
                case 2: //Frames
                    ImageTabActive = false;
                    MaskTabActive = false;
                    FrameTabActive = true;
                    break;
            }
        }

        private void autofillNpcFramesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FolderLibraryDialog.ShowDialog() != DialogResult.OK) return;

            var path = FolderLibraryDialog.SelectedPath;

            var files = Directory.GetFiles(path, "*.Lib");

            if (MessageBox.Show($"确定要用 {files.Count()} Libs及其匹配的框架集",
                "自动填充 Libs.",
                MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            foreach (var file in files)
            {
                if (_library != null) _library.Close();
                _library = new MLibraryV2(file);

                // Show .Lib path in application title.
                this.Text = file;

                var name = Path.GetFileNameWithoutExtension(file);

                if (!int.TryParse(name, out int imageNumber)) continue;

                _library.Frames = GetFrameSetByImage((Monster)imageNumber);
                _library.Save();
            }
        }

        private void frameGridView_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            frameGridView.Rows[e.RowIndex].ErrorText = "";

            if (frameGridView.Rows[e.RowIndex].IsNewRow) { return; }

            if (e.ColumnIndex >= 1 && e.ColumnIndex <= 8)
            {
                if (!int.TryParse(e.FormattedValue.ToString(), out _))
                {
                    e.Cancel = true;
                    frameGridView.Rows[e.RowIndex].ErrorText = "该值必须是整数";
                }
            }
        }

        private void frameGridView_DefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            e.Row.Cells["FrameStart"].Value = 0;
            e.Row.Cells["FrameCount"].Value = 0;
            e.Row.Cells["FrameSkip"].Value = 0;
            e.Row.Cells["FrameInterval"].Value = 0;
            e.Row.Cells["FrameEffectStart"].Value = 0;
            e.Row.Cells["FrameEffectCount"].Value = 0;
            e.Row.Cells["FrameEffectSkip"].Value = 0;
            e.Row.Cells["FrameEffectInterval"].Value = 0;
            e.Row.Cells["FrameReverse"].Value = false;
            e.Row.Cells["FrameBlend"].Value = false;
        }


        private void UpdateFrameGridView()
        {
            frameGridView.Rows.Clear();

            foreach (var action in _library.Frames.Keys)
            {
                var frame = _library.Frames[action];

                int rowIndex = frameGridView.Rows.Add();

                var row = frameGridView.Rows[rowIndex];

                row.Cells["FrameAction"].Value = action;
                row.Cells["FrameStart"].Value = frame.Start;
                row.Cells["FrameCount"].Value = frame.Count;
                row.Cells["FrameSkip"].Value = frame.Skip;
                row.Cells["FrameInterval"].Value = frame.Interval;
                row.Cells["FrameEffectStart"].Value = frame.EffectStart;
                row.Cells["FrameEffectCount"].Value = frame.EffectCount;
                row.Cells["FrameEffectSkip"].Value = frame.EffectSkip;
                row.Cells["FrameEffectInterval"].Value = frame.EffectInterval;
                row.Cells["FrameReverse"].Value = frame.Reverse;
                row.Cells["FrameBlend"].Value = frame.Blend;
            }
        }

        private void UpdateFrameGridData()
        {
            if (_library == null) return;

            _library.Frames.Clear();

            foreach (DataGridViewRow row in frameGridView.Rows)
            {
                var cells = row.Cells;

                if (cells["FrameAction"].Value == null) continue;

                var action = (MirAction)row.Cells["FrameAction"].Value;

                if (_library.Frames.ContainsKey(action))
                {
                    MessageBox.Show(string.Format($"操作的 '{action}' 存在多次，因此将不会保存"));
                    continue;
                }

                var frame = new Frame(cells["FrameStart"].Value.ValueOrDefault<int>(),
                                        cells["FrameCount"].Value.ValueOrDefault<int>(),
                                        cells["FrameSkip"].Value.ValueOrDefault<int>(),
                                        cells["FrameInterval"].Value.ValueOrDefault<int>(),
                                        cells["FrameEffectStart"].Value.ValueOrDefault<int>(),
                                        cells["FrameEffectCount"].Value.ValueOrDefault<int>(),
                                        cells["FrameEffectSkip"].Value.ValueOrDefault<int>(),
                                        cells["FrameEffectInterval"].Value.ValueOrDefault<int>())
                {
                    Reverse = cells["FrameReverse"].Value.ValueOrDefault<bool>(),
                    Blend = cells["FrameBlend"].Value.ValueOrDefault<bool>()
                };

                _library.Frames.Add(action, frame);
            }
        }

        private void frameGridView_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
            var row = frameGridView.Rows[e.RowIndex];

            if (row == null) return;

            var cells = row.Cells;

            if (cells["FrameAction"].Value == null) return;

            var frame = new Frame(cells["FrameStart"].Value.ValueOrDefault<int>(),
                                        cells["FrameCount"].Value.ValueOrDefault<int>(),
                                        cells["FrameSkip"].Value.ValueOrDefault<int>(),
                                        cells["FrameInterval"].Value.ValueOrDefault<int>(),
                                        cells["FrameEffectStart"].Value.ValueOrDefault<int>(),
                                        cells["FrameEffectCount"].Value.ValueOrDefault<int>(),
                                        cells["FrameEffectSkip"].Value.ValueOrDefault<int>(),
                                        cells["FrameEffectInterval"].Value.ValueOrDefault<int>())
            {
                Reverse = cells["FrameReverse"].Value.ValueOrDefault<bool>(),
                Blend = cells["FrameBlend"].Value.ValueOrDefault<bool>()
            };

            if (frame.Interval == 0) return;

            _drawFrame = frame;

            FrameAnimTimer.Interval = frame.Interval;
            FrameAnimTimer.Start();
        }

        private Frame _drawFrame;
        private int _currentFrame;
        private MirDirection _currentDirection;

        private void FrameAnimTimer_Tick(object sender, EventArgs e)
        {
            if (_drawFrame == null) return;

            try
            {
                if (_currentFrame >= _drawFrame.Count - 1)
                {
                    _currentFrame = 0;
                    MirDirection[] arr = (MirDirection[])Enum.GetValues(typeof(MirDirection));
                    int j = Array.IndexOf<MirDirection>(arr, _currentDirection) + 1;
                    _currentDirection = (arr.Length == j) ? arr[0] : arr[j];
                }

                var drawFrame = _drawFrame.Start + (_drawFrame.OffSet * (byte)_currentDirection) + _currentFrame;

                _selectedImage = _library.GetMImage(drawFrame);

                if (ViewMode == "Image")
                {
                    ImageBox.Location = new Point(250 + _selectedImage.X, 250 + _selectedImage.Y);
                    ImageBox.Image = _selectedImage.Image;
                }
                else
                {
                    ImageBox.Location = new Point(250 + _selectedImage.X, 250 + _selectedImage.Y);
                    ImageBox.Image = _selectedImage.MaskImage;
                }

                _currentFrame++;
            }
            catch { }
        }

        private void PreviewListViewMask_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void RButtonViewMode_CheckedChanged(object sender, EventArgs e)
        {
            if (RButtonImage.Checked)
            {
                ViewMode = "Image";
            }
            else if (RButtonOverlay.Checked)
            {
                ViewMode = "Overlay";
            }

            if (_selectedImage != null)
            {
                if (ViewMode == "Image")
                {
                    ImageBox.Image = _selectedImage.Image;
                }
                else
                {
                    ImageBox.Image = _selectedImage.MaskImage;
                }
            }
        }

        /// <summary>
        /// List of monsters and matching frames
        /// Method MUST be edited before use. The existing code is only here as an example.
        /// READ THE COMMENTS WITHIN THIS METHOD BEFORE USE
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        private FrameSet GetFrameSetByImage(Monster image)
        {
            //REMOVE THE BELOW EXCEPTION ONCE THE DESIRED CODE HAS BEEN ADDED          
            throw new NotImplementedException("必须先更新 'GetFrameSetByImage' 后才能使用此函数");

            //UNCOMMENT THE CODE BELOW, IT SERVES AS AN EXAMPLE OF HOW TO MATCH IMAGES UP TO THE CORRECT FRAMES
            //List<FrameSet> FrameList = new List<FrameSet>();
            //FrameSet frame;

            ////ADD LIST OF FRAMES (CAN BE COPIED FROM THE CLIENTS FRAME.CS)
            //FrameList.Add(frame = new FrameSet());
            //frame.Add(MirAction.Standing, new Frame(0, 4, 0, 450));
            //frame.Add(MirAction.Harvest, new Frame(12, 10, 0, 200));

            ////ADD SWITCH OF IMAGE TO CORRECT FRAME (CAN BE COPIED FROM THE MONSTEROBJECT.CS FRAME LIST)
            //FrameSet matchingFrame = new FrameSet();
            //switch (image)
            //{
            //    case Monster.Hen:
            //        matchingFrame = FrameList[0];
            //        break;
            //}

            //return matchingFrame;
        }
        #endregion

        private void openReferenceFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (OpenLibraryDialog.ShowDialog() != DialogResult.OK) return;

            OpenReferenceLibrary(OpenLibraryDialog.FileName);
            PreviewListView.Invoke(new EventHandler(PreviewListView_SelectedIndexChanged), EventArgs.Empty);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            PreviewListView.Invoke(new EventHandler(PreviewListView_SelectedIndexChanged), EventArgs.Empty);
        }

        private void importShadowsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (OpenLibraryDialog.ShowDialog() != DialogResult.OK) return;
            OpenShadowLibraryAndImport(OpenLibraryDialog.FileName);
        }

        private void openReferenceImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_library == null) return;
            if (_library.FileName == null) return;

            if (ImportImageDialog.ShowDialog() != DialogResult.OK) return;

            string fileName = ImportImageDialog.FileNames[0];
            _referenceImage = new Bitmap(fileName);
        }

        private void BulkButton_Click(object sender, EventArgs e)
        {
            // Create an instance of the InputDialog class
            InputDialog dlg = new InputDialog();

            // Show the dialog as a modal dialog
            DialogResult result = dlg.ShowDialog();

            // If the user clicked the Ok button, retrieve the values entered by the user
            if (result == DialogResult.OK)
            {
                for (int i = 0; i < PreviewListView.SelectedIndices.Count; i++)
                {
                    MLibraryV2.MImage image = _library.GetMImage(PreviewListView.SelectedIndices[i]);
                    if (image == null || image.Image == null) continue;
                    image.X += (short)dlg.Value1;
                    image.Y += (short)dlg.Value2;
                }
            }
        }

        private void numericUpDownX_ValueChanged(object sender, EventArgs e)
        {
            if (_syncingOffsetBoxes) return;

            for (int i = 0; i < PreviewListView.SelectedIndices.Count; i++)
            {
                MLibraryV2.MImage image = _library.GetMImage(PreviewListView.SelectedIndices[i]);
                if (image == null) continue;
                image.X = (short)ClampToBox(numericUpDownX, numericUpDownX.Value);
            }
            PreviewListView.Invoke(new EventHandler(PreviewListView_SelectedIndexChanged), EventArgs.Empty);
        }

        private void numericUpDownY_ValueChanged(object sender, EventArgs e)
        {
            if (_syncingOffsetBoxes) return;

            for (int i = 0; i < PreviewListView.SelectedIndices.Count; i++)
            {
                MLibraryV2.MImage image = _library.GetMImage(PreviewListView.SelectedIndices[i]);
                if (image == null) continue;
                image.Y = (short)ClampToBox(numericUpDownY, numericUpDownY.Value);
            }
            PreviewListView.Invoke(new EventHandler(PreviewListView_SelectedIndexChanged), EventArgs.Empty);
        }

        private void frameGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
    }
}