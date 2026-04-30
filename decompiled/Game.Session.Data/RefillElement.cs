using Game.Core;
using SomaSim.Util;

namespace Game.Session.Data;

public struct RefillElement
{
	public Label id;

	public Label eth;

	public Fixnum below;

	public Fixnum get;

	public bool needsEthPack;
}
