using MudBlazor.Services;
using Bunit;
using Habitica.Application.Notes;
using Habitica.Domain.Notes;
using Habitica.Storage;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
namespace Habitica.WebApp.Tests.Notes;
public sealed class NotesPanelTests:BunitContext
{
    private NotesFixture Setup(){JSInterop.Mode=JSRuntimeMode.Loose;var f=new NotesFixture();Services.AddSingleton<IAppSessionController>(f.Session);Services.AddSingleton(f.Controller);return f;}
    private static void Evidence(string name,string markup,string state,string test)=>Qualification.PageLayoutEvidence.Save(name,markup,"/notes",state,["form","actions","confirmation","tables","disabled-controls","error"],test);
    [Fact] public async Task Initial_loading_fences_New_until_the_component_can_preserve_editor_entry()
    {
        var f=Setup();await f.Controller.LoadAsync();var before=NotesJson.Serialize(f.Controller.Book!);
        f.Database.Current.Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Database.Current.Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        IRenderedComponent<NotesPanel>? cut=null;
        try
        {
            cut=Render<NotesPanel>();await f.Database.Current.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(cut.Find("[data-testid=note-new]").HasAttribute("disabled"));
            Assert.Equal("true",cut.Find("[data-testid=notes-panel]").GetAttribute("aria-busy"));
            Assert.Contains("Loading Notes",cut.Find("[data-testid=notes-initializing]").TextContent);
            Evidence("notes-initial-load",cut.Markup,"retained-notebook-initial-load-entry-fenced",nameof(Initial_loading_fences_New_until_the_component_can_preserve_editor_entry));
        }
        finally {f.Database.Current.Release.TrySetResult();}
        cut!.WaitForAssertion(()=>Assert.False(cut.Find("[data-testid=note-new]").HasAttribute("disabled")));
        await cut.Find("[data-testid=note-new]").ClickAsync(new());
        cut.WaitForAssertion(()=>Assert.Equal("editor",cut.Find("[data-testid=notes-workbench]").GetAttribute("data-notes-mode")));
        cut.Find("[data-testid=note-title]").Input("Exact unsaved entry");cut.Find("[data-testid=note-body]").Input("Keep original <>& 🧥");
        cut.Render();Assert.Equal("editor",cut.Find("[data-testid=notes-workbench]").GetAttribute("data-notes-mode"));
        Assert.Equal("Exact unsaved entry",f.Controller.Draft.Title);Assert.Equal("Keep original <>& 🧥",f.Controller.Draft.Markdown);
        Assert.Equal(before,NotesJson.Serialize(f.Controller.Book!));Assert.Null(f.Database.Current.Book);Assert.Equal(0,f.Client.Submits);
        Evidence("notes-initial-entry",cut.Markup,"released-initial-load-retains-unsaved-editor",nameof(Initial_loading_fences_New_until_the_component_can_preserve_editor_entry));
    }
    [Fact] public async Task Phone_library_editor_back_and_resume_preserve_exact_drafts_versions_and_navigation_guards()
    {
        var f=Setup();await f.Controller.LoadAsync();
        for(var i=0;i<25;i++){f.Controller.NewNote(true);f.Controller.Draft.Title="Existing note "+i;f.Controller.Draft.Markdown="Exact source "+i+"\r\n```md\r\n<literal> 🧥\r\n```";await f.Controller.SaveAsync();}
        f.Controller.Draft.Markdown+="\r\nSecond revision";await f.Controller.SaveAsync();f.Controller.NewNote(true);
        var original=NotesJson.Serialize(f.Database.Current.Book!);var cut=Render<NotesPanel>();
        Assert.Equal("library",cut.Find("[data-testid=notes-workbench]").GetAttribute("data-notes-mode"));
        Evidence("phone-notes-library",cut.Markup,"long-library-with-explicit-editor-entry",nameof(Phone_library_editor_back_and_resume_preserve_exact_drafts_versions_and_navigation_guards));
        var evidence=Environment.GetEnvironmentVariable("HABITICA_BROWSER_EVIDENCE");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(Path.Combine(evidence,"phone-notes-book.json"),original);
        cut.Find("[data-testid=note-open]").Click();var id=f.Controller.SelectedId!.Value;var body=f.Controller.Draft.Markdown;var history=f.Controller.Selected!.History;
        Assert.Equal("editor",cut.Find("[data-testid=notes-workbench]").GetAttribute("data-notes-mode"));
        Assert.True(cut.Markup.IndexOf("data-testid=\"note-title\"",StringComparison.Ordinal)<cut.Markup.IndexOf("data-testid=\"note-project-picker\"",StringComparison.Ordinal));
        Assert.True(cut.Markup.IndexOf("data-testid=\"note-body\"",StringComparison.Ordinal)<cut.Markup.IndexOf("data-testid=\"note-folder-picker\"",StringComparison.Ordinal));
        Evidence("phone-notes-editor",cut.Markup,"title-writing-before-optional-associations",nameof(Phone_library_editor_back_and_resume_preserve_exact_drafts_versions_and_navigation_guards));
        cut.Find("[data-testid=note-body]").Input("Private unsaved exact draft\r\n<>&");cut.Find("[data-testid=notes-back]").Click();
        Assert.Equal("library",cut.Find("[data-testid=notes-workbench]").GetAttribute("data-notes-mode"));Assert.Equal(id,f.Controller.SelectedId);Assert.Equal(history,f.Controller.Selected!.History);
        cut.Find($"[data-note-id='{id:D}']").Click();Assert.Empty(cut.FindAll("[aria-label='Unsaved draft']"));Assert.Equal("Private unsaved exact draft\r\n<>&",f.Controller.Draft.Markdown);
        cut.Find("[data-testid=notes-back]").Click();cut.Find("[data-testid=note-new]").Click();Assert.Single(cut.FindAll("[aria-label='Unsaved draft']"));
        Evidence("phone-notes-guard",cut.Markup,"original-unsaved-change-guard-remains",nameof(Phone_library_editor_back_and_resume_preserve_exact_drafts_versions_and_navigation_guards));
        cut.Find("[aria-label='Unsaved draft'] button:nth-child(2)").Click();Assert.Equal("editor",cut.Find("[data-testid=notes-workbench]").GetAttribute("data-notes-mode"));Assert.Equal(id,f.Controller.SelectedId);
        cut.Find("[data-testid=notes-back]").Click();cut.Find("[data-testid=note-new]").Click();cut.Find("[data-testid=note-discard]").Click();Assert.Null(f.Controller.SelectedId);Assert.Equal("",f.Controller.Draft.Markdown);
        cut.Find("[data-testid=note-title]").Input("Unsaved new capture");cut.Find("[data-testid=note-body]").Input("Keep this local draft");cut.Find("[data-testid=notes-back]").Click();cut.Find("[data-testid=note-resume]").Click();
        Assert.Equal("Unsaved new capture",f.Controller.Draft.Title);Assert.Equal("Keep this local draft",f.Controller.Draft.Markdown);Assert.Equal("editor",cut.Find("[data-testid=notes-workbench]").GetAttribute("data-notes-mode"));
        Evidence("phone-notes-draft",cut.Markup,"new-draft-back-resume-without-save",nameof(Phone_library_editor_back_and_resume_preserve_exact_drafts_versions_and_navigation_guards));
        Assert.Equal(original,NotesJson.Serialize(f.Database.Current.Book!));Assert.Equal(0,f.Client.Submits);
    }
    [Fact] public async Task Phone_back_and_resume_keep_unknown_original_save_readonly_until_receipt_recovery()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-new]").Click();cut.Find("[data-testid=note-title]").Input("Unknown original save");cut.Find("[data-testid=note-body]").Input("Exact retained payload");f.Database.Current.LoseResponse=true;cut.Find("form").Submit();
        Assert.True(f.Controller.HasPending);var command=f.Controller.PendingCommandId;var original=NotesJson.Serialize(f.Database.Current.Book!);
        Evidence("phone-notes-pending-editor",cut.Markup,"unknown-original-editor-keeps-exact-payload",nameof(Phone_back_and_resume_keep_unknown_original_save_readonly_until_receipt_recovery));
        cut.Find("[data-testid=notes-back]").Click();Assert.True(f.Controller.HasPending);Assert.Equal(command,f.Controller.PendingCommandId);Assert.True(cut.Find("[data-testid=note-new]").HasAttribute("disabled"));
        Evidence("phone-notes-pending-library",cut.Markup,"unknown-original-library-retains-recovery",nameof(Phone_back_and_resume_keep_unknown_original_save_readonly_until_receipt_recovery));
        cut.Find("[data-testid=note-resume]").Click();Assert.Equal("Exact retained payload",f.Controller.Draft.Markdown);Assert.True(cut.Find("form fieldset").HasAttribute("disabled"));
        await cut.InvokeAsync(()=>f.Controller.RecoverAsync());Assert.False(f.Controller.HasPending);Assert.Equal(command,f.Controller.LastReceipt!.CommandId);Assert.Equal(original,NotesJson.Serialize(f.Database.Current.Book!));Assert.Single(f.Controller.Book!.Notes);Assert.Equal(0,f.Client.Submits);
    }
    [Fact]public void Notes_library_has_explicit_folder_navigation_separate_from_project_assignment()
    {
        Setup();var cut=Render<NotesPanel>();Assert.Single(cut.FindAll("[data-testid=notes-folder-filter]"));Assert.Contains("Without a folder",cut.Markup);Assert.Single(cut.FindAll("[data-testid=note-folder-picker]"));
    }
    [Fact]public async Task Create_search_edit_preview_and_export_preserve_full_markdown_and_safe_rendering()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-new]").Click();cut.Find("[data-testid=note-title]").Input("Clothing 🧥");var markdown="# Measurements\r\n| Body | Size |\r\n| --- | --- |\r\n| Chest | 100 cm |\r\n```\r\n<script>literal</script>\r\n```\r\n![remote](https://image.invalid/x)\r\n";cut.Find("[data-testid=note-body]").Input(markdown);cut.Find("form").Submit();
        Assert.Equal(markdown,Assert.Single(f.Database.Current.Book!.Notes).Current.Markdown);Assert.Contains("Original command receipt confirmed",cut.Markup);
        cut.Find("[data-testid=note-preview]").Click();Assert.Single(cut.FindAll(".notes-markdown table"));Assert.Empty(cut.FindAll(".notes-markdown img,.notes-markdown script"));Assert.Contains("&lt;script&gt;",cut.Markup);
        cut.Find("[data-testid=notes-search]").Input("Measurements");Assert.Single(cut.FindAll("[data-testid=note-open]"));cut.Find("[data-testid=notes-search]").Input("no match");Assert.Empty(cut.FindAll("[data-testid=note-open]"));
        var module=JSInterop.SetupModule("./js/diagnosticsExport.js");module.SetupVoid("downloadTextFile",_=>true).SetVoidResult();cut.Find("[data-testid=note-export]").Click();cut.WaitForAssertion(()=>Assert.Single(module.Invocations));Assert.Equal(markdown,module.Invocations.Single().Arguments[1]);
        Evidence("notes-document",cut.Markup,"saved-safe-preview",nameof(Create_search_edit_preview_and_export_preserve_full_markdown_and_safe_rendering));await Task.CompletedTask;
    }
    [Fact]public void Dirty_navigation_cancel_and_account_switch_do_not_transfer_private_drafts()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-new]").Click();cut.Find("[data-testid=note-title]").Input("Unsaved private");cut.Find("[data-testid=note-body]").Input("private source");cut.Find("[data-testid=note-new]").Click();Assert.Single(cut.FindAll("[aria-label='Unsaved draft']"));Assert.Contains(JSInterop.Invocations,c=>c.Identifier=="Blazor._internal.domWrapper.focus");
        cut.Find("[aria-label='Unsaved draft'] button:nth-child(2)").Click();Assert.Equal("private source",f.Controller.Draft.Markdown);cut.Find("[data-testid=note-new]").Click();f.Session.SetState(f.Session.State with{UserId=Guid.NewGuid().ToString("D")});cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll("[data-testid=note-discard]")));Assert.Empty(f.Controller.Draft.Markdown!);Assert.DoesNotContain("private source",cut.Markup);
        Evidence("notes-empty",cut.Markup,"account-changed-empty",nameof(Dirty_navigation_cancel_and_account_switch_do_not_transfer_private_drafts));
    }
    [Fact]public async Task Separate_consent_and_unavailable_cloud_are_clear_without_uploading_browser_content()
    {
        var f=Setup();await f.Controller.SwitchAsync(NotesSource.Cloud);var cut=Render<NotesPanel>();Assert.Empty(cut.FindAll("[data-testid=note-source]"));Assert.True(cut.Find("[data-testid=notes-connect]").HasAttribute("disabled"));Assert.Contains("16 prior versions",cut.Markup);Assert.Empty(f.Client.Book.Notes);Evidence("notes-consent",cut.Markup,"separate-consent",nameof(Separate_consent_and_unavailable_cloud_are_clear_without_uploading_browser_content));
        cut.Find("[data-testid=notes-consent-check]").Change(true);cut.Find("[data-testid=notes-connect]").Click();Assert.True(f.Client.Connected);Assert.Equal(0,f.Client.Submits);
    }
    [Fact]public void Trash_purge_and_history_actions_have_concrete_cancelable_reviews()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-new]").Click();cut.Find("[data-testid=note-title]").Input("Private retained note");cut.Find("[data-testid=note-body]").Input("draft");cut.Find("form").Submit();cut.Find("[data-testid=note-trash]").Click();Assert.False(f.Controller.Selected!.Current.IsTrashed);cut.Find("[data-testid=note-action-confirm]").Click();Assert.True(f.Controller.Selected!.Current.IsTrashed);
        cut.Find("[data-testid=note-purge]").Click();Assert.Contains("all 2 retained versions",cut.Markup);Assert.True(cut.Find("[data-testid=note-action-confirm]").HasAttribute("disabled"));Evidence("notes-purge",cut.Markup,"irreversible-review",nameof(Trash_purge_and_history_actions_have_concrete_cancelable_reviews));cut.Find("[data-testid=note-review] input").Change(true);cut.Find("[data-testid=note-action-confirm]").Click();Assert.Empty(f.Controller.Book!.Notes);Assert.DoesNotContain("Private retained note",cut.Markup);
    }
    [Fact]public async Task Conflict_and_unknown_acceptance_remain_visible_without_an_extra_write()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-new]").Click();cut.Find("[data-testid=note-title]").Input("Current title");cut.Find("[data-testid=note-body]").Input("Original");cut.Find("form").Submit();cut.Find("[data-testid=note-body]").Input("Unsaved draft");var book=f.Database.Current.Book!;f.Database.Current.Book=NotesEngine.Apply(book,new(Guid.NewGuid(),f.Controller.SelectedId!.Value,1,1,NoteOperation.Update,Markdown:"Other device"),DateTimeOffset.UtcNow).Notebook;await cut.InvokeAsync(()=>f.Controller.LoadAsync());Assert.Single(cut.FindAll("[aria-label='Note conflict']"));Assert.True(cut.Find("[data-testid=note-save]").HasAttribute("disabled"));Evidence("notes-conflict",cut.Markup,"retained-draft-conflict",nameof(Conflict_and_unknown_acceptance_remain_visible_without_an_extra_write));
        cut.Find("[data-testid=notes-reload]").Click();cut.Find("[data-testid=note-discard]").Click();Assert.Equal("Other device",f.Controller.Draft.Markdown);
        f.Database.Current.LoseResponse=true;cut.Find("[data-testid=note-body]").Input("New retained value");cut.Find("form").Submit();Assert.True(f.Controller.HasPending);Evidence("notes-unknown",cut.Markup,"uncertain-original-save",nameof(Conflict_and_unknown_acceptance_remain_visible_without_an_extra_write));cut.Find("[aria-label='Uncertain save'] button").Click();Assert.False(f.Controller.HasPending);Assert.Equal(3,f.Controller.Book!.Revision);
    }
    [Fact]public void Long_editor_and_document_content_are_captured_for_theme_and_enlargement_checks()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-new]").Click();cut.Find("[data-testid=note-title]").Input(new string('T',256));cut.Find("[data-testid=note-body]").Input("# Travel journal\n"+new string('W',500)+"\n| Item | Observations |\n| --- | --- |\n| Jacket | "+new string('W',300)+" |\n```\n"+new string('c',500)+"\n```\n- A related fact\n- Another fact");cut.Find("[data-testid=note-preview]").Click();Evidence("notes-long",cut.Markup,"long-code-table-title",nameof(Long_editor_and_document_content_are_captured_for_theme_and_enlargement_checks));
    }
    [Fact]public void Import_file_is_an_unsaved_exact_draft_and_version_history_can_be_loaded_without_overwriting()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-new]").Click();var text="# Journal\r\n```\r\nraw 🧥\r\n```\r\n";
        cut.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles(InputFileContent.CreateFromText(text,"Journal.md",contentType:"text/markdown"));
        Assert.Equal(text,f.Controller.Draft.Markdown);Assert.Equal("Journal",f.Controller.Draft.Title);Assert.Null(f.Database.Current.Book);cut.Find("form").Submit();cut.Find("[data-testid=note-body]").Input("revised");cut.Find("form").Submit();
        var history=cut.FindAll("details button").Single(b=>b.TextContent.Contains("Load revision"));history.Click();Assert.Equal(text,f.Controller.Draft.Markdown);Assert.Equal("revised",f.Database.Current.Book!.Notes.Single().Current.Markdown);Assert.True(f.Controller.Dirty);
    }
    [Fact]public async Task Unavailable_cloud_and_whole_notebook_cleanup_have_explicit_separate_states()
    {
        var f=Setup();f.Client.IsAvailable=false;await f.Controller.SwitchAsync(NotesSource.Cloud);var cut=Render<NotesPanel>();Assert.Empty(cut.FindAll("[data-testid=note-source]"));Assert.Empty(cut.FindAll("[data-testid=notes-connect]"));Assert.Contains("unavailable in this deployment",cut.Markup);Evidence("notes-unavailable",cut.Markup,"cloud-unavailable-browser-independent",nameof(Unavailable_cloud_and_whole_notebook_cleanup_have_explicit_separate_states));
    }
    [Fact]public void Privacy_page_explains_separate_notes_retention_and_browser_data_deletion()
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var cut=Render<Habitica.WebApp.Pages.PrivacyPage>();Assert.Contains("Personal Notes",cut.Markup);Assert.Contains("export any browser Notes",cut.Markup);Qualification.PageLayoutEvidence.Save("notes-privacy",cut.Markup,"/privacy","notes-data-handling",["disclosure"],nameof(Privacy_page_explains_separate_notes_retention_and_browser_data_deletion));
    }
}
