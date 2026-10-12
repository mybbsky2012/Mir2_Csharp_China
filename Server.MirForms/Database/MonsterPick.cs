using Server.MirDatabase;

namespace Server
{
    /// <summary>
    /// 「怪物名称」下拉框用的条目包装。
    ///
    /// 只做一件事：让显示文本以<怪名>开头（"雷帝2 (997)"）。
    /// 直接把 MonsterInfo 丢进 ComboBox 时显示的是 MonsterInfo.ToString() 的
    /// "997: 雷帝2"，开头是数字，导致按名字搜索/首字定位完全失效。
    /// 本服有 1200+ 个怪物，滚列表找怪非常痛苦，所以统一用这个包一层。
    /// </summary>
    public sealed class MonsterPick
    {
        public readonly MonsterInfo Info;

        public MonsterPick(MonsterInfo info)
        {
            Info = info;
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", Info.Name, Info.Index);
        }
    }
}
