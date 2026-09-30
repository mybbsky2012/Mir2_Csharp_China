using System.Text;
namespace Server.Database
{
    public partial class RecipeInfoForm : Form
    {
        private string currentFilePath;
        private bool isModified = false;
        private string originalCraftAmount;
        private string originalChance;
        private string originalGold;
        private string originalTool;
        private string originalIngredientName1;
        private string originalIngredientAmount1;
        private string Quality1;
        private string originalIngredientName2;
        private string originalIngredientAmount2;
        private string Quality2;
        private string originalIngredientName3;
        private string originalIngredientAmount3;
        private string Quality3;
        private string originalIngredientName4;
        private string originalIngredientAmount4;
        private string Quality4;
        public RecipeInfoForm()
        {
            InitializeComponent();
            this.Load += RecipeInfoForm_Load;
            RecipeList.SelectedIndexChanged += RecipeList_SelectedIndexChanged;

            #region Text Box Changed
            CraftAmountTextBox.TextChanged += TextBox_TextChanged;
            ChanceTextBox.TextChanged += TextBox_TextChanged;
            GoldTextBox.TextChanged += TextBox_TextChanged;
            ToolTextBox.TextChanged += TextBox_TextChanged;
            IngredientName1TextBox.TextChanged += TextBox_TextChanged;
            IngredientAmount1TextBox.TextChanged += TextBox_TextChanged;
            Quality1TextBox.TextChanged += TextBox_TextChanged;
            IngredientName2TextBox.TextChanged += TextBox_TextChanged;
            IngredientAmount2TextBox.TextChanged += TextBox_TextChanged;
            Quality3TextBox.TextChanged += TextBox_TextChanged;
            IngredientName3TextBox.TextChanged += TextBox_TextChanged;
            IngredientAmount3TextBox.TextChanged += TextBox_TextChanged;
            IngredientName4TextBox.TextChanged += TextBox_TextChanged;
            IngredientAmount4TextBox.TextChanged += TextBox_TextChanged;
            Quality4TextBox.TextChanged += TextBox_TextChanged;
            #endregion
        }
        #region Form Load
        private void RecipeInfoForm_Load(object sender, EventArgs e)
        {
            // Define the path to the directory containing recipes
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string directoryPath = Path.Combine(currentDirectory, "Envir", "Recipe");

            // Ensure the directory exists
            if (Directory.Exists(directoryPath))
            {
                // Get all recipe files from the directory
                string[] recipeFiles = Directory.GetFiles(directoryPath, "*.txt");

                // Clear existing items from ListBox
                RecipeList.Items.Clear();

                // Populate the ListBox with recipe file names with index
                for (int i = 0; i < recipeFiles.Length; i++)
                {
                    // Get the file name without the path and without the extension
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(recipeFiles[i]);

                    // Format the item with an index number
                    string listItem = $"{i + 1}. {fileNameWithoutExtension}";

                    // Add the formatted item to the ListBox
                    RecipeList.Items.Add(listItem);
                }
            }
            else
            {
                MessageBox.Show("配方目录不存在", "目录错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Recipe Selected Index Changed
        private void RecipeList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (RecipeList.SelectedIndex == -1)
            {
                ClearTextBoxes();
                return;
            }

            if (isModified)
            {
                SaveRecipe();
            }

            string selectedItem = RecipeList.SelectedItem.ToString();
            string fileNameWithoutExtension = selectedItem.Substring(selectedItem.IndexOf(' ') + 1);

            ItemTextBox.Text = fileNameWithoutExtension;
            currentFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Envir", "Recipe", fileNameWithoutExtension + ".txt");

            if (File.Exists(currentFilePath))
            {
                string[] fileLines = File.ReadAllLines(currentFilePath);
                ParseAndDisplayRecipe(fileLines);
            }
            else
            {
                MessageBox.Show("所选的配方文件不存在", "文件错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Parse and Display Recipe file
        private void ParseAndDisplayRecipe(string[] fileLines)
        {
            string currentSection = "";
            int ingredientIndex = 1;

            // Clear previous data
            ClearTextBoxes();

            foreach (string line in fileLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line;
                    continue;
                }

                switch (currentSection)
                {
                    case "[Recipe]":
                        var recipeParts = line.Split(new[] { ' ' }, 2);
                        if (recipeParts.Length == 2)
                        {
                            string key = recipeParts[0].Trim();
                            string value = recipeParts[1].Trim();

                            if (key == "Amount")
                            {
                                CraftAmountTextBox.Text = value;
                            }
                            else if (key == "Chance")
                            {
                                ChanceTextBox.Text = value;
                            }
                            else if (key == "Gold")
                            {
                                GoldTextBox.Text = value;
                            }
                        }
                        break;

                    case "[Tools]":
                        ToolTextBox.Text = line.Trim();
                        break;

                    case "[Ingredients]":
                        string[] parts = line.Split(new[] { ' ' }, 3);
                        string ingredientName = parts[0].Trim();
                        string ingredientAmount = parts.Length > 1 ? parts[1].Trim() : "";
                        string ingredientQuality = parts.Length > 2 ? parts[2].Trim() : "";

                        switch (ingredientIndex)
                        {
                            case 1:
                                IngredientName1TextBox.Text = ingredientName;
                                IngredientAmount1TextBox.Text = ingredientAmount;
                                Quality1TextBox.Text = ingredientQuality;
                                break;
                            case 2:
                                IngredientName2TextBox.Text = ingredientName;
                                IngredientAmount2TextBox.Text = ingredientAmount;
                                Quality2TextBox.Text = ingredientQuality;
                                break;
                            case 3:
                                IngredientName3TextBox.Text = ingredientName;
                                IngredientAmount3TextBox.Text = ingredientAmount;
                                Quality3TextBox.Text = ingredientQuality;
                                break;
                            case 4:
                                IngredientName4TextBox.Text = ingredientName;
                                IngredientAmount4TextBox.Text = ingredientAmount;
                                Quality4TextBox.Text = ingredientQuality;
                                break;
                        }

                        ingredientIndex++;
                        break;
                }
            }
        }
        #endregion

        #region Clear Text Boxes
        private void ClearTextBoxes()
        {
            CraftAmountTextBox.Clear();
            ChanceTextBox.Clear();
            GoldTextBox.Clear();
            ToolTextBox.Clear();
            IngredientName1TextBox.Clear();
            IngredientAmount1TextBox.Clear();
            Quality1TextBox.Clear();
            IngredientName2TextBox.Clear();
            IngredientAmount2TextBox.Clear();
            Quality2TextBox.Clear();
            IngredientName3TextBox.Clear();
            IngredientAmount3TextBox.Clear();
            Quality3TextBox.Clear();
            IngredientName4TextBox.Clear();
            IngredientAmount4TextBox.Clear();
            Quality4TextBox.Clear();
        }
        #endregion

        #region Save Recipe
        private void SaveRecipe()
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                return;
            }

            using (StreamWriter writer = new StreamWriter(currentFilePath))
            {
                writer.WriteLine("[Recipe]");
                writer.WriteLine($"Amount {CraftAmountTextBox.Text}");
                writer.WriteLine($"Chance {ChanceTextBox.Text}");
                writer.WriteLine($"Gold {GoldTextBox.Text}");
                writer.WriteLine();

                writer.WriteLine("[Tools]");
                writer.WriteLine(ToolTextBox.Text);
                writer.WriteLine();

                writer.WriteLine("[Ingredients]");
                if (!string.IsNullOrEmpty(IngredientName1TextBox.Text))
                {
                    writer.WriteLine($"{IngredientName1TextBox.Text} {IngredientAmount1TextBox.Text} {Quality1TextBox.Text}");
                }
                if (!string.IsNullOrEmpty(IngredientName2TextBox.Text))
                {
                    writer.WriteLine($"{IngredientName2TextBox.Text} {IngredientAmount2TextBox.Text} {Quality1TextBox.Text}");
                }
                if (!string.IsNullOrEmpty(IngredientName3TextBox.Text))
                {
                    writer.WriteLine($"{IngredientName3TextBox.Text} {IngredientAmount3TextBox.Text} {Quality1TextBox.Text}");
                }
                if (!string.IsNullOrEmpty(IngredientName4TextBox.Text))
                {
                    writer.WriteLine($"{IngredientName4TextBox.Text} {IngredientAmount4TextBox.Text} {Quality1TextBox.Text}");
                }
            }

            // Mark as not modified
            isModified = false;
        }

        private void RecipeInfoForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isModified)
            {
                SaveRecipe();
            }
        }
        #endregion

        #region Text Box Change Events
        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            // Check if any text box value has changed from its original value
            if (CraftAmountTextBox.Text != originalCraftAmount ||
                ChanceTextBox.Text != originalChance ||
                GoldTextBox.Text != originalGold ||
                ToolTextBox.Text != originalTool ||
                IngredientName1TextBox.Text != originalIngredientName1 ||
                IngredientAmount1TextBox.Text != originalIngredientAmount1 ||
                Quality1TextBox.Text != Quality1 ||
                IngredientName2TextBox.Text != originalIngredientName2 ||
                IngredientAmount2TextBox.Text != originalIngredientAmount2 ||
                Quality2TextBox.Text != Quality2 ||
                IngredientName3TextBox.Text != originalIngredientName3 ||
                IngredientAmount3TextBox.Text != originalIngredientAmount3 ||
                Quality3TextBox.Text != Quality3 ||
                IngredientName4TextBox.Text != originalIngredientName4 ||
                IngredientAmount4TextBox.Text != originalIngredientAmount4 ||
                Quality4TextBox.Text != Quality4)
            {
                isModified = true;
            }
        }
        #endregion

        #region Create new Recipe
        private const string TempFileName = "UntitledRecipe.txt";
        private void NewRecipeButton_Click(object sender, EventArgs e)
        {
            // Define the path to the directory containing recipes
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string directoryPath = Path.Combine(currentDirectory, "Envir", "Recipe");

            // Ensure the directory exists
            if (!Directory.Exists(directoryPath))
            {
                MessageBox.Show("配方目录不存在", "目录错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Get the filename from ItemTextBox and prepare full filename
            string displayFileName = ItemTextBox.Text.Trim();
            string fileName = string.IsNullOrEmpty(displayFileName) ? TempFileName : $"{displayFileName}.txt";

            // Path to the file
            string filePath = Path.Combine(directoryPath, fileName);

            // Check if file already exists
            if (File.Exists(filePath) && fileName != TempFileName)
            {
                MessageBox.Show("已存在同名文件", "文件已存在", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Create an empty text file at the generated path
            File.Create(filePath).Close(); // .Create() creates the file, .Close() closes the file handle

            // Update the ListBox to include the new recipe file
            string newItem = $"{RecipeList.Items.Count + 1}. {Path.GetFileNameWithoutExtension(fileName)}";
            RecipeList.Items.Add(newItem);

            // Select the new item in the ListBox
            RecipeList.SelectedIndex = RecipeList.Items.Count - 1;

            // Set the ItemTextBox to the new filename without extension if it was initially temporary
            if (fileName == TempFileName)
            {
                ItemTextBox.Text = Path.GetFileNameWithoutExtension(fileName);
            }
        }
        #endregion

        #region Name Text Box
        private bool isUpdatingTextBox = false;
        private void ItemTextBox_TextChanged(object sender, EventArgs e)
        {
            // Ensure there's a selected item in the ListBox
            if (RecipeList.SelectedIndex == -1)
                return;

            // Get the new display filename from ItemTextBox
            string newDisplayName = ItemTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newDisplayName))
            {
                MessageBox.Show("文件名不能为空", "无效文件名", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Prepare full filename with extension
            string newFileName = $"{newDisplayName}.txt";
            string oldDisplayName = RecipeList.SelectedItem.ToString().Substring(RecipeList.SelectedItem.ToString().IndexOf(' ') + 1).Trim(); // Extract old display name
            string oldFileName = $"{oldDisplayName}.txt";

            // Paths for old and new files
            string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Envir", "Recipe");
            string oldFilePath = Path.Combine(directoryPath, oldFileName);
            string newFilePath = Path.Combine(directoryPath, newFileName);

            // Check if the new filename already exists
            if (File.Exists(newFilePath))
            {
                // Revert the ItemTextBox to the old filename
                ItemTextBox.Text = oldDisplayName;
                return;
            }

            // Rename the file if needed
            if (oldFileName != newFileName && File.Exists(oldFilePath))
            {
                try
                {
                    File.Move(oldFilePath, newFilePath);

                    // Update ListBox item
                    RecipeList.Items[RecipeList.SelectedIndex] = $"{RecipeList.SelectedIndex + 1}. {newDisplayName}";
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"重命名文件时发生错误: {ex.Message}", "重命名错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            #endregion
        }

        #region 导出 / 导入 CSV
        private void ExportCsvButton_Click(object sender, EventArgs e)
        {
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string directoryPath = Path.Combine(currentDirectory, "Envir", "Recipe");
            if (!Directory.Exists(directoryPath))
            {
                MessageBox.Show("配方目录不存在", "目录错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "CSV (*.csv)|*.csv";
            sfd.FileName = "合成配方.csv";
            if (sfd.ShowDialog() != DialogResult.OK) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("配方文件名,合成数量,成功几率,制作费用,辅助工具,材料1:数量:品质,材料2:数量:品质,材料3:数量:品质,材料4:数量:品质");

            string[] recipeFiles = Directory.GetFiles(directoryPath, "*.txt");
            foreach (string file in recipeFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string[] lines;
                try { lines = File.ReadAllLines(file); }
                catch { continue; }

                string amount = "", chance = "", gold = "", tool = "";
                var ingredients = new List<string>();
                string currentSection = "";

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.ToLower();
                        continue;
                    }
                    switch (currentSection)
                    {
                        case "[recipe]":
                            var rp = line.Split(new[] { ' ' }, 2);
                            if (rp.Length == 2)
                            {
                                if (rp[0].ToLower() == "amount") amount = rp[1].Trim();
                                else if (rp[0].ToLower() == "chance") chance = rp[1].Trim();
                                else if (rp[0].ToLower() == "gold") gold = rp[1].Trim();
                            }
                            break;
                        case "[tools]":
                            tool = line.Trim();
                            break;
                        case "[ingredients]":
                            ingredients.Add(line.Trim());
                            break;
                    }
                }

                var cells = new List<string> { CsvEscape(fileName), amount, chance, gold, CsvEscape(tool) };
                for (int i = 0; i < 4; i++)
                {
                    if (i < ingredients.Count) cells.Add(CsvEscape(ingredients[i]));
                    else cells.Add("");
                }
                sb.AppendLine(string.Join(",", cells));
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("配方数据已导出: " + sfd.FileName, "导出完成");
        }

        private void ImportCsvButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "CSV (*.csv)|*.csv";
            if (ofd.ShowDialog() != DialogResult.OK) return;

            string[] lines;
            try { lines = File.ReadAllLines(ofd.FileName, Encoding.UTF8); }
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

            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string directoryPath = Path.Combine(currentDirectory, "Envir", "Recipe");
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            int imported = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cells = CsvSplit(lines[i]);
                if (cells.Length < 5) continue;

                string recipeName = cells[0].Trim();
                if (string.IsNullOrEmpty(recipeName)) continue;

                string amount = cells.Length > 1 ? cells[1].Trim() : "";
                string chance = cells.Length > 2 ? cells[2].Trim() : "";
                string gold = cells.Length > 3 ? cells[3].Trim() : "";
                string tool = cells.Length > 4 ? cells[4].Trim() : "";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[Recipe]");
                if (!string.IsNullOrEmpty(amount)) sb.AppendLine("Amount " + amount);
                if (!string.IsNullOrEmpty(chance)) sb.AppendLine("Chance " + chance);
                if (!string.IsNullOrEmpty(gold)) sb.AppendLine("Gold " + gold);
                sb.AppendLine();
                if (!string.IsNullOrEmpty(tool))
                {
                    sb.AppendLine("[Tools]");
                    sb.AppendLine(tool);
                    sb.AppendLine();
                }
                sb.AppendLine("[Ingredients]");
                for (int j = 5; j < cells.Length && j < 9; j++)
                {
                    if (string.IsNullOrWhiteSpace(cells[j])) continue;
                    sb.AppendLine(cells[j].Trim());
                }

                string filePath = Path.Combine(directoryPath, recipeName + ".txt");
                try
                {
                    File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                    imported++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("写入配方失败: " + recipeName + " - " + ex.Message, "导入错误");
                }
            }

            // 刷新列表
            RecipeList.Items.Clear();
            if (Directory.Exists(directoryPath))
            {
                string[] recipeFiles = Directory.GetFiles(directoryPath, "*.txt");
                for (int i = 0; i < recipeFiles.Length; i++)
                {
                    RecipeList.Items.Add(string.Format("{0}. {1}", i + 1, Path.GetFileNameWithoutExtension(recipeFiles[i])));
                }
            }

            MessageBox.Show("导入完成: 已导入 " + imported + " 个配方", "导入完成");
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private static string[] CsvSplit(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else current.Append(ch);
                }
                else
                {
                    if (ch == '"') inQuotes = true;
                    else if (ch == ',') { result.Add(current.ToString()); current.Clear(); }
                    else current.Append(ch);
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }
        #endregion
    }
}
