using Game.Session;

namespace Game.UI.Session;

public sealed class SpriteAtlasManager : ISubManager<HUDManager>
{
	public SpriteAtlasCache portraits;

	public SpriteAtlasCache photos;

	public SpriteAtlasCache moduleIcons;

	public SpriteAtlasCache moduleCards;

	public SpriteAtlasCache uiatlas;

	public SpriteAtlasCache tutorial;

	public void Initialize(HUDManager manager)
	{
		portraits = new SpriteAtlasCache("UI Images/Portraits Atlas");
		photos = new SpriteAtlasCache("UI Images/Photos Atlas");
		moduleIcons = new SpriteAtlasCache("UI Images/Business Atlas");
		moduleCards = new SpriteAtlasCache("UI Images/Module Cards");
		uiatlas = new SpriteAtlasCache("UI Images/UI Atlas");
		tutorial = new SpriteAtlasCache("UI Images/Tutorial");
	}

	public void Release()
	{
		portraits.Reset();
		photos.Reset();
		moduleIcons.Reset();
		moduleCards.Reset();
		uiatlas.Reset();
		tutorial.Reset();
	}
}
