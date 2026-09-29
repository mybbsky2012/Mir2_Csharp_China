namespace Server.MirEnvir
{
    /// <summary>
    /// 循环（泡点）定时器。
    /// 与 Timer 的一次性倒计时不同，LoopTimer 会按固定间隔不断重复触发，
    /// 每次触发都去执行指定 NPC 脚本中的某个页面（标签），直到被 EXPIRETIMER 关闭。
    /// </summary>
    public class LoopTimer
    {
        private static Envir Envir
        {
            get { return Envir.Main; }
        }

        /// <summary>定时器名称，脚本中用于开启/关闭的唯一标识</summary>
        public string Key;

        /// <summary>两次触发之间的间隔，单位秒</summary>
        public int Interval;

        /// <summary>触发时执行的 NPC 脚本页，形如 [@泡点奖励]</summary>
        public string Label;

        /// <summary>所属脚本 ID（脚本引擎用）</summary>
        public int ScriptID;

        /// <summary>所属 NPC 对象 ID（脚本引擎用）</summary>
        public uint ObjectID;

        /// <summary>下次触发时间（Envir.Time 毫秒）</summary>
        public long NextTime;

        public LoopTimer(string key, int interval, string label, int scriptID, uint objectID)
        {
            Key = key;
            Interval = interval < 1 ? 1 : interval;
            Label = label;
            ScriptID = scriptID;
            ObjectID = objectID;

            NextTime = Envir.Time + (Interval * Settings.Second);
        }

        /// <summary>重置下一次触发时间（本次执行完毕后再排期）</summary>
        public void Reset()
        {
            NextTime = Envir.Time + (Interval * Settings.Second);
        }
    }
}
