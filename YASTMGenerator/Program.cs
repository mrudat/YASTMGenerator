using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using System.IO.Abstractions;
using System.Text;

namespace YASTMGenerator
{
    public class Program
    {
        private readonly ILoadOrder<IModListing<ISkyrimModGetter>> loadOrder;
        private readonly string dataFolderPath;
        private readonly ISkyrimMod patchMod;

        readonly IFileSystem fileSystem;

        private IFile File => fileSystem.File;

        private static readonly SoulGem.TranslationMask copyMask = new(true)
        {
            EditorID = false,
            ContainedSoul = false,
            LinkedTo = false,
        };

        public static readonly Dictionary<SoulGem.Level, uint> soulGemValue = new()
        {
            { SoulGem.Level.None, 0 },
            { SoulGem.Level.Petty, 250 },
            { SoulGem.Level.Lesser, 500 },
            { SoulGem.Level.Common, 1000 },
            { SoulGem.Level.Greater, 2000 },
            { SoulGem.Level.Grand, 3000 },
        };

        public Program(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, IFileSystem? fileSystem = null)
        {
            loadOrder = state.LoadOrder;
            dataFolderPath = state.DataFolderPath;
            patchMod = state.PatchMod;
            this.fileSystem = fileSystem ?? new FileSystem();
        }

        public Program(ILoadOrder<IModListing<ISkyrimModGetter>> loadOrder, string dataFolderPath, ISkyrimMod patchMod, IFileSystem? fileSystem = null)
        {
            this.loadOrder = loadOrder;
            this.dataFolderPath = dataFolderPath;
            this.patchMod = patchMod;
            this.fileSystem = fileSystem ?? new FileSystem();
        }

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "YASTMGenerator.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state) => new Program(state).Run();

        static string FormatSoulGemLink(ISoulGemGetter slgm) => $"    [0x{slgm.FormKey.ID:x}, \"{slgm.FormKey.ModKey.FileName}\"], ";

        public void Run()
        {
            var classifiedSoulGems = from slgm in loadOrder.PriorityOrder.SoulGem().WinningOverrides()
                                     where slgm.Name?.String is not null
                                        && slgm.MaximumCapacity != SoulGem.Level.None
                                        && slgm.ContainedSoul <= slgm.MaximumCapacity
                                     group slgm by (name: slgm.Name!.String!,
                                     maximumCapacity: slgm.MaximumCapacity,
                                     canHoldNpcSoul: slgm.MajorFlags.HasFlag(SoulGem.MajorFlag.CanHoldNpcSoul),
                                     isReusable: slgm.Keywords?.Contains(Skyrim.Keyword.ReusableSoulGem) == true);

            var sb = new StringBuilder();

            foreach (var (key, soulGemVariants3) in classifiedSoulGems.ToDictionary(i => i.Key))
            {
                var soulGemVariants = (from slgm in soulGemVariants3
                                       group slgm by slgm.ContainedSoul).ToDictionary(i => i.Key, i => i.First());

                if (!soulGemVariants.TryGetValue(SoulGem.Level.None, out var emptySoulGem))
                    continue;

                if (key.canHoldNpcSoul && key.maximumCapacity != SoulGem.Level.Grand)
                    continue;

                var baseValue = emptySoulGem.Value;
                var maxValue = soulGemVariants.GetValueOrDefault(key.maximumCapacity)?.Value ?? baseValue * 2;
                var deltaValue = maxValue - baseValue;
                var ratioFactor = 1.0 / soulGemValue[key.maximumCapacity];

                ISoulGemGetter FindOrAddSoulGem(SoulGem.Level containedSoul)
                {
                    if (soulGemVariants.TryGetValue(containedSoul, out var existing))
                    {
                        if (key.isReusable && !existing.LinkedTo.Equals(emptySoulGem))
                            patchMod.SoulGems.GetOrAddAsOverride(existing).LinkedTo.SetTo(emptySoulGem);
                        return existing;
                    }

                    var containedSoulName = (containedSoul < key.maximumCapacity) ? containedSoul.ToString() : "";
                    var editorID = $"{emptySoulGem.EditorID}Filled{containedSoulName}";

                    var newSoulGem = patchMod.SoulGems.AddNew(editorID);
                    newSoulGem.DeepCopyIn(emptySoulGem, copyMask);

                    newSoulGem.ContainedSoul = containedSoul;

                    if (key.isReusable)
                        newSoulGem.LinkedTo.SetTo(emptySoulGem);

                    newSoulGem.Value = (uint)(baseValue + (soulGemValue[containedSoul] * ratioFactor * deltaValue));

                    soulGemVariants.Add(containedSoul, newSoulGem);
                    return newSoulGem;
                }

                List<Tuple<string, string>> members = new();

                void AddSoulGemLink(SoulGem.Level containedSoul)
                {
                    var soulGemLink = FormatSoulGemLink(FindOrAddSoulGem(containedSoul));
                    if (containedSoul < key.maximumCapacity)
                        members.Add(new(containedSoul.ToString(), soulGemLink));
                    else
                        members.Add(new("Filled", soulGemLink));
                }

                members.Add(new("Empty", FormatSoulGemLink(emptySoulGem)));

                int capacity;

                if (key.canHoldNpcSoul)
                {
                    capacity = 6;
                    AddSoulGemLink(SoulGem.Level.Grand);
                }
                else
                {
                    capacity = key.maximumCapacity switch
                    {
                        SoulGem.Level.Petty => 1,
                        SoulGem.Level.Lesser => 2,
                        SoulGem.Level.Common => 3,
                        SoulGem.Level.Greater => 4,
                        SoulGem.Level.Grand => 5,
                        _ => throw new InvalidDataException(),
                    };

                    if (capacity >= 1)
                        AddSoulGemLink(SoulGem.Level.Petty);
                    if (capacity >= 2)
                        AddSoulGemLink(SoulGem.Level.Lesser);
                    if (capacity >= 3)
                        AddSoulGemLink(SoulGem.Level.Common);
                    if (capacity >= 4)
                        AddSoulGemLink(SoulGem.Level.Greater);
                    if (capacity >= 5)
                        AddSoulGemLink(SoulGem.Level.Grand);
                }

                sb.AppendLine("[[soulGems]]");
                sb.AppendLine($"id = \"{key.name}\"");
                if (key.isReusable)
                    sb.AppendLine("isReusable = true");
                sb.AppendLine($"capacity = {capacity}");
                sb.AppendLine("members = [");

                var maxLength = members.Max(i => i.Item2.Length);

                foreach (var member in members)
                    sb.AppendLine($"{member.Item2.PadRight(maxLength)}# {member.Item1}");

                sb.AppendLine("]");
                sb.AppendLine("");
            }

            // TODO only emit a configuration if one is not already defined?

            var configFilePath = Path.Combine(dataFolderPath, $"YASTM_{patchMod.ModKey.FileName}.toml");

            if (sb.Length > 0)
                File.WriteAllText(configFilePath, sb.ToString());
            else if (File.Exists(configFilePath))
                File.Delete(configFilePath);
        }
    }
}
