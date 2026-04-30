using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AfterProhibitionPreloader
{
    public sealed class AssemblyPatchManifest
    {
        public List<LabelFieldPatchDefinition> LabelFields { get; set; } = new List<LabelFieldPatchDefinition>();

        public List<FixnumFieldPatchDefinition> FixnumFields { get; set; } = new List<FixnumFieldPatchDefinition>();

        public static AssemblyPatchManifest Load(string path)
        {
            AssemblyPatchManifest defaults = CreateDefault();
            if (!File.Exists(path))
            {
                return defaults;
            }

            try
            {
                string json = File.ReadAllText(path);
                AssemblyPatchManifest parsed = Parse(json);
                if (!parsed.ValidLabelFields().Any() && !parsed.ValidFixnumFields().Any())
                {
                    return defaults;
                }

                return parsed;
            }
            catch
            {
                return defaults;
            }
        }

        public static AssemblyPatchManifest CreateDefault()
        {
            return new AssemblyPatchManifest
            {
                LabelFields = new List<LabelFieldPatchDefinition>
                {
                    new LabelFieldPatchDefinition("Game.Session.Sim.ResourceConstants", "COUNTERFEITCASH", "counterfeitcash"),
                    new LabelFieldPatchDefinition("Game.Session.Sim.ResourceConstants", "DIRTYCASH", "dirty-cash"),
                    new LabelFieldPatchDefinition("Game.Session.Sim.ResourceConstants", "STOLENGOODS", "stolenelectronics"),
                    new LabelFieldPatchDefinition("Game.Session.Sim.ResourceConstants", "CANNABIS", "cannabis-pound"),
                    new LabelFieldPatchDefinition("Game.Session.Sim.ResourceConstants", "COCAINE", "cocaine-tiles"),
                    new LabelFieldPatchDefinition("Game.Session.Sim.ResourceConstants", "HEROIN", "heroin-packs"),
                    new LabelFieldPatchDefinition("Game.Session.Sim.ResourceConstants", "HOMES", "home-mid"),
                    new LabelFieldPatchDefinition("Game.Session.Sim.ResourceConstants", "JAZZ", "bandlow")
                },
                FixnumFields = new List<FixnumFieldPatchDefinition>
                {
                    new FixnumFieldPatchDefinition("Game.Services.VictorySettings", "amtDirtyCash"),
                    new FixnumFieldPatchDefinition("Game.Services.VictorySettings", "amtStolenGoods"),
                    new FixnumFieldPatchDefinition("Game.Services.VictorySettings", "amtCounterfeit")
                }
            };
        }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"labelFields\": [");

            List<LabelFieldPatchDefinition> labelFields = ValidLabelFields().ToList();
            for (int i = 0; i < labelFields.Count; i++)
            {
                LabelFieldPatchDefinition field = labelFields[i];
                sb.AppendLine("    {");
                sb.AppendLine("      \"targetType\": \"" + Escape(field.TargetType) + "\",");
                sb.AppendLine("      \"fieldName\": \"" + Escape(field.FieldName) + "\",");
                sb.Append("      \"label\": \"" + Escape(field.LabelValue) + "\"");
                sb.AppendLine();
                sb.Append("    }");
                sb.AppendLine(i < labelFields.Count - 1 ? "," : string.Empty);
            }

            sb.AppendLine("  ],");
            sb.AppendLine("  \"fixnumFields\": [");

            List<FixnumFieldPatchDefinition> fixnumFields = ValidFixnumFields().ToList();
            for (int i = 0; i < fixnumFields.Count; i++)
            {
                FixnumFieldPatchDefinition field = fixnumFields[i];
                sb.AppendLine("    {");
                sb.AppendLine("      \"targetType\": \"" + Escape(field.TargetType) + "\",");
                sb.Append("      \"fieldName\": \"" + Escape(field.FieldName) + "\"");
                sb.AppendLine();
                sb.Append("    }");
                sb.AppendLine(i < fixnumFields.Count - 1 ? "," : string.Empty);
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public IEnumerable<LabelFieldPatchDefinition> ValidLabelFields()
        {
            return (LabelFields ?? Enumerable.Empty<LabelFieldPatchDefinition>())
                .Where(def => def != null && !string.IsNullOrWhiteSpace(def.TargetType) && !string.IsNullOrWhiteSpace(def.FieldName) && !string.IsNullOrWhiteSpace(def.LabelValue));
        }

        public IEnumerable<FixnumFieldPatchDefinition> ValidFixnumFields()
        {
            return (FixnumFields ?? Enumerable.Empty<FixnumFieldPatchDefinition>())
                .Where(def => def != null && !string.IsNullOrWhiteSpace(def.TargetType) && !string.IsNullOrWhiteSpace(def.FieldName));
        }

        private static AssemblyPatchManifest Parse(string json)
        {
            AssemblyPatchManifest manifest = new AssemblyPatchManifest();

            foreach (Match match in Regex.Matches(
                json,
                "\\{\\s*\"targetType\"\\s*:\\s*\"(?<target>[^\"]+)\"\\s*,\\s*\"fieldName\"\\s*:\\s*\"(?<field>[^\"]+)\"\\s*,\\s*\"label\"\\s*:\\s*\"(?<label>[^\"]+)\"\\s*\\}",
                RegexOptions.Singleline))
            {
                manifest.LabelFields.Add(new LabelFieldPatchDefinition(
                    Unescape(match.Groups["target"].Value),
                    Unescape(match.Groups["field"].Value),
                    Unescape(match.Groups["label"].Value)));
            }

            foreach (Match match in Regex.Matches(
                json,
                "\\{\\s*\"targetType\"\\s*:\\s*\"(?<target>[^\"]+)\"\\s*,\\s*\"fieldName\"\\s*:\\s*\"(?<field>[^\"]+)\"\\s*\\}",
                RegexOptions.Singleline))
            {
                string fieldName = Unescape(match.Groups["field"].Value);
                string targetType = Unescape(match.Groups["target"].Value);

                if (manifest.LabelFields.Any(def => def.TargetType == targetType && def.FieldName == fieldName))
                {
                    continue;
                }

                manifest.FixnumFields.Add(new FixnumFieldPatchDefinition(targetType, fieldName));
            }

            return manifest;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static string Unescape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
    }

    public sealed class LabelFieldPatchDefinition
    {
        public LabelFieldPatchDefinition()
        {
        }

        public LabelFieldPatchDefinition(string targetType, string fieldName, string labelValue)
        {
            TargetType = targetType;
            FieldName = fieldName;
            LabelValue = labelValue;
        }

        public string TargetType { get; set; }

        public string FieldName { get; set; }

        public string LabelValue { get; set; }
    }

    public sealed class FixnumFieldPatchDefinition
    {
        public FixnumFieldPatchDefinition()
        {
        }

        public FixnumFieldPatchDefinition(string targetType, string fieldName)
        {
            TargetType = targetType;
            FieldName = fieldName;
        }

        public string TargetType { get; set; }

        public string FieldName { get; set; }
    }
}
