using System;
using Game.Session;

namespace Game.UI;

public class GSStartSession : GSBase
{
	private SessionPrepResults _sessionData;

	public GSStartSession(SessionPrepResults sessionData)
	{
		_sessionData = sessionData;
	}

	public override void OnPushed(object stack)
	{
		base.OnPushed(stack);
		try
		{
			Game.ctx = new SessionContext();
			Game.ctx.Initialize(_sessionData);
		}
		catch (Exception ex)
		{
			Logger.Error("GSStartSession error: " + ex);
		}
	}

	public override void OnPopped()
	{
		Game.serv.input.Flush();
		Game.serv.sequencer.StopAll();
		if (Game.ctx != null)
		{
			Game.ctx.Release();
			Game.ctx = null;
		}
		base.OnPopped();
	}
}
