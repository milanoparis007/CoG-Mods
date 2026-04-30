using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Politics;

public class ElectionDayRundownAnimator : MonoBehaviour
{
	public Dictionary<EntityID, int> counted;

	public Dictionary<EntityID, Election.CandidateInfo> toCount;

	public int votesCast;

	public EntityID winner;

	public Xorshift rng;

	public const string CANDIDATE = "BG/Totals/Vote ";

	public const string PERCENTAGE_BAR = "/Percentage Bar";

	public const string PERCENTAGE_TEXT = "/Percentage Bar/Text";

	public const string VOTE_COUNT = "BG/Vote Count";

	public const string INFLUENCE = "BG/Influence";

	public const string INFLUENCE_AMT = "BG/Influence/Text";

	public const string BORDER = "BG/Border";

	public const string HEADER = "Header";

	public void SetModel(Dictionary<EntityID, Election.CandidateInfo> model, Xorshift rng, EntityID winner)
	{
		toCount = model;
		votesCast = model.Select((KeyValuePair<EntityID, Election.CandidateInfo> x) => x.Value.votes).Sum();
		counted = new Dictionary<EntityID, int>();
		foreach (KeyValuePair<EntityID, Election.CandidateInfo> item in toCount)
		{
			counted.Add(item.Key, 0);
		}
		this.rng = rng;
		this.winner = winner;
	}

	public void Update()
	{
		ElectionDayPopup activePopup = Game.serv.ui.GetActivePopup<ElectionDayPopup>();
		if (!activePopup.startCount)
		{
			return;
		}
		int num = counted.Select((KeyValuePair<EntityID, int> x) => x.Value).Sum();
		int num2 = toCount.Values.Select((Election.CandidateInfo x) => x.votes).Sum();
		if (num2 == 0)
		{
			base.gameObject.SetText("BG/Vote Count", Loc.Get("ui.election-day.winner", "winner", winner.FindEntity().data.person.FullName));
			bool flag = Game.ctx.simman.politics.GetPoliticianData(winner).relToHumanInLastElection == PoliticalRelationshipType.Sponsored;
			Fixnum humanInfluenceFrom = Game.ctx.simman.politics.GetHumanInfluenceFrom(winner);
			base.gameObject.GetChild("BG/Influence").SetActive(flag);
			if (flag)
			{
				base.gameObject.SetText("BG/Influence/Text", humanInfluenceFrom.ToString());
			}
			base.gameObject.GetChild("BG/Border").SetActive(value: true);
			base.gameObject.GetImage("BG/Border").color = (flag ? ColorConstants.WON_ELECTION_COLOR : ColorConstants.LOST_ELECTION_COLOR);
			if (flag)
			{
				base.gameObject.GetImage("Header").color = ColorConstants.WON_ELECTION_HEADER_COLOR;
			}
			activePopup.wardsFinished++;
			if (activePopup.wardsFinished == Game.ctx.simman.politics.GetWards().Count())
			{
				activePopup.RefreshResults();
			}
			UnityEngine.Object.Destroy(this);
			return;
		}
		PoliticsSettings.ElectionSettings elections = Game.serv.globals.settings.politics.elections;
		int value = rng.DieRoll(elections.voteCountChunkHigh - elections.voteCountChunkLow) + elections.voteCountChunkLow;
		EntityID key = rng.PickElement(toCount.Keys.ToList(), ((IEnumerable<Election.CandidateInfo>)toCount.Values).Select((Func<Election.CandidateInfo, float>)((Election.CandidateInfo x) => x.votes)).ToList());
		int num3 = MathUtil.ClampMax(value, toCount[key].votes);
		toCount[key].votes -= num3;
		num2 -= num3;
		counted.Increment(key, num3);
		int num4 = 1;
		foreach (KeyValuePair<EntityID, int> item in counted)
		{
			RectTransform obj = base.gameObject.GetChild("BG/Totals/Vote " + num4 + "/Percentage Bar").transform as RectTransform;
			Fixnum fixnum = new Fixnum((float)item.Value / (float)votesCast);
			obj.sizeDelta = obj.sizeDelta.SetX((float)fixnum * 100f);
			base.gameObject.SetText("BG/Totals/Vote " + num4 + "/Percentage Bar/Text", Loc.Percentage(fixnum));
			num4++;
		}
		base.gameObject.SetText("BG/Vote Count", Loc.Get("ui.election-day.votes-counted", "counted", num, "total", votesCast));
	}
}
