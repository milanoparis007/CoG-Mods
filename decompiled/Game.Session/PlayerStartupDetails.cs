using Game.Core;
using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session;

public struct PlayerStartupDetails
{
	public PeepCreationDetails player;

	public bool tutorial;

	public Label startingSkill;

	public PlayerStartupDetails(PeepCreationDetails player, bool tutorial, Label startingSkill)
	{
		this.player = player;
		this.tutorial = tutorial;
		this.startingSkill = startingSkill;
	}

	public static PeepCreationDetails MakeRandomPeepDeets(Label eth, Xorshift rng, Gender gender = Gender.U, Skin skin = Skin.Unknown)
	{
		EthnicityDef ethnicityDef = Game.serv.globals.settings.ethnicities.FindEthnicityDef(eth);
		if (gender == Gender.U)
		{
			gender = (rng.CoinFlip() ? Gender.M : Gender.F);
		}
		int y = (int)rng.y;
		PortraitInfo portraitPieces = Game.serv.portraits.GetPortraitPieces(y, y, gender, skin, iscop: false, isfed: false, eth);
		string randomFirstName = ethnicityDef.loc.GetRandomFirstName(rng, gender);
		string randomLastName = ethnicityDef.loc.GetRandomLastName(rng, gender);
		PeepCreationDetails peepCreationDetails = new PeepCreationDetails(eth, randomFirstName, randomLastName, gender, skin, portraitPieces);
		peepCreationDetails.group = NameUtils.MakeHumanGroupName(new Xorshift(rng.y), peepCreationDetails);
		return peepCreationDetails;
	}
}
