using System.IO;

namespace ClientPackets;

public sealed class NewCharacter : Packet
{
	public string Name = string.Empty;

	public MirGender Gender;

	public MirClass Class;

	public override short Index => 6;

	protected override void ReadPacket(BinaryReader reader)
	{
		Name = reader.ReadString();
		Gender = (MirGender)reader.ReadByte();
		Class = (MirClass)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Name);
		writer.Write((byte)Gender);
		writer.Write((byte)Class);
	}
}
