using Game.Services;

namespace Game.UI;

public class GSGenerateCity : GSBase
{
	private GSGenerateCityPopup _popup;

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		_popup = Game.serv.ui.AddPopup<GSGenerateCityPopup>();
		_popup.SetText("");
	}

	public override void OnDeactivated(bool popped)
	{
		Game.serv.ui.RemovePopup(_popup);
		_popup = null;
		base.OnDeactivated(popped);
	}

	public override void Update()
	{
		base.Update();
		string cityName = Game.ctx.session.mapconfig.CityName;
		if (Game.ctx.IsBoardInit)
		{
			string key = (Game.ctx.IsSessionFromSaveFile ? "ui.init.loading" : "ui.init.mapgen");
			_popup.SetText(Loc.Get(key, "cityname", cityName));
		}
		if (Game.ctx.IsCityGen)
		{
			string text = Loc.FormatNumber(Game.ctx.clock.Now.YearsInt);
			_popup.SetText(Loc.Get("ui.init.citygen", "cityname", cityName, "year", text));
		}
		if (Game.ctx.IsCityGenDone)
		{
			_popup.SetText(Loc.Get("ui.init.final"));
		}
	}
}
