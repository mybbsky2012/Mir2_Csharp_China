using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using System;
using System.IO;
using System.Globalization;

string asm = args[0];
string outDir = args.Length > 1 ? args[1] : "out";
string filter = args.Length > 2 ? args[2] : null;

var settings = new DecompilerSettings(ICSharpCode.Decompiler.CSharp.LanguageVersion.CSharp11_0);
var decompiler = new CSharpDecompiler(asm, settings);

var ts = decompiler.TypeSystem;
int n = 0;
foreach (var ns in ts.RootNamespace.ChildNamespaces)
{
    Walk(ns);
}
void Walk(INamespace ns)
{
    foreach (var t in ns.Types)
    {
        if (filter != null && !t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
        try
        {
            string fullName = t.FullName.Replace('`', '_').Replace('/', '_');
            string dir = Path.Combine(outDir, ns.FullName.Replace('.', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(dir);
            string code = decompiler.DecompileTypeAsString(new FullTypeName(t.FullName));
            File.WriteAllText(Path.Combine(dir, t.Name.Replace('`','_') + ".cs"), code);
            n++;
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + t.FullName + ": " + ex.Message); }
    }
    foreach (var c in ns.ChildNamespaces) Walk(c);
}
Console.WriteLine("decompiled " + n + " types -> " + Path.GetFullPath(outDir));
