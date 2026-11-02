using System.Net;
using System.Text;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.Library.Utils
{
    class HttpServer : HttpService
    {
        Thread _thread;
        CancellationTokenSource tokenSource = new();

        public HttpServer()
        {
            Host = Settings.HTTPIPAddress;
        }

        public void Start()
        {
            // Stop() 会把 tokenSource 置空释放（关闭/重启都会走 Stop）。
            // 这里必须补建新的，否则 Reboot()（Stop+Start）会拿已释放的 token 直接抛异常。
            if (tokenSource == null)
                tokenSource = new CancellationTokenSource();

            _thread = new Thread(Listen);
            _thread.Start(tokenSource.Token);
        }

        public new void Stop()
        {
            base.Stop();

            // ★Stop 可能被调用多次：先点「停止服务器」再点关闭按钮时，
            // SMain_FormClosing 会再走一遍 Envir.Stop() → 这里。
            // 原实现在第二次调用时对已 Dispose 的 tokenSource 调 Cancel()
            // → ObjectDisposedException → 弹「未经处理的异常」对话框。
            // 改成幂等 + 容错：释放后置空，重复调用直接跳过。
            var ts = tokenSource;
            if (ts == null) return;

            tokenSource = null;
            try
            {
                ts.Cancel();
                Thread.Sleep(1000);
            }
            catch (Exception)
            {
                // 关停路径上的收尾操作，任何失败都不该阻断进程退出。
            }
            finally
            {
                ts.Dispose();
            }
        }


        public override void OnGetRequest(IHttpRequestLite request, IHttpResponseLite response)
        {
            var url = request.PathAndQuery;
            if (url.Contains("?"))
                url = url.Substring(0, url.IndexOf("?", StringComparison.Ordinal));

            url = url.ToLower();

            // 「启用HTTP服务」(StartHTTPService) 管的是注册/改名/广播这类管理接口。
            // 微端资源下发是另一条独立能力：EnableResourceService 一开就必须要监听，
            // 但那时只放行 /resource、/resindex、/resinfo，管理接口一律 404 ——
            // 否则「关了 HTTP 服务却还能注册账号」会让人以为开关失效。
            if (!Settings.StartHTTPService && !url.StartsWith("/res", StringComparison.Ordinal))
            {
                WriteNotFound(response, "http service disabled (StartHTTPService=False)");
                return;
            }

            try
            {
                switch (url)
                {
                    case "/":
                        WriteResponse(response, GameLanguage.GameName);
                        break;
                    case "/newaccount":
                        var id = request.QueryParam("id");
                        var psd = request.QueryParam("psd");
                        var email = request.QueryParam("email");
                        var name = request.QueryParam("name");
                        var question = request.QueryParam("question");
                        var answer = request.QueryParam("answer");
                        var ip = request.QueryParam("ip");
                        var p = new ClientPackets.NewAccount();
                        p.AccountID = id;
                        p.Password = psd;
                        p.EMailAddress = email;
                        p.UserName = name;
                        p.SecretQuestion = question;
                        p.SecretAnswer = answer;
                        var result = Envir.Main.HTTPNewAccount(p, ip);
                        WriteResponse(response, result.ToString());
                        break;                               
                    case "/addnamelist":
                        id = request.QueryParam("id");
                        var fileName = request.QueryParam("fileName");
                        AddNameList(id, fileName);
                        WriteResponse(response, "true");
                        break;              
                    case "/broadcast":
                        var msg = request.QueryParam("msg");
                        if (msg.Length < 5)
                        {
                            WriteResponse(response, "short");
                            return;
                        }
                        Envir.Main.Broadcast(new S.Chat
                        {
                            Message = msg.Trim(),
                            Type = ChatType.Shout2
                        });
                        WriteResponse(response, "true");
                        break;
                    case "/resource":
                        HandleResourceRequest(request, response);
                        break;
                    case "/resindex":
                        WriteBinary(response, BuildResourceIndexBytes(), "text/plain; charset=UTF-8");
                        break;
                    case "/resinfo":
                        WriteResponse(response, GetResourceInfo());
                        break;
                    case "/onlineplayers":
                        // web 查询全服在线玩家+装备（10-30）。key 校验同 /accountinfo。
                        WriteResponse(response, QueryOnlinePlayers(request));
                        break;
                    case "/playeritems":
                        // 点击玩家查看背包装备（10-30）：按角色名返回 14 槽装备 + 86 格背包 + 金币。
                        WriteResponse(response, QueryPlayerItems(request));
                        break;
                    case "/online":
                        // 自托管展示页（同源无跨域）：页面里输入 HTTPQueryKey 即可查看。
                        // 想自定义外观：把 HTML 放到 Configs\Web\online.html 即优先使用。
                        WriteResponse(response, BuildOnlinePage());
                        break;
                    case "/admin":
                        // 管理后台页（10-30）：账号分页 + 看板 + 封禁/解封/踢人按钮，直接浏览器操作。
                        WriteResponse(response, BuildAdminPage());
                        break;
                    case "/accounts":
                        // 账号分页列表（10-30）：含在线状态、封禁状态、角色摘要，并内嵌服务器运行状态。
                        // 参数：page/size/filter/online/banned，均可选。
                        WriteResponse(response, QueryAccounts(request));
                        break;
                    case "/serverstatus":
                        // 服务器运行状态 + 经验倍率 + 爆率（web 后台看板用）。
                        WriteResponse(response, QueryServerStatus(request));
                        break;
                    case "/ban":
                        // 封禁（写操作）：id= 或 name=，reason= 原因，days=/hours= 时长（都不填 = 永久），在线则踢下线。
                        WriteResponse(response, SetAccountBan(request, true));
                        break;
                    case "/unban":
                        // 解封（写操作）：id= 或 name=。
                        WriteResponse(response, SetAccountBan(request, false));
                        break;
                    case "/kick":
                        // 只踢下线不封号（写操作）：id= 或 name=。
                        WriteResponse(response, KickAccount(request));
                        break;
                    case "/accountinfo":
                        // web 程序查询账号信息（10-30）。安全约定：
                        //   1. 必须带 &key=Settings 的 HTTPQueryKey（留空 = 接口整体禁用，一律拒绝）；
                        //   2. 永不返回密码哈希/盐/密保答案；
                        //   3. 按账号 id 或角色 name 二选一查询，找不到返回 "notfound"。
                        WriteResponse(response, QueryAccountInfo(request));
                        break;
                    default:
                        WriteResponse(response, "error");
                        break;
                }
            }
            catch (Exception error)
            {
                WriteResponse(response, "request error: " + error);
            }
        }      

        /// <summary>/admin 管理后台页：优先读 Configs\Web\admin.html（可自行美化），否则用内置页面。</summary>
        string BuildAdminPage()
        {
            try
            {
                string custom = Path.Combine(Settings.ConfigPath, "Web", "admin.html");
                if (File.Exists(custom)) return File.ReadAllText(custom, Encoding.UTF8);
            }
            catch { }

            return @"<!DOCTYPE html>
<html lang=""zh"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>服务端管理后台</title>
<style>
  * { box-sizing: border-box; }
  body { font-family: 'Microsoft YaHei', sans-serif; background:#f2f4f7; margin:0; padding:20px; color:#1f2937; }
  .wrap { max-width:1180px; margin:0 auto; }
  h1 { font-size:20px; margin:0 0 14px; }
  .bar { display:flex; gap:8px; align-items:center; margin-bottom:14px; flex-wrap:wrap; }
  input, select { padding:6px 10px; border:1px solid #cbd5e1; border-radius:6px; font-size:13px; background:#fff; color:#1f2937; }
  input[type=password] { width:260px; }
  #filter { width:280px; }
  button { padding:6px 14px; border:0; border-radius:6px; background:#2563eb; color:#fff; cursor:pointer; font-size:13px; }
  button.sec { background:#64748b; }
  button.danger { background:#dc2626; }
  button.ok { background:#059669; }
  button:disabled { opacity:.5; cursor:not-allowed; }
  label { font-size:13px; color:#475569; }
  #msg { font-size:13px; color:#64748b; }

  .dash { display:grid; grid-template-columns:repeat(auto-fit,minmax(150px,1fr)); gap:10px; margin-bottom:16px; }
  .kpi { background:#fff; border-radius:10px; padding:12px 14px; box-shadow:0 1px 3px rgba(0,0,0,.07); }
  .kpi .k { font-size:12px; color:#64748b; margin-bottom:6px; }
  .kpi .v { font-size:18px; font-weight:700; color:#0f172a; }
  .kpi .v small { font-size:12px; font-weight:400; color:#94a3b8; }
  .dot { display:inline-block; width:8px; height:8px; border-radius:50%; margin-right:6px; vertical-align:middle; }
  .dot.on { background:#16a34a; box-shadow:0 0 0 3px rgba(22,163,74,.15); }
  .dot.off { background:#dc2626; box-shadow:0 0 0 3px rgba(220,38,38,.15); }

  .filters { display:flex; gap:8px; align-items:center; margin-bottom:10px; flex-wrap:wrap; }

  table { width:100%; border-collapse:collapse; background:#fff; border-radius:10px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,.07); }
  th, td { padding:9px 12px; text-align:left; font-size:13px; border-bottom:1px solid #eef2f7; vertical-align:middle; }
  th { background:#f8fafc; color:#475569; font-weight:600; font-size:12px; white-space:nowrap; }
  tr:last-child td { border-bottom:0; }
  tbody tr:hover { background:#f8fafc; }
  .acc { font-weight:600; }
  .sub { font-size:12px; color:#94a3b8; margin-top:2px; }
  .tag { display:inline-block; padding:1px 7px; border-radius:999px; font-size:11px; margin-right:4px; }
  .tag.on { background:#dcfce7; color:#15803d; }
  .tag.off { background:#f1f5f9; color:#64748b; }
  .tag.ban { background:#fee2e2; color:#b91c1c; }
  .tag.gm { background:#ede9fe; color:#6d28d9; }
  .ops { display:flex; gap:6px; flex-wrap:wrap; }
  .ops button { padding:4px 10px; font-size:12px; }

  .pager { display:flex; gap:8px; align-items:center; justify-content:flex-end; margin-top:12px; font-size:13px; color:#475569; }
  .pager button { background:#fff; color:#334155; border:1px solid #cbd5e1; }
  .pager button:disabled { color:#cbd5e1; }

  .modal { position:fixed; inset:0; background:rgba(15,23,42,.55); display:none; align-items:center; justify-content:center; z-index:9; }
  .modal.open { display:flex; }
  .panel { background:#fff; border-radius:12px; width:420px; max-width:92vw; padding:18px 20px; }
  .panel h2 { margin:0 0 12px; font-size:16px; }
  .field { margin-bottom:12px; }
  .field label { display:block; margin-bottom:5px; font-size:12px; color:#64748b; }
  .field input, .field select { width:100%; }
  .panel .foot { display:flex; gap:8px; justify-content:flex-end; margin-top:16px; }
</style>
</head>
<body>
<div class=""wrap"">
  <h1>🛠 服务端管理后台</h1>

  <div class=""bar"">
    <input id=""key"" type=""password"" placeholder=""访问密钥（!Setting.txt 的 HTTPQueryKey）"">
    <button onclick=""reload()"">刷新</button>
    <label><input type=""checkbox"" id=""auto"" checked> 每30秒自动刷新</label>
    <span id=""msg""></span>
  </div>

  <div class=""dash"" id=""dash""></div>

  <div class=""filters"">
    <input id=""filter"" placeholder=""搜索 账号 / 角色名 / 邮箱 / IP，回车搜索"">
    <select id=""fOnline"">
      <option value="""">全部在线状态</option>
      <option value=""1"">仅在线</option>
      <option value=""0"">仅离线</option>
    </select>
    <select id=""fBanned"">
      <option value="""">全部封禁状态</option>
      <option value=""1"">仅已封禁</option>
      <option value=""0"">仅正常</option>
    </select>
    <select id=""size"">
      <option value=""20"">20 条/页</option>
      <option value=""50"">50 条/页</option>
      <option value=""100"">100 条/页</option>
    </select>
    <button class=""sec"" onclick=""reload()"">查询</button>
  </div>

  <table>
    <thead>
      <tr>
        <th style=""width:200px"">账号</th>
        <th>角色</th>
        <th style=""width:150px"">状态</th>
        <th style=""width:150px"">最后登录</th>
        <th style=""width:210px"">操作</th>
      </tr>
    </thead>
    <tbody id=""rows""></tbody>
  </table>

  <div class=""pager"">
    <span id=""pageInfo""></span>
    <button id=""prev"" onclick=""gotoPage(-1)"">上一页</button>
    <button id=""next"" onclick=""gotoPage(1)"">下一页</button>
  </div>
</div>

<div class=""modal"" id=""banModal"">
  <div class=""panel"">
    <h2>封禁账号 <span id=""banWho""></span></h2>
    <div class=""field"">
      <label>封禁原因（玩家登录时会看到）</label>
      <input id=""banReason"" placeholder=""例如：使用外挂 / 恶意刷屏"">
    </div>
    <div class=""field"">
      <label>封禁时长</label>
      <select id=""banDuration"" onchange=""toggleCustom()"">
        <option value=""1"">1 天</option>
        <option value=""3"">3 天</option>
        <option value=""7"" selected>7 天</option>
        <option value=""30"">30 天</option>
        <option value=""0"">永久</option>
        <option value=""-1"">自定义小时</option>
      </select>
    </div>
    <div class=""field"" id=""customWrap"" style=""display:none"">
      <label>自定义时长（小时）</label>
      <input id=""banHours"" type=""number"" min=""1"" max=""87600"" value=""12"">
    </div>
    <div class=""foot"">
      <button class=""sec"" onclick=""closeBan()"">取消</button>
      <button class=""danger"" onclick=""confirmBan()"">确认封禁</button>
    </div>
  </div>
</div>

<script>
var state = { page: 1, pages: 1, size: 20, banTarget: '' };
var kInput = document.getElementById('key');
kInput.value = localStorage.getItem('admin_key') || '';
kInput.onchange = function () { localStorage.setItem('admin_key', kInput.value); reload(); };

function esc(s) {
  return (s == null ? '' : String(s)).replace(/&/g, '&amp;').replace(/</g, '&lt;')
    .replace(/>/g, '&gt;').replace(/""/g, '&quot;');
}
function setMsg(t, isErr) {
  var m = document.getElementById('msg');
  m.textContent = t || '';
  m.style.color = isErr ? '#dc2626' : '#64748b';
}
function key() { return kInput.value.trim(); }
function q(v) { return encodeURIComponent(v == null ? '' : v); }

async function getJson(path) {
  var r = await fetch(path);
  var t = await r.text();
  if (t === 'denied') throw new Error('密钥错误（或服务端未设置 HTTPQueryKey）');
  if (t === 'notfound') throw new Error('找不到该账号');
  if (t === 'offline') throw new Error('该账号当前不在线');
  return JSON.parse(t);
}

function reload() {
  state.page = 1;
  load();
}

async function load() {
  if (!key()) { setMsg('请先输入访问密钥', true); return; }

  var url = '/accounts?key=' + q(key()) + '&page=' + state.page + '&size=' + state.size
    + '&filter=' + q(document.getElementById('filter').value.trim())
    + '&online=' + q(document.getElementById('fOnline').value)
    + '&banned=' + q(document.getElementById('fBanned').value);

  setMsg('加载中…');
  try {
    var data = await getJson(url);
    setMsg('');

    state.pages = data.pages || 1;
    state.page = data.page || 1;

    renderDash(data.server || {}, data);
    renderRows(data.items || []);
    renderPager(data);
  } catch (e) {
    setMsg(e.message, true);
  }
}

function kpi(k, v, extra) {
  return '<div class=""kpi""><div class=""k"">' + k + '</div><div class=""v"">' + v +
    (extra ? ' <small>' + extra + '</small>' : '') + '</div></div>';
}

function renderDash(s, d) {
  var running = s.running
    ? '<span class=""dot on""></span>运行中'
    : '<span class=""dot off""></span>已停止';

  var exp = s.expRate == null ? '-' : s.expRate + 'x';
  var expExtra = (s.highLevelExpRate && s.highLevelExpRate != 1)
    ? ('Lv' + s.highLevelExpRateMinLevel + '+ ' + s.highLevelExpRate + 'x')
    : '';

  var html = '';
  html += kpi('运行状态', running, s.uptimeText ? ('已运行 ' + esc(s.uptimeText)) : '');
  html += kpi('在线人数', (s.onlinePlayers == null ? '-' : s.onlinePlayers), '连接 ' + (s.connections == null ? '-' : s.connections));
  html += kpi('经验倍率', esc(exp), esc(expExtra));
  html += kpi('爆率倍率', (s.dropRate == null ? '-' : s.dropRate + 'x'), '');
  html += kpi('账号总数', (d.total == null ? '-' : d.total), '在线 ' + (d.onlineCount || 0) + ' / 封禁 ' + (d.bannedCount || 0));
  html += kpi('启动时间', '<span style=""font-size:13px;font-weight:500"">' + esc(s.startedAt || '-') + '</span>', '');
  document.getElementById('dash').innerHTML = html;
}

function renderRows(items) {
  if (!items.length) {
    document.getElementById('rows').innerHTML = '<tr><td colspan=""5"" style=""text-align:center;color:#94a3b8;padding:28px"">没有符合条件的账号</td></tr>';
    return;
  }

  var html = items.map(function (a) {
    var chars = (a.characters || []).map(function (c) {
      return esc(c.name) + ' <span class=""sub"">Lv' + c.level + ' ' + esc(c.class) + '</span>';
    }).join('、');

    if (a.playing) {
      chars += '<div class=""sub"">🟢 正在游戏：' + esc(a.playing.name) + ' Lv' + a.playing.level +
        ' @ ' + esc(a.playing.map) + ' (' + a.playing.x + ',' + a.playing.y + ')</div>';
    }

    var status = '';
    if (a.online) status += '<span class=""tag on"">在线</span>';
    else status += '<span class=""tag off"">离线</span>';
    if (a.admin) status += '<span class=""tag gm"">GM</span>';
    if (a.banned) status += '<span class=""tag ban"">已封禁</span>';

    var banSub = a.banned
      ? '<div class=""sub"">' + esc(a.banReason || '') + (a.banExpire ? '｜至 ' + esc(a.banExpire) : '') + '</div>'
      : '';

    var ops = '';
    if (a.banned) {
      ops += '<button class=""ok"" onclick=""unban(\'' + q(a.account) + '\')"">解封</button>';
    } else {
      ops += '<button class=""danger"" onclick=""openBan(\'' + q(a.account) + '\')"">封禁</button>';
    }
    if (a.online) {
      ops += '<button class=""sec"" onclick=""kick(\'' + q(a.account) + '\')"">踢下线</button>';
    }

    return '<tr>' +
      '<td><div class=""acc"">' + esc(a.account) + '</div><div class=""sub"">' +
        (a.email ? esc(a.email) + '｜' : '') + 'IP ' + esc(a.lastIP || '-') + '</div></td>' +
      '<td>' + (chars || '<span class=""sub"">无角色</span>') + '</td>' +
      '<td>' + status + banSub + '</td>' +
      '<td>' + esc(a.lastDate || '-') + '</td>' +
      '<td><div class=""ops"">' + ops + '</div></td>' +
      '</tr>';
  }).join('');

  document.getElementById('rows').innerHTML = html;
}

function renderPager(d) {
  document.getElementById('pageInfo').textContent =
    '共 ' + d.total + ' 个账号，第 ' + d.page + ' / ' + (d.pages || 1) + ' 页';
  document.getElementById('prev').disabled = d.page <= 1;
  document.getElementById('next').disabled = d.page >= (d.pages || 1);
}

function gotoPage(delta) {
  var p = state.page + delta;
  if (p < 1 || p > (state.pages || 1)) return;
  state.page = p;
  load();
}

// ── 封禁弹层 ──
function openBan(account) {
  state.banTarget = decodeURIComponent(account);
  document.getElementById('banWho').textContent = state.banTarget;
  document.getElementById('banReason').value = '';
  document.getElementById('banDuration').value = '7';
  document.getElementById('customWrap').style.display = 'none';
  document.getElementById('banModal').classList.add('open');
  document.getElementById('banReason').focus();
}
function closeBan() {
  document.getElementById('banModal').classList.remove('open');
}
function toggleCustom() {
  var v = document.getElementById('banDuration').value;
  document.getElementById('customWrap').style.display = v === '-1' ? 'block' : 'none';
}

async function confirmBan() {
  var account = state.banTarget;
  if (!account) return;

  var reason = document.getElementById('banReason').value.trim();
  var duration = document.getElementById('banDuration').value;
  var hours = document.getElementById('banHours').value;

  var url = '/ban?key=' + q(key()) + '&id=' + q(account) + '&reason=' + q(reason);
  if (duration === '-1') url += '&hours=' + q(hours);
  else url += '&days=' + q(duration);

  closeBan();
  setMsg('正在封禁 ' + account + ' …');
  try {
    var r = await getJson(url);
    var part = '账号 ' + account + ' 已封禁（' + (r.expire || '') + '）' + (r.kicked ? '，已踢下线' : '');
    await load();          // 先刷完列表再提示，否则提示会被刷新请求覆盖
    setMsg(part);
  } catch (e) {
    setMsg(e.message, true);
  }
}

async function unban(account) {
  account = decodeURIComponent(account);
  if (!confirm('确定解封账号 ' + account + ' ？')) return;

  setMsg('正在解封 ' + account + ' …');
  try {
    await getJson('/unban?key=' + q(key()) + '&id=' + q(account));
    await load();
    setMsg('账号 ' + account + ' 已解封');
  } catch (e) {
    setMsg(e.message, true);
  }
}

async function kick(account) {
  account = decodeURIComponent(account);
  if (!confirm('确定把 ' + account + ' 踢下线？')) return;

  setMsg('正在踢下线 ' + account + ' …');
  try {
    await getJson('/kick?key=' + q(key()) + '&id=' + q(account));
    await load();
    setMsg('账号 ' + account + ' 已踢下线');
  } catch (e) {
    setMsg(e.message, true);
  }
}

document.getElementById('filter').addEventListener('keydown', function (e) {
  if (e.key === 'Enter') reload();
});
document.getElementById('fOnline').onchange = reload;
document.getElementById('fBanned').onchange = reload;
document.getElementById('size').onchange = function () {
  state.size = parseInt(this.value, 10) || 20;
  reload();
};

setInterval(function () {
  if (document.getElementById('auto').checked && !document.getElementById('banModal').classList.contains('open')) load();
}, 30000);

if (key()) load();
</script>
</body>
</html>
";
        }

        /// <summary>装备槽位名（下标 = EquipmentSlot 枚举值，HumanObject.Equipment 固定 14 格）。</summary>
        static readonly string[] EquipmentSlotNames =
        {
            "武器", "盔甲", "头盔", "照明物", "项链", "左手镯", "右手镯",
            "左戒指", "右戒指", "护身符", "腰带", "靴子", "守护石", "坐骑"
        };

        /// <summary>附加属性展示名（其余 Stat 用枚举名兜底）。</summary>
        static readonly Dictionary<Stat, string> StatNames = new()
        {
            [Stat.MinDC] = "攻下限", [Stat.MaxDC] = "攻击",
            [Stat.MinMC] = "魔下限", [Stat.MaxMC] = "魔法",
            [Stat.MinSC] = "道下限", [Stat.MaxSC] = "道术",
            [Stat.MinAC] = "防下限", [Stat.MaxAC] = "防御",
            [Stat.MinMAC] = "魔御下限", [Stat.MaxMAC] = "魔御",
            [Stat.HP] = "生命", [Stat.MP] = "魔法",
        };

        static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "");
        }

        /// <summary>把一件装备的 Stats 字典拼成 "攻击+7 防+3" 摘要。</summary>
        static string StatSummary(Stats stats)
        {
            if (stats == null || stats.Values.Count == 0) return "";
            var parts = new List<string>();
            foreach (var kv in stats.Values)
            {
                if (kv.Value == 0) continue;
                parts.Add((StatNames.TryGetValue(kv.Key, out var n) ? n : kv.Key.ToString()) + (kv.Value > 0 ? "+" : "") + kv.Value);
            }
            return string.Join(" ", parts);
        }

        /// <summary>
        /// /onlineplayers：全服在线玩家 + 身上装备，JSON。
        /// 只读展示用途：读 Players 列表时加锁快照，装备读的是当前内存态（改动中一瞬间可能有半拍误差，展示可接受）。
        /// </summary>
        string QueryOnlinePlayers(IHttpRequestLite request)
        {
            string key = request.QueryParam("key");
            if (string.IsNullOrEmpty(Settings.HTTPQueryKey) || key != Settings.HTTPQueryKey)
                return "denied";

            var sb = new StringBuilder();
            sb.Append('[');

            bool firstPlayer = true;
            lock (Envir.Main.Players)
            {
                // 等级降序，页面直接按顺序渲染
                var players = Envir.Main.Players.Where(p => p != null && p.Info != null)
                                                .OrderByDescending(p => p.Level)
                                                .ToList();

                foreach (var p in players)
                {
                    if (!firstPlayer) sb.Append(',');
                    firstPlayer = false;

                    sb.Append("{\"name\":\"").Append(JsonEscape(p.Name)).Append('"');
                    sb.Append(",\"level\":").Append(p.Level);
                    sb.Append(",\"class\":\"").Append(p.Info.Class).Append('"');
                    sb.Append(",\"gender\":\"").Append(p.Info.Gender).Append('"');
                    sb.Append(",\"map\":\"").Append(JsonEscape(p.CurrentMap?.Info?.Title ?? "")).Append('"');
                    sb.Append(",\"x\":").Append(p.CurrentLocation.X);
                    sb.Append(",\"y\":").Append(p.CurrentLocation.Y);
                    sb.Append(",\"guild\":\"").Append(JsonEscape(p.MyGuild?.Name ?? "")).Append('"');
                    sb.Append(",\"equipment\":{");

                    bool firstSlot = true;
                    for (int i = 0; i < p.Info.Equipment.Length && i < EquipmentSlotNames.Length; i++)
                    {
                        var item = p.Info.Equipment[i];
                        if (item == null || item.Info == null) continue;
                        if (!firstSlot) sb.Append(',');
                        firstSlot = false;

                        sb.Append('"').Append(EquipmentSlotNames[i]).Append("\":{");
                        AppendItemCore(sb, item, i);
                        sb.Append('}');
                    }

                    sb.Append("}}");
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>一件装备/物品的公共 JSON 字段（name/id/stats/count/dura），装备对象与背包数组共用。</summary>
        static void AppendItemCore(StringBuilder sb, UserItem item, int slot)
        {
            sb.Append("\"name\":\"").Append(JsonEscape(item.Info.Name)).Append('"');
            sb.Append(",\"id\":").Append(item.Info.Index);

            var summary = new List<string>();
            // 武器/盔甲带上基础攻防区间，直观
            if (slot == 0 && (item.Info.Stats[Stat.MaxDC] > 0 || item.Info.Stats[Stat.MinDC] > 0))
                summary.Add("攻 " + item.Info.Stats[Stat.MinDC] + "-" + item.Info.Stats[Stat.MaxDC]);
            if (slot == 1 && (item.Info.Stats[Stat.MaxAC] > 0 || item.Info.Stats[Stat.MinAC] > 0))
                summary.Add("防 " + item.Info.Stats[Stat.MinAC] + "-" + item.Info.Stats[Stat.MaxAC]);
            string added = StatSummary(item.AddedStats);
            if (added.Length > 0) summary.Add(added);
            sb.Append(",\"stats\":\"").Append(JsonEscape(string.Join("  ", summary))).Append('"');

            if (item.Count > 1) sb.Append(",\"count\":").Append(item.Count);
            if (item.MaxDura > 0) sb.Append(",\"dura\":\"").Append(item.CurrentDura).Append('/').Append(item.MaxDura).Append('"');
        }

        /// <summary>
        /// /playeritems：按角色名返回 14 槽装备 + 86 格背包 + 金币（点击玩家查看背包用）。
        /// 与 /onlineplayers 同一把密钥；找不到角色返回 notfound。
        /// </summary>
        string QueryPlayerItems(IHttpRequestLite request)
        {
            string key = request.QueryParam("key");
            if (string.IsNullOrEmpty(Settings.HTTPQueryKey) || key != Settings.HTTPQueryKey)
                return "denied";

            string name = request.QueryParam("name");
            if (string.IsNullOrEmpty(name)) return "notfound";

            lock (Envir.Main.Players)
            {
                var p = Envir.Main.Players.FirstOrDefault(x => x != null && x.Info != null &&
                        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (p == null) return "notfound";

                var sb = new StringBuilder();
                sb.Append("{\"name\":\"").Append(JsonEscape(p.Name)).Append('"');
                sb.Append(",\"level\":").Append(p.Level);
                sb.Append(",\"class\":\"").Append(p.Info.Class).Append('"');
                sb.Append(",\"gender\":\"").Append(p.Info.Gender).Append('"');
                sb.Append(",\"map\":\"").Append(JsonEscape(p.CurrentMap?.Info?.Title ?? "")).Append('"');
                sb.Append(",\"gold\":").Append(p.Account?.Gold ?? 0);
                sb.Append(",\"credit\":").Append(p.Account?.Credit ?? 0);

                sb.Append(",\"equipment\":{");
                bool firstSlot = true;
                for (int i = 0; i < p.Info.Equipment.Length && i < EquipmentSlotNames.Length; i++)
                {
                    var item = p.Info.Equipment[i];
                    if (item == null || item.Info == null) continue;
                    if (!firstSlot) sb.Append(',');
                    firstSlot = false;
                    sb.Append('"').Append(EquipmentSlotNames[i]).Append("\":{");
                    AppendItemCore(sb, item, i);
                    sb.Append('}');
                }

                sb.Append("},\"inventory\":[");
                bool firstItem = true;
                var inv = p.Info.Inventory;
                for (int i = 0; i < inv.Length; i++)
                {
                    var item = inv[i];
                    if (item == null || item.Info == null) continue;
                    if (!firstItem) sb.Append(',');
                    firstItem = false;
                    sb.Append("{\"slot\":").Append(i).Append(',');
                    AppendItemCore(sb, item, -1);
                    sb.Append('}');
                }
                sb.Append("]}");
                return sb.ToString();
            }
        }

        /// <summary>/online 展示页：优先读 Configs\Web\online.html（可自行美化），否则用内置页面。</summary>
        string BuildOnlinePage()
        {
            try
            {
                string custom = Path.Combine(Settings.ConfigPath, "Web", "online.html");
                if (File.Exists(custom)) return File.ReadAllText(custom, Encoding.UTF8);
            }
            catch { }

            return @"<!DOCTYPE html>
<html lang=""zh"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>全服在线一览</title>
<style>
  body { font-family: 'Microsoft YaHei', sans-serif; background:#f2f4f7; margin:0; padding:20px; color:#222; }
  h1 { font-size:20px; margin:0 0 12px; }
  .bar { display:flex; gap:8px; align-items:center; margin-bottom:16px; flex-wrap:wrap; }
  input[type=password] { padding:6px 10px; border:1px solid #ccc; border-radius:6px; width:220px; }
  button { padding:6px 14px; border:0; border-radius:6px; background:#2563eb; color:#fff; cursor:pointer; }
  button.sec { background:#64748b; }
  label { font-size:13px; color:#555; }
  .cards { display:grid; grid-template-columns:repeat(auto-fill,minmax(340px,1fr)); gap:12px; }
  .card { background:#fff; border-radius:10px; padding:14px 16px; box-shadow:0 1px 3px rgba(0,0,0,.08); }
  .head { display:flex; justify-content:space-between; align-items:baseline; border-bottom:1px solid #eee; padding-bottom:8px; margin-bottom:8px; }
  .name { font-size:16px; font-weight:600; }
  .meta { font-size:12px; color:#667; }
  .eq { display:grid; grid-template-columns:1fr 1fr; gap:4px 10px; font-size:13px; }
  .slot b { color:#555; font-weight:500; }
  .stats { color:#b45309; font-size:12px; }
  .empty { color:#ccc; }
  #msg { color:#c00; font-size:13px; margin-left:8px; }
  .card { cursor:pointer; transition:box-shadow .15s; }
  .card:hover { box-shadow:0 4px 14px rgba(0,0,0,.16); }
  .modal { position:fixed; inset:0; background:rgba(15,23,42,.55); display:none; align-items:flex-start; justify-content:center; padding:30px 16px; overflow:auto; z-index:9; }
  .modal.open { display:flex; }
  .panel { background:#fff; border-radius:12px; max-width:940px; width:100%; padding:18px 20px; }
  .panel h2 { margin:0 0 4px; font-size:18px; }
  .sec-title { font-size:13px; font-weight:600; color:#334155; margin:14px 0 8px; }
  .bag { display:grid; grid-template-columns:repeat(auto-fill,minmax(96px,1fr)); gap:6px; }
  .cell { border:1px solid #e5e7eb; border-radius:8px; padding:6px 7px; font-size:12px; min-height:46px; background:#fafafa; }
  .cell.empty { border-style:dashed; color:#d4d4d4; }
  .cell .cn { font-weight:600; color:#1f2937; line-height:1.3; word-break:break-all; }
  .cell .cs { color:#b45309; font-size:11px; }
  .cell .cd { color:#94a3b8; font-size:11px; }
  .close { float:right; border:0; background:#eef2f7; color:#334155; border-radius:8px; padding:4px 10px; cursor:pointer; }
  .gold { font-size:13px; color:#334155; }
</style>
</head>
<body>
<h1>⚔️ 全服在线一览 <span id=""count"" class=""meta""></span></h1>
<div class=""bar"">
  <input id=""key"" type=""password"" placeholder=""访问密钥（!Setting.txt 的 HTTPQueryKey）"">
  <button onclick=""load()"">刷新</button>
  <label><input type=""checkbox"" id=""auto"" checked> 每30秒自动刷新</label>
  <span id=""msg""></span>
</div>
<div id=""cards"" class=""cards""></div>
<div class=""modal"" id=""modal""><div class=""panel"">
  <button class=""close"" onclick=""closeModal()"">关闭 ✕</button>
  <h2 id=""mTitle""></h2>
  <div class=""gold"" id=""mMeta""></div>
  <div class=""sec-title"">身上装备</div>
  <div class=""bag"" id=""mEquip""></div>
  <div class=""sec-title"">背包</div>
  <div class=""bag"" id=""mBag""></div>
</div></div>
<script>
const SLOT_ORDER = [""武器"",""盔甲"",""头盔"",""照明物"",""项链"",""左手镯"",""右手镯"",""左戒指"",""右戒指"",""护身符"",""腰带"",""靴子"",""守护石"",""坐骑""];
const kInput = document.getElementById('key');
kInput.value = localStorage.getItem('ow_key') || '';
kInput.onchange = () => { localStorage.setItem('ow_key', kInput.value); load(); };

function esc(s){ return (s||'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/""/g,'&quot;'); }

async function load(){
  const key = kInput.value.trim();
  if(!key){ document.getElementById('msg').textContent = '请先输入密钥'; return; }
  document.getElementById('msg').textContent = '加载中…';
  try {
    const r = await fetch('/onlineplayers?key=' + encodeURIComponent(key));
    const t = await r.text();
    if(t === 'denied'){ document.getElementById('msg').textContent = '密钥错误（或服务端未设置 HTTPQueryKey）'; return; }
    const data = JSON.parse(t);
    document.getElementById('msg').textContent = '';
    document.getElementById('count').textContent = '共 ' + data.length + ' 人';
    const cards = data.map(p => {
      const eq = SLOT_ORDER.filter(s => p.equipment[s]).map(s =>
        '<div class=""slot""><b>' + s + '</b> ' + esc(p.equipment[s].name) +
        (p.equipment[s].stats ? ' <span class=""stats"">' + esc(p.equipment[s].stats) + '</span>' : '') + '</div>'
      ).join('');
      const empty = SLOT_ORDER.filter(s => !p.equipment[s]).length;
      return '<div class=""card"" data-name=""' + esc(p.name) + '""><div class=""head""><span class=""name"">' + esc(p.name) + '</span>' +
        '<span class=""meta"">Lv.' + p.level + ' ' + esc(p.class) + ' ' + esc(p.gender) +
        (p.guild ? '｜' + esc(p.guild) : '') + '</span></div>' +
        '<div class=""meta"" style=""margin-bottom:8px"">📍 ' + esc(p.map) + ' (' + p.x + ',' + p.y + ')</div>' +
        '<div class=""eq"">' + eq +
        (empty ? '<div class=""empty"">（' + empty + ' 个空槽位略）</div>' : '') + '</div></div>';
    }).join('');
    document.getElementById('cards').innerHTML = cards || '<p>当前没有在线玩家</p>';
  } catch(e) {
    document.getElementById('msg').textContent = '无法连接服务端（确认 StartHTTPService 已开启）';
  }
}
document.getElementById('cards').addEventListener('click', e => {
  const card = e.target.closest('.card');
  if (card && card.dataset.name) openPlayer(card.dataset.name);
});

function cellHtml(it){
  return '<div class=""cell""><div class=""cn"">' + esc(it.name) +
         (it.count ? ' <span class=""cd"">x' + it.count + '</span>' : '') + '</div>' +
         (it.stats ? '<div class=""cs"">' + esc(it.stats) + '</div>' : '') +
         (it.dura ? '<div class=""cd"">持久 ' + esc(it.dura) + '</div>' : '') + '</div>';
}

async function openPlayer(name){
  const key = kInput.value.trim();
  if(!key){ document.getElementById('msg').textContent = '请先输入密钥'; return; }
  try {
    const r = await fetch('/playeritems?key=' + encodeURIComponent(key) + '&name=' + encodeURIComponent(name));
    const t = await r.text();
    if(t === 'denied'){ document.getElementById('msg').textContent = '密钥错误'; return; }
    if(t === 'notfound'){ document.getElementById('msg').textContent = '该角色已不在线'; return; }
    const p = JSON.parse(t);
    document.getElementById('mTitle').textContent = p.name + '  Lv.' + p.level + ' ' + p.class + ' ' + p.gender;
    document.getElementById('mMeta').textContent = '📍 ' + p.map + '　金币 ' + p.gold + '　元宝 ' + p.credit;
    document.getElementById('mEquip').innerHTML = SLOT_ORDER.map(s => {
      const it = p.equipment[s];
      return it ? cellHtml(it) : '<div class=""cell empty"">' + s + '</div>';
    }).join('');

    const cells = new Array(86).fill(null);
    (p.inventory || []).forEach(it => { if(it.slot >= 0 && it.slot < 86) cells[it.slot] = it; });
    document.getElementById('mBag').innerHTML = cells.map(it => it ? cellHtml(it) : '<div class=""cell empty""></div>').join('');

    document.getElementById('modal').classList.add('open');
  } catch(e) {
    document.getElementById('msg').textContent = '加载玩家背包失败';
  }
}
function closeModal(){ document.getElementById('modal').classList.remove('open'); }
document.getElementById('modal').addEventListener('click', e => { if(e.target.id === 'modal') closeModal(); });
document.addEventListener('keydown', e => { if(e.key === 'Escape') closeModal(); });

setInterval(() => { if(document.getElementById('auto').checked) load(); }, 30000);
load();
</script>
</body>
</html>";
        }

        /// <summary>
        /// /accountinfo 查询实现：key 校验 → 按账号 id 或角色 name 在内存 AccountList 里查 → 拼 JSON。
        /// 账号数据只在服务端进程内存 + 二进制存档里，外部程序禁止直接读存档文件，
        /// 统一走这个进程内接口（读取时加锁，避免和工作线程的登录/建号写入撞车）。
        /// </summary>
        string QueryAccountInfo(IHttpRequestLite request)
        {
            string key = request.QueryParam("key");
            if (string.IsNullOrEmpty(Settings.HTTPQueryKey) || key != Settings.HTTPQueryKey)
                return "denied";

            string id = request.QueryParam("id");
            string name = request.QueryParam("name");

            lock (Envir.Main.AccountList)
            {
                Server.MirDatabase.AccountInfo account = null;

                if (!string.IsNullOrEmpty(id))
                    account = Envir.Main.AccountList.FirstOrDefault(a =>
                        string.Equals(a.AccountID, id, StringComparison.OrdinalIgnoreCase));
                else if (!string.IsNullOrEmpty(name))
                    account = Envir.Main.AccountList.FirstOrDefault(a =>
                        a.Characters != null && a.Characters.Any(c =>
                            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)));

                if (account == null) return "notfound";

                var sb = new StringBuilder();
                sb.Append("{\"account\":\"").Append(account.AccountID).Append('"');
                sb.Append(",\"userName\":\"").Append(account.UserName ?? "").Append('"');
                sb.Append(",\"email\":\"").Append(account.EMailAddress ?? "").Append('"');
                sb.Append(",\"banned\":").Append(account.Banned ? "true" : "false");
                if (account.Banned) sb.Append(",\"banReason\":\"").Append(account.BanReason ?? "").Append('"');
                sb.Append(",\"creationDate\":\"").Append(account.CreationDate.ToString("yyyy-MM-dd HH:mm:ss")).Append('"');
                sb.Append(",\"lastDate\":\"").Append(account.LastDate.ToString("yyyy-MM-dd HH:mm:ss")).Append('"');
                sb.Append(",\"lastIP\":\"").Append(account.LastIP ?? "").Append('"');
                sb.Append(",\"characters\":[");

                if (account.Characters != null)
                {
                    bool first = true;
                    foreach (var c in account.Characters)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append("{\"name\":\"").Append(c.Name ?? "").Append('"');
                        sb.Append(",\"level\":").Append(c.Level);
                        sb.Append(",\"class\":\"").Append(c.Class).Append('"');
                        sb.Append(",\"gender\":\"").Append(c.Gender).Append('"');
                        sb.Append('}');
                    }
                }

                sb.Append("]}");
                return sb.ToString();
            }
        }

        // ───────────────────────── 账号管理（10-30） ─────────────────────────
        // 约定：所有管理接口共用 Settings.HTTPQueryKey（留空 = 全部拒绝）；
        // 写操作（ban/unban/kick）不直接碰 socket 和存档文件，只改内存 + 入队，
        // 由游戏工作线程在下一拍执行「踢人」和「立即存档」，避免跨线程抢连接/抢文件。

        static bool CheckKey(IHttpRequestLite request)
        {
            string key = request.QueryParam("key");
            return !string.IsNullOrEmpty(Settings.HTTPQueryKey) && key == Settings.HTTPQueryKey;
        }

        static int ParseInt(string s, int def)
        {
            int v;
            return int.TryParse(s, out v) ? v : def;
        }

        static string FormatNumber(float v)
        {
            return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        static string FormatUptime(long ms)
        {
            var t = TimeSpan.FromMilliseconds(ms < 0 ? 0 : ms);
            if (t.TotalDays >= 1) return string.Format("{0}天{1}小时{2}分", (int)t.TotalDays, t.Hours, t.Minutes);
            if (t.TotalHours >= 1) return string.Format("{0}小时{1}分", t.Hours, t.Minutes);
            return string.Format("{0}分{1}秒", t.Minutes, t.Seconds);
        }

        /// <summary>封禁到期时间展示：20 年以上视为永久。</summary>
        static string FormatBanExpire(Server.MirDatabase.AccountInfo account)
        {
            if (!account.Banned) return "";
            if ((account.ExpiryDate - DateTime.Now).TotalDays > 365 * 20) return "永久";
            return account.ExpiryDate.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>服务器运行状态（/serverstatus 主体，/accounts 里也内嵌一份）。</summary>
        string BuildServerStatusJson()
        {
            var envir = Envir.Main;
            long uptime = envir.Time;
            DateTime startedAt = DateTime.Now.AddMilliseconds(-uptime);

            int online = 0;
            lock (envir.Players)
            {
                for (int i = 0; i < envir.Players.Count; i++)
                    if (envir.Players[i] != null) online++;
            }

            int connections = 0;
            lock (envir.Connections)
                connections = envir.Connections.Count;

            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"gameName\":\"").Append(JsonEscape(GameLanguage.GameName)).Append('"');
            sb.Append(",\"running\":").Append(envir.Running ? "true" : "false");
            sb.Append(",\"startedAt\":\"").Append(startedAt.ToString("yyyy-MM-dd HH:mm:ss")).Append('"');
            sb.Append(",\"uptimeSeconds\":").Append(uptime / 1000);
            sb.Append(",\"uptimeText\":\"").Append(FormatUptime(uptime)).Append('"');
            sb.Append(",\"onlinePlayers\":").Append(online);
            sb.Append(",\"connections\":").Append(connections);
            // 倍率：经验（含 60 级以上额外倍率）/ 爆率（Chance ÷ DropRate，越高越爱掉）
            sb.Append(",\"expRate\":").Append(FormatNumber(Settings.ExpRate));
            sb.Append(",\"highLevelExpRate\":").Append(FormatNumber(Settings.HighLevelExpRate));
            sb.Append(",\"highLevelExpRateMinLevel\":").Append(Settings.HighLevelExpRateMinLevel);
            sb.Append(",\"dropRate\":").Append(FormatNumber(Settings.DropRate));
            sb.Append(",\"saveDelayMinutes\":").Append(Settings.SaveDelay);
            sb.Append('}');
            return sb.ToString();
        }

        string QueryServerStatus(IHttpRequestLite request)
        {
            if (!CheckKey(request)) return "denied";
            return BuildServerStatusJson();
        }

        /// <summary>账号匹配：账号 / 用户名 / 邮箱 / 任一角色名 包含关键字即可。</summary>
        static bool AccountMatches(Server.MirDatabase.AccountInfo a, string filter)
        {
            var cmp = StringComparison.OrdinalIgnoreCase;

            if (!string.IsNullOrEmpty(a.AccountID) && a.AccountID.IndexOf(filter, cmp) >= 0) return true;
            if (!string.IsNullOrEmpty(a.UserName) && a.UserName.IndexOf(filter, cmp) >= 0) return true;
            if (!string.IsNullOrEmpty(a.EMailAddress) && a.EMailAddress.IndexOf(filter, cmp) >= 0) return true;
            if (!string.IsNullOrEmpty(a.LastIP) && a.LastIP.IndexOf(filter, cmp) >= 0) return true;

            if (a.Characters != null)
            {
                for (int i = 0; i < a.Characters.Count; i++)
                {
                    var c = a.Characters[i];
                    if (c != null && !string.IsNullOrEmpty(c.Name) && c.Name.IndexOf(filter, cmp) >= 0) return true;
                }
            }

            return false;
        }

        static void AppendAccountJson(StringBuilder sb, Server.MirDatabase.AccountInfo a,
            Dictionary<Server.MirDatabase.AccountInfo, Server.MirObjects.PlayerObject> playing)
        {
            bool isOnline = a.Connection != null && a.Connection.Connected;

            sb.Append("{\"account\":\"").Append(JsonEscape(a.AccountID)).Append('"');
            sb.Append(",\"userName\":\"").Append(JsonEscape(a.UserName)).Append('"');
            sb.Append(",\"email\":\"").Append(JsonEscape(a.EMailAddress)).Append('"');
            sb.Append(",\"admin\":").Append(a.AdminAccount ? "true" : "false");
            sb.Append(",\"online\":").Append(isOnline ? "true" : "false");
            sb.Append(",\"banned\":").Append(a.Banned ? "true" : "false");
            sb.Append(",\"banReason\":\"").Append(JsonEscape(a.BanReason)).Append('"');
            sb.Append(",\"banExpire\":\"").Append(FormatBanExpire(a)).Append('"');
            sb.Append(",\"creationDate\":\"").Append(a.CreationDate.ToString("yyyy-MM-dd HH:mm:ss")).Append('"');
            sb.Append(",\"lastDate\":\"").Append(a.LastDate.ToString("yyyy-MM-dd HH:mm:ss")).Append('"');
            sb.Append(",\"lastIP\":\"").Append(JsonEscape(a.LastIP)).Append('"');

            sb.Append(",\"characters\":[");
            if (a.Characters != null)
            {
                bool first = true;
                for (int i = 0; i < a.Characters.Count; i++)
                {
                    var c = a.Characters[i];
                    if (c == null) continue;

                    if (!first) sb.Append(',');
                    first = false;

                    sb.Append("{\"name\":\"").Append(JsonEscape(c.Name)).Append('"');
                    sb.Append(",\"level\":").Append(c.Level);
                    sb.Append(",\"class\":\"").Append(c.Class).Append('"');
                    sb.Append(",\"gender\":\"").Append(c.Gender).Append('"');
                    sb.Append('}');
                }
            }
            sb.Append(']');

            // 在线时附带「正在玩的角色 + 位置」，后台列表一眼能看出人在哪
            Server.MirObjects.PlayerObject p;
            if (isOnline && playing.TryGetValue(a, out p) && p != null)
            {
                sb.Append(",\"playing\":{\"name\":\"").Append(JsonEscape(p.Name)).Append('"');
                sb.Append(",\"level\":").Append(p.Level);
                sb.Append(",\"map\":\"").Append(JsonEscape(p.CurrentMap?.Info?.Title ?? "")).Append('"');
                sb.Append(",\"x\":").Append(p.CurrentLocation.X);
                sb.Append(",\"y\":").Append(p.CurrentLocation.Y);
                sb.Append('}');
            }

            sb.Append('}');
        }

        /// <summary>
        /// /accounts：账号分页列表。
        /// 参数：page（默认 1）、size（默认 20，上限 200）、filter（账号/用户名/邮箱/角色名/IP 模糊）、
        ///       online=1|0、banned=1|0。
        /// 排序：账号 Index 倒序（新号在前），保证分页稳定不跳条。
        /// </summary>
        string QueryAccounts(IHttpRequestLite request)
        {
            if (!CheckKey(request)) return "denied";

            int page = ParseInt(request.QueryParam("page"), 1);
            int size = ParseInt(request.QueryParam("size"), 20);
            if (page < 1) page = 1;
            if (size < 1) size = 20;
            if (size > 200) size = 200;

            string filter = request.QueryParam("filter").Trim();
            string online = request.QueryParam("online");
            string banned = request.QueryParam("banned");

            // 在线玩家快照（先锁 Players 再锁 AccountList，和游戏线程的加锁顺序保持一致）
            var playing = new Dictionary<Server.MirDatabase.AccountInfo, Server.MirObjects.PlayerObject>();
            lock (Envir.Main.Players)
            {
                for (int i = 0; i < Envir.Main.Players.Count; i++)
                {
                    var p = Envir.Main.Players[i];
                    if (p == null || p.Account == null) continue;
                    playing[p.Account] = p;
                }
            }

            var sb = new StringBuilder();

            // 服务器状态不放在账号锁内取（它要锁 Players/Connections，与游戏线程的
            // 「Connections → AccountLock」加锁顺序相反，放锁内会埋死锁隐患）
            string serverStatus = BuildServerStatusJson();

            lock (Envir.AccountLock)
            {
                var matched = new List<Server.MirDatabase.AccountInfo>();

                for (int i = Envir.Main.AccountList.Count - 1; i >= 0; i--)
                {
                    var a = Envir.Main.AccountList[i];
                    if (a == null) continue;

                    bool isOnline = a.Connection != null && a.Connection.Connected;
                    if (online == "1" && !isOnline) continue;
                    if (online == "0" && isOnline) continue;
                    if (banned == "1" && !a.Banned) continue;
                    if (banned == "0" && a.Banned) continue;

                    if (filter.Length > 0 && !AccountMatches(a, filter)) continue;

                    matched.Add(a);
                }

                int total = matched.Count;
                int pages = total == 0 ? 0 : (total + size - 1) / size;
                int skip = (page - 1) * size;

                sb.Append("{\"page\":").Append(page);
                sb.Append(",\"size\":").Append(size);
                sb.Append(",\"total\":").Append(total);
                sb.Append(",\"pages\":").Append(pages);

                int onlineCount = 0, bannedCount = 0;
                for (int i = 0; i < total; i++)
                {
                    if (matched[i].Connection != null && matched[i].Connection.Connected) onlineCount++;
                    if (matched[i].Banned) bannedCount++;
                }
                sb.Append(",\"onlineCount\":").Append(onlineCount);
                sb.Append(",\"bannedCount\":").Append(bannedCount);

                sb.Append(",\"server\":").Append(serverStatus);
                sb.Append(",\"items\":[");

                for (int i = skip; i < matched.Count && i < skip + size; i++)
                {
                    if (i > skip) sb.Append(',');
                    AppendAccountJson(sb, matched[i], playing);
                }

                sb.Append("]}");
            }

            return sb.ToString();
        }

        /// <summary>按 id（账号）或 name（角色名）定位账号；都为空返回 null。</summary>
        static Server.MirDatabase.AccountInfo FindAccount(string id, string name)
        {
            if (!string.IsNullOrEmpty(id))
                return Envir.Main.GetAccount(id);

            if (!string.IsNullOrEmpty(name))
                return Envir.Main.GetAccountByCharacter(name);

            return null;
        }

        /// <summary>
        /// /ban 与 /unban：封禁/解封账号（写操作）。
        /// days/hours 都不填 = 永久封禁（用 30 年表示，便于客户端展示）；
        /// 封禁时若账号在线，入队踢下线（工作线程执行），并请求立即存档。
        /// </summary>
        string SetAccountBan(IHttpRequestLite request, bool ban)
        {
            if (!CheckKey(request)) return "denied";

            string id = request.QueryParam("id").Trim();
            string name = request.QueryParam("name").Trim();
            if (id.Length == 0 && name.Length == 0) return "missing id or name";

            string reason = request.QueryParam("reason").Trim();
            int days = ParseInt(request.QueryParam("days"), 0);
            int hours = ParseInt(request.QueryParam("hours"), 0);

            string accountID;
            bool kicked = false;
            DateTime expiry = DateTime.MinValue;

            lock (Envir.AccountLock)
            {
                var account = FindAccount(id, name);
                if (account == null) return "notfound";

                accountID = account.AccountID;

                if (ban)
                {
                    if (reason.Length == 0) reason = "违反游戏规则";

                    double totalHours = days * 24 + hours;
                    if (totalHours > 0)
                    {
                        try { expiry = Envir.Main.Now.AddHours(totalHours); }
                        catch (ArgumentOutOfRangeException) { expiry = DateTime.Now.AddYears(30); }
                    }
                    else
                    {
                        expiry = DateTime.Now.AddYears(30); // 永久
                    }

                    account.Banned = true;
                    account.BanReason = reason;
                    account.ExpiryDate = expiry;
                }
                else
                {
                    account.Banned = false;
                    account.BanReason = string.Empty;
                    account.ExpiryDate = DateTime.MinValue;
                    reason = string.Empty;
                }

                if (account.Connection != null && account.Connection.Connected)
                {
                    Envir.Main.QueueHttpKick(account.Connection);
                    kicked = true;
                }
            }

            // 交给工作线程下一拍存档（HTTP 线程不碰存档文件）
            Envir.Main.HttpSaveRequested = true;

            MessageQueue.Instance.Enqueue(string.Format("[管理接口] 账号 {0} {1}{2}{3}",
                accountID,
                ban ? "已封禁" : "已解封",
                ban ? "，原因：" + reason : "",
                kicked ? "，已踢下线" : ""));

            var sb = new StringBuilder();
            sb.Append("{\"ok\":true,\"action\":\"").Append(ban ? "ban" : "unban").Append('"');
            sb.Append(",\"account\":\"").Append(JsonEscape(accountID)).Append('"');
            sb.Append(",\"banned\":").Append(ban ? "true" : "false");
            sb.Append(",\"reason\":\"").Append(JsonEscape(reason)).Append('"');
            sb.Append(",\"expire\":\"").Append(ban ? ((expiry - DateTime.Now).TotalDays > 365 * 20 ? "永久" : expiry.ToString("yyyy-MM-dd HH:mm:ss")) : "").Append('"');
            sb.Append(",\"kicked\":").Append(kicked ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>/kick：只踢下线（不封号）。离线返回 offline。</summary>
        string KickAccount(IHttpRequestLite request)
        {
            if (!CheckKey(request)) return "denied";

            string id = request.QueryParam("id").Trim();
            string name = request.QueryParam("name").Trim();
            if (id.Length == 0 && name.Length == 0) return "missing id or name";

            string accountID;
            bool queued = false;

            lock (Envir.AccountLock)
            {
                var account = FindAccount(id, name);
                if (account == null) return "notfound";

                accountID = account.AccountID;

                if (account.Connection == null || !account.Connection.Connected) return "offline";

                Envir.Main.QueueHttpKick(account.Connection);
                queued = true;
            }

            if (queued)
                MessageQueue.Instance.Enqueue(string.Format("[管理接口] 账号 {0} 已被踢下线", accountID));

            return "{\"ok\":true,\"account\":\"" + JsonEscape(accountID) + "\",\"kicked\":true}";
        }

        void AddNameList(string playerName, string fileName)        {
            fileName = Path.Combine(Settings.NameListPath, fileName);
            var sDirectory = Path.GetDirectoryName(fileName);
            Directory.CreateDirectory(sDirectory ?? throw new InvalidOperationException());
            if (!File.Exists(fileName))
                File.Create(fileName).Close();

            var tempString = fileName;
            if (File.ReadAllLines(tempString).All(t => playerName != t))
            {
                using (var line = File.AppendText(tempString))
                {
                    line.WriteLine(playerName);
                }
            }
        }    

        /// <summary>
        /// 生成资源清单：每行一个相对客户端根目录的路径，如 Data/Monster/000.Lib。
        /// 客户端靠它推算各图库目录应有的文件数量，从而在本地目录为空时也能正确建立索引。
        ///
        /// 清单在本服是静态资源，生成一次后缓存一段时间。
        /// 缓存这里用锁做双检：客户端是并发请求的，如果上百个请求同时发现缓存为空，
        /// 就会一起全盘遍历目录，白白把磁盘打满。
        /// </summary>
        string BuildResourceIndex()
        {
            if (!Settings.EnableResourceService) return string.Empty;

            lock (_indexLock)
            {
                if (_resourceIndex != null && (DateTime.UtcNow - _resourceIndexTime).TotalMinutes < ResourceIndexCacheMinutes)
                    return _resourceIndex;

                try
                {
                    var root = Path.GetFullPath(Settings.ResourcePath);
                    if (!Directory.Exists(root)) return string.Empty;

                    int rootLength = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                        ? root.Length
                        : root.Length + 1;

                    var sb = new StringBuilder();
                    int count = 0;

                    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        if (file.Length <= rootLength) continue;

                        sb.Append(file.Substring(rootLength).Replace('\\', '/'));
                        sb.Append('\n');
                        count++;
                    }

                    _resourceIndex = sb.ToString();
                    _resourceIndexBytes = Encoding.UTF8.GetBytes(_resourceIndex);
                    _resourceIndexTime = DateTime.UtcNow;

                    MessageQueue.Instance.Enqueue(string.Format("微端资源清单已生成，共 {0} 个文件，{1} 字节", count, _resourceIndex.Length));
                    return _resourceIndex;
                }
                catch (Exception ex)
                {
                    MessageQueue.Instance.Enqueue("生成微端资源清单失败: " + ex.Message);
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// 清单的字节形态（缓存）。
        /// 清单本身有几十 KB，若每个请求都做一次 UTF8 编码就是重复劳动，
        /// 缓存成 byte[] 后可以直接写 socket。
        /// </summary>
        byte[] BuildResourceIndexBytes()
        {
            var text = BuildResourceIndex();
            if (string.IsNullOrEmpty(text)) return Array.Empty<byte>();

            lock (_indexLock)
            {
                if (_resourceIndexBytes != null && _resourceIndex == text)
                    return _resourceIndexBytes;
            }

            return Encoding.UTF8.GetBytes(text);
        }

        private readonly object _indexLock = new object();
        private string _resourceIndex;
        private byte[] _resourceIndexBytes;
        private DateTime _resourceIndexTime = DateTime.MinValue;
        private const int ResourceIndexCacheMinutes = 10;

        /// <summary>
        /// 微端资源服务：按相对路径返回客户端缺失的资源文件。
        /// 请求示例：GET /resource?f=Data/Monsters/000.Lib
        /// 相对路径以客户端根目录为基准，因此 Data 与 Map 目录都能覆盖。
        ///
        /// 支持断点续传（10-23）：客户端带 Range: bytes=start- 请求头时，
        /// 从 start 处继续发送并回 206 + Content-Range；起点越界回 416；
        /// 不带 Range 或解析失败按全量 200 发送（兼容不支持续传的旧客户端）。
        ///
        /// 大文件走流式发送，不整块读进内存：单人下载时无所谓，
        /// 上百人同时下 47MB 的图库时，整块读会直接把内存吃光。
        /// </summary>
        internal void HandleResourceRequest(IHttpRequestLite request, IHttpResponseLite response)
        {
            if (!Settings.EnableResourceService)
            {
                WriteNotFound(response, "resource service disabled");
                return;
            }

            var relative = request.QueryParam("f");
            if (string.IsNullOrWhiteSpace(relative))
            {
                WriteNotFound(response, "missing parameter: f");
                return;
            }

            var fullPath = ResolveResourcePath(relative);
            if (fullPath == null)
            {
                WriteNotFound(response, "invalid path");
                return;
            }

            long length;
            try
            {
                var info = new FileInfo(fullPath);
                if (!info.Exists)
                {
                    WriteNotFound(response, "file not found: " + relative);
                    return;
                }
                length = info.Length;
            }
            catch (Exception ex)
            {
                WriteNotFound(response, "read error: " + ex.Message);
                return;
            }

            // ---- 断点续传：解析 Range: bytes=start- / bytes=start-end ----
            long rangeStart = -1;
            long rangeEnd = -1;
            string rangeHeader = request.Header("Range");

            if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            {
                string spec = rangeHeader.Substring(6).Trim();
                int dash = spec.IndexOf('-');
                if (dash > 0)
                {
                    string startPart = spec.Substring(0, dash).Trim();
                    string endPart = spec.Substring(dash + 1).Trim();
                    long start;
                    if (long.TryParse(startPart, out start) && start >= 0)
                    {
                        rangeStart = start;
                        if (endPart.Length > 0)
                        {
                            long end;
                            if (long.TryParse(endPart, out end) && end >= start) rangeEnd = end;
                        }
                    }
                }
            }

            response.AddHeader("Accept-Ranges", "bytes");

            if (rangeStart >= 0)
            {
                if (rangeStart >= length)
                {
                    // 续传起点越界：多半是客户端 .tmp 已经下完整（写完还没改名就断了）。
                    // 按规范回 416，客户端收到后直接把本地半截文件转正。
                    try
                    {
                        response.StatusCode = 416;
                        response.AddHeader("Content-Range", "bytes */" + length);
                        var output = response.OutputStream;
                        output.Flush();
                        output.Close();
                    }
                    catch
                    {
                    }
                    return;
                }

                // start-end 形式：end 超过文件尾就按文件尾算
                long end = rangeEnd >= 0 ? Math.Min(rangeEnd, length - 1) : length - 1;
                long count = end - rangeStart + 1;

                response.AddHeader("Content-Range", "bytes " + rangeStart + "-" + end + "/" + length);
                WriteFileStream(response, fullPath, length, rangeStart, count);

                if (Settings.ResourceVerboseLog)
                    MessageQueue.Instance.Enqueue(string.Format("微端续传资源 {0}（从 {1}/{2} 字节继续）", relative, rangeStart, length));
                return;
            }

            bool ok = WriteFileStream(response, fullPath, length);

            // 逐条写日志在高并发下本身就是瓶颈：每个请求一次消息入队 + 一次日志落盘。
            // 默认关闭，排障时再打开。
            if (ok && Settings.ResourceVerboseLog)
                MessageQueue.Instance.Enqueue(string.Format("微端下发资源 {0} ({1} 字节)", relative, length));
        }

        /// <summary>
        /// 允许对外提供的资源目录白名单（相对客户端根目录、小写）。
        ///
        /// 部署时资源目录通常直接指向客户端根目录，如果不限制目录，
        /// 攻击者可以顺着资源接口把 Mir2Config.ini 这类配置（内含保存的账号密码）一起拖走。
        /// </summary>
        private static readonly string[] ResourceAllowedRoots =
        {
            "data/", "map/", "sound/", "directx/", "localization/"
        };

        /// <summary>
        /// 把客户端传来的相对路径安全解析到资源根目录之内。
        /// 三重限制：目录白名单、规范化后必须仍在根目录内（防 ../ 穿越）、路径必须真实存在。
        /// </summary>
        string ResolveResourcePath(string relative)
        {
            try
            {
                relative = relative.Replace('\\', '/').TrimStart('/');

                bool allowed = false;
                string lower = relative.ToLowerInvariant();
                foreach (var prefix in ResourceAllowedRoots)
                {
                    if (lower.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed) return null;

                var root = Path.GetFullPath(Settings.ResourcePath);
                if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    root += Path.DirectorySeparatorChar;

                var combined = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

                if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return null;

                return combined;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 资源服务状态探测接口，方便客户端或运维确认是否配置正确。
        /// </summary>
        string GetResourceInfo()
        {
            try
            {
                var root = Path.GetFullPath(Settings.ResourcePath);
                return string.Format("{{\"enabled\":{0},\"exists\":{1},\"path\":\"{2}\",\"maxConcurrent\":{3},\"gateAvailable\":{4}}}",
                    Settings.EnableResourceService ? "true" : "false",
                    Directory.Exists(root) ? "true" : "false",
                    root.Replace("\\", "\\\\"),
                    Settings.ResourceMaxConcurrent,
                    TransferGate.CurrentCount);
            }
            catch (Exception ex)
            {
                return "{\"error\":\"" + ex.Message + "\"}";
            }
        }

        public override void OnPostRequest(IHttpRequestLite request, IHttpResponseLite response)
        {
            Console.WriteLine("POST request: {0}", request.PathAndQuery);
        }
    }

}
