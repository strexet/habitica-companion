using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceProjectAliasTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-23T10:00:00Z");
    private static JsonDocument Fixture()=>JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","project-alias-v1.json")));
    private static WorkspaceJournalSnapshot Initial()=>new(new(){Id=Guid.NewGuid()},1,[],[]);
    private static WorkspaceOrganizationCommand Command(WorkspaceJournalSnapshot state,WorkspaceOrganizationPatch patch,Guid? project=null)=>new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOrganizationKind.Project,project is null?WorkspaceOrganizationOperation.Create:WorkspaceOrganizationOperation.Update,project??Guid.NewGuid(),patch);
    private static WorkspaceJournalSnapshot Apply(WorkspaceJournalSnapshot state,WorkspaceOrganizationCommand command)
    {
        var result=WorkspaceOrganizationEngine.Accept(state,command,Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);
        return WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(result.Snapshot!));
    }
    [Fact]
    public void Portable_hashes_lifecycle_and_original_receipts_retain_exact_identity()
    {
        using var fixture=Fixture();var state=WorkspaceJournalJson.Deserialize(fixture.RootElement.GetProperty("initial").GetRawText());
        foreach(var step in fixture.RootElement.GetProperty("steps").EnumerateArray())
        {
            var command=WorkspaceTaskApiJson.Deserialize<WorkspaceOrganizationCommand>(step.GetProperty("command").GetRawText());
            Assert.Equal(step.GetProperty("payloadHash").GetString(),WorkspaceOrganizationCommands.Hash(command));state=Apply(state,command);
            Assert.True(WorkspaceTaskApiJson.Deserialize<WorkspaceOrganizationSnapshot>(step.GetProperty("snapshot").GetRawText()).Equivalent(state.Document.OrganizationHistory![^1].Snapshot));
            Assert.Empty(state.Commands);Assert.Empty(state.Events!);Assert.Empty(state.Document.Tasks);
        }
        foreach(var entry in state.Document.OrganizationHistory!)
        {
            var result=WorkspaceOrganizationEngine.Accept(state,entry.Command,Now);Assert.Equal(SubmissionDisposition.Duplicate,result.Disposition);Assert.Equal(entry,result.Entry);
        }
        Assert.True(state.Document.Projects[1].IsRemoved);Assert.Null(state.Document.Projects[1].Alias);
    }
    [Theory]
    [InlineData("")][InlineData(" ")][InlineData("a--b")][InlineData("-a")][InlineData("a-")][InlineData("a_b")][InlineData("a b")][InlineData("česky")][InlineData("K")][InlineData("<codev>")]
    public void Invalid_aliases_and_mixed_clear_cannot_enter_commands(string alias)
    {
        var state=Initial();Assert.False(WorkspaceOrganizationCommands.IsValid(Command(state,new(Name:"Name",Alias:alias))));
        Assert.False(WorkspaceOrganizationCommands.IsValid(Command(state,new(Name:"Name",Alias:"valid",ClearAlias:true))));
        Assert.False(WorkspaceOrganizationCommands.IsValid(Command(state,new(Name:"Name",Alias:"valid")) with {Kind=WorkspaceOrganizationKind.Label}));
    }
    [Fact]
    public void Uniqueness_imports_stale_writers_and_workspace_isolation_preserve_tasks()
    {
        var state=Initial();var a=Command(state,new(Name:"First",Alias:" CodeV "));state=Apply(state,a);var before=state;
        var b=Command(state,new(Name:"Second",Alias:"CODEV"));Assert.Equal("project_alias_in_use",WorkspaceOrganizationEngine.Accept(state,b,Now).ErrorCode);
        state=Apply(state,Command(state,new(Name:"renamed"),a.EntityId));Assert.Equal("codev",state.Document.Projects[0].Alias);
        Assert.Equal(SubmissionDisposition.RevisionConflict,WorkspaceOrganizationEngine.Accept(state,b,Now).Disposition);
        var other=Initial();other=Apply(other,Command(other,new(Name:"Other owner",Alias:"codev")));Assert.Equal("codev",other.Document.Projects[0].Alias);
        var duplicate=before.Document with {Projects=[..before.Document.Projects,new(Guid.NewGuid(),2,"Duplicate",Alias:"codev")]};Assert.Throws<JsonException>(()=>WorkspaceJson.Serialize(duplicate));
        var malformed=before.Document with {Projects=[before.Document.Projects[0] with {Alias="CodeV"}]};Assert.Throws<JsonException>(()=>WorkspaceJson.Serialize(malformed));
        Assert.DoesNotContain("\"alias\"",WorkspaceJson.Serialize(Initial().Document));
        var marker=Initial();marker=Apply(marker,Command(marker,new(Name:"<codev> Marker only")));Assert.Null(marker.Document.Projects[0].Alias);
        Assert.Equal(before.Document.Tasks,state.Document.Tasks);Assert.Equal(before.Commands,state.Commands);Assert.Equal(before.Events,state.Events);
        Assert.False(WorkspaceProjectAliases.IsValid(new string('a',65)));Assert.True(WorkspaceProjectAliases.IsValid(new string('a',64)));
    }
    [Fact]
    public void Imported_history_cannot_hide_an_intermediate_alias_collision()
    {
        var state=Initial();var a=Command(state,new(Name:"A",Alias:"reserved"));state=Apply(state,a);
        var b=Command(state,new(Name:"B",Alias:"unique"));state=Apply(state,b);
        state=Apply(state,Command(state,new(Alias:"final"),b.EntityId));
        var history=state.Document.OrganizationHistory!.ToArray();var forged=b with {Patch=b.Patch! with {Alias="reserved"}};
        history[1]=history[1] with {Command=forged,PayloadHash=WorkspaceOrganizationCommands.Hash(forged),Snapshot=history[1].Snapshot with {Alias="reserved"}};
        Assert.False(WorkspaceJournalValidation.IsValid(state with {Document=state.Document with {OrganizationHistory=history}}));
    }
    [Fact]
    public void Resolution_refuses_name_alias_collisions_and_never_guesses_markers()
    {
        var a=new WorkspaceProject(Guid.NewGuid(),1,"Exact",Alias:"codev");var b=new WorkspaceProject(Guid.NewGuid(),1,"codev");var c=new WorkspaceProject(Guid.NewGuid(),1,"Exact",IsArchived:true);
        var document=new WorkspaceDocument{Id=Guid.NewGuid(),Projects=[a,b,c]};
        Assert.Equal(a.Id,WorkspaceOrganization.ResolveProject(document,a.Id.ToString("D")).Id);
        Assert.Equal("ambiguous_project",Assert.Throws<InvalidOperationException>(()=>WorkspaceOrganization.ResolveProject(document,"codev")).Message);
        Assert.Equal("ambiguous_project",Assert.Throws<InvalidOperationException>(()=>WorkspaceOrganization.ResolveProject(document,"Exact")).Message);
        Assert.Equal(a.Id,WorkspaceOrganization.ResolveProject(document," CODEV ").Id);
        Assert.Equal("stale_revision",Assert.Throws<InvalidOperationException>(()=>WorkspaceOrganization.ResolveProject(document,"CODEV",2)).Message);
        Assert.Equal("project_unavailable",Assert.Throws<InvalidOperationException>(()=>WorkspaceOrganization.ResolveProject(document,"<codev>")).Message);
    }
}
