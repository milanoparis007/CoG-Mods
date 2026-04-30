namespace Game.Session;

public class SessionInitStatus
{
	public SessionState state;

	public int target;

	public int sofar;

	public bool procGenFailed;

	public float progress
	{
		get
		{
			if (target != 0)
			{
				return (float)sofar / (float)target;
			}
			return 1f;
		}
	}

	public void Set(SessionState state, int sofar, int target)
	{
		this.state = state;
		this.target = target;
		this.sofar = sofar;
	}
}
