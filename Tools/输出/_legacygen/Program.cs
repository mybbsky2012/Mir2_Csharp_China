using Server;
using Server.MirDatabase;
using Server.MirEnvir;

// 把一份已迁移的账号库「降级」成旧编码（159→230，160~169→232~241），
// 用来验证升级工具的旧编码识别/迁移路径。只用于测试。
if (args.Length < 1)
{
    Console.WriteLine("用法: LegacyGen <服务端目录>");
    return 2;
}

Directory.SetCurrentDirectory(Path.GetFullPath(args[0]));

Settings.Load();
var envir = Envir.Main;

envir.BuffInfoList.Clear();
foreach (var b in BuffInfo.Load()) envir.BuffInfoList.Add(b);

if (!envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 3; }
envir.LoadAccounts();

int chars = 0, buffs = 0;

foreach (var c in envir.CharacterList)
{
    if (c?.Buffs == null) continue;
    int hit = 0;

    foreach (var buff in c.Buffs)
    {
        if (buff == null) continue;
        int t = (int)buff.Type;

        if (t == (int)BuffType.羽化登仙)
        {
            buff.Info = envir.GetBuffInfo(BuffType.Custom1);
            buff.UnknownType = false;
            hit++;
        }
        else if (t >= (int)BuffType.羽化一重 && t <= (int)BuffType.羽化十重)
        {
            int oldType = 232 + (t - (int)BuffType.羽化一重);
            buff.Info = envir.GetBuffInfo((BuffType)oldType);
            buff.UnknownType = false;
            hit++;
        }
    }

    if (hit > 0) { chars++; buffs += hit; }
    Console.WriteLine($"{c.Name}: {hit}");
}

envir.SaveAccounts();
Console.WriteLine($"已降级 {buffs} 条 BUFF / {chars} 个角色");
return 0;
