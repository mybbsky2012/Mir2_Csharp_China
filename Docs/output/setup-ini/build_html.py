# -*- coding: utf-8 -*-
"""根据 Setup.ini 实际内容 + Settings.cs 默认值，生成《Setup.ini 配置说明》HTML（供 html-review 与 convert_doc.py 使用）。"""
import json, os, re, html

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
INI = os.path.join(ROOT, 'Tools', 'MonsterLightProbe', 'Configs', 'Setup.ini')
RAW = os.path.join(os.path.dirname(__file__), '_raw2.json')
TPL = os.path.join(ROOT, 'Docs', 'output', '20261106-mir2-report', 'stage2', 'formatted-Mir2改造总结报告.html')
OUT = os.path.join(os.path.dirname(__file__), 'stage2', 'setup-ini-guide.html')
os.makedirs(os.path.dirname(OUT), exist_ok=True)

raw = json.load(open(RAW, encoding='utf-8'))
fields = raw['fields']
reads = {}
for sec, key, fld in raw['reads']:
    reads.setdefault((sec, key), fld)

cur = {}
order = []
sec = None
for s, k, v in raw['keys']:
    cur[(s, k)] = v
    if s not in order:
        order.append(s)


def norm_default(d):
    """把 Settings.cs 里抓到的默认值清洗成可直接展示的形式。"""
    d = (d or '').strip()
    m = re.search(r',\s*[A-Za-z_]\w*\s*=', d)   # 同一行/多行被误捕到下一个字段
    if m:
        d = d[:m.start()]
    d = d.split('//')[0].strip()
    if 'Path.Combine' in d:
        parts = re.findall(r'"([^"]*)"', d)
        if parts:
            d = '.\\' + parts[parts.__len__() - 1]
    if d.startswith('new '):
        d = '空'
    if re.fullmatch(r'-?\d+(\.\d+)?[Ff]', d):
        d = d[:-1]
    if d.lower() in ('true', 'false'):
        d = d.capitalize()
    return d.strip()


def df(key, section):
    fld = reads.get((section, key))
    if not fld or fld not in fields:
        return '—'
    d = norm_default(fields[fld]['default']).strip('"')
    if len(d) > 22:
        d = d[:20] + '…'
    return d if d else '—'


def esc(t):
    return html.escape(t, quote=False)


# ---------------------------------------------------------------- 内容
def T(rows):
    """rows: [(key, desc)]，section 由调用方给出"""
    return ('table', rows)


BODY = []


def P(t):
    BODY.append(('p', t))


def H2(t, i):
    BODY.append(('h2', (t, i)))


def CAL(t):
    BODY.append(('callout', t))


def TB(section, rows):
    BODY.append(('table', (section, rows)))


# ============ 一、怎么用
H2('1.1 这个文件在哪、谁在读', 's1-1')
P('服务端每次启动（以及界面上点「重新加载配置」时）会读取 <strong>Configs\\Setup.ini</strong>，把每一项灌进 Settings 类的静态字段。路径由 Settings.cs 里的 ConfigPath 决定，写死为程序目录下的 Configs，不跟 ini 放在一起也不会被读到。')
P('解析器是 Shared/Functions/IniReader.cs。它有三个必须知道的行为：')
TB('General', [
    ('段名与键名严格区分大小写', '比对用的是 String.CompareOrdinal，[General] 写成 [general] 或 General 就会整段失效，且不报错，直接退回默认值。'),
    ('读不到就静默用默认值，并顺手写回文件', 'ReadXxx(section, key, Default, writeWhenNull = true)：键缺失或值非法时，取代码里的默认值，然后把这个默认值追加写进 Setup.ini。所以服务端跑过一次后，文件里会多出一批你没写过的键。'),
    ('布尔值只认 True / False', 'bool.TryParse 不接受 1 / 0 / yes / no / 开 / 关。写 1 会被当成非法值，直接回落默认值（多数默认是 False），这是最常见的「改了没反应」。'),
])
CAL('另外两点：值里不要再出现等号（解析按第一个 = 切分，后面的部分会被截断）；注释行统一用分号开头，且注释行不要写等号。')

H2('1.2 改完怎么生效', 's1-2')
P('绝大多数项在「重新加载配置」或重启服务端后生效，不需要重新编译。有几类例外：网络监听类（Port、IPAddress、StartHTTPService、HTTPIPAddress、HttpTransport、资源服务相关）在服务进程启动时绑定端口，必须<strong>重启</strong>服务端；MainUser 上限、线程数同理。')
P('还有一类是「客户端也读」的：Port、MaxResolution 由客户端 Configs\\Setup.ini 的同名项配合，只改服务端不改客户端会出现连不上或被压分辨率。')

H2('1.3 本文的标注约定', 's1-3')
P('每个段一张表，四列：键名、配置文件里的当前值、代码里的默认值、中文说明。说明里出现下面两种标记时要注意：')
TB('General', [
    ('已失效', '代码里没有任何地方引用这个键（或引用处被注释掉了）。改了不会有任何效果，属于历史遗留或从别的分支合并进来但没实现的功能。'),
    ('【注意】', '改这个会明显改变手感、经济或安全性，动之前先想清楚后果。'),
])
CAL('本文的「当前值」取自工程内最新的一份样本：Tools\\MonsterLightProbe\\Configs\\Setup.ini（2026-11-06，201 个键）。你实际服务器上的 Configs\\Setup.ini 可能被服务端自动补写过，以自己那份为准。')

# ============ 二、General
H2('2.1 段内各项', 's2-1')
TB('General', [
    ('VersionPath', '客户端版本文件的相对路径。CheckVersion 打开时，服务端按这个路径去读客户端版本号做比对；关着就只是个摆设。'),
    ('CheckVersion', '是否校验客户端版本。False = 任意版本客户端都能登（开发、内测常用）；True = 版本不符直接拒绝登录。'),
    ('RelogDelay', '同账号重复登录的最小间隔（毫秒）。<strong>已失效</strong>：MirConnection.cs 里校验这一段被注释掉了，改了不起作用。'),
    ('GMPassword', 'GM 命令密码，客户端发 GM 命令时校验。<strong>【注意】</strong>这是全服权限最高的一把钥匙，泄露等于人人能刷装备；别提交到公开仓库，开服前务必换成强密码。'),
    ('Multithreaded', '是否启用多线程处理（地图、怪物按线程分摊）。关掉 = 全服单线程跑，人多必卡。'),
    ('ThreadLimit', '并行线程数上限。经验值取 CPU 物理核数减一：2 核填 1~2，8 核填 6~7。填太大会因为线程调度反而更慢。'),
    ('TestServer', '<strong>【注意】</strong>测试服模式。True 时玩家一上线就自动执行 @GAMEMASTER（人人 GM），并且 @LEVEL、@CHANGEGENDER 这类命令对非 GM 也放行。正式服必须 False，写成 True 等于全服开挂。'),
    ('EnforceDBChecks', '启动时强制校验数据库完整性（物品、怪物、魔法之间的引用关系）。建议保持 True，坏数据早发现；代价是启动慢几十秒。'),
    ('MonsterProcessWhenAlone', '地图上没有真人时怪物是否继续跑 AI。False = 无人地图冻结（省 CPU，适合大世界）；True = 怪物照常活动。假人服有假人在图里，两种都能跑。'),
    ('Language', '服务端语言标记。<strong>已失效</strong>：本分支不读这个键，服务端语言实际来自 Configs\\Language.ini。'),
    ('DuraLossRate', '装备持久消耗倍率（百分比）。100 = 原版速度，30 ≈ 三分之一速度，0 = 完全不掉持久。被怪打全身掉、攻击只掉武器，都乘这个系数。'),
])

# ============ 三、Network
H2('3.1 连接与人数', 's3-1')
TB('Network', [
    ('IPAddress', '游戏端口监听地址。0.0.0.0 = 所有网卡（对外开服）；127.0.0.1 = 只允许本机连（配合中转/测试）。'),
    ('Port', '游戏主端口，登录与游戏共用。客户端 Configs\\Setup.ini 的 Port 必须一致。<strong>【注意】</strong>改端口后要同步开防火墙入站规则。'),
    ('TimeOut', '连接超时（毫秒）。30000 = 30 秒没响应就断开；网络抖动大的服可以放到 60000。'),
    ('MaxUser', '同时在线人数上限（含假人）。假人 300 的话，这个值至少要比「真人预期峰值 + 300」大。'),
    ('MaxIP', '同一 IP 允许同时在线的角色数。网吧/公司网要放大；想防多开就调小。'),
    ('MaxPacket', '收包上限。<strong>【注意】</strong>口径是「5 秒窗口内累计收到的数据包个数」，超过就判定为刷包外挂：断线并封该 IP 24 小时。50 相当于平均每秒 10 包，真人走位快、连招密时容易误判，建议 100~150。'),
])

H2('3.2 HTTP 服务', 's3-2')
TB('Network', [
    ('StartHTTPService', '是否开启内置 HTTP 服务。微端资源下发、账号查询接口、服务端状态页都跑在它上面；关掉则客户端无法按需下载资源。'),
    ('HTTPIPAddress', 'HTTP 监听地址，形如 http://0.0.0.0:5679/。端口不能与游戏 Port 相同。'),
    ('HTTPTrustedIPAddress', '受信 IP。账号查询接口只允许这些地址访问；0.0.0.0 表示不限制（此时安全全靠 HTTPQueryKey）。'),
    ('HttpTransport', 'HTTP 监听实现方式。auto = 先试内核态 http.sys（性能最好、需要管理员权限与 URL 预留），失败自动回落纯 Socket；也可写死 httpsys 或 socket。'),
    ('HTTPQueryKey', '账号查询接口 /accountinfo 的密钥（10~30 位）。留空 = 该接口一律拒绝；网页端调用时带 &key=此值。'),
])

H2('3.3 微端资源服务', 's3-3')
P('这一组只在 EnableResourceService = True 时有意义，作用是让客户端按需下载缺失的 Data 资源，不必整包更新。')
TB('Network', [
    ('EnableResourceService', '资源服务总开关。'),
    ('ResourcePath', '资源根目录（相对服务端目录）。客户端请求的相对路径会拼在这个目录下，务必不要指到系统目录。'),
    ('ResourceAllowAnyIP', '是否允许任意 IP 下载资源。False = 只允许 HTTPTrustedIPAddress 里的地址，适合只对内网/自己人开微端。'),
    ('ResourceMaxConcurrent', '同时传输的请求数上限。首次开服、集中更新时就是带宽峰值，按上行带宽定，别照抄 64。'),
    ('ResourceWriteTimeout', '单个请求最长传输秒数，超时断开并清理连接，防止半死连接长期占坑。'),
    ('ResourceBufferKB', '流式发送的分块大小（KB）。大文件多时 64~128 比较合适，太小会增加系统调用次数。'),
    ('ResourceVerboseLog', '是否逐条记录资源下发日志。高并发时务必关，日志本身就会把磁盘和 CPU 拖满。'),
    ('ResourceMaxKBps', '资源下发总限速（KB/s）。0 = 不限。窄带宽服务器一定要设，否则玩家下载资源会把游戏包挤掉，表现为集体卡顿。'),
])

# ============ 四、Permission
H2('4.1 开关类', 's4-1')
TB('Permission', [
    ('AllowNewAccount', '是否允许注册新账号。关掉 = 只能由 GM 在数据库工具里建号，适合「邀请制」服。'),
    ('AllowChangePassword', '是否允许玩家自己改密码。'),
    ('AllowLogin', '是否允许登录。关掉 = 维护模式，玩家连不上。'),
    ('AllowNewCharacter', '是否允许新建角色。'),
    ('AllowDeleteCharacter', '是否允许删除角色（配合 [Archive] 段决定删掉的角色留多久）。'),
    ('AllowStartGame', '是否允许真正进入游戏。关掉 = 只能停在选人界面，适合做「只开放登录看公告」的场景。'),
    ('AllowCreateAssassin', '是否允许创建刺客。关掉不影响已有刺客角色。'),
    ('AllowCreateArcher', '是否允许创建弓箭手。'),
    ('RequireStoragePassword', '仓库密码开关。<strong>已失效</strong>：本分支没有实现仓库密码，代码里没有任何引用，改了不会让仓库上锁。'),
    ('MaxResolution', '客户端可用分辨率宽度上限（像素）。客户端请求超过这个值会被压回；2560 = 放开到 2K 屏。注意客户端 Configs 里也有同名项，两边要一致。'),
])

# ============ 五、Optional
H2('5.1 地图与规则', 's5-1')
TB('Optional', [
    ('SafeZoneBorder', '安全区是否显示边界（脚下的安全区标记）。'),
    ('SafeZoneHealing', '安全区内是否自动回血回蓝。关掉 = 安全区只保护不治疗。'),
    ('GatherOrbsPerLevel', '冥想珠（元素珠）是否随等级自动成长：开启后每升一级自动涨珠，不用再手动放冥想技能；关掉则只能靠施法维持。'),
    ('ExpMobLevelDifference', '打低级怪是否按等级差衰减经验。开启时：人物等级比怪物高 10 级以上，每多 1 级扣掉本次经验的十五分之一；关闭则打多低的怪都拿全经验（快餐服常关）。'),
    ('GameMasterEffect', 'GM 上线是否带特殊光环。人多时建议关，光环是额外的绘制开销。'),
    ('LineMessageTimer', '全服公告（LineMessage）在屏幕上停留的分钟数。10 = 一条公告挂 10 分钟后自动撤下。'),
])

H2('5.2 穿行与手感', 's5-2')
TB('Optional', [
    ('EnableNoLamp', '免蜡：夜晚和洞穴不需要照明道具。'),
    ('EnableWalkThrough', '穿人：可以走过其他玩家和英雄。'),
    ('EnableWalkThroughMonster', '穿怪：可以走过普通怪物。'),
    ('EnableWalkThroughNPC', '穿 NPC：可以走过商人、守卫等 NPC。'),
    ('EnableWalkThroughWall', '穿城门与城墙。<strong>【注意】</strong>攻城时建议保持 False，开了以后门堵不住人，攻城战直接失去意义。'),
    ('EnableOverWeight', '超负重：负重超限仍可跑动、可穿脱装备。关掉 = 原版（超重只能走、捡不了东西）。'),
    ('EnableMountTai', '泰山：被攻击时不后仰、不打断跑动与施法。开了手感更顺，但也意味着「被打不会断技能」。'),
    ('CleanZeroDuraOnLogin', '登录（以及召唤英雄）时清理「持久为 0 却还占格子」的残留物品。物品定义的持久改过之后，老实例会卡在 0 持久清不掉，靠这个开关自愈。'),
])

H2('5.3 诱惑之光（宝宝）', 's5-3')
TB('Optional', [
    ('TameSuccessRate', '诱惑之光成功率（%）。0 = 走原版的多重随机（默认，不影响手感）；1~99 = 每次施法只掷一次骰，按该百分比判定；100 = 必定成功（秒招）。当前值是 100，等于所有可招的怪都能一次招走。'),
    ('TameIgnoreLevelLimit', '允许召唤等级高于「自身等级 + 2」的怪。'),
    ('TameIgnoreHpPenalty', '无视高血量抗性（原版 HP 越高越难招）。'),
    ('TameIgnoreUntameable', '允许召唤库里标了「不可诱惑」的怪（BOSS 等）。城门、城墙仍然硬排除，不受此开关影响。'),
])
CAL('这四项改完保存即生效，也可以在服务端界面「其他选项 → 诱惑之光」页里调，两条路改的是同一份数据。')

# ============ 六、Database
H2('6.1 段内各项', 's6-1')
TB('Database', [
    ('SaveDelay', '自动存盘间隔（分钟）。1 = 每分钟把角色、行会、账号落一次盘。越小越安全（崩服丢的进度少），越吃磁盘 IO。'),
    ('CredxGold', '游戏商城（GameShop）里 Credit 商品折算金币的系数：金币价 = 1000 × 该值。30 表示 1000 点券折 30000 金币。'),
])

# ============ 七、Game
H2('7.1 经验、掉落与尸体', 's7-1')
TB('Game', [
    ('DropRate', '爆率倍数。<strong>【注意】</strong>代码里的算法是 rate = Chance / DropRate，所以它是倍率不是百分比：1 = 原版，2 = 约两倍，0.5 = 约一半。当前值是 0，属于非法值（除零后溢出），等效于「几乎什么都不掉」，建议改回 1 或你要的倍数。'),
    ('ExpRate', '全局经验倍数。当前 200 = 200 倍。'),
    ('HighLevelExpRate', '高等级额外经验倍率，在 ExpRate 之上再乘一次。1 = 无加成。<strong>【注意】</strong>这里是倍数不是百分比，当前值 90 意味着 60 级以上在 200 倍之外再乘 90 倍，合计 18000 倍。如果本意是「额外 90%」，应写 1.9。'),
    ('HighLevelExpRateMinLevel', '触发上面那档倍率的最低等级。'),
    ('DeathExpLossRate', '死亡掉经验百分比（0 = 关闭）。扣除「当前等级升级所需经验」的该百分比，GM 不受影响。'),
    ('ItemTimeOut', '地面普通掉落物的消失时间（分钟）。当前 2 分钟，等于物品掉地上两分钟就没了，玩家跑回来常常已经消失。'),
    ('PlayerDiedItemTimeOut', '玩家死亡掉落物（尸体包）的消失时间（分钟）。120 = 两小时后才消失，给捡尸留足时间。'),
    ('CorpseTime', '怪物尸体停留时间（秒）。也受界面「其他选项」里的同项控制，两处改的是同一个值。'),
    ('PetSave', '宠物、召唤物是否存档保留。False = 下线就没，True = 下次上线还在。'),
    ('PetDieWithMaster', '主人死亡时宠物是否一起死。关闭 = 主人躺尸，宝宝还能继续打（分身宠物不受此项影响）。'),
])

H2('7.2 战斗、交易与冷却', 's7-2')
TB('Game', [
    ('PKDelay', 'PK 值（灰名/黄名）衰减计时（秒）。代码是「PKPointTime = 现在 + PKDelay 秒」，所以填 12 是 12 秒一个档位，不是分钟。'),
    ('GroupInviteDelay', '组队邀请的冷却（毫秒）。2000 = 两秒内不能重复邀请，防刷屏。'),
    ('TradeDelay', '交易请求的冷却（毫秒）。2000 = 两秒内不能重复发起交易。'),
    ('TeleportToNPCCost', 'NPC 传送费用（金币）。0 = 免费。'),
    ('CloneSmartSkill', '分身是否智能选技能。False = 退回原版，分身只会雷电术。'),
    ('CloneSkillMpCost', '分身施法消耗主人法力的百分比。100 = 与主人自己施法一致，0 = 不耗蓝。'),
])

H2('7.3 新手与公会', 's7-3')
TB('Game', [
    ('NewbieGuild', '新人公会名。新角色会自动加入这个公会；行会系统里对它有特殊处理（不会成为敌对目标）。'),
    ('NewbieGuildMaxSize', '新人公会人数上限。假人多、新手多的服要放大，否则新手进不了会。'),
])

H2('7.4 召唤物与怪物名称映射', 's7-4')
P('这一段全是「名字」，不是数值。它们指向怪物库（Monster）里的 Name 字段：技能要召出什么怪、Boss 要放什么小怪、柱子/图腾用什么模型，都从这里取。<strong>名字写错或怪物库里没有同名条目，表现是技能放不出来、或者放出来什么都没有</strong>，而且日志里通常不会报错，很难查。改这些之前先在数据库工具里确认目标怪物名存在。')
TB('Game', [
    ('SkeletonName', '道士「骷髅召唤」召出的怪。'),
    ('BugBatName', '虫系/蝙蝠类召唤物。'),
    ('ShinsuName', '道士「神兽」召唤。'),
    ('Zuma1', '祖玛系 Boss 召唤的第一种怪。'),
    ('Zuma2', '祖玛系 Boss 召唤的第二种怪。'),
    ('Zuma3', '祖玛系 Boss 召唤的第三种怪。'),
    ('Zuma4', '祖玛系 Boss 召唤的第四种怪。'),
    ('Zuma5', '带「3」后缀的祖玛强化变体一。'),
    ('Zuma6', '带「3」后缀的祖玛强化变体二。'),
    ('Zuma7', '带「3」后缀的祖玛强化变体三。'),
    ('Turtle1', '龟系召唤第一种（红龟）。'),
    ('Turtle2', '龟系召唤第二种（绿龟）。'),
    ('Turtle3', '龟系召唤第三种（蓝龟）。'),
    ('Turtle4', '龟系召唤第四种（塔龟）。'),
    ('Turtle5', '龟系召唤第五种（寿龟）。'),
    ('BoneMonster1', '骨系召唤一阶（骷髅士兵）。'),
    ('BoneMonster2', '骨系召唤二阶（骷髅武士）。'),
    ('BoneMonster3', '骨系召唤三阶（骷髅弓箭手）。'),
    ('BoneMonster4', '骨系召唤四阶（骷髅武将）。'),
    ('BehemothMonster1', '抱脸虫一阶，Behemoth 类 Boss 召唤。'),
    ('BehemothMonster2', '抱脸虫二阶。'),
    ('BehemothMonster3', '抱脸虫三阶。'),
    ('HellKnight1', '守护将系列之一（玄冰）。'),
    ('HellKnight2', '守护将系列之二（魔焰）。'),
    ('HellKnight3', '守护将系列之三（暗黑）。'),
    ('HellKnight4', '守护将系列之四（光明）。'),
    ('HellBomb1', 'Boss 技能弹体：冰弹。'),
    ('HellBomb2', 'Boss 技能弹体：雷弹。'),
    ('HellBomb3', 'Boss 技能弹体：焰弹。'),
    ('GeneralMeowMeowMob1', '猫卫系列一（铁锤猫卫）。'),
    ('GeneralMeowMeowMob2', '猫卫系列二（黑锤猫卫）。'),
    ('GeneralMeowMeowMob3', '陵庙守卫系列一（刀卫）。'),
    ('GeneralMeowMeowMob4', '陵庙守卫系列二（巫师）。'),
    ('KingHydraxMob', 'KingHydrax 类 Boss 召唤的怪（大蟒）。'),
    ('SnowWolfKingMob', '雪狼王召唤的怪。'),
    ('CallScrollMob', '召唤卷轴召出的怪（冰蛇）。'),
    ('ShardMaidenMob1', '冰宫系列一（地宫勇士）。'),
    ('ShardMaidenMob2', '冰宫系列二（地宫修道者）。'),
    ('ShardMaidenMob3', '冰宫系列三（冰封学者）。'),
    ('ShardMaidenMob4', '冰宫系列四（冰封贤者）。'),
    ('Mon637BMob', '怪物 637B 召唤的小怪（海之蜈蚣，20261103 韩服 AI 移植时加的）。'),
    ('WhiteSnake', '白蛇类 Boss 召唤的怪。'),
    ('AngelName', '月灵（道士系的召唤物）。'),
    ('BombSpiderName', '炸弹蜘蛛，蜘蛛类自爆召唤。'),
    ('CloneName', '法师「分身」的默认名字。'),
    ('Mon570NMirrorImageMob', '怪物 570N 的镜像分身怪。'),
    ('Mon573NMob', '怪物 573N 召唤的怪。'),
    ('FishMonster', '钓鱼时水域刷出的怪。'),
    ('AssassinCloneName', '刺客分身的名字。'),
    ('VampireName', '吸血系召唤（血蛛）。'),
    ('ToadName', '火蟾。'),
    ('SnakeTotemName', '蛇柱（蛇系图腾）。'),
    ('SnakesName', '魅蛇。'),
    ('StoneName', '蜘蛛柱，也是弓箭技能「地柱钉」生成的柱子。'),
    ('AncientBatName', '恶魔蝙蝠。'),
    ('TucsonGeneralEgg', '虫卵孵化出的统领（瘟疫统领）。'),
    ('HornedCommanderMob1', '角斗指挥官系列召唤一（魔风旋）。'),
    ('HornedCommanderMob2', '角斗指挥官系列召唤二（魔角大巫）。'),
    ('HornedCommanderBombMob', '角斗指挥官系列弹体（岩弹）。'),
])

H2('7.5 本段的失效项', 's7-5')
P('下面这些键在 Setup.ini 里存在、看着像功能开关，但代码里全工程搜不到引用（或引用处已注释）。改了没有任何效果，遇到「功能不生效」时先对照这张表排除。')
TB('Game', [
    ('MaxBossTames', '已失效。全工程无引用，早期版本用来限制「每人能招几只 BOSS 宝宝」，现在由其他逻辑接管。'),
    ('MonsterRecallEnabled', '已失效。本分支没有实现「怪物召回」功能，整组三个键都没有代码引用。'),
    ('MonsterRecallRange', '已失效，同上。'),
    ('MonsterRecallCooldown', '已失效，同上。'),
    ('Mon409BMob1', '已失效。与 HornedCommanderMob1 同名同义，但代码引用的是 HornedCommander 那组，这一组是历史残留。'),
    ('Mon409BMob2', '已失效，同上。'),
    ('Mon409BBombMob', '已失效，同上。'),
    ('Mon570BMob', '已失效。代码只引用 570N 的镜像怪（Mon570NMirrorImageMob），B 版无引用。'),
    ('Mon573BMob', '已失效。代码只引用 Mon573NMob。'),
    ('Mon580BMob', '已失效，无引用。'),
    ('Mon603BMob', '已失效，无引用。'),
    ('HeroName', '已失效。只有 Settings 自己读和写，没有任何地方使用；英雄名实际由角色数据决定。'),
])

# ============ 八、Rested
H2('8.1 段内各项', 's8-1')
P('这一组是「休息得经验加成」：角色<strong>站在安全区</strong>时每秒累计一点计数，攒够一定点数就换成「精力充沛」状态，在该状态下打怪有额外经验。')
TB('Rested', [
    ('Period', '在安全区每累计多少分钟折算 1 点休息点。0 = 关闭整套机制。'),
    ('BuffLength', '每点休息点折算的「精力充沛」时长（分钟）。'),
    ('ExpBonus', '「精力充沛」状态下的经验加成（百分比）。5 = 多 5%。'),
    ('MaxBonus', '最多能攒多少点。时长上限 = BuffLength × MaxBonus，当前 10 × 24 = 240 分钟。'),
])

# ============ 九、Items
H2('9.1 特殊戒指技能名', 's9-1')
TB('Items', [
    ('HealRing', '治愈戒指附带的技能名（对应技能库里的名字）。'),
    ('FireRing', '火焰戒指附带的技能名。'),
    ('BlinkSkill', '瞬移戒指附带的技能名。'),
])
P('这三个名字必须与技能库（Magic）里的技能名完全一致，写错的表现是戴上戒指不涨技能、或者技能列表里出现一个空条目。')

H2('9.2 随机属性权重', 's9-2')
P('装备洗练、随机附加属性时按权重抽取，权重越大越容易抽到，0 = 抽不到。整组数值是相对关系，改一项等于改所有项的占比。')
TB('Items', [
    ('MagicResistWeight', '魔法躲避（魔法抵抗）权重。'),
    ('PoisonResistWeight', '毒物躲避权重。'),
    ('CriticalRateWeight', '暴击率权重。'),
    ('CriticalDamageWeight', '暴击伤害权重。'),
    ('FreezingAttackWeight', '冰冻攻击权重。'),
    ('PoisonAttackWeight', '毒攻击权重。'),
    ('HealthRegenWeight', '体力恢复权重。'),
    ('ManaRegenWeight', '魔力恢复权重。'),
])

H2('9.3 其它', 's9-3')
TB('Items', [
    ('MaxLuck', '幸运值上限。诅咒下限取同值的负数，所以这一项同时决定「最多几点幸运、最多几点诅咒」。当前 10。'),
    ('SealDelay', '物品封印额外时长（分钟）。封印到期时间 = 物品自身封印时间 + 该值，用来给全服封印统一加一道冷却。'),
    ('PvpCanResistMagic', 'PVP 时魔法能否被「魔法躲避」抵挡。False = 打人必中，躲避只在打怪时生效。'),
    ('PvpCanResistPoison', 'PVP 时中毒能否被「毒物躲避」抵挡。'),
    ('PvpCanFreeze', 'PVP 时能否被冰冻。False = 玩家之间不会被冻住，防止被无限连。'),
])

# ============ 十、其余小段
H2('10.1 [PKTown] 红名村', 's10-1')
P('红名（PK 值超标）角色被守卫击杀或被抓后，会被传送到这里「关起来」，需要洗 PK 值才能离开。')
TB('PKTown', [
    ('PKTownMapName', '红名村所在的地图，可写地图序号（3）或地图文件名。'),
    ('PKTownPositionX', '红名村出生点 X 坐标。'),
    ('PKTownPositionY', '红名村出生点 Y 坐标。'),
])

H2('10.2 [DropGold] 掉落金钱', 's10-2')
TB('DropGold', [
    ('DropGold', '怪物是否掉落金币。关掉 = 金币只能靠任务、回收、商城，适合自建经济体系。'),
    ('MaxDropGold', '单次掉落的金币上限（单堆）。'),
])

H2('10.3 [Bonus] 远程命中', 's10-3')
TB('Bonus', [
    ('RangeAccuracyBonus', '远程命中率加成（百分比）。命中率 = 100 + 该值 - 距离衰减，0 = 原版手感；调大等于让弓箭、法师更不容易打空。'),
])

H2('10.4 [IntelligentCreatures] 灵宠', 's10-4')
TB('IntelligentCreatures', [
    ('CreatureBlackStoneName', '灵宠用的「黑石/蓝石」物品名（喂食、进化消耗的那块石头）。必须与物品库里的名字一致。'),
])

H2('10.5 [Observe] 观战', 's10-5')
TB('Observe', [
    ('AllowObserve', '是否允许普通玩家观战（GM 始终可以）。观战是只读跟随，开启后要注意别让人用来偷看 BOSS 刷新。'),
])

H2('10.6 [Archive] 角色归档', 's10-6')
P('归档 = 从主库移到 Archive 目录，角色数据还在，可以恢复，但不占主库内存和加载时间。两项都做了「最小 1 个月」的下限保护。')
TB('Archive', [
    ('InactiveCharacterMonths', '角色连续多少个月没登录就自动归档。'),
    ('DeletedCharacterMonths', '已删除的角色保留多少个月后彻底归档（这是找回的最后期限）。'),
])

# ============ 十一、FakePlayer
H2('11.1 规模与登录', 's11-1')
TB('FakePlayer', [
    ('Enabled', '假人系统总开关。'),
    ('Count', '生成多少个假人。0 = 不生成；代码里上限 300，写更大也按 300 算。'),
    ('LevelMin', '随机等级下限（含）。'),
    ('LevelMax', '随机等级上限（含）。'),
    ('Maps', '假人练级地图，写地图序号或文件名，逗号分隔。留空 = 自动挑「有怪物刷新点」的地图，按序号取前 20 张。'),
    ('RandomLogin', '假人是否随机上下线。False = Count 个假人一直在线（在线人数好看，但常驻内存更高）。'),
    ('Spawns', '内联登录点，格式 地图:X,Y，多个用分号隔开。留空 = 只读登录点文件；两项都空 = 在活动地图上随机登录。'),
    ('SpawnFile', '登录点文件（相对 Configs 目录），每行一个「地图 X Y」。'),
])
H2('11.2 行为', 's11-2')
TB('FakePlayer', [
    ('Chat', '假人是否说话、喊话。'),
    ('ChatFile', '台词文件（相对 Configs 目录），每行一句，以 # ; // 开头的行会被忽略。留空 = 用内置台词库。'),
    ('EquipFile', '装备方案文件（相对 Configs 目录）。文件不存在会自动生成一份模板，照着改即可。'),
    ('PK', '假人之间是否在野外随机 PK（互相切磋）。False = 只打怪。'),
    ('PKChance', '每轮判定「要不要挑个人打」的概率（%）。6 = 每轮 6% 概率开打。'),
    ('Follow', '是否允许假人接受组队邀请、跟随队长换图打怪（队长阵亡时它会自行下线重登）。'),
])

# ============ 十二、Hero
H2('12.1 段内各项', 's12-1')
TB('Hero', [
    ('SmartAI', '英雄智能战斗：主人打谁我打谁、卡住会绕路、残血会躲、被围会击退脱身。关掉 = 原版呆站。'),
    ('AutoSkill', '英雄自主施法，不再要求玩家给技能设快捷键。关掉 = 只放设过键的技能。'),
    ('DodgePercent', '血量低于该百分比时开始「且退且打」（风筝）。30 = 三成血开始走位。'),
    ('PushMinSurround', '贴身敌人达到这个数量才发动「击退脱围」。3 = 被三只围住才推。'),
    ('PushCooldownMs', '两次击退脱围的最小间隔（毫秒）。8000 = 八秒一次，防止无脑推怪。'),
])

# ============ 十三、OfflineAFK
H2('13.1 段内各项', 's13-1')
P('离线挂机 = 玩家断线后角色继续留在线上练级。死亡 5 秒后会自动回城复活，不会一直躺尸占地图。')
TB('OfflineAFK', [
    ('Enabled', '是否允许离线挂机。关掉 = 断线即下线。'),
    ('NoConsumableDura', '挂机时不扣消耗类物品的持久与电量（气血石、魔血石、照明物），回血照常给。'),
    ('NoEquipDura', '挂机时不扣装备持久（含武器，被怪打也不磨损）。'),
])
CAL('这两项「不扣持久」是挂机玩家的主要收益点，开之前想清楚经济影响：等于挂机完全零成本。')

# ============ 十四、速查
H2('14.1 一眼看出的问题项', 's14-1')
P('按代码实际行为核对这份样本文件，下面几项要么是非法值、要么与直觉相反，建议开服前逐条确认：')
TB('Game', [
    ('DropRate = 0', '除零后溢出，等效「几乎不掉落」。应改为 1（原版）或目标倍数。'),
    ('HighLevelExpRate = 90', '是倍数不是百分比。90 表示 60 级以上额外 90 倍，叠加 ExpRate 200 后是 18000 倍。若本意「额外 90%」，应写 1.9。'),
    ('TameSuccessRate = 100', '必定招中。所有可招的怪一次就到手，包括本来极难招的。'),
    ('ItemTimeOut = 2', '地上物品两分钟消失，远低于默认 30 分钟。玩家打完一波怪回头捡，常常已经没了。'),
    ('MaxPacket = 50', '5 秒 50 包，高强度操作下可能被误判刷包并封 IP 24 小时。建议 100~150。'),
    ('RelogDelay / Language / RequireStoragePassword / MaxBossTames / MonsterRecall* / Mon4xxB* / HeroName', '全部已失效，改了没有任何效果，可以删掉减少干扰。'),
])

H2('14.2 代码支持但这份文件里没写的项', 's14-2')
P('下面这些键在当前样本里没有，但代码里都有默认值，属于<strong>按默认值生效</strong>。需要调的时候自己往对应段里加一行即可（服务端启动时也会自动补写一部分）。')
TB('FakePlayer', [
    ('ChatIntervalMs', '默认 10000（10 秒）。假人说话/喊话的间隔，实际会在正负 30% 内抖动。'),
    ('NameFile', '默认 FakePlayerNames.txt。假人名文件，每行一个名字；文件不存在时按 Count 自动生成初始名。'),
    ('IdleIntervalMs', '默认 5000。无人地图里假人的处理间隔下限（毫秒），实际取该值到两倍之间随机。'),
    ('VisibleCap', '默认 50。每个真人视野内最多显示的假人数。超出部分不发出现包，玩家看不见，这是减轻客户端同屏压力最有效的一项。'),
    ('LoginBatch', '默认 100。启动或重置后每秒分批登录的假人数。0 = 一次性全部登录（旧行为，启动会明显卡顿）。'),
    ('ShowRampMax', '默认 20。渐显：每个真人每秒最多「出现」几个假人（按 1、2、4 翻倍到该值封顶）。'),
    ('CrowdCalm', '默认 True。被看就文静：假人在真人视野内多站少走，降低客户端同屏动画压力。'),
])
TB('Memory', [
    ('CompactMinutes', '默认 0（关闭）。定时全堆压缩的间隔分钟数。20~60 适合假人常驻的服，把 GC 缓存的空闲段还给系统；代价是压缩瞬间一次 Full GC 停顿（几十到几百毫秒）。'),
])
TB('Game', [
    ('PoisonLockMagicOnly', '默认 False。中特殊毒后只锁魔法攻击，不锁物理平砍。'),
])

# ============ 附录 A 差异表（自动）
diff = []


def bare(s):
    s = (s or '').strip().strip('"')
    return s


for (s, k), v in cur.items():
    fld = reads.get((s, k))
    if not fld or fld not in fields:
        continue
    base = bare(norm_default(fields[fld]['default']))
    val = bare(v)
    if base != val and not (base.lower() == val.lower()):
        diff.append((s, k, val, base))
diff.sort(key=lambda x: (x[0], x[1]))

# ---------------------------------------------------------------- 渲染
def render_table(section, rows):
    out = ['<table class="data-table"><thead><tr><th style="width:4.0cm">键名</th><th style="width:2.8cm">当前值</th><th style="width:1.6cm">默认</th><th>说明</th></tr></thead><tbody>']
    for k, desc in rows:
        cv = cur.get((section, k))
        if cv is None:
            cv = '—'
        cv = esc(cv)
        out.append('<tr><td><strong>%s</strong></td><td>%s</td><td>%s</td><td>%s</td></tr>'
                   % (esc(k), cv, esc(df(k, section)), desc))
    out.append('</tbody></table>')
    return ''.join(out)


h1_titles = [
    ('s1', '一、这份文档怎么用'),
    ('s2', '二、[General] 常规设置'),
    ('s3', '三、[Network] 网络与 HTTP'),
    ('s4', '四、[Permission] 权限开关'),
    ('s5', '五、[Optional] 可选玩法'),
    ('s6', '六、[Database] 数据库'),
    ('s7', '七、[Game] 游戏规则'),
    ('s8', '八、[Rested] 休息经验'),
    ('s9', '九、[Items] 物品'),
    ('s10', '十、其它小段（PKTown / DropGold / Bonus / 灵宠 / 观战 / 归档）'),
    ('s11', '十一、[FakePlayer] 假人系统'),
    ('s12', '十二、[Hero] 英雄 AI'),
    ('s13', '十三、[OfflineAFK] 离线挂机'),
    ('s14', '十四、速查：问题项与未写入项'),
    ('s15', '附录 A：与代码默认值的差异'),
]

# 把 h2 归到对应的 h1 下
sec_h2 = {}
cur_h1 = None
body_parts = []
for kind, val in BODY:
    if kind == 'h2':
        title, aid = val
        if cur_h1:
            sec_h2.setdefault(cur_h1, []).append((aid, title))
        body_parts.append('<h2 id="%s">%s</h2>' % (aid, title))
    elif kind == 'p':
        body_parts.append('<p>%s</p>' % val)
    elif kind == 'callout':
        body_parts.append('<p class="callout">%s</p>' % val)
    elif kind == 'table':
        s, rows = val
        body_parts.append(render_table(s, rows))

# h1 需要在 BODY 里显式插入：这里用「在每个 h2 块前插 h1」的规则不方便，
# 改为按顺序扫描 body_parts，遇到记录在 H1_AT 的位置插 h1。
H1_AT = {
    's1-1': ('s1', h1_titles[0][1]),
    's2-1': ('s2', h1_titles[1][1]),
    's3-1': ('s3', h1_titles[2][1]),
    's4-1': ('s4', h1_titles[3][1]),
    's5-1': ('s5', h1_titles[4][1]),
    's6-1': ('s6', h1_titles[5][1]),
    's7-1': ('s7', h1_titles[6][1]),
    's8-1': ('s8', h1_titles[7][1]),
    's9-1': ('s9', h1_titles[8][1]),
    's10-1': ('s10', h1_titles[9][1]),
    's11-1': ('s11', h1_titles[10][1]),
    's12-1': ('s12', h1_titles[11][1]),
    's13-1': ('s13', h1_titles[12][1]),
    's14-1': ('s14', h1_titles[13][1]),
}

final = []
for chunk in body_parts:
    m = re.match(r'<h2 id="([^"]+)"', chunk)
    if m and m.group(1) in H1_AT:
        aid, title = H1_AT[m.group(1)]
        final.append('<h1 id="%s">%s</h1>' % (aid, title))
    final.append(chunk)

# 附录 A
final.append('<h1 id="s15">%s</h1>' % h1_titles[14][1])
final.append('<p>下表只列「文件里的值」与「代码默认值」不一致的项，方便回溯这台服被调过什么。默认值一列取自 Settings.cs 的字段初始值，值被截断的地方以源码为准。</p>')
rows = ''.join('<tr><td>%s</td><td><strong>%s</strong></td><td>%s</td><td>%s</td></tr>' % (esc(s), esc(k), esc(v), esc(d)) for s, k, v, d in diff)
final.append('<table class="data-table"><thead><tr><th style="width:3.2cm">段</th><th style="width:5.2cm">键名</th><th style="width:3.6cm">文件值</th><th>代码默认值</th></tr></thead><tbody>%s</tbody></table>' % rows)

# 目录
toc = ['<ol class="toc-list">']
for aid, title in h1_titles:
    toc.append('<li><a href="#%s">%s</a>' % (aid, title))
    subs = sec_h2.get(aid, [])
    if subs and aid in ('s7',):
        toc.append('<ol>')
        for said, st in subs:
            toc.append('<li><a href="#%s">%s</a></li>' % (said, st))
        toc.append('</ol>')
    toc.append('</li>')
toc.append('</ol>')

tpl = open(TPL, encoding='utf-8').read()
head = tpl.split('<body>')[0]
head = head.replace('<title>Mir2 服务端与客户端改造总结报告</title>', '<title>Mir2 服务端 Setup.ini 配置说明</title>')


def prune_css(h):
    """删掉本文件用不到的 .abstract-card / .code-block 规则，避免 preflight PF-03（class 与结构脱钩）。"""
    m = re.search(r'<style>(.*?)</style>', h, re.S)
    css = m.group(1)
    parts = css.split('}')
    keep = []
    for seg in parts:
        if '{' in seg:
            sel = seg.split('{')[0]
            if '.abstract-card' in sel or '.code-block' in sel:
                # 连同行尾残留一起丢掉
                keep.append('')
                continue
        keep.append(seg)
    return h[:m.start(1)] + '}'.join(keep) + h[m.end(1):]


head = prune_css(head)


cover = '''<section role="cover">
  <div class="cover-wrap">
    <p class="cover-kicker">运维参考手册</p>
    <p class="cover-title">Mir2 服务端<br>Setup.ini 配置说明</p>
    <p class="cover-sub">201 个配置项逐条中文释义 · 含失效项与风险项标注</p>
    <table class="cover-meta-table"><thead><tr><th>文档信息</th></tr></thead><tbody>
    <tr><td>对应工程</td><td>Mir2（Crystal 血统）服务端</td></tr>
    <tr><td>解析源码</td><td>Server\\Settings.cs、Shared\\Functions\\IniReader.cs</td></tr>
    <tr><td>样本文件</td><td>Tools\\MonsterLightProbe\\Configs\\Setup.ini（201 个键 / 17 段）</td></tr>
    <tr><td>编写日期</td><td>2026 年 11 月 6 日</td></tr>
    </tbody></table>
  </div>
</section>

<section role="cover">
  <div class="toc-wrap">
    <p class="toc-title">目 录</p>
    %s
  </div>
</section>

<section role="body" data-page-restart="1">
''' % ''.join(toc)

doc = head + '<body>\n' + cover + '\n'.join(final) + '\n</section>\n</body>\n</html>\n'
open(OUT, 'w', encoding='utf-8').write(doc)

# ---- 覆盖度自检
used = []
for kind, val in BODY:
    if kind == 'table':
        for k, _ in val[1]:
            used.append((val[0], k))
from collections import Counter
c = Counter(used)
allkeys = set(cur.keys())
miss = sorted(allkeys - set(used))
dup = [k for k, n in c.items() if n > 1]
print('HTML:', OUT, len(doc), 'bytes')
print('覆盖', len(set(used)), '/', len(allkeys))
print('缺失:', miss)
print('重复:', dup)
print('差异项:', len(diff))
