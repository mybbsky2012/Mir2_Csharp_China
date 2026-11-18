using System.IO;

namespace ClientPackets;

public sealed class SendMail : Packet
{
	public string Name;

	public string Message;

	public uint Gold;

	public ulong[] ItemsIdx = new ulong[5];

	public bool Stamped;

	public override short Index => 114;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
		Message = reader.ReadString();
		Gold = reader.ReadUInt32();
		for (int i = 0; i < 5; i++)
		{
			ItemsIdx[i] = reader.ReadUInt64();
		}
		Stamped = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
		writer.Write(Message);
		writer.Write(Gold);
		for (int i = 0; i < 5; i++)
		{
			writer.Write(ItemsIdx[i]);
		}
		writer.Write(Stamped);
	}
}
