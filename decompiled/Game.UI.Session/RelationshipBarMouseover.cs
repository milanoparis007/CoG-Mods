using Game.UI.Mouseovers;

namespace Game.UI.Session;

public sealed class RelationshipBarMouseover : BaseCustomTextMouseover
{
	protected override string ProduceText()
	{
		EntityHolderContext componentInParent = context.GetComponentInParent<EntityHolderContext>();
		if (componentInParent == null)
		{
			return null;
		}
		return PersonInfoUtil.GenerateRelationshipExplanation(componentInParent.GetTargetEntityOrNull());
	}
}
