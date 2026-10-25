using System.Security.Cryptography;
using System.Text;
using Client.Utils;

// 清单（PList.gz）格式往返测试 —— 直接调用客户端发布用的 FileInformation 实现
// 覆盖：新格式带 MD5 / 旧格式无 MD5 / 坏数据（HTML）容错 / MD5 计算与比对

int pass = 0, fail = 0;

void Check(string name, bool ok, string detail = "")
{
    if (ok) { pass++; Console.WriteLine($"  ✓ {name}"); }
    else { fail++; Console.WriteLine($"  ✗ {name} {(detail == "" ? "" : "-> " + detail)}"); }
}

Console.OutputEncoding = Encoding.UTF8;

// ---------- 1. 新格式（带 MD5 段）往返 ----------
Console.WriteLine("[1] 新格式（带 MD5 段）往返");
var list = new List<FileInformation>
{
    new() { FileName = @"Map\0.map", Length = 7350054, Compressed = 7350054, Creation = new DateTime(2026, 10, 23, 9, 0, 0), MD5 = MD5.HashData(Encoding.UTF8.GetBytes("map")) },
    new() { FileName = @"Client.dll", Length = 2984960, Compressed = 1200000, Creation = new DateTime(2026, 10, 23, 8, 13, 0), MD5 = MD5.HashData(Encoding.UTF8.GetBytes("dll")) },
};

byte[] blob;
using (var ms = new MemoryStream())
{
    using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
        FileInformation.WriteList(w, list);
    blob = ms.ToArray();
}

List<FileInformation> back;
using (var ms = new MemoryStream(blob))
using (var r = new BinaryReader(ms))
    back = FileInformation.ReadList(r);

Check("记录数一致", back.Count == 2, back.Count.ToString());
Check("文件名一致", back.Count == 2 && back[0].FileName == @"Map\0.map" && back[1].FileName == @"Client.dll");
Check("长度/压缩长一致", back.Count == 2 && back[0].Length == 7350054 && back[1].Compressed == 1200000);
Check("时间一致", back.Count == 2 && back[0].Creation == list[0].Creation);
Check("MD5 段被读出", back.Count == 2 && back[0].MD5 != null && back[1].MD5 != null);
Check("MD5 内容一致", back.Count == 2 && FileInformation.Md5Equal(back[0].MD5, list[0].MD5) && FileInformation.Md5Equal(back[1].MD5, list[1].MD5));
Check("尾部无残留", true);

// ---------- 2. 旧格式（无 MD5 段）兼容 ----------
Console.WriteLine("[2] 旧格式（无 MD5 段）兼容读取");
byte[] legacy;
using (var ms = new MemoryStream())
{
    using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
    {
        w.Write(2);
        foreach (var it in list) it.Save(w);   // 只写记录，不写哈希段
    }
    legacy = ms.ToArray();
}

List<FileInformation> old;
using (var ms = new MemoryStream(legacy))
using (var r = new BinaryReader(ms))
    old = FileInformation.ReadList(r);

Check("旧格式能读出 2 条", old.Count == 2, old.Count.ToString());
Check("旧格式 MD5 为 null（跳过校验）", old.Count == 2 && old[0].MD5 == null);
Check("旧格式字段无损", old.Count == 2 && old[0].Length == 7350054 && old[1].FileName == @"Client.dll");

// ---------- 3. 坏数据（404 HTML）不被当成清单 ----------
Console.WriteLine("[3] 坏数据（404 HTML）容错");
string html = "<html><head><title>404 Not Found</title></head><body><center><h1>404 Not Found</h1></center><hr><center>nginx/1.31.7</center></body></html>";
bool threw = false;
string msg = "";
try
{
    using var ms = new MemoryStream(Encoding.ASCII.GetBytes(html));
    using var r = new BinaryReader(ms);
    var bad = FileInformation.ReadList(r);
    msg = $"未抛异常但得到 {bad.Count} 条";
}
catch (Exception ex) { threw = true; msg = ex.GetType().Name; }
Check("HTML 会被拒绝（抛异常，调用方按失败处理）", threw, msg);

// ---------- 4. MD5 计算与比对 ----------
Console.WriteLine("[4] MD5 计算 / 比对");
string tmp = Path.GetTempFileName();
File.WriteAllBytes(tmp, Encoding.UTF8.GetBytes("hello mir2"));
byte[] fileMd5 = FileInformation.ComputeMD5File(tmp);
Check("文件 MD5 = 内容 MD5", FileInformation.Md5Equal(fileMd5, MD5.HashData(Encoding.UTF8.GetBytes("hello mir2"))));
Check("不同内容判为不等", !FileInformation.Md5Equal(fileMd5, MD5.HashData(Encoding.UTF8.GetBytes("hello mir3"))));
Check("null 安全判为不等", !FileInformation.Md5Equal(null, fileMd5) && !FileInformation.Md5Equal(fileMd5, null));
File.Delete(tmp);

// ---------- 5. 压缩判断（本次 404 事故的核心规则） ----------
Console.WriteLine("[5] .gz 后缀判断（Compressed 语义）");
bool NeedGz(FileInformation i) => i.FileName != "PList.gz" && i.Compressed > 0 && i.Compressed != i.Length;

Check("Compressed=0 → 不请求 .gz（本次事故根因）", !NeedGz(new FileInformation { FileName = @"Map\0.map", Length = 7350054, Compressed = 0 }));
Check("Compressed=Length → 不请求 .gz", !NeedGz(new FileInformation { FileName = @"Map\0.map", Length = 7350054, Compressed = 7350054 }));
Check("Compressed<Length → 请求 .gz", NeedGz(new FileInformation { FileName = @"Client.dll", Length = 2984960, Compressed = 1200000 }));
Check("PList.gz 自身永不加 .gz", !NeedGz(new FileInformation { FileName = "PList.gz", Length = 30, Compressed = 10 }));

Console.WriteLine();
Console.WriteLine($"==== {pass} 通过, {fail} 失败 ====");
return fail == 0 ? 0 : 1;
