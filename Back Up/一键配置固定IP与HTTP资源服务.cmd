@echo off
setlocal

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo   需要管理员权限，正在请求提升，请在弹窗里点「是」...
    echo.
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

chcp 936 >nul

echo ==========================================================
echo   Mir2 服务端  固定IP / 对外开放 / HTTP资源服务  一键配置
echo ==========================================================
echo.
echo [1] 本机可用 IPv4 地址（对应 Setup.ini 的 IPAddress）
echo ----------------------------------------------------------
for /f "tokens=2 delims=:" %%a in ('ipconfig ^| findstr /c:"IPv4"') do echo     %%a
echo ----------------------------------------------------------
echo     建议 IPAddress 填 0.0.0.0：同时监听本机和对外网卡。
echo     填具体某个 IP 会导致「本机的 127.0.0.1 反而连不上」。
echo.

set HTTPPORT=5679
set /p HTTPPORT=请输入 HTTP 资源服务端口（直接回车 = 5679）：
if "%HTTPPORT%"=="" set HTTPPORT=5679

echo.
echo [2] 放行防火墙端口  7000(游戏)  3000(状态)  %HTTPPORT%(资源)
echo     不打这一步，玩家会「连得上游戏但下不到微端资源」。
netsh advfirewall firewall delete rule name="Mir2-Server" >nul 2>&1
netsh advfirewall firewall add rule name="Mir2-Server" dir=in action=allow protocol=TCP localport=7000,3000,%HTTPPORT%

echo.
echo [3] 登记 HTTP.sys 的 URL 保留项（可选，不是必须）
echo ----------------------------------------------------------
echo     新版服务端会自动挑选监听通道：http.sys 起不来就自动改用
echo     纯 Socket 模式（和游戏主端口同一套权限模型），所以跳过这步
echo     也能正常对外提供资源下载。
echo     登记的好处：本机/内网走内核态 http.sys，性能略高。
echo.
set ANSWER=
set /p ANSWER=要登记请输入 Y，直接回车跳过：
if /i not "%ANSWER%"=="Y" goto SKIPURLACL

netsh http delete urlacl url=http://+:%HTTPPORT%/ >nul 2>&1
netsh http add urlacl url=http://+:%HTTPPORT%/ user=Everyone
echo     当前保留项：
netsh http show urlacl | findstr /c:"%HTTPPORT%"
:SKIPURLACL

echo.
echo ==========================================================
echo   完成。接着改 Configs\Setup.ini 这几行，然后重启服务端：
echo.
echo       IPAddress=0.0.0.0
echo       HTTPIPAddress=http://+:%HTTPPORT%/
echo       HttpTransport=auto
echo.
echo   注意：HTTPIPAddress 不能写成 http://0.0.0.0:%HTTPPORT%/ 
echo         http.sys 不认 0.0.0.0 这个主机名，会报
echo         「不支持该请求」(System.Net.HttpListenerException 50)。
echo         要监听所有网卡就写 + 或 *。
echo.
echo   客户端（含微端包）的 Mir2Config.ini 要填服务端实际地址：
echo       [Network]      IPAddress = 你的公网IP / 域名
echo       [MicroClient]  Host      = http://你的公网IP:%HTTPPORT%/
echo.
echo   服务端启动后控制台会直接打印「微端资源服务 已开启: ...」
echo   以及「客户端 Mir2Config.ini 请填: ...」，照着抄即可。
echo.
echo   提示：若跨网段仍连不上，是路由器没做端口映射，
echo         需把 7000 和 %HTTPPORT% 映射到这台机器的内网IP。
echo ==========================================================
echo.
pause
