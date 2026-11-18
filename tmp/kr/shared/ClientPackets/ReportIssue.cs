using System.IO;

namespace ClientPackets;

public sealed class ReportIssue : Packet
{
	public byte[] Image;

	public int ImageSize;

	public int ImageChunk;

	public string Message;

	public override short Index => 131;

	protected override void ReadPacket(BinaryReader reader)
	{
		Image = reader.ReadBytes(reader.ReadInt32());
		ImageSize = reader.ReadInt32();
		ImageChunk = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Image.Length);
		writer.Write(Image);
		writer.Write(ImageSize);
		writer.Write(ImageChunk);
	}
}
