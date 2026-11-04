using Habitica.Application.Diagnostics;
using Habitica.WebApp.State;
namespace Habitica.WebApp.Tests.Notes;
public sealed class NotesDiagnosticsTests
{
    [Fact]public async Task Notes_save_and_lifecycle_diagnostics_retain_operations_and_outcomes_without_private_text()
    {
        var f=new NotesFixture();var logs=new Logs();await using var c=new NotesController(f.Session,f.Database,f.Client,new(),new DiagnosticsLogWriter(logs,TimeProvider.System));await c.LoadAsync();c.Draft.Title="private clothing measurements";c.Draft.Markdown="secret birthday";await c.SaveAsync();c.PrepareLifecycle(Habitica.Domain.Notes.NoteOperation.Trash);await c.AcceptLifecycleAsync();
        var rows=await logs.GetRecentAsync(default);Assert.Contains(rows,r=>r.Operation=="notes-save");Assert.Contains(rows,r=>r.Operation=="notes-lifecycle");var json=System.Text.Json.JsonSerializer.Serialize(rows);Assert.DoesNotContain("private clothing",json);Assert.DoesNotContain("secret birthday",json);Assert.Contains(c.LastReceipt!.CommandId.ToString("D"),json);
    }
    [Fact]public async Task Folder_diagnostics_retain_original_identity_without_private_names()
    {
        var f=new NotesFixture();var logs=new Logs();await using var c=new NotesController(f.Session,f.Database,f.Client,new(),new DiagnosticsLogWriter(logs,TimeProvider.System));await c.LoadAsync();Assert.True(c.PrepareFolder(Habitica.Domain.Notes.NoteFolderOperation.Create,null,"private passport archive"));var id=c.FolderReview!.Command.Id;await c.AcceptFolderAsync();
        var rows=await logs.GetRecentAsync(default);Assert.Contains(rows,r=>r.Operation=="notes-folder-save");var json=System.Text.Json.JsonSerializer.Serialize(rows);Assert.DoesNotContain("private passport archive",json);Assert.Contains(id.ToString("D"),json);
    }
    private sealed class Logs:Habitica.Storage.IDiagnosticsLogStore
    {
        private readonly List<Habitica.Domain.Diagnostics.DiagnosticsLogEntry> _rows=[];
        public Task<IReadOnlyList<Habitica.Domain.Diagnostics.DiagnosticsLogEntry>> GetRecentAsync(CancellationToken token)=>Task.FromResult<IReadOnlyList<Habitica.Domain.Diagnostics.DiagnosticsLogEntry>>(_rows);
        public Task AppendAsync(Habitica.Domain.Diagnostics.DiagnosticsLogEntry row,CancellationToken token){_rows.Add(row);return Task.CompletedTask;}
        public Task ClearAsync(CancellationToken token){_rows.Clear();return Task.CompletedTask;}
    }
}
