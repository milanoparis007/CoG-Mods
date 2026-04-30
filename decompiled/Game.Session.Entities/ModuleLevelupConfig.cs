using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class ModuleLevelupConfig
{
	public Label id;

	public Test @is;

	public int value;

	public ModuleExpansionTarget target;

	public Fixnum multiplier = 1;

	public Fixnum delta = 0;
}
