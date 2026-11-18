using System.IO;

namespace ClientPackets;

public sealed class ChangePassword : Packet
{
	public string AccountID = string.Empty;

	public string CurrentPassword = string.Empty;

	public string NewPassword = string.Empty;

	public override short Index => 4;

	protected override void ReadPacket(BinaryReader reader)
	{
		AccountID = reader.ReadString();
		CurrentPassword = reader.ReadString();
		NewPassword = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(AccountID);
		writer.Write(CurrentPassword);
		writer.Write(NewPassword);
	}
}
