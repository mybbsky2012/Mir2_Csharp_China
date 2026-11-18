using System.IO;

namespace ClientPackets;

public sealed class Login : Packet
{
	public string AccountID = string.Empty;

	public string Password = string.Empty;

	public override short Index => 5;

	protected override void ReadPacket(BinaryReader reader)
	{
		AccountID = reader.ReadString();
		Password = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(AccountID);
		writer.Write(Password);
	}
}
