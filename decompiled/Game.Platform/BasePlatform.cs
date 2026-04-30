using System;
using System.Collections;
using System.Collections.Generic;
using Game.Services;

namespace Game.Platform;

public abstract class BasePlatform
{
	public const string METADATA_FILE = "metadata";

	public const string GAMEDATA_FILE = "savedata";

	public const string SCREENIE_FILE = "screenshot";

	public const ResourceType METADATA_TYPE = ResourceType.SimFile;

	public const ResourceType GAMEDATA_TYPE = ResourceType.SimZip;

	public const ResourceType SCREENIE_TYPE = ResourceType.PngFile;

	public abstract bool LoadPrefsAfterEngagement { get; }

	public virtual IEnumerator Initialize()
	{
		yield break;
	}

	public virtual void Update()
	{
	}

	public virtual void Shutdown()
	{
	}

	public virtual byte[] ReadStreamingAssetBytes(string path)
	{
		return new byte[0];
	}

	public virtual IEnumerable<string> GetStreamingAssetFileNames(string directory, ResourceType type)
	{
		return new List<string>();
	}

	public virtual IEnumerator GetSaveFiles(Action<List<IPlatformSaveSlotDescriptor>> onSuccess, Action<kGetFileResult> onFailure)
	{
		onFailure(kGetFileResult.GenericFailure);
		yield break;
	}

	public virtual IEnumerator SaveGame(PlatformSaveSlotRequest saveRequest, Action onSuccess, Action<kSaveFileResult> onFailure)
	{
		onFailure(kSaveFileResult.GenericFailure);
		yield break;
	}

	public virtual IEnumerator SavePrefs(PlatformSavePrefsRequest saveRequest, Action onSuccess, Action<kSaveFileResult> onFailure)
	{
		onFailure(kSaveFileResult.GenericFailure);
		yield break;
	}

	public virtual IEnumerator LoadGame(IPlatformSaveSlotDescriptor desc, Action<byte[]> onSuccess, Action<kLoadFileResult> onFailure)
	{
		onFailure(kLoadFileResult.GenericFailure);
		yield break;
	}

	public virtual IEnumerator LoadPrefs(PlatformLoadPrefsRequest loadRequest, Action<byte[]> onSuccess, Action<kLoadFileResult> onFailure)
	{
		onFailure(kLoadFileResult.GenericFailure);
		yield break;
	}

	public virtual IEnumerator DeleteGame(IPlatformSaveSlotDescriptor desc, Action onSuccess, Action<kDeleteFileResult> onFailure)
	{
		onFailure(kDeleteFileResult.GenericFailure);
		yield break;
	}
}
