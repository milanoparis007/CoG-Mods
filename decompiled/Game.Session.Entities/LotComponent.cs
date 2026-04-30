namespace Game.Session.Entities;

public sealed class LotComponent : BaseComponent
{
	public LotConfig Config => _baseConfig as LotConfig;
}
