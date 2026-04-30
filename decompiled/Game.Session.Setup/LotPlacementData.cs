using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Setup;

internal sealed class LotPlacementData
{
	public List<WorldPos> industryCenters = new List<WorldPos>();

	public List<WorldPos> commercialCenters = new List<WorldPos>();

	public List<WorldPos> residentialCenters = new List<WorldPos>();
}
