namespace Game.Session.Data;

public struct ReqExplanation
{
	public bool passed;

	public string message;

	public ReqExplanation(bool passed, string message)
	{
		this.passed = passed;
		this.message = message;
	}
}
