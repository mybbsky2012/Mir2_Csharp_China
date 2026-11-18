using System;
using System.IO;

namespace ClientPackets;

public sealed class NewAccount : Packet
{
	public string AccountID = string.Empty;

	public string Password = string.Empty;

	public DateTime BirthDate;

	public string UserName = string.Empty;

	public string SecretQuestion = string.Empty;

	public string SecretAnswer = string.Empty;

	public string EMailAddress = string.Empty;

	public override short Index => 3;

	protected override void ReadPacket(BinaryReader reader)
	{
		AccountID = reader.ReadString();
		Password = reader.ReadString();
		BirthDate = DateTime.FromBinary(reader.ReadInt64());
		UserName = reader.ReadString();
		SecretQuestion = reader.ReadString();
		SecretAnswer = reader.ReadString();
		EMailAddress = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(AccountID);
		writer.Write(Password);
		writer.Write(BirthDate.ToBinary());
		writer.Write(UserName);
		writer.Write(SecretQuestion);
		writer.Write(SecretAnswer);
		writer.Write(EMailAddress);
	}
}
