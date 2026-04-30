using Game.Core;
using SomaSim.Util;

namespace Game.Services;

public sealed class EthnicitySettings
{
	public static readonly Label DEFAULT_ETHNICITY = (Label)"am";

	public LabelDictionary<EthnicityDef> definitions = new LabelDictionary<EthnicityDef>();

	public EthnicityDef FindEthnicityDef(Label id)
	{
		EthnicityDef ethnicityDef = definitions.FindOrNull(id);
		if (ethnicityDef == null)
		{
			ethnicityDef = definitions.FindOrNull(DEFAULT_ETHNICITY);
		}
		return ethnicityDef;
	}
}
