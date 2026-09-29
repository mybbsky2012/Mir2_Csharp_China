using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Resolution
{
    /// <summary>
    /// 客户端支持的分辨率档位。
    ///
    /// 注意两点：
    /// 1. 枚举「名称」必须严格是 w{宽}h{高} 的格式，DisplayResolutions 用这个名称
    ///    去匹配系统显示模式，名字写错该档位在配置界面就会是灰的。
    /// 2. 枚举「值」取的是宽度，Settings.Resolution 存的就是这个值，
    ///    实际窗口高宽由 ResolutionHelper 统一换算。
    /// </summary>
    public enum eSupportedResolution
    {
        w1024h768 = 1024,
        w1280h720 = 1280,
        w1366h768 = 1366,
        w1600h900 = 1600,
        w1920h1080 = 1920,
        w2560h1440 = 2560
    }
}
