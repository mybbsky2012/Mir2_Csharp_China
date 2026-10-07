using System.Diagnostics;
using Launcher;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Client.Resolution;

namespace Client
{
    internal static class Program
    {
        public static CMain Form;
        public static AMain PForm;

        public static bool Restart;

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                foreach (var arg in args)
                {
                    if (arg.ToLower() == "-tc") Settings.UseTestConfig = true;
                }
            }

            #if DEBUG
                Settings.UseTestConfig = true;
            #endif

            try
            {
                if (UpdatePatcher()) return;

                if (RuntimePolicyHelper.LegacyV2RuntimeEnabledSuccessfully == true) { }

                Packet.IsServer = false;
                Settings.Load();

                // 微端：后台先把资源清单（几十 KB 的目录索引）拉下来。
                // 清单只用于推算图库数组长度，先预热可以让启动阶段完全不阻塞在网络上；
                // 注意这里不下载任何真实资源，资源一律等到被用到时才取。
                Client.Utils.ResourceDownloader.WarmUp();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                CheckResolutionSetting();

                if (Settings.P_Patcher) Application.Run(PForm = new Launcher.AMain());
                else Application.Run(Form = new CMain());

                Settings.Save();

                if (Restart)
                {
                    Application.Restart();
                    return;
                }

                // 正常路径也走硬退出：Application.Run 返回后进程同样可能卡在 ExitProcess 的
                // DLL 卸载阶段（和全屏退出是同一个坑：界面没了、进程还在）。这里直接内核回收。
                HardExit();
            }
            catch (Exception ex)
            {
                CMain.SaveError(ex.ToString());
            }
        }

        /// <summary>
        /// 硬退出：先把启动器的 WebView2 限时释放掉，再用 TerminateProcess 直接结束进程。
        ///
        /// 为什么不用 Environment.Exit / 让 Main 自然结束：
        /// 这两条路最终都会调用 ExitProcess，而 ExitProcess 必须逐个卸载进程里已经加载的 DLL
        /// （每个 DLL 的 DllMain(DLL_PROCESS_DETACH) 都会被调用一次）。只要其中任何一个不返回
        /// ——显示驱动 / Direct3D / WebView2 运行时都是常见的「卸载时卡住」对象，而且全屏独占
        /// 模式下 D3D 设备的关系更复杂——进程就会永远停在那里：界面已经没了、进程还在，
        /// 只能去任务管理器手动结束。这就是「全屏退出后进程驻留」。
        /// TerminateProcess 不卸载任何 DLL、不等待任何线程，由内核直接回收进程，不存在被卡住的可能。
        /// </summary>
        public static void HardExit()
        {
            // ① 限时释放 WebView2：目的只是不留 msedgewebview2.exe 子进程。
            //    放在独立后台线程里、最多等 1.2 秒 —— 它自己也可能卡住，绝不能让它拖住退出。
            try
            {
                var release = new System.Threading.Thread(delegate ()
                {
                    try { if (PForm != null) PForm.ReleaseBrowser(); } catch { }
                });
                release.IsBackground = true;
                release.Start();
                release.Join(1200);
            }
            catch { }

            // ② 硬杀：不经过 ExitProcess，所以不会被任何 DLL 的卸载过程卡住
            try { Process.GetCurrentProcess().Kill(); } catch { }

            // 理论上到不了这里；万一 Kill 失败再兜一层
            Environment.Exit(0);
        }

        private static bool UpdatePatcher()
        {
            try
            {
                const string fromName = @".\AutoPatcher.gz", toName = @".\AutoPatcher.exe";
                if (!File.Exists(fromName)) return false;

                Process[] processes = Process.GetProcessesByName("AutoPatcher");

                if (processes.Length > 0)
                {
                    string patcherPath = Application.StartupPath + @"\AutoPatcher.exe";

                    for (int i = 0; i < processes.Length; i++)
                        if (processes[i].MainModule.FileName == patcherPath)
                            processes[i].Kill();

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    bool wait = true;
                    processes = Process.GetProcessesByName("AutoPatcher");

                    while (wait)
                    {
                        wait = false;
                        for (int i = 0; i < processes.Length; i++)
                            if (processes[i].MainModule.FileName == patcherPath)
                            {
                                wait = true;
                            }

                        if (stopwatch.ElapsedMilliseconds <= 3000) continue;
                        MessageBox.Show("更新期间无法关闭自动修补程序");
                        return true;
                    }
                }

                if (File.Exists(toName)) File.Delete(toName);
                File.Move(fromName, toName);
                Process.Start(toName, "Auto");

                return true;
            }
            catch (Exception ex)
            {
                CMain.SaveError(ex.ToString());
                
                throw;
            }
        }

        public static class RuntimePolicyHelper
        {
            public static bool LegacyV2RuntimeEnabledSuccessfully { get; private set; }

            static RuntimePolicyHelper()
            {
                //ICLRRuntimeInfo clrRuntimeInfo =
                //    (ICLRRuntimeInfo)RuntimeEnvironment.GetRuntimeInterfaceAsObject(
                //        Guid.Empty,
                //        typeof(ICLRRuntimeInfo).GUID);

                //try
                //{
                //    clrRuntimeInfo.BindAsLegacyV2Runtime();
                //    LegacyV2RuntimeEnabledSuccessfully = true;
                //}
                //catch (COMException)
                //{
                //    // This occurs with an HRESULT meaning 
                //    // "A different runtime was already bound to the legacy CLR version 2 activation policy."
                //    LegacyV2RuntimeEnabledSuccessfully = false;
                //}
            }

            [ComImport]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            [Guid("BD39D1D2-BA2F-486A-89B0-B4B0CB466891")]
            private interface ICLRRuntimeInfo
            {
                void xGetVersionString();
                void xGetRuntimeDirectory();
                void xIsLoaded();
                void xIsLoadable();
                void xLoadErrorString();
                void xLoadLibrary();
                void xGetProcAddress();
                void xGetInterface();
                void xSetDefaultStartupFlags();
                void xGetDefaultStartupFlags();

                [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
                void BindAsLegacyV2Runtime();
            }
        }

        public static void CheckResolutionSetting()
        {
            var parsedOK = DisplayResolutions.GetDisplayResolutions();
            if (!parsedOK)
            {
                MessageBox.Show("无法获取显示分辨率", "获取显示分辨率问题", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }

            if (!DisplayResolutions.IsSupported(Settings.Resolution))
            {
                MessageBox.Show($"客户端不支持 {Settings.Resolution} 将设置成默认分辨率 1024x768",
                                "无效的客户端分辨率",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);

                Settings.Resolution = (int)eSupportedResolution.w1024h768;
                Settings.Save();
            }
        }

    }
}