using Habitica.Application.Notes;
using Habitica.Domain.Notes;
using Habitica.Domain.Workspace;

namespace Habitica.WebApp.State;

public sealed record NotesProjectReview(NoteCommand Command,NoteProjectLink? Before,NoteProjectLink? After,string BeforeLabel,string AfterLabel);
public sealed partial class NotesController
{
    private readonly TodoWorkspacePresentation? _projects;
    private NoteProjectLink? _originalProject;
    public NotesProjectReview? ProjectReview {get;private set;}
    public bool CanRead=>_account is not null&&(Source==NotesSource.Browser||CloudConnected);
    private string ProjectSource=>Source==NotesSource.Browser?"browser":"cloud";
    public bool ProjectContextAvailable=>_projects?.Snapshot is not null&&_projects.Selection?.Source==(Source==NotesSource.Browser?TodoWorkspaceSource.Browser:TodoWorkspaceSource.Cloud);
    public IReadOnlyList<WorkspaceProject> ProjectChoices=>ProjectContextAvailable&&_projects!.Commands.IsReadOnly==false?_projects!.Snapshot!.Document.Projects.Where(p=>!p.IsArchived&&!p.IsRemoved).ToArray():[];
    public Guid? ProjectWorkspaceId=>ProjectContextAvailable&&_projects!.Commands.IsReadOnly==false?_projects!.Selection!.WorkspaceId:null;
    private void ProjectsChanged()=>Changed?.Invoke();
    public NoteProjectLink? ProjectLink(Guid projectId)=>Book is not null&&ProjectWorkspaceId is {} workspace&&ProjectChoices.Any(p=>p.Id==projectId)
        ?new(projectId,workspace,ProjectSource,Book.OwnerScope):null;
    private bool EligibleProject(NoteProjectLink link)=>ProjectLink(link.ProjectId)==link;
    public string ProjectLabel(NoteProjectLink? link)
    {
        if(link is null)return "Unassigned";
        var state="unavailable";string? name=null;
        if(CanRead&&Book?.OwnerScope==link.OwnerScope&&ProjectContextAvailable&&link.WorkspaceId==ProjectWorkspaceId&&link.Source==ProjectSource)
        {
            var project=_projects!.Snapshot!.Document.Projects.FirstOrDefault(p=>p.Id==link.ProjectId);
            state=project is null?"missing":project.IsRemoved?"removed":project.IsArchived?"archived":"active";name=project?.Name;
        }
        return $"{name??"Retained project"} · {state} · {link.ProjectId:D} · workspace {link.WorkspaceId:D} · {link.Source}";
    }
    public bool ChooseProject(Guid? id)
    {
        EnsureAccount();if(!CanWrite||HasConflict||ProjectReview is not null||Selected?.Current.IsTrashed==true)return false;
        var link=id is {} project?ProjectLink(project):null;if(id is not null&&link is null)return false;
        Draft.Project=link;Changed?.Invoke();return true;
    }
    public NotesQueryPage? Search(string query,bool trash,int offset,long? revision,NotesProjectFilter? filter=null)
        =>CanRead&&Book is not null?NotesQueries.Search(Book,query,trash,offset,revision,filter):null;
    public Task AcceptProjectAsync(CancellationToken token=default)=>Observe("notes-save",async()=>
    {
        EnsureAccount();if(ProjectReview is not {} review||!CanWrite||HasPending)return;
        if(HasConflict||Book!.Revision!=review.Command.ExpectedNotebookRevision||SelectedId!=(_editorNoteRevision==0?null:review.Command.NoteId)
            ||(review.Command.Title??_originalTitle)!=Draft.Title||(review.Command.Markdown??_originalMarkdown)!=Draft.Markdown
            ||Draft.Project!=review.After||review.After is not null&&!EligibleProject(review.After))
        {ProjectReview=null;Say("The note or project context changed. Your draft was preserved; refresh and review again.",true);return;}
        await SubmitAsync(review.Command,token);
    });
}
