using FluentAssertions;
using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Skyrim;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using Xunit;
using YASTMGenerator;

namespace Tests
{
    public class UnitTest1
    {
        public static readonly string DataFolderPath = Path.Join(Directory.GetCurrentDirectory(), "Data");
        public static readonly string configFilePath = Path.Join(DataFolderPath, "YASTM_Patch.esp.toml");


        public static readonly ModKey MasterModKey = ModKey.FromNameAndExtension("Master.esm");
        public static readonly ModKey PatchModKey = ModKey.FromNameAndExtension("Patch.esp");

        [Fact]
        public void TestDoesNothing()
        {
            var masterMod = new SkyrimMod(MasterModKey, SkyrimRelease.SkyrimSE);

            var patchMod = new SkyrimMod(PatchModKey, SkyrimRelease.SkyrimSE);

            var loadOrder = new LoadOrder<IModListing<ISkyrimModGetter>>()
            {
                new ModListing<ISkyrimModGetter>(masterMod),
                new ModListing<ISkyrimModGetter>(patchMod),
            };

            var fs = new MockFileSystem();

            var program = new Program(loadOrder, DataFolderPath, patchMod, fs);
            program.Run();

            fs.FileExists(configFilePath).Should().BeFalse();

            patchMod.SoulGems.Should().BeEmpty();
        }

        [Fact]
        public void TestRemovesOldConfigFile()
        {
            var masterMod = new SkyrimMod(MasterModKey, SkyrimRelease.SkyrimSE);

            var patchMod = new SkyrimMod(PatchModKey, SkyrimRelease.SkyrimSE);

            var loadOrder = new LoadOrder<IModListing<ISkyrimModGetter>>()
            {
                new ModListing<ISkyrimModGetter>(masterMod),
                new ModListing<ISkyrimModGetter>(patchMod),
            };

            var fs = new MockFileSystem(new Dictionary<string, MockFileData>() {
                { configFilePath, new MockFileData("") }
            });

            var program = new Program(loadOrder, DataFolderPath, patchMod, fs);
            program.Run();

            fs.FileExists(configFilePath).Should().BeFalse();

            patchMod.SoulGems.Should().BeEmpty();
        }

        public class OrdinaryData : TheoryData<SoulGem.Level, bool, bool, bool, string>
        {

            readonly string configFileHeading = @"[[soulGems]]
id = ""My Soul Gem""";

            void Add2(SoulGem.Level level, string configFileContents1, string configFileContents2)
            {
                Add(level, false, false, false, configFileHeading + configFileContents1);
                Add(level, true, false, false, configFileHeading + @"
isReusable = true" + configFileContents1);
                Add(level, false, true, false, configFileHeading + configFileContents2);
                Add(level, true, true, false, configFileHeading + @"
isReusable = true" + configFileContents2);
                Add(level, false, true, true, configFileHeading + configFileContents2);
                Add(level, true, true, true, configFileHeading + @"
isReusable = true" + configFileContents2);
            }

            public OrdinaryData()
            {
                Add2(SoulGem.Level.Grand, @"
capacity = 5
members = [
    [0x800, ""Master.esm""], # Empty
    [0x800, ""Patch.esp""],  # Petty
    [0x801, ""Patch.esp""],  # Lesser
    [0x802, ""Patch.esp""],  # Common
    [0x803, ""Patch.esp""],  # Greater
    [0x804, ""Patch.esp""],  # Filled
]

", @"
capacity = 5
members = [
    [0x800, ""Master.esm""], # Empty
    [0x800, ""Patch.esp""],  # Petty
    [0x801, ""Patch.esp""],  # Lesser
    [0x802, ""Patch.esp""],  # Common
    [0x803, ""Patch.esp""],  # Greater
    [0x801, ""Master.esm""], # Filled
]

");
                Add2(SoulGem.Level.Greater, @"
capacity = 4
members = [
    [0x800, ""Master.esm""], # Empty
    [0x800, ""Patch.esp""],  # Petty
    [0x801, ""Patch.esp""],  # Lesser
    [0x802, ""Patch.esp""],  # Common
    [0x803, ""Patch.esp""],  # Filled
]

", @"
capacity = 4
members = [
    [0x800, ""Master.esm""], # Empty
    [0x800, ""Patch.esp""],  # Petty
    [0x801, ""Patch.esp""],  # Lesser
    [0x802, ""Patch.esp""],  # Common
    [0x801, ""Master.esm""], # Filled
]

");
                Add2(SoulGem.Level.Common, @"
capacity = 3
members = [
    [0x800, ""Master.esm""], # Empty
    [0x800, ""Patch.esp""],  # Petty
    [0x801, ""Patch.esp""],  # Lesser
    [0x802, ""Patch.esp""],  # Filled
]

", @"
capacity = 3
members = [
    [0x800, ""Master.esm""], # Empty
    [0x800, ""Patch.esp""],  # Petty
    [0x801, ""Patch.esp""],  # Lesser
    [0x801, ""Master.esm""], # Filled
]

");
                Add2(SoulGem.Level.Lesser, @"
capacity = 2
members = [
    [0x800, ""Master.esm""], # Empty
    [0x800, ""Patch.esp""],  # Petty
    [0x801, ""Patch.esp""],  # Filled
]

", @"
capacity = 2
members = [
    [0x800, ""Master.esm""], # Empty
    [0x800, ""Patch.esp""],  # Petty
    [0x801, ""Master.esm""], # Filled
]

");
                Add2(SoulGem.Level.Petty, @"
capacity = 1
members = [
    [0x800, ""Master.esm""], # Empty
    [0x800, ""Patch.esp""],  # Filled
]

", @"
capacity = 1
members = [
    [0x800, ""Master.esm""], # Empty
    [0x801, ""Master.esm""], # Filled
]

");
            }
        }

        [Theory]
        [ClassData(typeof(OrdinaryData))]
        public void TestOrdinary(SoulGem.Level size, bool isReusable, bool createFilled, bool omitLink, string configFileContents)
        {
            var masterMod = new SkyrimMod(MasterModKey, SkyrimRelease.SkyrimSE);

            var patchMod = new SkyrimMod(PatchModKey, SkyrimRelease.SkyrimSE);

            var loadOrder = new LoadOrder<IModListing<ISkyrimModGetter>>()
            {
                new ModListing<ISkyrimModGetter>(masterMod),
                new ModListing<ISkyrimModGetter>(patchMod),
            };

            var myEmptySoulGem = masterMod.SoulGems.AddNew("mySoulGem");
            myEmptySoulGem.Name = "My Soul Gem";
            myEmptySoulGem.Value = 200;
            myEmptySoulGem.ContainedSoul = SoulGem.Level.None;
            myEmptySoulGem.MaximumCapacity = size;
            if (isReusable)
                (myEmptySoulGem.Keywords ??= new()).Add(Skyrim.Keyword.ReusableSoulGem);

            var baseValue = myEmptySoulGem.Value;
            var maxValue = baseValue * 2;

            if (createFilled)
            {
                var myFilledSoulGem = masterMod.SoulGems.AddNew("mySoulGemFilled");
                myFilledSoulGem.Name = "My Soul Gem";
                myFilledSoulGem.Value = 500;
                myFilledSoulGem.ContainedSoul = size;
                myFilledSoulGem.MaximumCapacity = size;
                if (isReusable)
                {
                    (myFilledSoulGem.Keywords ??= new()).Add(Skyrim.Keyword.ReusableSoulGem);
                    if (!omitLink)
                        myFilledSoulGem.LinkedTo.SetTo(myEmptySoulGem);
                }
                maxValue = myFilledSoulGem.Value;
            }

            var fs = new MockFileSystem();
            fs.AddDirectory(DataFolderPath);

            var program = new Program(loadOrder, DataFolderPath, patchMod, fs);
            program.Run();

            fs.FileExists(configFilePath).Should().BeTrue();

            fs.File.ReadAllText(configFilePath).Should().Be(configFileContents);

            if (createFilled && (!isReusable || !omitLink) && size == SoulGem.Level.Petty)
                patchMod.SoulGems.Should().BeEmpty();
            else
            {
                patchMod.SoulGems.Should().NotBeEmpty();

                var soulGems = loadOrder.PriorityOrder.SoulGem().WinningOverrides();

                foreach (var slgm in soulGems)
                {
                    if (isReusable && slgm.ContainedSoul != SoulGem.Level.None)
                        Assert.True(slgm.LinkedTo.Equals(myEmptySoulGem));
                    slgm.Value.Should().Be(baseValue + ((Program.soulGemValue[slgm.ContainedSoul] * (maxValue - baseValue)) / Program.soulGemValue[size]));
                }
            }
        }


        public class BlackData : TheoryData<bool, bool, string>
        {

            readonly string configFileHeading = @"[[soulGems]]
id = ""My Soul Gem""";

            void Add2(string configFileContents1, string configFileContents2)
            {
                Add(false, false, configFileHeading + configFileContents1);
                Add(true, false, configFileHeading + @"
isReusable = true" + configFileContents1);
                Add(false, true, configFileHeading + configFileContents2);
                Add(true, true, configFileHeading + @"
isReusable = true" + configFileContents2);
            }

            public BlackData()
            {
                Add2(@"
capacity = 6
members = [
    [0x800, ""Master.esm""], # Empty
    [0x800, ""Patch.esp""],  # Filled
]

", @"
capacity = 6
members = [
    [0x800, ""Master.esm""], # Empty
    [0x801, ""Master.esm""], # Filled
]

");
            }
        }

        [Theory]
        [ClassData(typeof(BlackData))]
        public void TestBlack(bool isReusable, bool createFilled, string configFileContents)
        {
            var masterMod = new SkyrimMod(MasterModKey, SkyrimRelease.SkyrimSE);

            var patchMod = new SkyrimMod(PatchModKey, SkyrimRelease.SkyrimSE);

            var loadOrder = new LoadOrder<IModListing<ISkyrimModGetter>>()
            {
                new ModListing<ISkyrimModGetter>(masterMod),
                new ModListing<ISkyrimModGetter>(patchMod),
            };

            var myEmptySoulGem = masterMod.SoulGems.AddNew("mySoulGem");
            myEmptySoulGem.Name = "My Soul Gem";
            myEmptySoulGem.Value = 200;
            myEmptySoulGem.MajorFlags = SoulGem.MajorFlag.CanHoldNpcSoul;
            myEmptySoulGem.ContainedSoul = SoulGem.Level.None;
            myEmptySoulGem.MaximumCapacity = SoulGem.Level.Grand;
            if (isReusable)
                (myEmptySoulGem.Keywords ??= new()).Add(Skyrim.Keyword.ReusableSoulGem);

            var baseValue = myEmptySoulGem.Value;
            var maxValue = baseValue * 2;

            if (createFilled)
            {
                var myFilledSoulGem = masterMod.SoulGems.AddNew("mySoulGemFilled");
                myFilledSoulGem.Name = "My Soul Gem";
                myFilledSoulGem.Value = 500;
                myFilledSoulGem.MajorFlags = SoulGem.MajorFlag.CanHoldNpcSoul;
                myFilledSoulGem.ContainedSoul = SoulGem.Level.Grand;
                myFilledSoulGem.MaximumCapacity = SoulGem.Level.Grand;
                if (isReusable)
                {
                    (myFilledSoulGem.Keywords ??= new()).Add(Skyrim.Keyword.ReusableSoulGem);
                    myFilledSoulGem.LinkedTo.SetTo(myEmptySoulGem);
                }
                maxValue = myFilledSoulGem.Value;
            }

            var fs = new MockFileSystem();
            fs.AddDirectory(DataFolderPath);

            var program = new Program(loadOrder, DataFolderPath, patchMod, fs);
            program.Run();

            fs.FileExists(configFilePath).Should().BeTrue();

            fs.File.ReadAllText(configFilePath).Should().Be(configFileContents);

            if (createFilled)
                patchMod.SoulGems.Should().BeEmpty();
            else
            {
                patchMod.SoulGems.Should().NotBeEmpty();

                var soulGems = loadOrder.PriorityOrder.SoulGem().WinningOverrides();

                foreach (var slgm in soulGems)
                {
                    if (isReusable && slgm.ContainedSoul != SoulGem.Level.None)
                        Assert.True(slgm.LinkedTo.Equals(myEmptySoulGem));
                    slgm.Value.Should().Be(baseValue + ((Program.soulGemValue[slgm.ContainedSoul] * (maxValue - baseValue)) / Program.soulGemValue[SoulGem.Level.Grand]));
                }
            }
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void TestIgnoredNoEmpty(bool isReusable, bool createFilled)
        {
            var masterMod = new SkyrimMod(MasterModKey, SkyrimRelease.SkyrimSE);

            var patchMod = new SkyrimMod(PatchModKey, SkyrimRelease.SkyrimSE);

            var loadOrder = new LoadOrder<IModListing<ISkyrimModGetter>>()
            {
                new ModListing<ISkyrimModGetter>(masterMod),
                new ModListing<ISkyrimModGetter>(patchMod),
            };

            var mySoulGem = masterMod.SoulGems.AddNew("mySoulGemFilledPetty");
            mySoulGem.Name = "My Soul Gem";
            mySoulGem.Value = 200;
            mySoulGem.ContainedSoul = SoulGem.Level.Petty;
            mySoulGem.MaximumCapacity = SoulGem.Level.Greater;
            if (isReusable)
                (mySoulGem.Keywords ??= new()).Add(Skyrim.Keyword.ReusableSoulGem);

            if (createFilled)
            {
                var myFilledSoulGem = masterMod.SoulGems.AddNew("mySoulGemFilled");
                myFilledSoulGem.Name = "My Soul Gem";
                myFilledSoulGem.Value = 400;
                myFilledSoulGem.ContainedSoul = SoulGem.Level.Greater;
                myFilledSoulGem.MaximumCapacity = SoulGem.Level.Greater;
                if (isReusable)
                {
                    (myFilledSoulGem.Keywords ??= new()).Add(Skyrim.Keyword.ReusableSoulGem);
                    myFilledSoulGem.LinkedTo.SetTo(mySoulGem);
                }
            }

            var fs = new MockFileSystem();
            fs.AddDirectory(DataFolderPath);

            var program = new Program(loadOrder, DataFolderPath, patchMod, fs);
            program.Run();

            fs.FileExists(configFilePath).Should().BeFalse();

            patchMod.SoulGems.Should().BeEmpty();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void TestIgnoredNameless(bool isReusable, bool createFilled)
        {
            var masterMod = new SkyrimMod(MasterModKey, SkyrimRelease.SkyrimSE);

            var patchMod = new SkyrimMod(PatchModKey, SkyrimRelease.SkyrimSE);

            var loadOrder = new LoadOrder<IModListing<ISkyrimModGetter>>()
            {
                new ModListing<ISkyrimModGetter>(masterMod),
                new ModListing<ISkyrimModGetter>(patchMod),
            };

            var mySoulGem = masterMod.SoulGems.AddNew("mySoulGemFilledPetty");
            mySoulGem.Value = 200;
            mySoulGem.ContainedSoul = SoulGem.Level.Petty;
            mySoulGem.MaximumCapacity = SoulGem.Level.Greater;
            if (isReusable)
                (mySoulGem.Keywords ??= new()).Add(Skyrim.Keyword.ReusableSoulGem);

            if (createFilled)
            {
                var myFilledSoulGem = masterMod.SoulGems.AddNew("mySoulGemFilled");
                myFilledSoulGem.Value = 400;
                myFilledSoulGem.ContainedSoul = SoulGem.Level.Greater;
                myFilledSoulGem.MaximumCapacity = SoulGem.Level.Greater;
                if (isReusable)
                {
                    (myFilledSoulGem.Keywords ??= new()).Add(Skyrim.Keyword.ReusableSoulGem);
                    myFilledSoulGem.LinkedTo.SetTo(mySoulGem);
                }
            }

            var fs = new MockFileSystem();
            fs.AddDirectory(DataFolderPath);

            var program = new Program(loadOrder, DataFolderPath, patchMod, fs);
            program.Run();

            fs.FileExists(configFilePath).Should().BeFalse();

            patchMod.SoulGems.Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TestIgnoredInvalid(bool isReusable)
        {
            var masterMod = new SkyrimMod(MasterModKey, SkyrimRelease.SkyrimSE);

            var patchMod = new SkyrimMod(PatchModKey, SkyrimRelease.SkyrimSE);

            var loadOrder = new LoadOrder<IModListing<ISkyrimModGetter>>()
            {
                new ModListing<ISkyrimModGetter>(masterMod),
                new ModListing<ISkyrimModGetter>(patchMod),
            };

            var mySoulGem = masterMod.SoulGems.AddNew("mySoulGemFilledPetty");
            mySoulGem.Value = 200;
            mySoulGem.ContainedSoul = SoulGem.Level.Greater;
            mySoulGem.MaximumCapacity = SoulGem.Level.Lesser;
            if (isReusable)
                (mySoulGem.Keywords ??= new()).Add(Skyrim.Keyword.ReusableSoulGem);

            var fs = new MockFileSystem();
            fs.AddDirectory(DataFolderPath);

            var program = new Program(loadOrder, DataFolderPath, patchMod, fs);
            program.Run();

            fs.FileExists(configFilePath).Should().BeFalse();

            patchMod.SoulGems.Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TestIgnoredInvalidBlack(bool isReusable)
        {
            var masterMod = new SkyrimMod(MasterModKey, SkyrimRelease.SkyrimSE);

            var patchMod = new SkyrimMod(PatchModKey, SkyrimRelease.SkyrimSE);

            var loadOrder = new LoadOrder<IModListing<ISkyrimModGetter>>()
            {
                new ModListing<ISkyrimModGetter>(masterMod),
                new ModListing<ISkyrimModGetter>(patchMod),
            };

            var mySoulGem = masterMod.SoulGems.AddNew("mySoulGem");
            mySoulGem.Value = 200;
            mySoulGem.MajorFlags = SoulGem.MajorFlag.CanHoldNpcSoul;
            mySoulGem.ContainedSoul = SoulGem.Level.None;
            mySoulGem.MaximumCapacity = SoulGem.Level.Lesser;
            if (isReusable)
                (mySoulGem.Keywords ??= new()).Add(Skyrim.Keyword.ReusableSoulGem);

            var fs = new MockFileSystem();
            fs.AddDirectory(DataFolderPath);

            var program = new Program(loadOrder, DataFolderPath, patchMod, fs);
            program.Run();

            fs.FileExists(configFilePath).Should().BeFalse();

            patchMod.SoulGems.Should().BeEmpty();
        }
    }
}
