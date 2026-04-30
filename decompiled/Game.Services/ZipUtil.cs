using System.Collections.Generic;
using System.IO;
using Ionic.Zip;

namespace Game.Services;

public static class ZipUtil
{
	public static byte[] CreateZipFile(string filename, byte[] bytes)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using (MemoryStream stream = new MemoryStream(bytes))
		{
			using ZipFile zipFile = new ZipFile();
			zipFile.AddEntry(filename, stream);
			zipFile.Save(memoryStream);
		}
		memoryStream.Position = 0L;
		return memoryStream.ToArray();
	}

	public static (string filename, byte[] contents) ExtractFirstFileFromZipFile(byte[] bytes)
	{
		string item = "";
		using MemoryStream memoryStream = new MemoryStream();
		using (MemoryStream zipStream = new MemoryStream(bytes))
		{
			using ZipFile zipFile = ZipFile.Read(zipStream);
			using IEnumerator<ZipEntry> enumerator = zipFile.GetEnumerator();
			if (enumerator.MoveNext())
			{
				ZipEntry current = enumerator.Current;
				current.Extract(memoryStream);
				item = current.FileName;
			}
		}
		memoryStream.Position = 0L;
		byte[] item2 = memoryStream.ToArray();
		return (filename: item, contents: item2);
	}
}
