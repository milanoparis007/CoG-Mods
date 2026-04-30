using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ReflectionAssembly = System.Reflection.Assembly;

namespace AfterProhibitionPreloader
{
    public static class AfterProhibitionAssemblyPatcher
    {
        private const string ManifestFileName = "AssemblyPatchManifest.json";

        private static string _patcherDirectory;
        private static AssemblyPatchManifest _manifest;

        public static IEnumerable<string> TargetDLLs
        {
            get { yield return "Assembly-CSharp.dll"; }
        }

        public static void Initialize()
        {
            _patcherDirectory = Path.GetDirectoryName(ReflectionAssembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
            string manifestPath = Path.Combine(_patcherDirectory, ManifestFileName);
            _manifest = AssemblyPatchManifest.Load(manifestPath);

            if (!File.Exists(manifestPath))
            {
                File.WriteAllText(manifestPath, _manifest.ToJson());
                Log("Created default manifest at " + manifestPath);
            }
        }

        public static void Patch(ref AssemblyDefinition assembly)
        {
            if (_manifest == null)
            {
                _manifest = AssemblyPatchManifest.CreateDefault();
                Log("Manifest was not initialized; using built-in defaults.");
            }

            ModuleDefinition module = assembly.MainModule;

            int labelFieldCount = PatchLabelFields(module, _manifest.ValidLabelFields());
            int fixnumFieldCount = PatchFixnumFields(module, _manifest.ValidFixnumFields());
            int priceSerializerCount = PatchPriceSerialization(module);

            Log("Patched Assembly-CSharp.dll with " + labelFieldCount + " label field(s), " + fixnumFieldCount + " Fixnum field(s), and " + priceSerializerCount + " Price serializer registration(s).");
        }

        public static void Finish()
        {
            Log("Assembly patching complete.");
        }

        private static int PatchLabelFields(ModuleDefinition module, IEnumerable<LabelFieldPatchDefinition> definitions)
        {
            int addedCount = 0;
            TypeReference labelType = module.ImportReference(module.GetType("Game.Core.Label"));
            TypeDefinition labelTypeDefinition = labelType.Resolve();
            MethodReference explicitStringToLabel = module.ImportReference(
                labelTypeDefinition.Methods.First(method =>
                    method.Name == "op_Explicit" &&
                    method.IsStatic &&
                    method.Parameters.Count == 1 &&
                    method.Parameters[0].ParameterType.FullName == "System.String"));

            foreach (IGrouping<string, LabelFieldPatchDefinition> group in definitions.GroupBy(def => def.TargetType))
            {
                TypeDefinition type = FindType(module, group.Key);
                if (type == null)
                {
                    Log("Skipped missing type " + group.Key + " while adding label fields.");
                    continue;
                }

                MethodDefinition cctor = GetOrCreateStaticConstructor(module, type);
                Instruction ret = cctor.Body.Instructions.Last(instruction => instruction.OpCode == OpCodes.Ret);

                foreach (LabelFieldPatchDefinition definition in group)
                {
                    if (type.Fields.Any(existingField => existingField.Name == definition.FieldName))
                    {
                        continue;
                    }

                    FieldDefinition newField = new FieldDefinition(
                        definition.FieldName,
                        Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.InitOnly,
                        labelType);

                    type.Fields.Add(newField);
                    ILProcessor il = cctor.Body.GetILProcessor();
                    il.InsertBefore(ret, il.Create(OpCodes.Ldstr, definition.LabelValue));
                    il.InsertBefore(ret, il.Create(OpCodes.Call, explicitStringToLabel));
                    il.InsertBefore(ret, il.Create(OpCodes.Stsfld, newField));

                    addedCount++;
                    Log("Added label field " + definition.TargetType + "::" + definition.FieldName + " -> " + definition.LabelValue);
                }
            }

            return addedCount;
        }

        private static int PatchFixnumFields(ModuleDefinition module, IEnumerable<FixnumFieldPatchDefinition> definitions)
        {
            int addedCount = 0;
            TypeDefinition victorySettingsType = FindType(module, "Game.Services.VictorySettings");
            if (victorySettingsType == null)
            {
                Log("Skipped Fixnum field patching because Game.Services.VictorySettings was not found.");
                return 0;
            }

            TypeReference fixnumType = victorySettingsType.Fields.First(field => field.FieldType.FullName == "SomaSim.Util.Fixnum").FieldType;

            foreach (IGrouping<string, FixnumFieldPatchDefinition> group in definitions.GroupBy(def => def.TargetType))
            {
                TypeDefinition type = FindType(module, group.Key);
                if (type == null)
                {
                    Log("Skipped missing type " + group.Key + " while adding Fixnum fields.");
                    continue;
                }

                foreach (FixnumFieldPatchDefinition definition in group)
                {
                    if (type.Fields.Any(existingField => existingField.Name == definition.FieldName))
                    {
                        continue;
                    }

                    FieldDefinition newField = new FieldDefinition(
                        definition.FieldName,
                        Mono.Cecil.FieldAttributes.Public,
                        fixnumType);

                    type.Fields.Add(newField);
                    addedCount++;
                    Log("Added Fixnum field " + definition.TargetType + "::" + definition.FieldName);
                }
            }

            return addedCount;
        }

        private static int PatchPriceSerialization(ModuleDefinition module)
        {
            TypeDefinition serializerServiceType = FindType(module, "Game.Services.SerializerService");
            if (serializerServiceType == null)
            {
                Log("Skipped Price serializer patching because Game.Services.SerializerService was not found.");
                return 0;
            }

            MethodDefinition onCreated = serializerServiceType.Methods.FirstOrDefault(method => method.Name == "OnCreated" && !method.HasParameters);
            if (onCreated == null)
            {
                Log("Skipped Price serializer patching because Game.Services.SerializerService::OnCreated was not found.");
                return 0;
            }

            TypeDefinition priceType = FindType(module, "Game.Core.Price");
            if (priceType == null)
            {
                Log("Skipped Price serializer patching because Game.Core.Price was not found.");
                return 0;
            }

            PriceSerializationCompatMethods compatMethods = GetOrCreatePriceSerializationCompat(module, serializerServiceType, priceType);
            if (compatMethods == null)
            {
                Log("Skipped Price serializer patching because compatibility helpers could not be created.");
                return 0;
            }

            if (onCreated.Body.Instructions.Any(instruction => IsMethod(instruction.Operand, compatMethods.RegisterPriceSerializer)))
            {
                return 0;
            }

            FieldDefinition serializerInstanceField = serializerServiceType.Fields.FirstOrDefault(field => field.Name == "instance");
            if (serializerInstanceField == null)
            {
                Log("Skipped Price serializer patching because Game.Services.SerializerService::instance was not found.");
                return 0;
            }

            Instruction insertionPoint = onCreated.Body.Instructions.FirstOrDefault(instruction => IsMethodNamed(instruction.Operand, "AddImplicitNamespace")) ??
                                         onCreated.Body.Instructions.LastOrDefault(instruction => instruction.OpCode == OpCodes.Ret);
            if (insertionPoint == null)
            {
                Log("Skipped Price serializer patching because no insertion point was found in Game.Services.SerializerService::OnCreated.");
                return 0;
            }

            ILProcessor il = onCreated.Body.GetILProcessor();
            il.InsertBefore(insertionPoint, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(insertionPoint, il.Create(OpCodes.Ldfld, serializerInstanceField));
            il.InsertBefore(insertionPoint, il.Create(OpCodes.Call, compatMethods.RegisterPriceSerializer));

            Log("Registered Price compatibility serializer in Game.Services.SerializerService::OnCreated");
            return 1;
        }

        private static PriceSerializationCompatMethods GetOrCreatePriceSerializationCompat(ModuleDefinition module, TypeDefinition serializerServiceType, TypeDefinition priceType)
        {
            TypeDefinition compatType = FindType(module, "Game.Services.AfterProhibitionPriceSerializationCompat");
            if (compatType == null)
            {
                compatType = new TypeDefinition(
                    "Game.Services",
                    "AfterProhibitionPriceSerializationCompat",
                    Mono.Cecil.TypeAttributes.NotPublic | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed | Mono.Cecil.TypeAttributes.BeforeFieldInit | Mono.Cecil.TypeAttributes.Class,
                    module.TypeSystem.Object);

                module.Types.Add(compatType);
            }

            FieldDefinition serializerInstanceField = serializerServiceType.Fields.FirstOrDefault(field => field.Name == "instance");
            FieldDefinition priceCashField = priceType.Fields.FirstOrDefault(field => field.Name == "cash");
            if (serializerInstanceField == null || priceCashField == null)
            {
                return null;
            }

            DefaultAssemblyResolver resolver = CreateResolver(module);
            TypeDefinition serializerType = ResolveExternalType(resolver, serializerInstanceField.FieldType);
            if (serializerType == null)
            {
                Log("Could not resolve " + serializerInstanceField.FieldType.FullName + " while creating Price compatibility helpers.");
                return null;
            }

            TypeReference serializerTypeReference = module.ImportReference(serializerInstanceField.FieldType);
            TypeReference fixnumTypeReference = module.ImportReference(priceCashField.FieldType);
            MethodDefinition priceCtor = priceType.Methods.FirstOrDefault(method =>
                method.IsConstructor &&
                !method.IsStatic &&
                method.Parameters.Count == 1 &&
                method.Parameters[0].ParameterType.FullName == fixnumTypeReference.FullName);

            if (priceCtor == null)
            {
                Log("Could not find Game.Core.Price(Fixnum) while creating Price compatibility helpers.");
                return null;
            }

            MethodDefinition serializePrice = compatType.Methods.FirstOrDefault(method => method.Name == "SerializePrice");
            if (serializePrice == null)
            {
                serializePrice = CreateSerializePriceMethod(module, compatType, priceType, serializerTypeReference, fixnumTypeReference, priceCashField, serializerType);
            }

            MethodDefinition extractPriceValue = compatType.Methods.FirstOrDefault(method => method.Name == "ExtractPriceValue");
            if (extractPriceValue == null)
            {
                extractPriceValue = CreateExtractPriceValueMethod(module, compatType);
            }

            MethodDefinition deserializePrice = compatType.Methods.FirstOrDefault(method => method.Name == "DeserializePrice");
            if (deserializePrice == null)
            {
                deserializePrice = CreateDeserializePriceMethod(module, compatType, priceType, serializerTypeReference, fixnumTypeReference, serializerType, priceCtor, extractPriceValue);
            }

            MethodDefinition registerPriceSerializer = compatType.Methods.FirstOrDefault(method => method.Name == "RegisterPriceSerializer");
            if (registerPriceSerializer == null)
            {
                registerPriceSerializer = CreateRegisterPriceSerializerMethod(module, compatType, priceType, serializerTypeReference, serializerType, serializePrice, deserializePrice);
            }

            return new PriceSerializationCompatMethods
            {
                CompatType = compatType,
                SerializePrice = serializePrice,
                DeserializePrice = deserializePrice,
                RegisterPriceSerializer = registerPriceSerializer
            };
        }

        private static MethodDefinition CreateSerializePriceMethod(
            ModuleDefinition module,
            TypeDefinition compatType,
            TypeDefinition priceType,
            TypeReference serializerTypeReference,
            TypeReference fixnumTypeReference,
            FieldDefinition priceCashField,
            TypeDefinition serializerType)
        {
            MethodDefinition method = new MethodDefinition(
                "SerializePrice",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
                module.TypeSystem.Object);

            method.Parameters.Add(new ParameterDefinition("price", Mono.Cecil.ParameterAttributes.None, priceType));
            method.Parameters.Add(new ParameterDefinition("serializer", Mono.Cecil.ParameterAttributes.None, serializerTypeReference));
            compatType.Methods.Add(method);

            MethodReference hashtableCtor = module.ImportReference(typeof(Hashtable).GetConstructor(Type.EmptyTypes));
            MethodReference hashtableSetItem = module.ImportReference(typeof(Hashtable).GetProperty("Item").SetMethod);
            MethodReference serializerSerialize = module.ImportReference(serializerType.Methods.First(candidate =>
                candidate.Name == "Serialize" &&
                candidate.Parameters.Count == 1 &&
                candidate.Parameters[0].ParameterType.FullName == module.TypeSystem.Object.FullName));

            ILProcessor il = method.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Newobj, hashtableCtor));
            il.Append(il.Create(OpCodes.Dup));
            il.Append(il.Create(OpCodes.Ldstr, "cash"));
            il.Append(il.Create(OpCodes.Ldarg_1));
            il.Append(il.Create(OpCodes.Ldarga_S, method.Parameters[0]));
            il.Append(il.Create(OpCodes.Ldfld, priceCashField));
            il.Append(il.Create(OpCodes.Box, fixnumTypeReference));
            il.Append(il.Create(OpCodes.Callvirt, serializerSerialize));
            il.Append(il.Create(OpCodes.Callvirt, hashtableSetItem));
            il.Append(il.Create(OpCodes.Ret));

            return method;
        }

        private static MethodDefinition CreateExtractPriceValueMethod(
            ModuleDefinition module,
            TypeDefinition compatType)
        {
            MethodDefinition method = new MethodDefinition(
                "ExtractPriceValue",
                Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
                module.TypeSystem.Object);

            method.Parameters.Add(new ParameterDefinition("value", Mono.Cecil.ParameterAttributes.None, module.TypeSystem.Object));
            method.Body.InitLocals = true;
            compatType.Methods.Add(method);

            VariableDefinition dictionaryVariable = new VariableDefinition(module.ImportReference(typeof(IDictionary)));
            method.Body.Variables.Add(dictionaryVariable);

            MethodReference dictionaryContains = module.ImportReference(typeof(IDictionary).GetMethod("Contains"));
            MethodReference dictionaryGetItem = module.ImportReference(typeof(IDictionary).GetProperty("Item").GetMethod);

            Instruction valueIsNotNull = Instruction.Create(OpCodes.Nop);
            Instruction returnInput = Instruction.Create(OpCodes.Ldarg_0);
            Instruction returnCashValue = Instruction.Create(OpCodes.Ldloc, dictionaryVariable);
            Instruction returnDirtyCashValue = Instruction.Create(OpCodes.Ldloc, dictionaryVariable);

            ILProcessor il = method.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Brtrue, valueIsNotNull));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Ret));

            il.Append(valueIsNotNull);
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Isinst, module.ImportReference(typeof(IDictionary))));
            il.Append(il.Create(OpCodes.Stloc, dictionaryVariable));
            il.Append(il.Create(OpCodes.Ldloc, dictionaryVariable));
            il.Append(il.Create(OpCodes.Brfalse, returnInput));

            il.Append(il.Create(OpCodes.Ldloc, dictionaryVariable));
            il.Append(il.Create(OpCodes.Ldstr, "cash"));
            il.Append(il.Create(OpCodes.Callvirt, dictionaryContains));
            il.Append(il.Create(OpCodes.Brtrue, returnCashValue));

            il.Append(il.Create(OpCodes.Ldloc, dictionaryVariable));
            il.Append(il.Create(OpCodes.Ldstr, "dirty-cash"));
            il.Append(il.Create(OpCodes.Callvirt, dictionaryContains));
            il.Append(il.Create(OpCodes.Brtrue, returnDirtyCashValue));

            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Ret));

            il.Append(returnCashValue);
            il.Append(il.Create(OpCodes.Ldstr, "cash"));
            il.Append(il.Create(OpCodes.Callvirt, dictionaryGetItem));
            il.Append(il.Create(OpCodes.Ret));

            il.Append(returnDirtyCashValue);
            il.Append(il.Create(OpCodes.Ldstr, "dirty-cash"));
            il.Append(il.Create(OpCodes.Callvirt, dictionaryGetItem));
            il.Append(il.Create(OpCodes.Ret));

            il.Append(returnInput);
            il.Append(il.Create(OpCodes.Ret));

            return method;
        }

        private static MethodDefinition CreateDeserializePriceMethod(
            ModuleDefinition module,
            TypeDefinition compatType,
            TypeDefinition priceType,
            TypeReference serializerTypeReference,
            TypeReference fixnumTypeReference,
            TypeDefinition serializerType,
            MethodDefinition priceCtor,
            MethodDefinition extractPriceValue)
        {
            MethodDefinition method = new MethodDefinition(
                "DeserializePrice",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
                priceType);

            method.Parameters.Add(new ParameterDefinition("value", Mono.Cecil.ParameterAttributes.None, module.TypeSystem.Object));
            method.Parameters.Add(new ParameterDefinition("serializer", Mono.Cecil.ParameterAttributes.None, serializerTypeReference));
            method.Body.InitLocals = true;
            compatType.Methods.Add(method);

            VariableDefinition rawValueVariable = new VariableDefinition(module.TypeSystem.Object);
            method.Body.Variables.Add(rawValueVariable);

            MethodReference typeGetTypeFromHandle = module.ImportReference(typeof(Type).GetMethod("GetTypeFromHandle"));
            MethodReference serializerDeserialize = module.ImportReference(serializerType.Methods.First(candidate =>
                candidate.Name == "Deserialize" &&
                candidate.Parameters.Count == 2 &&
                candidate.Parameters[0].ParameterType.FullName == module.TypeSystem.Object.FullName &&
                candidate.Parameters[1].ParameterType.FullName == module.ImportReference(typeof(Type)).FullName));
            MethodReference importedPriceCtor = module.ImportReference(priceCtor);
            FieldDefinition zeroField = priceType.Fields.First(field => field.Name == "ZERO");

            Instruction deserializeValue = Instruction.Create(OpCodes.Nop);

            ILProcessor il = method.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Call, extractPriceValue));
            il.Append(il.Create(OpCodes.Stloc, rawValueVariable));
            il.Append(il.Create(OpCodes.Ldloc, rawValueVariable));
            il.Append(il.Create(OpCodes.Brtrue, deserializeValue));
            il.Append(il.Create(OpCodes.Ldsfld, zeroField));
            il.Append(il.Create(OpCodes.Ret));

            il.Append(deserializeValue);
            il.Append(il.Create(OpCodes.Ldarg_1));
            il.Append(il.Create(OpCodes.Ldloc, rawValueVariable));
            il.Append(il.Create(OpCodes.Ldtoken, fixnumTypeReference));
            il.Append(il.Create(OpCodes.Call, typeGetTypeFromHandle));
            il.Append(il.Create(OpCodes.Callvirt, serializerDeserialize));
            il.Append(il.Create(OpCodes.Unbox_Any, fixnumTypeReference));
            il.Append(il.Create(OpCodes.Newobj, importedPriceCtor));
            il.Append(il.Create(OpCodes.Ret));

            return method;
        }

        private static MethodDefinition CreateRegisterPriceSerializerMethod(
            ModuleDefinition module,
            TypeDefinition compatType,
            TypeDefinition priceType,
            TypeReference serializerTypeReference,
            TypeDefinition serializerType,
            MethodDefinition serializePrice,
            MethodDefinition deserializePrice)
        {
            MethodDefinition method = new MethodDefinition(
                "RegisterPriceSerializer",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
                module.TypeSystem.Void);

            method.Parameters.Add(new ParameterDefinition("serializer", Mono.Cecil.ParameterAttributes.None, serializerTypeReference));
            compatType.Methods.Add(method);

            MethodReference addCustomSerializerElement = module.ImportReference(serializerType.Methods.First(candidate =>
                candidate.Name == "AddCustomSerializer" &&
                candidate.HasGenericParameters &&
                candidate.Parameters.Count == 2));
            GenericInstanceMethod addCustomSerializer = new GenericInstanceMethod(addCustomSerializerElement);
            addCustomSerializer.GenericArguments.Add(priceType);

            MethodReference serializeDelegateCtor = CreateFunc3Constructor(module, priceType, serializerTypeReference, module.TypeSystem.Object);
            MethodReference deserializeDelegateCtor = CreateFunc3Constructor(module, module.TypeSystem.Object, serializerTypeReference, priceType);

            ILProcessor il = method.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Ldftn, serializePrice));
            il.Append(il.Create(OpCodes.Newobj, serializeDelegateCtor));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Ldftn, deserializePrice));
            il.Append(il.Create(OpCodes.Newobj, deserializeDelegateCtor));
            il.Append(il.Create(OpCodes.Callvirt, addCustomSerializer));
            il.Append(il.Create(OpCodes.Ret));

            return method;
        }

        private static MethodReference CreateFunc3Constructor(ModuleDefinition module, TypeReference arg0, TypeReference arg1, TypeReference arg2)
        {
            GenericInstanceType delegateType = new GenericInstanceType(module.ImportReference(typeof(Func<,,>)));
            delegateType.GenericArguments.Add(module.ImportReference(arg0));
            delegateType.GenericArguments.Add(module.ImportReference(arg1));
            delegateType.GenericArguments.Add(module.ImportReference(arg2));

            MethodReference ctor = new MethodReference(".ctor", module.TypeSystem.Void, delegateType)
            {
                HasThis = true
            };

            ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
            ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.IntPtr));
            return ctor;
        }

        private static TypeDefinition FindType(ModuleDefinition module, string fullName)
        {
            return module.Types.FirstOrDefault(type => type.FullName == fullName) ??
                   module.Types.SelectMany(type => type.NestedTypes).FirstOrDefault(type => type.FullName == fullName);
        }

        private static MethodDefinition GetOrCreateStaticConstructor(ModuleDefinition module, TypeDefinition type)
        {
            MethodDefinition existing = type.Methods.FirstOrDefault(method => method.Name == ".cctor");
            if (existing != null)
            {
                return existing;
            }

            MethodDefinition cctor = new MethodDefinition(
                ".cctor",
                Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName,
                module.TypeSystem.Void);

            cctor.Body.GetILProcessor().Append(cctor.Body.GetILProcessor().Create(OpCodes.Ret));
            type.Methods.Add(cctor);
            return cctor;
        }

        private static DefaultAssemblyResolver CreateResolver(ModuleDefinition module)
        {
            DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
            HashSet<string> searchDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddSearchDirectory(resolver, searchDirectories, Path.GetDirectoryName(module.FileName));
            AddSearchDirectory(resolver, searchDirectories, _patcherDirectory);
            AddSearchDirectory(
                resolver,
                searchDirectories,
                string.IsNullOrWhiteSpace(_patcherDirectory)
                    ? null
                    : Path.GetFullPath(Path.Combine(_patcherDirectory, "..", "..", "CoG_Data", "Managed")));

            return resolver;
        }

        private static void AddSearchDirectory(DefaultAssemblyResolver resolver, HashSet<string> searchDirectories, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) || !searchDirectories.Add(path))
            {
                return;
            }

            resolver.AddSearchDirectory(path);
        }

        private static TypeDefinition ResolveExternalType(DefaultAssemblyResolver resolver, TypeReference typeReference)
        {
            AssemblyNameReference assemblyName = typeReference.Scope as AssemblyNameReference;
            if (assemblyName == null)
            {
                return null;
            }

            try
            {
                AssemblyDefinition assembly = resolver.Resolve(assemblyName);
                return assembly?.MainModule.Types.FirstOrDefault(type => type.FullName == typeReference.FullName);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsMethod(object operand, MethodReference target)
        {
            MethodReference method = operand as MethodReference;
            return method != null &&
                   method.Name == target.Name &&
                   method.DeclaringType.FullName == target.DeclaringType.FullName;
        }

        private static bool IsMethodNamed(object operand, string methodName)
        {
            MethodReference method = operand as MethodReference;
            return method != null && method.Name == methodName;
        }

        private static void Log(string message)
        {
            Console.WriteLine("[AfterProhibitionPreloader] " + message);
        }

        private sealed class PriceSerializationCompatMethods
        {
            public TypeDefinition CompatType { get; set; }

            public MethodDefinition SerializePrice { get; set; }

            public MethodDefinition DeserializePrice { get; set; }

            public MethodDefinition RegisterPriceSerializer { get; set; }
        }
    }
}
