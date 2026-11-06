using System;
using System.Reflection;
using System.Windows.Forms;

// 验证 CustomFormControl.dll 里的 ListViewNF：虚拟模式下点表头不再调 Sort() 抛异常
class Program
{
    [STAThread]
    static void Main()
    {
        var list = new CustomFormControl.ListViewNF();
        list.VirtualMode = true;

        // 反射调私有的 ListViewNF_ColumnClick（表头点击的真实入口）
        var m = typeof(CustomFormControl.ListViewNF).GetMethod("ListViewNF_ColumnClick",
            BindingFlags.NonPublic | BindingFlags.Instance);

        try
        {
            m.Invoke(list, new object[] { list, new ColumnClickEventArgs(0) });
            Console.WriteLine("[通过] 虚拟模式点表头不再触发 Sort()，无异常");
            Environment.Exit(0);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
        {
            Console.WriteLine("[失败] 仍然抛出：虚拟 ListView 中不允许排序 —— DLL 是旧的");
            Environment.Exit(1);
        }
    }
}
