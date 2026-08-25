using System.Text;
using RPC.Content;

namespace RPC.Tests;

/// <summary>
/// Covers the catalogue a shipped build actually reads from.
///
/// Content is loaded through one of two catalogues: the filesystem during development, and a
/// packed .rpk in a release. Only the first had any coverage, so every test in this suite — and
/// the whole browser suite, which runs against a dev host — exercised a code path that the
/// shipped game does not use. A packed build that could not list its own segment directories
/// would have generated dungeons out of nothing, and nothing here would have failed.
/// </summary>
public class RpkCatalogTests
{
    private static RpkCatalog CatalogWith(params (string Path, string Content)[] entries) =>
        new(TestContentPack.Reader(entries));

    [Fact]
    public void Finds_A_File_By_Its_Packed_Path()
    {
        var catalog = CatalogWith(("classes/bonewarden.json", """{"id":"bonewarden"}"""));

        Assert.True(catalog.Exists("classes/bonewarden.json"));
        Assert.Equal("""{"id":"bonewarden"}""", catalog.GetString("classes/bonewarden.json"));
    }

    [Fact]
    public void Missing_Files_Report_Absent_Rather_Than_Throwing()
    {
        var catalog = CatalogWith(("classes/bonewarden.json", "{}"));

        Assert.False(catalog.Exists("classes/nobody.json"));
        Assert.Null(catalog.GetString("classes/nobody.json"));
    }

    /// <summary>
    /// Callers pass paths the way the filesystem catalogue accepts them — with a leading separator,
    /// or with the platform's backslashes — and a pack stores neither. The two catalogues have to
    /// answer the same question the same way or content resolves in development and vanishes in a
    /// release.
    /// </summary>
    [Theory]
    [InlineData("/classes/bonewarden.json")]
    [InlineData(@"classes\bonewarden.json")]
    [InlineData(@"\classes\bonewarden.json")]
    public void Accepts_The_Same_Path_Spellings_The_Filesystem_Catalogue_Does(string spelling)
    {
        var catalog = CatalogWith(("classes/bonewarden.json", "{}"));

        Assert.True(catalog.Exists(spelling));
        Assert.NotNull(catalog.GetString(spelling));
    }

    /// <summary>
    /// Listing a directory is what content loading is built on — segments, classes and encounter
    /// tables are all discovered rather than named. This is the operation that used to reach into
    /// the reader's private dictionary by reflection and quietly yield nothing if it could not find
    /// it.
    /// </summary>
    [Fact]
    public void Lists_Only_The_Matching_Files_In_A_Directory()
    {
        var catalog = CatalogWith(
            ("segments/ossuary/hall.json", "{}"),
            ("segments/ossuary/crypt.json", "{}"),
            ("segments/ossuary/readme.md", "notes"),
            ("segments/sump/pool.json", "{}"),
            ("classes/bonewarden.json", "{}"));

        var found = catalog.EnumerateFiles("segments/ossuary", "*.json").OrderBy(p => p).ToArray();

        Assert.Equal(new[] { "segments/ossuary/crypt.json", "segments/ossuary/hall.json" }, found);
    }

    /// <summary>
    /// A sibling directory whose name merely starts with the requested one must not be swept in —
    /// "segments/sump" is not inside "segments/su".
    /// </summary>
    [Fact]
    public void Directory_Listing_Does_Not_Prefix_Match_A_Sibling()
    {
        var catalog = CatalogWith(
            ("segments/sump/pool.json", "{}"),
            ("segments/su/other.json", "{}"));

        var found = catalog.EnumerateFiles("segments/su", "*.json").ToArray();

        Assert.Equal(new[] { "segments/su/other.json" }, found);
    }

    [Fact]
    public void Directory_Listing_Of_An_Unknown_Directory_Is_Empty()
    {
        var catalog = CatalogWith(("segments/ossuary/hall.json", "{}"));

        Assert.Empty(catalog.EnumerateFiles("segments/nowhere", "*.json"));
    }
}

/// <summary>
/// Builds an in-memory .rpk so tests can exercise the packed-content path without a build step.
/// Mirrors the layout the content-pack tool writes: a "RPK1" header, a version and an entry count,
/// then per entry its data offset, data length, path length, path and bytes.
/// </summary>
internal static class TestContentPack
{
    public static byte[] Bytes(params (string Path, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RPK1"));
        writer.Write(1u);
        writer.Write((uint)entries.Length);

        foreach (var (path, content) in entries)
        {
            var pathBytes = Encoding.UTF8.GetBytes(path);
            var data = Encoding.UTF8.GetBytes(content);

            // The reader verifies that the recorded offset is where the data actually starts.
            writer.Write((uint)(stream.Position + 4 + 4 + 2 + pathBytes.Length));
            writer.Write((uint)data.Length);
            writer.Write((ushort)pathBytes.Length);
            writer.Write(pathBytes);
            writer.Write(data);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static ContentPackReader Reader(params (string Path, string Content)[] entries)
    {
        var reader = new ContentPackReader();
        reader.Read(Bytes(entries));
        return reader;
    }
}
