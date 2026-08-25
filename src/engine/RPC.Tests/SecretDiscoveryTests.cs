using System.Linq;
using RPC.Engine;
using RPC.Engine.Dungeons;

namespace RPC.Tests;

public class SecretDiscoveryTests
{
    [Fact]
    public void ReadDocument_DiscoversLinkedSecret_WithTelemetry()
    {
        var gs = new GameState(seed: 1);
        gs.Secrets.Register(new SecretDef("secret-vault-east", "concealed_compartment", "doc-ledger", "A loose floorboard?"));

        var discovered = gs.ReadDocument("doc-ledger");

        Assert.Contains("secret-vault-east", discovered);
        Assert.True(gs.Journal.IsDiscovered("secret-vault-east"));

        var secretLog = gs.ActionLog.First(e => e.Type == "secret_discovered");
        Assert.Equal("concealed_compartment", secretLog.Payload["secretType"]);
        Assert.Equal("secret-vault-east", secretLog.Payload["secretId"]);
        Assert.Equal("document", secretLog.Payload["trigger"]);

        Assert.Contains(gs.ActionLog, e => e.Type == "document_read" && e.Payload["documentId"] == "doc-ledger");
        Assert.Contains("secret-vault-east", gs.Analytics.GetData().SecretsDiscovered);
        Assert.Contains("doc-ledger", gs.Analytics.GetData().DocumentsRead);
    }

    [Fact]
    public void ReadDocument_UnlinkedDocument_DiscoversNothing()
    {
        var gs = new GameState(seed: 1);
        gs.Secrets.Register(new SecretDef("s1", "illusory_floor", "doc-A"));

        var discovered = gs.ReadDocument("doc-B");

        Assert.Empty(discovered);
        Assert.False(gs.Journal.IsDiscovered("s1"));
    }

    [Fact]
    public void ReadDocument_IsIdempotent_PerDocument()
    {
        var gs = new GameState(seed: 1);
        gs.Secrets.Register(new SecretDef("s1", "illusory_floor", "doc-A"));

        var first = gs.ReadDocument("doc-A");
        var second = gs.ReadDocument("doc-A");

        Assert.Single(first);
        Assert.Empty(second);
        Assert.Equal(1, gs.ActionLog.Count(e => e.Type == "secret_discovered"));
        Assert.Equal(1, gs.ActionLog.Count(e => e.Type == "document_read"));
    }

    [Fact]
    public void ReadDocument_SkipsAlreadyDiscoveredSecret()
    {
        var gs = new GameState(seed: 1);
        gs.Secrets.Register(new SecretDef("s1", "illusory_floor", "doc-A"));
        Assert.True(gs.DiscoverSecret("illusory_floor", "s1")); // found by another means first

        var discovered = gs.ReadDocument("doc-A");

        Assert.Empty(discovered); // nothing new — the secret was already known
    }

    [Fact]
    public void OneDocument_CanRevealMultipleSecrets()
    {
        var gs = new GameState(seed: 1);
        gs.Secrets.Register(new SecretDef("s1", "concealed_compartment", "doc-map"));
        gs.Secrets.Register(new SecretDef("s2", "illusory_floor", "doc-map"));

        var discovered = gs.ReadDocument("doc-map");

        Assert.Equal(2, discovered.Count);
        Assert.Contains("s1", discovered);
        Assert.Contains("s2", discovered);
    }

    [Fact]
    public void DiscoverSecret_IsIdempotent()
    {
        var gs = new GameState(seed: 1);

        Assert.True(gs.DiscoverSecret("breakable_wall", "s1"));
        Assert.False(gs.DiscoverSecret("breakable_wall", "s1"));
        Assert.Equal(1, gs.ActionLog.Count(e => e.Type == "secret_discovered"));
    }

    [Fact]
    public void Reset_ClearsSecretRegistry_NoCrossRunLeak()
    {
        var gs = new GameState(seed: 1);
        gs.Secrets.Register(new SecretDef("s1", "illusory_floor", "doc-A"));
        Assert.NotNull(gs.Secrets.Get("s1"));

        gs.Reset();

        // Secrets from the prior run must not survive into the next.
        Assert.Null(gs.Secrets.Get("s1"));
        Assert.Empty(gs.ReadDocument("doc-A"));
    }

    [Fact]
    public void DiscoverSecret_NullType_DefaultsToUnknown()
    {
        var gs = new GameState(seed: 1);
        Assert.True(gs.DiscoverSecret(null, "s1"));
        var entry = gs.ActionLog.First(e => e.Type == "secret_discovered");
        Assert.Equal("unknown", entry.Payload["secretType"]);
    }

    [Fact]
    public void SecretRegistry_LoadFromJson_ParsesDocLink()
    {
        var registry = new SecretRegistry();
        registry.LoadFromJson("{\"id\":\"s1\",\"type\":\"illusory_floor\",\"docLinkId\":\"doc-map\",\"hint\":\"The tiles ring hollow.\"}");

        var secret = registry.Get("s1");
        Assert.NotNull(secret);
        Assert.Equal("doc-map", secret!.DocLinkId);
        Assert.Equal("illusory_floor", secret.Type);
        Assert.Contains("s1", registry.SecretsForDocument("doc-map"));
        Assert.Empty(registry.SecretsForDocument("doc-other"));
    }

    // ---- Bloodline-gated secrets (T59c) ----

    [Fact]
    public void DiscoverSecret_BloodlineLocked_RefusesWhenFamilyNameMismatches()
    {
        var gs = new GameState(seed: 1);
        gs.Campaign.FamilyName = "Ashford";
        gs.Secrets.Register(new SecretDef("crypt-vault", "sealed_vault", BloodlineRequirement: "Thornwick"));

        var discovered = gs.DiscoverSecret("sealed_vault", "crypt-vault");

        Assert.False(discovered);
        Assert.False(gs.Journal.IsDiscovered("crypt-vault"));
        Assert.DoesNotContain(gs.ActionLog, e => e.Type == "secret_discovered");
        var locked = gs.ActionLog.First(e => e.Type == "secret_bloodline_locked");
        Assert.Equal("crypt-vault", locked.Payload["secretId"]);
        Assert.Equal("Thornwick", locked.Payload["requiredBloodline"]);
    }

    [Fact]
    public void DiscoverSecret_BloodlineLocked_AllowsWhenFamilyNameMatchesCaseInsensitive()
    {
        var gs = new GameState(seed: 1);
        gs.Campaign.FamilyName = "thornwick";
        gs.Secrets.Register(new SecretDef("crypt-vault", "sealed_vault", BloodlineRequirement: "Thornwick"));

        var discovered = gs.DiscoverSecret("sealed_vault", "crypt-vault");

        Assert.True(discovered);
        Assert.True(gs.Journal.IsDiscovered("crypt-vault"));
        Assert.Contains(gs.ActionLog, e => e.Type == "secret_discovered" && e.Payload["secretId"] == "crypt-vault");
        Assert.DoesNotContain(gs.ActionLog, e => e.Type == "secret_bloodline_locked");
    }

    [Fact]
    public void DiscoverSecret_NoBloodlineRequirement_DiscoversRegardlessOfFamilyName()
    {
        var gs = new GameState(seed: 1);
        gs.Campaign.FamilyName = "Ashford";
        gs.Secrets.Register(new SecretDef("open-secret", "breakable_wall"));

        Assert.True(gs.DiscoverSecret("breakable_wall", "open-secret"));
    }

    [Fact]
    public void ReadDocument_BloodlineLockedSecret_NotRevealedOnMismatch()
    {
        var gs = new GameState(seed: 1);
        gs.Campaign.FamilyName = "Ashford";
        gs.Secrets.Register(new SecretDef("crypt-vault", "sealed_vault", "doc-ledger", BloodlineRequirement: "Thornwick"));

        var discovered = gs.ReadDocument("doc-ledger");

        Assert.Empty(discovered);
        Assert.False(gs.Journal.IsDiscovered("crypt-vault"));
        Assert.Contains(gs.ActionLog, e => e.Type == "secret_bloodline_locked");
    }

    [Fact]
    public void SecretRegistry_LoadFromJson_ParsesBloodlineRequirement()
    {
        var registry = new SecretRegistry();
        registry.LoadFromJson("{\"id\":\"s1\",\"type\":\"sealed_vault\",\"bloodlineRequirement\":\"Thornwick\"}");

        var secret = registry.Get("s1");
        Assert.NotNull(secret);
        Assert.Equal("Thornwick", secret!.BloodlineRequirement);
    }
}
