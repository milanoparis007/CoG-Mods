using Game.Core;

namespace Game.Services;

public struct SocialInferencePayload
{
	public EntityID from;

	public EntityID to;

	public SocialLink link;

	public Label label;

	public SocialInferencePayload(EntityID from, EntityID to, SocialLink link, string label)
	{
		this.from = from;
		this.to = to;
		this.link = link;
		this.label = (Label)label;
	}

	public override string ToString()
	{
		return $"{from} to {to}: {link} / {label}";
	}
}
