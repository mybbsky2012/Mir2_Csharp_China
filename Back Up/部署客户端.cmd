@echo off
chcp 65001 >nul
set SRC=D:\mir2-20241027\Build\Client\Debug
set DST=E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Client

echo 正在部署最新客户端文件...
echo 源: %SRC%
echo 目标: %DST%
echo.

copy /y "%SRC%\Client.dll" "%DST%\"
copy /y "%SRC%\Client.exe" "%DST%\"
copy /y "%SRC%\Client.pdb" "%DST%\"
copy /y "%SRC%\Client.deps.json" "%DST%\"
copy /y "%SRC%\Client.runtimeconfig.json" "%DST%\"
copy /y "%SRC%\Shared.dll" "%DST%\"

echo.
echo 部署完成。请确认上方 6 个文件均显示"已复制 1 个文件"。
echo 如果提示"拒绝访问"，请先关闭游戏客户端再重试。
pause
