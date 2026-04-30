using System;
using System.Collections.Generic;

namespace Game.Services.Mods;

public interface IModsProvider
{
	void Initialize();

	void Release();

	List<ModID> GetModIds();

	void StartLoadingModDefinitions(Action<ModID, ModMetadata> callback);

	string MakeModPreviewPath(ModID modid);

	string MakeModDataDirPath(ModID modid);
}
