using Game.Core;
using SomaSim.Util;

namespace Game.Services;

public sealed class LODDefinition
{
	public string model;

	public Label lod;

	public LODDefinitionValues lodoverride;

	public LODDefinitionValues GetLods()
	{
		if (lodoverride != null)
		{
			return lodoverride;
		}
		return Game.serv.globals.settings.modelimport.loddefs.FindOrNull(lod);
	}
}
