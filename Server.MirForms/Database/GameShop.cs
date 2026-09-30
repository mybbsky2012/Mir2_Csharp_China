using System.Text;
using Server.MirEnvir;

namespace Server
{
    public partial class GameShop : Form
    {

        private List<GameShopItem> SelectedItems;

        public Envir Envir => SMain.EditEnvir;

        public GameShop()
        {
            InitializeComponent();

            LoadGameShopItems();
        }

        private void GameShop_Load(object sender, EventArgs e)
        {
            UpdateInterface();
        }

        private void GameShop_FormClosed(object sender, FormClosedEventArgs e)
        {
            Envir.SaveDB();
        }

        public class ListBoxItem
        {
            public string DisplayMember { get; set; }
            public object ValueMember { get; set; }

            public override string ToString()
            {
                return DisplayMember;
            }
        }

        private void LoadGameShopItems()
        {


            ClassFilter_lb.Items.Clear();
            CategoryFilter_lb.Items.Clear();
            GameShopListBox.Items.Clear();

            ClassFilter_lb.Items.Add("All Classes");
            CategoryFilter_lb.Items.Add("All Categories");


            for (int i = 0; i < SMain.EditEnvir.GameShopList.Count; i++)
            {
                if (!ClassFilter_lb.Items.Contains(SMain.EditEnvir.GameShopList[i].Class)) ClassFilter_lb.Items.Add(SMain.EditEnvir.GameShopList[i].Class);
                if (!CategoryFilter_lb.Items.Contains(SMain.EditEnvir.GameShopList[i].Category)) CategoryFilter_lb.Items.Add(SMain.EditEnvir.GameShopList[i].Category);

                GameShopListBox.Items.Add(SMain.EditEnvir.GameShopList[i]);
            }

            ClassFilter_lb.Text = "All Classes";
            CategoryFilter_lb.Text = "All Categories";
            SectionFilter_lb.Text = "All Items";
        }

        private void UpdateGameShopList()
        {

            GameShopListBox.Items.Clear();
            for (int i = 0; i < SMain.EditEnvir.GameShopList.Count; i++)
            {
                if (ClassFilter_lb.Text == "All Classes" || SMain.EditEnvir.GameShopList[i].Class == ClassFilter_lb.Text)
                    if (SectionFilter_lb.Text == "All Items" || SMain.EditEnvir.GameShopList[i].TopItem && SectionFilter_lb.Text == "Top Items" || SMain.EditEnvir.GameShopList[i].Deal && SectionFilter_lb.Text == "Sale Items" || SMain.EditEnvir.GameShopList[i].Date > Envir.Now.AddDays(-7) && SectionFilter_lb.Text == "New Items")
                        if (CategoryFilter_lb.Text == "All Categories" || SMain.EditEnvir.GameShopList[i].Category == CategoryFilter_lb.Text)
                            GameShopListBox.Items.Add(SMain.EditEnvir.GameShopList[i]);
            }
        }

        private void GameShopListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateInterface();
        }

        public void UpdateInterface(bool refreshList = false)
        {
            SelectedItems = GameShopListBox.SelectedItems.Cast<GameShopItem>().ToList();


            if (SelectedItems.Count == 0)
            {
                GoldPrice_textbox.Text = String.Empty;
                GPPrice_textbox.Text = String.Empty;
                Stock_textbox.Text = String.Empty;
                Individual_checkbox.Checked = false;
                Class_combo.Text = "All";
                Category_textbox.Text = "";
                TopItem_checkbox.Checked = false;
                DealofDay_checkbox.Checked = false;
                CredxGold_textbox.Text = Settings.CredxGold.ToString();
                ItemDetails_gb.Visible = false;
                TotalSold_label.Text = "0";
                LeftinStock_label.Text = "";
                Count_textbox.Text = String.Empty;
                CreditOnlyBox.Checked = false;
                GoldOnlyBox.Checked = false;
                return;
            }

            ItemDetails_gb.Visible = true;

            GoldPrice_textbox.Text = SelectedItems[0].GoldPrice.ToString();
            GPPrice_textbox.Text = SelectedItems[0].CreditPrice.ToString();
            Stock_textbox.Text = SelectedItems[0].Stock.ToString();
            Individual_checkbox.Checked = SelectedItems[0].iStock;
            Class_combo.Text = SelectedItems[0].Class;
            Category_textbox.Text = SelectedItems[0].Category;
            TopItem_checkbox.Checked = SelectedItems[0].TopItem;
            DealofDay_checkbox.Checked = SelectedItems[0].Deal;
            Count_textbox.Text = SelectedItems[0].Count.ToString();
            CreditOnlyBox.Checked = SelectedItems[0].CanBuyCredit;
            GoldOnlyBox.Checked = SelectedItems[0].CanBuyGold;
            GetStats();

        }

        private void GetStats()
        {
            int purchased;

            SMain.Envir.GameshopLog.TryGetValue(SelectedItems[0].GIndex, out purchased);
            TotalSold_label.Text = purchased.ToString();

            if (!Individual_checkbox.Checked && SelectedItems[0].Stock != 0)
            {
                if (SelectedItems[0].Stock - purchased >= 0)
                    LeftinStock_label.Text = (SelectedItems[0].Stock - purchased).ToString();
                else
                    LeftinStock_label.Text = "";
            }
            else if (SelectedItems[0].Stock == 0)
            {
                LeftinStock_label.Text = "无限的";
            }
            else if (Individual_checkbox.Checked)
            {
                LeftinStock_label.Text = "不能单独结算";
            }
        }

        private void GoldPrice_textbox_TextChanged(object sender, EventArgs e)
        {

            uint temp;

            if (!uint.TryParse(GoldPrice_textbox.Text, out temp))
            {
                GoldPrice_textbox.BackColor = Color.Red;
                return;
            }

            GoldPrice_textbox.BackColor = SystemColors.Window;

            for (int i = 0; i < SelectedItems.Count; i++)
                SelectedItems[i].GoldPrice = temp;
        }

        private void GPPrice_textbox_TextChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;

            uint temp;

            if (!uint.TryParse(ActiveControl.Text, out temp))
            {
                ActiveControl.BackColor = Color.Red;
                return;
            }

            ActiveControl.BackColor = SystemColors.Window;

            for (int i = 0; i < SelectedItems.Count; i++)
                SelectedItems[i].CreditPrice = temp;

            if (ActiveControl.Text != "") GoldPrice_textbox.Text = (temp * Settings.CredxGold).ToString();
        }

        private void Class_combo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;
            string temp = ActiveControl.Text;

            for (int i = 0; i < SelectedItems.Count; i++)
                SelectedItems[i].Class = temp;
        }

        private void TopItem_checkbox_CheckedChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;

            for (int i = 0; i < SelectedItems.Count; i++)
                SelectedItems[i].TopItem = TopItem_checkbox.Checked;
        }

        private void Remove_button_Click(object sender, EventArgs e)
        {
            if (SelectedItems.Count == 0) return;

            if (MessageBox.Show("确定要删除选定物品？", "删除商城物品", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            for (int i = 0; i < SelectedItems.Count; i++) Envir.Remove(SelectedItems[i]);

            LoadGameShopItems();
            UpdateInterface();
        }

        private void DealofDay_checkbox_CheckedChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;

            for (int i = 0; i < SelectedItems.Count; i++)
                SelectedItems[i].Deal = DealofDay_checkbox.Checked;
        }

        private void Category_textbox_TextChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;
            string temp = ActiveControl.Text;

            for (int i = 0; i < SelectedItems.Count; i++)
                SelectedItems[i].Category = temp;
        }

        private void Stock_textbox_TextChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;

            int temp;

            if (!int.TryParse(ActiveControl.Text, out temp))
            {
                ActiveControl.BackColor = Color.Red;
                return;
            }

            ActiveControl.BackColor = SystemColors.Window;

            for (int i = 0; i < SelectedItems.Count; i++)
                SelectedItems[i].Stock = temp;

            GetStats();
        }

        private void Individual_checkbox_CheckedChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;

            for (int i = 0; i < SelectedItems.Count; i++)
                SelectedItems[i].iStock = Individual_checkbox.Checked;

        }

        private void CredxGold_textbox_TextChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;

            short temp;

            if (!short.TryParse(ActiveControl.Text, out temp))
            {
                ActiveControl.BackColor = Color.Red;
                return;
            }

            ActiveControl.BackColor = SystemColors.Window;
            Settings.CredxGold = temp;
        }

        private void Count_textbox_TextChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender) return;

            ushort temp;

            if (!ushort.TryParse(ActiveControl.Text, out temp) || temp > 999)
            {
                ActiveControl.BackColor = Color.Red;
                return;
            }

            if (temp < 1)
            {
                temp = 1;
                ActiveControl.Text = "1";
            }
            else if (temp > SelectedItems[0].Info.StackSize)
            {
                temp = SelectedItems[0].Info.StackSize;
                ActiveControl.Text = SelectedItems[0].Info.StackSize.ToString();
            }

            ActiveControl.BackColor = SystemColors.Window;
            SelectedItems[0].Count = temp;
        }

        private void ClassFilter_lb_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateGameShopList();
        }

        private void SectionFilter_lb_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateGameShopList();
        }

        private void CategoryFilter_lb_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateGameShopList();
        }

        private void ResetFilter_button_Click(object sender, EventArgs e)
        {
            ClassFilter_lb.Text = "All Classes";
            CategoryFilter_lb.Text = "All Categories";
            SectionFilter_lb.Text = "All Items";
            UpdateGameShopList();

        }

        private void ServerLog_button_Click(object sender, EventArgs e)
        {
            if (SMain.Envir.Running)
            {
                if (MessageBox.Show("重置购买日志无法恢复，库存将设置为默认值，操作立即生效。", "删除日志", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                SMain.Envir.ClearGameshopLog();
            }
            else
            {
                if (MessageBox.Show("重置购买日志无法恢复，库存将设置为默认值，启动服务器时生效", "删除日志", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                SMain.Envir.ResetGS = true;
            }
        }
        private void GoldOnlyBox_CheckedChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender)
                return;

            for (int i = 0; i < SelectedItems.Count; i++)
                SelectedItems[i].CanBuyGold = GoldOnlyBox.Checked;
        }

        private void CreditOnly_CheckedChanged(object sender, EventArgs e)
        {
            if (ActiveControl != sender)
                return;

            for (int i = 0; i < SelectedItems.Count; i++)
                SelectedItems[i].CanBuyCredit = CreditOnlyBox.Checked;
        }

        // ============ 导出 / 导入 CSV ============
        private void ExportCsv_button_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "CSV (*.csv)|*.csv";
            sfd.FileName = "商城物品.csv";
            if (sfd.ShowDialog() != DialogResult.OK) return;

            var sb = new StringBuilder();
            sb.AppendLine("物品名,金币价格,信用币价格,数量,职业,类别,库存,物品限制,热销,推荐,金币购买,信用币购买");

            foreach (GameShopItem item in SMain.EditEnvir.GameShopList)
            {
                string name = item.Info != null ? item.Info.Name : item.ItemIndex.ToString();
                string line = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}",
                    CsvEscape(name),
                    item.GoldPrice,
                    item.CreditPrice,
                    item.Count,
                    CsvEscape(item.Class),
                    CsvEscape(item.Category),
                    item.Stock,
                    item.iStock,
                    item.Deal,
                    item.TopItem,
                    item.CanBuyGold,
                    item.CanBuyCredit);
                sb.AppendLine(line);
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("商城物品已导出: " + sfd.FileName, "导出完成");
        }

        private void ImportCsv_button_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "CSV (*.csv)|*.csv";
            if (ofd.ShowDialog() != DialogResult.OK) return;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(ofd.FileName, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取文件失败: " + ex.Message, "导入错误");
                return;
            }

            if (lines.Length < 2)
            {
                MessageBox.Show("没有要导入的数据", "导入");
                return;
            }

            int updated = 0, added = 0;
            var envir = SMain.EditEnvir;

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] cells = CsvSplit(line);
                if (cells.Length < 12) continue;

                string itemName = cells[0].Trim();
                ItemInfo info = envir.GetItemInfo(itemName);
                if (info == null) continue;

                GameShopItem existing = null;
                for (int j = 0; j < envir.GameShopList.Count; j++)
                {
                    if (envir.GameShopList[j].ItemIndex == info.Index)
                    {
                        existing = envir.GameShopList[j];
                        break;
                    }
                }

                uint.TryParse(cells[1], out uint goldPrice);
                uint.TryParse(cells[2], out uint creditPrice);
                ushort.TryParse(cells[3], out ushort count);
                int.TryParse(cells[5], out int stock);
                bool.TryParse(cells[7], out bool iStock);
                bool.TryParse(cells[8], out bool deal);
                bool.TryParse(cells[9], out bool topItem);
                bool.TryParse(cells[10], out bool canBuyGold);
                bool.TryParse(cells[11], out bool canBuyCredit);

                if (existing == null)
                {
                    existing = new GameShopItem
                    {
                        ItemIndex = info.Index,
                        Info = info,
                        GoldPrice = goldPrice,
                        CreditPrice = creditPrice,
                        Count = count < 1 ? (ushort)1 : count,
                        Class = cells[4].Trim(),
                        Category = cells[5].Trim(),
                        Stock = stock,
                        iStock = iStock,
                        Deal = deal,
                        TopItem = topItem,
                        Date = envir.Now,
                        CanBuyGold = canBuyGold,
                        CanBuyCredit = canBuyCredit
                    };
                    existing.GIndex = ++envir.GameshopIndex;
                    envir.GameShopList.Add(existing);
                    added++;
                }
                else
                {
                    existing.GoldPrice = goldPrice;
                    existing.CreditPrice = creditPrice;
                    existing.Count = count < 1 ? (ushort)1 : count;
                    existing.Class = cells[4].Trim();
                    existing.Category = cells[5].Trim();
                    existing.Stock = stock;
                    existing.iStock = iStock;
                    existing.Deal = deal;
                    existing.TopItem = topItem;
                    existing.CanBuyGold = canBuyGold;
                    existing.CanBuyCredit = canBuyCredit;
                    updated++;
                }
            }

            LoadGameShopItems();
            UpdateInterface();
            MessageBox.Show(string.Format("导入完成: 更新 {0} 条, 新增 {1} 条", updated, added), "导入完成");
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        private static string[] CsvSplit(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }


    }
}
