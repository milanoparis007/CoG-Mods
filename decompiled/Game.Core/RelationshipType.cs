namespace Game.Core;

public enum RelationshipType : byte
{
	None = 0,
	Self = 1,
	Spouse = 10,
	Child = 20,
	Mother = 30,
	Father = 40,
	Sibling = 50,
	MotherSib = 60,
	FatherSib = 70,
	SibChild = 80,
	Cousin = 90,
	Acquaintance = 100
}
