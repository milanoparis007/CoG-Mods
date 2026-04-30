using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Services;

public static class StreamingAssetsUtil
{
	private static void Load<T>(string resource, ResourceType type, Func<byte[], T> process, Action<T> callback)
	{
		string path = MakePath(resource, type);
		Game.serv.sequencer.StartThreadedProducerConsumer(() => process(Game.platform.ReadStreamingAssetBytes(path)), delegate(T bytes)
		{
			WrapCallback(resource, callback, bytes, path);
		}, "load/" + resource);
	}

	private static void WrapCallback<T>(string resource, Action<T> callback, T results, string path)
	{
		try
		{
			callback(results);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to process file ", path, "\n", ex);
		}
	}

	private static string BytesToString(byte[] buffer)
	{
		if (buffer.Length == 0)
		{
			return string.Empty;
		}
		if (buffer.Length > 3 && buffer[0] == 239 && buffer[1] == 187 && buffer[2] == 191)
		{
			return Encoding.UTF8.GetString(buffer, 3, buffer.Length - 3);
		}
		return Encoding.UTF8.GetString(buffer);
	}

	public static string MakePath(string resource, ResourceType type)
	{
		string path = FileUtil.MakeFileName(resource, type);
		return Path.Combine(Application.streamingAssetsPath, path);
	}

	public static void LoadAsBytes(string resource, ResourceType type, Action<byte[]> callback)
	{
		Load(resource, type, (byte[] bytes) => bytes, callback);
	}

	public static void LoadAsString(string resource, ResourceType type, Action<string> callback)
	{
		Load(resource, type, BytesToString, callback);
	}

	public static void LoadAsHashtable(string resource, ResourceType type, Action<Hashtable> callback)
	{
		Load(resource, type, (byte[] bytes) => FileUtil.ParseAsHashtable(BytesToString(bytes), type), callback);
	}

	public static void LoadAsDeserialized<T>(string resource, ResourceType type, Action<T> callback) where T : class
	{
		Load(resource, ResourceType.SimFile, Deserializer, callback);
		T Deserializer(byte[] bytes)
		{
			Hashtable hashtable = FileUtil.ParseAsHashtable(BytesToString(bytes), type);
			if (hashtable == null)
			{
				return null;
			}
			return Game.serv.serializer.CloneInstance().Deserialize<T>(hashtable);
		}
	}

	public static void LoadTextFromZip(string resource, Action<string> callback)
	{
		Load(resource, ResourceType.TextZip, (byte[] bytes) => BytesToString(ZipUtil.ExtractFirstFileFromZipFile(bytes).contents), callback);
	}
}
