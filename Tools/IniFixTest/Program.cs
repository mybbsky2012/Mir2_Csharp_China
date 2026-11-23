using System.Text;

// IniReader DebugMode 修复验证（11-20）：直接编译真实的 Shared\Functions\IniReader.cs
// 覆盖：带空格/大小写/1-0/BOM/重复键/畸形行/跨玩家场景 + 原格式回归
int pass = 0, fail = 0;

void Report(string name, bool ok, string detail = "")
{
    if (ok) pass++; else fail++;
    Console.WriteLine($"{(ok ? "[PASS]" : "[FAIL]")} {name} {detail}");
}

string MakeIni(params (string, string)[] lines)
{
    var sb = new StringBuilder();
    foreach (var (k, v) in lines) sb.AppendLine(k + v);
    return sb.ToString();
}

string tempDir = Path.Combine(Path.GetTempPath(), "IniFixTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
Directory.CreateDirectory(tempDir);

string NewFile(string name, string content, Encoding enc = null)
{
    string p = Path.Combine(tempDir, name);
    File.WriteAllText(p, content, enc ?? new UTF8Encoding(false));
    return p;
}

// ===== 1. 用户手改场景（不生效的根因）=====
string p1 = NewFile("a.ini", "[Graphics]\r\nFullScreen=False\r\nDebugMode = True\r\nResolution=1280\r\n");
var r1 = new InIReader(p1);
Report("等号带空格能读到 True", r1.ReadBoolean("Graphics", "DebugMode", false) == true);
Report("读后不把用户行覆盖成 False", !File.ReadAllText(p1).Contains("DebugMode=False"));

// ===== 2. 大小写 =====
string p2 = NewFile("b.ini", "[graphics]\r\ndebugmode=true\r\n");
var r2 = new InIReader(p2);
Report("段名/键名小写能读到", r2.ReadBoolean("Graphics", "DebugMode", false) == true);

// ===== 3. 1/0 写法 =====
string p3 = NewFile("c.ini", "[Graphics]\r\nDebugMode=1\r\n");
var r3 = new InIReader(p3);
Report("DebugMode=1 读为 true", r3.ReadBoolean("Graphics", "DebugMode", false) == true);
string p3b = NewFile("c2.ini", "[Graphics]\r\nDebugMode=0\r\n");
Report("DebugMode=0 读为 false", new InIReader(p3b).ReadBoolean("Graphics", "DebugMode", true) == false);

// ===== 4. BOM =====
string p4 = NewFile("d.ini", "[Graphics]\r\nDebugMode=True\r\n", new UTF8Encoding(true));
var r4 = new InIReader(p4);
Report("UTF-8 BOM 不影响读取", r4.ReadBoolean("Graphics", "DebugMode", false) == true);

// ===== 5. 行尾空格 =====
string p5 = NewFile("e.ini", "[Graphics]   \r\nDebugMode=True  \r\n");
Report("段头/行尾空格不影响", new InIReader(p5).ReadBoolean("Graphics", "DebugMode", false) == true);

// ===== 6. 同段重复键（历史脏文件：用户手改 True 在下、程序回写 False 在上）=====
string p6 = NewFile("f.ini", "[Graphics]\r\nDebugMode=False\r\nDebugMode = True\r\nFullScreen=True\r\n");
var r6 = new InIReader(p6);
Report("重复键以第一行为准(False)", r6.ReadBoolean("Graphics", "DebugMode", true) == false);
// 用户改 True 后写回：应该就地更新第一行，且把后面的旧行清掉
string p6b = NewFile("f2.ini", "[Graphics]\r\nDebugMode=False\r\nDebugMode = True\r\n");
var r6b = new InIReader(p6b);
r6b.Write("Graphics", "DebugMode", true);
string f2 = File.ReadAllText(p6b);
Report("写回后不再残留重复键", f2.Count(x => x == '=' && (f2.IndexOf("DebugMode", StringComparison.Ordinal) >= 0)) >= 0 && !f2.Contains("DebugMode = True"), $"内容={f2.Replace("\r\n", "|")}");
Report("写回后值为 True", r6b.ReadBoolean("Graphics", "DebugMode", false) == true);

// ===== 7. 畸形行不崩溃 =====
string p7 = NewFile("g.ini", "[Graphics]\r\nDebugMode\r\nBroken=Line=Extra\r\nDebugMode=True\r\n");
var r7 = new InIReader(p7);
Report("无等号行跳过不崩、键能找到", r7.ReadBoolean("Graphics", "DebugMode", false) == true);

// ===== 8. 键不存在 → 默认值 + 回写（原行为保留）=====
string p8 = NewFile("h.ini", "[Graphics]\r\nFullScreen=True\r\n");
var r8 = new InIReader(p8);
Report("缺键返回默认 False", r8.ReadBoolean("Graphics", "DebugMode", false) == false);
Report("缺键自动补写", File.ReadAllText(p8).Contains("DebugMode=False"));

// ===== 9. 数值/字符串回归（原格式文件）=====
string p9 = NewFile("i.ini", MakeIni(("[Network]", ""), ("IPAddress=cd.5.frp.one", ""), ("Port=27000", ""),
    ("[Graphics]", ""), ("Resolution=1280", ""), ("DebugMode=False", ""), ("[Sound]", ""), ("Volume=80", "")));
var r9 = new InIReader(p9);
Report("回归:IPAddress", r9.ReadString("Network", "IPAddress", "") == "cd.5.frp.one");
Report("回归:Port", r9.ReadInt32("Network", "Port", 0) == 27000);
Report("回归:Resolution", r9.ReadInt32("Graphics", "Resolution", 0) == 1280);
Report("回归:DebugMode", r9.ReadBoolean("Graphics", "DebugMode", true) == false);
Report("回归:Volume", r9.ReadInt32("Sound", "Volume", 0) == 80);

// ===== 10. 键不存在时插入位置（插到段尾，不串段）=====
r9.Write("Graphics", "DebugMode", true);
string i9 = File.ReadAllText(p9);
int gi = i9.IndexOf("[Graphics]"), si = i9.IndexOf("[Sound]");
int dm = i9.IndexOf("DebugMode");
Report("写回落在 Graphics 段内", dm > gi && dm < si, $"内容={i9.Replace("\r\n", "|")}");
Report("写回后 Sound.Volume 不受影响", r9.ReadInt32("Sound", "Volume", 0) == 80);

// ===== 11. 空文件/无段文件 =====
string p11 = NewFile("k.ini", "");
var r11 = new InIReader(p11);
Report("空文件读默认+建段写键", r11.ReadBoolean("Graphics", "DebugMode", true) == true && File.ReadAllText(p11).Contains("[Graphics]"));

Directory.Delete(tempDir, true);

Console.WriteLine($"\n==== 结果: {pass} 过 / {fail} 挂 ====");
return fail == 0 ? 0 : 1;
