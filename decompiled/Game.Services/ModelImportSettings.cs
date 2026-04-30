using System.Collections.Generic;
using Game.Core;

namespace Game.Services;

public sealed class ModelImportSettings : IValidatingSettings
{
	public LabelDictionary<LODDefinitionValues> loddefs;

	public List<LODDefinition> modelconfigs;

	public void Validate()
	{
		foreach (LODDefinition modelconfig in modelconfigs)
		{
			_ = modelconfig;
		}
	}
}
