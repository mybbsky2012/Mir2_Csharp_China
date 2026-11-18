using System.Diagnostics;

namespace Shared.Helpers;

public static class FileIO
{
	public static void OpenScript(string scriptPath, bool useShellExecutable)
	{
		Process process = new Process();
		process.StartInfo = new ProcessStartInfo(scriptPath)
		{
			UseShellExecute = useShellExecutable
		};
		process.Start();
	}
}
