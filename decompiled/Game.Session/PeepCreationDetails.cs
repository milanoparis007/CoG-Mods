using System.Collections.Generic;
using Game.Core;
using Game.Services;

namespace Game.Session;

public struct PeepCreationDetails
{
	public Label ethnicity;

	public string fname;

	public string lname;

	public string group;

	public Gender gender;

	public Skin skin;

	public PortraitInfo portrait;

	public List<Label> traits;

	public PeepCreationDetails(Label ethnicity, string fname, string lname, Gender gender, Skin skin = Skin.Unknown, PortraitInfo portrait = null, List<Label> traits = null, string group = null)
	{
		this.ethnicity = ethnicity;
		this.fname = fname;
		this.lname = lname;
		this.gender = gender;
		this.skin = skin;
		this.portrait = portrait;
		this.traits = traits;
		this.group = group;
	}
}
