using System;

namespace Game.Services.Mods;

public interface IModsLoader
{
	string DataFileName { get; }

	ModData LoadDataFile(ModID modid);

	ModTexture LoadModPreview(ModID modid);

	string MakeModDataDirPath(ModID modid);

	string MakeModPreviewPath(ModID modid);

	void StartLoadingModDefinitions(Action<ModID, ModMetadata> itemProcessor);
}
