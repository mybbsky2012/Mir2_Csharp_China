using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class SendMemberLocation : Packet
{
	public string MemberName;

	public Point MemberLocation;

	public override short Index => 273;

	protected override void ReadPacket(BinaryReader reader)
	{
		MemberName = reader.ReadString();
		MemberLocation = new Point(reader.ReadInt32(), reader.ReadInt32());
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(MemberName);
		writer.Write(MemberLocation.X);
		writer.Write(MemberLocation.Y);
	}
}
