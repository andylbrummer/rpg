using System.Collections.Generic;
using RPC.Engine.Town;
using Xunit;

public class DialogueRepositoryTests
{
    private static DialogueRepository Make() => new(new List<DialogueDef>
    {
        new("generic", "vendor", new() { ["low"]="L", ["neutral"]="N", ["high"]="H" }),
        new("bureau", "vendor", new() { ["neutral"]="Bureau neutral", ["high"]="Bureau high" }),
        new("bonewarden", "recruit", new() { ["neutral"]="Bones" }),
    });

    [Fact]
    public void GetLine_PicksTierByRep()
    {
        var d = Make();
        Assert.Equal("L", d.GetLine("vendor", "generic", -5));
        Assert.Equal("N", d.GetLine("vendor", "generic", 0));
        Assert.Equal("H", d.GetLine("vendor", "generic", 30));
    }

    [Fact]
    public void GetLine_FallsBackToNeutralThenGenericThenDefault()
    {
        var d = Make();
        Assert.Equal("Bureau neutral", d.GetLine("vendor", "bureau", -50)); // no low → neutral
        Assert.Equal("N", d.GetLine("vendor", "unknown_faction", 0));       // unknown → generic
        Assert.Equal("Bones", d.GetLine("recruit", "bonewarden", 0));
        // real default-fallback test: repo with no generic speaker for "vendor" kind
        var bare = new DialogueRepository(new List<DialogueDef> { new("bonewarden", "recruit", new() { ["neutral"] = "x" }) });
        Assert.Equal("...", bare.GetLine("vendor", "anything", 0));
    }
}
