using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session;

public class InfoTabSubview : Subview<PersonInfoModel, PersonInfoDialog, PersonInfoController>
{
	private const string MAIN_INFO = "Header/Info";

	private const string RELATIONSHIPS = "Relationships/Text";

	private const string TRAITS = "Columns/Traits";

	private const string SKILLS = "Columns/Skills";

	public InfoTabSubview(GameObject go, PersonInfoController controller)
		: base(go, "Panel Info", controller)
	{
	}

	public override void RefreshSubview()
	{
		Entity entity = Model.entity;
		panel.SetText("Relationships/Text", PersonInfoUtil.GenerateRelationshipExplanation(entity));
		panel.SetText("Columns/Traits", (Model.data.relToHuman != null) ? Model.data.traitsShort : Loc.Get("ui.subview.infotab.no-traits"));
		panel.SetTextOrHide("Header/Info", Model.data.levelups);
	}
}
