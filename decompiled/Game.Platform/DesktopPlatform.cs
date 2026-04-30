using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Services;
using UnityEngine;

namespace Game.Platform;

public class DesktopPlatform : BasePlatform
{
	private DirectoryInfo _saveDirInfo;

	private DirectoryInfo _prefsDirInfo;

	private string metadataFile = FileUtil.MakeFileName("metadata", ResourceType.SimFile);

	private string gamedataFile = FileUtil.MakeFileName("savedata", ResourceType.SimZip);

	private string screenieFile = FileUtil.MakeFileName("screenshot", ResourceType.PngFile);

	public override bool LoadPrefsAfterEngagement => false;

	public override IEnumerator Initialize()
	{
		string path = Path.Combine(Application.persistentDataPath, "saves");
		string path2 = Path.Combine(Application.persistentDataPath, "prefs");
		_saveDirInfo = new DirectoryInfo(path);
		if (!_saveDirInfo.Exists)
		{
			Directory.CreateDirectory(_saveDirInfo.FullName);
		}
		_prefsDirInfo = new DirectoryInfo(path2);
		if (!_prefsDirInfo.Exists)
		{
			Directory.CreateDirectory(_prefsDirInfo.FullName);
		}
		yield break;
	}

	public override IEnumerator GetSaveFiles(Action<List<IPlatformSaveSlotDescriptor>> onSuccess, Action<kGetFileResult> onFailure)
	{
		List<IPlatformSaveSlotDescriptor> list = new List<IPlatformSaveSlotDescriptor>();
		bool flag = true;
		try
		{
			foreach (DirectoryInfo item2 in _saveDirInfo.GetDirectories().ToList())
			{
				List<FileInfo> list2 = item2.GetFiles().ToList();
				if (list2.Count == 0)
				{
					continue;
				}
				SaveFileMetadata saveFileMetadata = null;
				int num = 0;
				Texture2D texture2D = null;
				foreach (FileInfo item3 in list2)
				{
					if (item3.Name == metadataFile)
					{
						byte[] bytes = File.ReadAllBytes(item3.FullName);
						saveFileMetadata = SaveFileMetadata.Deserialize(Encoding.UTF8.GetString(bytes));
					}
					else if (item3.Name == gamedataFile)
					{
						num = (int)item3.Length;
					}
					else if (item3.Name == screenieFile)
					{
						byte[] data = File.ReadAllBytes(item3.FullName);
						Texture2D texture2D2 = new Texture2D(1, 1);
						if (texture2D2.LoadImage(data))
						{
							texture2D = texture2D2;
						}
					}
				}
				if (num == 0)
				{
					Debug.LogError("Could not find savedata for slot " + item2.FullName);
				}
				if (saveFileMetadata == null)
				{
					Debug.LogError("Could not find metadata for slot " + item2.FullName);
				}
				if (texture2D == null)
				{
					Debug.LogError("Could not find preview image for slot " + item2.FullName);
				}
				if (num > 0 && saveFileMetadata != null && texture2D != null)
				{
					DesktopSaveSlotDescriptor item = new DesktopSaveSlotDescriptor
					{
						Directory = item2.Name,
						Metadata = saveFileMetadata,
						SizeInBytes = num,
						PreviewImage = texture2D
					};
					list.Add(item);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Warning("Failed to get files: " + ex);
			flag = false;
		}
		if (flag)
		{
			onSuccess(list);
		}
		else
		{
			onFailure(kGetFileResult.GenericFailure);
		}
		yield break;
	}

	public override IEnumerator SaveGame(PlatformSaveSlotRequest saveRequest, Action onSuccess, Action<kSaveFileResult> onFailure)
	{
		byte[] saveData;
		byte[] SENTINEL = (saveData = new byte[1] { 42 });
		string innerFileName = FileUtil.MakeFileName("savedata", ResourceType.SimFile);
		Game.serv.sequencer.StartThreadedProducerConsumer(() => ZipUtil.CreateZipFile(innerFileName, SaveFileContents.ToBytes(saveRequest.savedata)), delegate(byte[] data)
		{
			saveData = data;
		});
		while (saveData == SENTINEL)
		{
			yield return null;
		}
		kSaveFileResult kSaveFileResult2 = kSaveFileResult.Success;
		try
		{
			if (saveData == null)
			{
				throw new InvalidOperationException("Failed to serialize save request");
			}
			string text = Path.Combine(_saveDirInfo.FullName, saveRequest.guid);
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
			Logger.LogAlways("Saving to file " + text);
			File.WriteAllBytes(Path.Combine(text, gamedataFile), saveData);
			File.WriteAllBytes(Path.Combine(text, screenieFile), saveRequest.texture);
			string path = Path.Combine(text, metadataFile);
			string s = SaveFileMetadata.Serialize(saveRequest.metadata);
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			File.WriteAllBytes(path, bytes);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to save file " + saveRequest.metadata.slotId + ", " + ex);
			kSaveFileResult2 = kSaveFileResult.GenericFailure;
		}
		if (kSaveFileResult2 == kSaveFileResult.Success)
		{
			onSuccess();
		}
		else
		{
			onFailure(kSaveFileResult2);
		}
	}

	public override IEnumerator SavePrefs(PlatformSavePrefsRequest saveRequest, Action onSuccess, Action<kSaveFileResult> onFailure)
	{
		kSaveFileResult kSaveFileResult2 = kSaveFileResult.Success;
		try
		{
			string text = Path.Combine(_prefsDirInfo.FullName, saveRequest.name);
			Logger.LogAlways("Saving prefs to path " + text);
			File.WriteAllBytes(text, saveRequest.data);
		}
		catch (Exception ex)
		{
			Logger.Warning("Failed to save prefs " + saveRequest.name + ", " + ex);
			kSaveFileResult2 = kSaveFileResult.GenericFailure;
		}
		if (kSaveFileResult2 == kSaveFileResult.Success)
		{
			onSuccess();
		}
		else
		{
			onFailure(kSaveFileResult2);
		}
		yield break;
	}

	public override IEnumerator LoadGame(IPlatformSaveSlotDescriptor desc, Action<byte[]> onSuccess, Action<kLoadFileResult> onFailure)
	{
		kLoadFileResult kLoadFileResult2 = kLoadFileResult.Success;
		byte[] obj = null;
		try
		{
			string path = Path.Combine(_saveDirInfo.FullName, desc.Directory, gamedataFile);
			if (File.Exists(path))
			{
				obj = ZipUtil.ExtractFirstFileFromZipFile(File.ReadAllBytes(path)).contents;
			}
			else
			{
				kLoadFileResult2 = kLoadFileResult.NoData;
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("Error in LoadGame: " + ex.Message);
			kLoadFileResult2 = kLoadFileResult.GenericFailure;
		}
		if (kLoadFileResult2 == kLoadFileResult.Success)
		{
			onSuccess(obj);
		}
		else
		{
			onFailure(kLoadFileResult2);
		}
		yield break;
	}

	public override IEnumerator LoadPrefs(PlatformLoadPrefsRequest loadRequest, Action<byte[]> onSuccess, Action<kLoadFileResult> onFailure)
	{
		kLoadFileResult kLoadFileResult2 = kLoadFileResult.Success;
		byte[] obj = null;
		try
		{
			string path = Path.Combine(_prefsDirInfo.FullName, loadRequest.name);
			if (File.Exists(path))
			{
				obj = File.ReadAllBytes(path);
			}
			else
			{
				kLoadFileResult2 = kLoadFileResult.NoData;
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("Error in LoadPrefs: " + ex.Message);
			kLoadFileResult2 = kLoadFileResult.GenericFailure;
		}
		if (kLoadFileResult2 == kLoadFileResult.Success)
		{
			onSuccess(obj);
		}
		else
		{
			onFailure(kLoadFileResult2);
		}
		yield break;
	}

	public override byte[] ReadStreamingAssetBytes(string path)
	{
		try
		{
			return File.ReadAllBytes(path);
		}
		catch (Exception ex)
		{
			Debug.LogError("Failed to read bytes from " + path + "\n" + ex);
			return null;
		}
	}

	public override IEnumerable<string> GetStreamingAssetFileNames(string directory, ResourceType type)
	{
		if (Application.isMobilePlatform)
		{
			Logger.Error("Resource discovery not optimized for mobile, might not work with pack files");
		}
		string path = Path.Combine(Application.streamingAssetsPath, directory);
		string extension = FileUtil.MakeExtension(type).ToLowerInvariant();
		return (from text in Directory.GetFiles(path)
			where text.ToLowerInvariant().EndsWith(extension)
			select text.Replace(Application.streamingAssetsPath, "").Replace(extension, "") into text
			select (text.Length <= 1 || text[0] != Path.DirectorySeparatorChar) ? text : text.Substring(1)).ToList();
	}

	public override IEnumerator DeleteGame(IPlatformSaveSlotDescriptor desc, Action onSuccess, Action<kDeleteFileResult> onFailure)
	{
		kDeleteFileResult kDeleteFileResult2 = kDeleteFileResult.Success;
		string path = Path.Combine(_saveDirInfo.FullName, desc.Directory);
		try
		{
			if (Directory.Exists(path))
			{
				string[] files = Directory.GetFiles(path);
				for (int i = 0; i < files.Length; i++)
				{
					File.Delete(files[i]);
				}
				Directory.Delete(path);
			}
			else
			{
				kDeleteFileResult2 = kDeleteFileResult.NoDirectory;
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("Error in DeleteGame: " + ex.Message);
			kDeleteFileResult2 = kDeleteFileResult.GenericFailure;
		}
		if (kDeleteFileResult2 == kDeleteFileResult.Success)
		{
			onSuccess();
		}
		else
		{
			onFailure(kDeleteFileResult2);
		}
		yield break;
	}
}
