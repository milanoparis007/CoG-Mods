using System;
using Game.Core;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Services;

public sealed class SerializerService : AbstractService
{
	public Serializer instance;

	public override void OnCreated()
	{
		instance = new Serializer();
		instance.Options.OnSpuriousDataCallback = delegate(string a, Type b)
		{
			Logger.Error($"Unexpected key: {a} in type {b}");
		};
		instance.Options.OnUnknownTypeCallback = delegate(string a)
		{
			Logger.Error("Unknown type: " + a);
		};
		instance.Options.OnSerializationException = delegate(string a, Exception b)
		{
			Logger.Error($"Serialization error: {a} - {b}");
		};
		instance.AddCustomSerializer(Label.Serialize, Label.Deserialize);
		instance.AddCustomSerializer(Fixnum.Serialize, Fixnum.Deserialize);
		instance.AddCustomSerializer(EntityID.Serialize, EntityID.Deserialize);
		instance.AddCustomSerializer(PlayerID.Serialize, PlayerID.Deserialize);
		instance.AddCustomSerializer(XorshiftSerializer.Serialize, XorshiftSerializer.Deserialize);
		instance.AddCustomSerializer(DateTimeSerializer.Serialize, DateTimeSerializer.Deserialize);
		instance.AddImplicitNamespace("Game.Services", isNamespace: true);
		instance.AddImplicitNamespace("Game.Session", isNamespace: true);
		instance.AddImplicitNamespace("Game.Session.Data", isNamespace: true);
		instance.AddImplicitNamespace("Game.Session.Player.AI", isNamespace: true);
		instance.AddImplicitNamespace("Game.Session.Quests", isNamespace: true);
		instance.AddImplicitNamespace("Game.Session.Sim", isNamespace: true);
		instance.AddImplicitNamespace("Game.Session.Sim.Modules", isNamespace: true);
		instance.AddImplicitNamespace("Game.Session.Sim.Actions", isNamespace: true);
		SION.PrintSettings.MaxFloatDoubleDecimalDigits = 4;
		SION.PrintSettings.IndentSpaces = 1;
	}

	public override void OnDestroyed()
	{
		instance = null;
	}

	public Serializer CloneInstance()
	{
		return Serializer.CloneSerializer(instance);
	}
}
