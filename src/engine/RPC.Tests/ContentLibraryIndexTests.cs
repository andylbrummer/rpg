using System.Linq;
using RPC.Engine.Content;

namespace RPC.Tests;

public class ContentLibraryIndexTests
{
    private sealed class FakeCatalog : IContentCatalog
    {
        // file path -> json content
        private readonly Dictionary<string, string> _files = new();

        public FakeCatalog Add(string dir, string name, string json)
        {
            _files[$"{dir}/{name}"] = json;
            return this;
        }

        public bool Exists(string path) => _files.ContainsKey(path);
        public string? GetString(string path) => _files.TryGetValue(path, out var v) ? v : null;
        public IEnumerable<string> EnumerateFiles(string directory, string pattern)
            => _files.Keys.Where(k => k.StartsWith($"{directory}/", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Build_IndexesArrayFiles_ByIdAndTag()
    {
        var catalog = new FakeCatalog()
            .Add("enemies", "beasts.json", """[{"id":"rat","tags":["beast","small"]},{"id":"wolf","tags":["beast"]}]""");

        var index = ContentLibraryIndex.Build(catalog, new[] { "enemies" });

        Assert.True(index.Contains("rat"));
        Assert.True(index.Contains("wolf"));
        Assert.Equal("enemies", index.Get("rat")!.Category);
        Assert.Equal(new[] { "rat", "wolf" }, index.ByTag("beast"));
        Assert.Equal(new[] { "rat" }, index.ByTag("small"));
    }

    [Fact]
    public void Build_IndexesSingleObjectFiles()
    {
        var catalog = new FakeCatalog()
            .Add("factions", "bureau.json", """{"id":"bureau","tags":["lawful"]}""");

        var index = ContentLibraryIndex.Build(catalog, new[] { "factions" });
        Assert.True(index.Contains("bureau"));
        Assert.Contains("bureau", index.ByTag("lawful"));
    }

    [Fact]
    public void Build_TracksDuplicateIds_FirstWins()
    {
        var catalog = new FakeCatalog()
            .Add("enemies", "a.json", """[{"id":"rat"}]""")
            .Add("items", "b.json", """[{"id":"rat"}]""");

        var index = ContentLibraryIndex.Build(catalog, new[] { "enemies", "items" });

        Assert.Equal(1, index.Count);
        Assert.Contains("rat", index.DuplicateIds);
    }

    [Fact]
    public void Build_SkipsMalformedJson()
    {
        var catalog = new FakeCatalog()
            .Add("enemies", "good.json", """[{"id":"rat"}]""")
            .Add("enemies", "bad.json", "{not json");

        var index = ContentLibraryIndex.Build(catalog, new[] { "enemies" });
        Assert.True(index.Contains("rat"));
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void FindMissing_ReturnsUnresolvedReferences()
    {
        var catalog = new FakeCatalog()
            .Add("enemies", "e.json", """[{"id":"rat"},{"id":"wolf"}]""");
        var index = ContentLibraryIndex.Build(catalog, new[] { "enemies" });

        var missing = ContentReferenceValidator.FindMissing(index, new[] { "rat", "ghost", "wolf", "ghost" });

        Assert.Equal(new[] { "ghost" }, missing); // unresolved + de-duplicated
        Assert.False(ContentReferenceValidator.AllResolve(index, new[] { "rat", "ghost" }));
        Assert.True(ContentReferenceValidator.AllResolve(index, new[] { "rat", "wolf" }));
    }

    [Fact]
    public void Build_FromRealContentCatalog_IndexesEntries()
    {
        var index = ContentLibraryIndex.Build(new FileSystemCatalog());
        Assert.True(index.Count > 0);
    }
}
