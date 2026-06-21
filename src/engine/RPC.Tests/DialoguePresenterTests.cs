using System.Collections.Generic;
using RPC.Engine;
using RPC.Engine.Town;
using Xunit;

public class DialoguePresenterTests
{
    [Fact]
    public void GameState_ExposesDialogueLine_ForRecruitClass()
    {
        var repo = new DialogueRepository(new List<DialogueDef>
        {
            new("bonewarden", "recruit", new() { ["neutral"] = "Bones at your service." }),
        });
        var state = new GameState(seed: 1, dialogue: repo);
        Assert.Equal("Bones at your service.", state.RecruitDialogue("bonewarden"));
    }

    [Fact]
    public void GameState_DefaultsToEllipsis_WhenNoDialogueRepo()
    {
        var state = new GameState(seed: 1);
        Assert.Equal("...", state.RecruitDialogue("bonewarden"));
    }
}
