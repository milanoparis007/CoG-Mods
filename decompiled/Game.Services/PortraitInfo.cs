namespace Game.Services;

public class PortraitInfo
{
	public int hash;

	public int hathash;

	public string hatmask;

	public string hat;

	public string head;

	public string bust;

	public bool iscop;

	public bool isfed;

	public override string ToString()
	{
		return $"PORTRAIT {hash} {hathash} {hatmask} {hat} {head} {bust} {iscop} {isfed}";
	}
}
