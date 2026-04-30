using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Services.Mods;

public class LocalModsProvider : DefaultFilesystemProvider, IModsProvider
{
	public const string METADATA_FILE = "Description.txt";

	public const string PREVIEW_FILE = "Preview.png";

	public const string DATA_SUBDIRECTORY = "Data";

	public LocalModsProvider()
		: base("mods")
	{
	}

	public string MakeModPreviewPath(ModID modid)
	{
		return Path.Combine(GetModRootPath(modid), "Preview.png");
	}

	public string MakeModDataDirPath(ModID modid)
	{
		return Path.Combine(GetModRootPath(modid), "Data");
	}

	public List<ModID> GetModIds()
	{
		return (from path in Directory.GetDirectories(_subdirpath)
			select new DirectoryInfo(path).Name into dirname
			select ModID.MakeLocal(dirname)).ToList();
	}

	public void StartLoadingModDefinitions(Action<ModID, ModMetadata> callback)
	{
		foreach (ModID modId in GetModIds())
		{
			LoadModDefinition(modId, callback);
		}
	}

	private string GetModRootPath(ModID modid)
	{
		return Path.Combine(_subdirpath, modid.id);
	}

	private void LoadModDefinition(ModID modid, Action<ModID, ModMetadata> callback)
	{
		ModMetadata modMetadata = null;
		string text = Path.Combine(GetModRootPath(modid), "Description.txt");
		try
		{
			if (File.Exists(text))
			{
				modMetadata = FileUtil.DeserializeFromTextFile<ModMetadata>(text);
				modMetadata.modid = modid;
			}
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to load Description.txt file at " + text + "\n" + ex.Message);
			return;
		}
		callback(modid, modMetadata);
	}

	public bool SaveModDefinition(ModMetadata def)
	{
		string text = Path.Combine(GetModRootPath(def.modid), "Description.txt");
		try
		{
			if (!File.Exists(text))
			{
				Logger.Error(string.Format("Missing {0} file for mod id {1}", "Description.txt", def.modid));
				return false;
			}
			FileUtil.SerializeToTextFile(text, def);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to load Description.txt file at " + text + "\n" + ex.Message);
		}
		return false;
	}

	public bool DeleteModPermanently(ModMetadata def)
	{
		if (!def.CanBeDeleted)
		{
			Logger.Error($"Cannot delete non-local mod: {def}");
			return false;
		}
		string modRootPath = GetModRootPath(def.modid);
		string path = Path.Combine(modRootPath, "Data");
		try
		{
			if (!Directory.Exists(modRootPath) || !Directory.Exists(path))
			{
				Logger.Error($"Corrupt mod directory structure for mod id {def.modid}");
				return false;
			}
			FileInfo[] files = new DirectoryInfo(path).GetFiles();
			for (int i = 0; i < files.Length; i++)
			{
				File.Delete(files[i].FullName);
			}
			Directory.Delete(path);
			files = new DirectoryInfo(modRootPath).GetFiles();
			for (int i = 0; i < files.Length; i++)
			{
				File.Delete(files[i].FullName);
			}
			Directory.Delete(modRootPath);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to delete mod at " + modRootPath + "\n" + ex.Message);
		}
		return false;
	}

	public bool CreateMod(ModMetadata meta, ModData data, byte[] preview, string datafilename)
	{
		try
		{
			string modRootPath = GetModRootPath(meta.modid);
			string text = Path.Combine(modRootPath, "Data");
			Directory.CreateDirectory(modRootPath);
			Directory.CreateDirectory(text);
			FileUtil.SerializeToTextFile(Path.Combine(modRootPath, "Description.txt"), meta);
			FileUtil.SerializeToTextFile(Path.Combine(text, datafilename), data);
			File.WriteAllBytes(MakeModPreviewPath(meta.modid), preview);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error($"Failed to create mod {meta.modid}\n{ex.Message}");
			return false;
		}
	}
}
