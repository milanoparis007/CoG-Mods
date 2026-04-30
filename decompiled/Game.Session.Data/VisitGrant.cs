namespace Game.Session.Data;

public abstract class VisitGrant
{
	public enum SelectorRequest
	{
		None,
		Building,
		Crew,
		Vehicle
	}

	public abstract GrantReq RequiredContext { get; }

	public abstract void Apply(GrantContext ctx);

	public virtual string Describe(GrantContext ctx)
	{
		return null;
	}

	public virtual SelectorRequest RequestSelector()
	{
		return SelectorRequest.None;
	}
}
