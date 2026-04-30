using System;
using System.Collections;
using System.IO;
using System.Text;
using SomaSim.SION;

namespace Game.Services;

public static class FileUtil
{
	public static string MakeExtension(ResourceType type)
	{
		switch (type)
		{
		case ResourceType.JSONFile:
			return ".json";
		case ResourceType.SimFile:
			return ".sim";
		case ResourceType.SimZip:
			return ".sim.zip";
		case ResourceType.TextFile:
			return ".txt";
		case ResourceType.TextZip:
			return ".txt.zip";
		case ResourceType.PngFile:
			return ".png";
		case ResourceType.BotlFile:
			return ".bot";
		default:
			Logger.Error("Invalid type: " + type);
			return "";
		}
	}

	public static string MakeFileName(string root, ResourceType type)
	{
		return root + MakeExtension(type);
	}

	public static Hashtable ParseAsHashtable(string contents, ResourceType type)
	{
		switch (type)
		{
		case ResourceType.SimFile:
			return SION.Parse(contents) as Hashtable;
		case ResourceType.JSONFile:
			return JSON.JsonDecode(contents) as Hashtable;
		default:
			Logger.Warning("Don't know how to parse file of type: " + type);
			return null;
		}
	}

	public static bool ParseTabSeparatedFile(string contents, Action<int, string[]> lineCallback)
	{
		if (string.IsNullOrEmpty(contents))
		{
			return false;
		}
		int num = 0;
		StringReader stringReader = new StringReader(contents);
		string text;
		while ((text = stringReader.ReadLine()) != null)
		{
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			string[] array = text.Split('\t');
			for (int i = 0; i < array.Length; i++)
			{
				string text2 = array[i];
				text2 = text2.Replace("\\n", "\n").Replace("\\r", "\r");
				if (text2.StartsWith("\"") && text2.Length > 2)
				{
					text2 = text2.Substring(1, text2.Length - 2).Replace("\"\"", "\"");
				}
				array[i] = text2;
			}
			lineCallback(num, array);
			num++;
		}
		return true;
	}

	public static T DeserializeFromString<T>(string contents)
	{
		return Game.serv.serializer.instance.Deserialize<T>(SION.Parse(contents));
	}

	public static string SerializeToString(object def)
	{
		return SION.Print(Game.serv.serializer.instance.Serialize(def));
	}

	public static void SerializeToTextFile(string path, object def)
	{
		File.WriteAllText(path, SerializeToString(def), Encoding.UTF8);
	}

	public static T DeserializeFromTextFile<T>(string path)
	{
		return DeserializeFromString<T>(File.ReadAllText(path, Encoding.UTF8));
	}
}
