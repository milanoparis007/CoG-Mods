using System;
using System.IO;
using UnityEngine;

namespace Game.Services.Mods;

public class ModsLoader<T> : IModsLoader where T : class, IModsProvider, new()
{
	public T Handler { get; private set; }

	public string DataFileName => "Data.txt";

	public void Initialize()
	{
		Handler = new T();
		Handler.Initialize();
	}

	public void Release()
	{
		Handler.Release();
		Handler = null;
	}

	public string MakeModPreviewPath(ModID modid)
	{
		return Handler.MakeModPreviewPath(modid);
	}

	public string MakeModDataDirPath(ModID modid)
	{
		return Handler.MakeModDataDirPath(modid);
	}

	public void StartLoadingModDefinitions(Action<ModID, ModMetadata> itemProcessor)
	{
		Handler.StartLoadingModDefinitions(itemProcessor);
	}

	public ModData LoadDataFile(ModID modid)
	{
		string text = ReadTextFile(modid, DataFileName, isMissingFileOkay: true);
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		return FileUtil.DeserializeFromString<ModData>(text);
	}

	private string ReadTextFile(ModID modid, string filename, bool isMissingFileOkay)
	{
		string text = Path.Combine(MakeModDataDirPath(modid), filename);
		try
		{
			if (File.Exists(text))
			{
				return File.ReadAllText(text);
			}
		}
		catch (Exception arg)
		{
			Logger.Error($"Failed to load text file {text}\n{arg}");
		}
		if (!isMissingFileOkay)
		{
			Logger.Error("Mod file missing: " + text);
		}
		return null;
	}

	public ModTexture LoadModPreview(ModID modid)
	{
		return LoadImage(MakeModPreviewPath(modid));
	}

	private ModTexture LoadImage(string path)
	{
		Texture2D texture2D = new Texture2D(64, 64, TextureFormat.ARGB32, mipChain: true);
		texture2D.anisoLevel = 0;
		texture2D.filterMode = FilterMode.Trilinear;
		string name = "";
		try
		{
			byte[] data = File.ReadAllBytes(path);
			texture2D.LoadImage(data);
			name = Path.GetFileName(path);
		}
		catch (Exception)
		{
		}
		return new ModTexture
		{
			name = name,
			texture = texture2D
		};
	}
}
