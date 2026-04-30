using System.Collections;
using System.Text;
using SomaSim.SION;

namespace Game.Platform;

public sealed class SaveFileContents
{
	public Hashtable data;

	public SaveFileContents(Hashtable data)
	{
		this.data = data;
	}

	public static byte[] ToBytes(SaveFileContents save)
	{
		string s = SION.Print(save.data);
		return Encoding.UTF8.GetBytes(s);
	}

	public static SaveFileContents FromBytes(byte[] bytes)
	{
		return new SaveFileContents(SION.Parse(Encoding.UTF8.GetString(bytes)) as Hashtable);
	}
}
