namespace Game.Session.Player;

public sealed class OutpostClosed
{
	public OutpostID outpostId;

	public int count;

	public OutpostClosed()
	{
	}

	public OutpostClosed(OutpostID outpostId, int count)
	{
		this.outpostId = outpostId;
		this.count = count;
	}
}
