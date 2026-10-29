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
    [Fact]public async Task Create_search_edit_preview_and_export_preserve_full_markdown_and_safe_rendering()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-title]").Input("Clothing 🧥");var markdown="# Measurements\r\n| Body | Size |\r\n| --- | --- |\r\n| Chest | 100 cm |\r\n```\r\n<script>literal</script>\r\n```\r\n![remote](https://image.invalid/x)\r\n";cut.Find("[data-testid=note-body]").Input(markdown);cut.Find("form").Submit();
        Assert.Equal(markdown,Assert.Single(f.Database.Current.Book!.Notes).Current.Markdown);Assert.Contains("Original command receipt confirmed",cut.Markup);
        cut.Find("[data-testid=note-preview]").Click();Assert.Single(cut.FindAll(".notes-markdown table"));Assert.Empty(cut.FindAll(".notes-markdown img,.notes-markdown script"));Assert.Contains("&lt;script&gt;",cut.Markup);
        cut.Find("[data-testid=notes-search]").Input("Measurements");Assert.Single(cut.FindAll("[data-testid=note-open]"));cut.Find("[data-testid=notes-search]").Input("no match");Assert.Empty(cut.FindAll("[data-testid=note-open]"));
        var module=JSInterop.SetupModule("./js/diagnosticsExport.js");module.SetupVoid("downloadTextFile",_=>true).SetVoidResult();cut.Find("[data-testid=note-export]").Click();cut.WaitForAssertion(()=>Assert.Single(module.Invocations));Assert.Equal(markdown,module.Invocations.Single().Arguments[1]);
        Evidence("notes-document",cut.Markup,"saved-safe-preview",nameof(Create_search_edit_preview_and_export_preserve_full_markdown_and_safe_rendering));await Task.CompletedTask;
    }
    [Fact]public void Dirty_navigation_cancel_and_account_switch_do_not_transfer_private_drafts()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-title]").Input("Unsaved private");cut.Find("[data-testid=note-body]").Input("private source");cut.Find("[data-testid=note-new]").Click();Assert.Single(cut.FindAll("[aria-label='Unsaved draft']"));Assert.Contains(JSInterop.Invocations,c=>c.Identifier=="Blazor._internal.domWrapper.focus");
        cut.Find("[aria-label='Unsaved draft'] button:nth-child(2)").Click();Assert.Equal("private source",f.Controller.Draft.Markdown);cut.Find("[data-testid=note-new]").Click();f.Session.SetState(f.Session.State with{UserId=Guid.NewGuid().ToString("D")});cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll("[data-testid=note-discard]")));Assert.Empty(f.Controller.Draft.Markdown!);Assert.DoesNotContain("private source",cut.Markup);
        Evidence("notes-empty",cut.Markup,"account-changed-empty",nameof(Dirty_navigation_cancel_and_account_switch_do_not_transfer_private_drafts));
    }
    [Fact]public void Separate_consent_and_unavailable_cloud_are_clear_without_uploading_browser_content()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-source]").Change("Cloud");Assert.True(cut.Find("[data-testid=notes-connect]").HasAttribute("disabled"));Assert.Contains("16 prior versions",cut.Markup);Assert.Empty(f.Client.Book.Notes);Evidence("notes-consent",cut.Markup,"separate-consent",nameof(Separate_consent_and_unavailable_cloud_are_clear_without_uploading_browser_content));
        cut.Find("[data-testid=notes-consent-check]").Change(true);cut.Find("[data-testid=notes-connect]").Click();Assert.True(f.Client.Connected);Assert.Equal(0,f.Client.Submits);
    }
    [Fact]public void Trash_purge_and_history_actions_have_concrete_cancelable_reviews()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-title]").Input("Private retained note");cut.Find("[data-testid=note-body]").Input("draft");cut.Find("form").Submit();cut.Find("[data-testid=note-trash]").Click();Assert.False(f.Controller.Selected!.Current.IsTrashed);cut.Find("[data-testid=note-action-confirm]").Click();Assert.True(f.Controller.Selected!.Current.IsTrashed);
        cut.Find("[data-testid=note-purge]").Click();Assert.Contains("all 2 retained versions",cut.Markup);Assert.True(cut.Find("[data-testid=note-action-confirm]").HasAttribute("disabled"));Evidence("notes-purge",cut.Markup,"irreversible-review",nameof(Trash_purge_and_history_actions_have_concrete_cancelable_reviews));cut.Find("[data-testid=note-review] input").Change(true);cut.Find("[data-testid=note-action-confirm]").Click();Assert.Empty(f.Controller.Book!.Notes);Assert.DoesNotContain("Private retained note",cut.Markup);
    }
    [Fact]public async Task Conflict_and_unknown_acceptance_remain_visible_without_an_extra_write()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-title]").Input("Current title");cut.Find("[data-testid=note-body]").Input("Original");cut.Find("form").Submit();cut.Find("[data-testid=note-body]").Input("Unsaved draft");var book=f.Database.Current.Book!;f.Database.Current.Book=NotesEngine.Apply(book,new(Guid.NewGuid(),f.Controller.SelectedId!.Value,1,1,NoteOperation.Update,Markdown:"Other device"),DateTimeOffset.UtcNow).Notebook;await cut.InvokeAsync(()=>f.Controller.LoadAsync());Assert.Single(cut.FindAll("[aria-label='Note conflict']"));Assert.True(cut.Find("[data-testid=note-save]").HasAttribute("disabled"));Evidence("notes-conflict",cut.Markup,"retained-draft-conflict",nameof(Conflict_and_unknown_acceptance_remain_visible_without_an_extra_write));
        cut.Find("[data-testid=notes-reload]").Click();cut.Find("[data-testid=note-discard]").Click();Assert.Equal("Other device",f.Controller.Draft.Markdown);
        f.Database.Current.LoseResponse=true;cut.Find("[data-testid=note-body]").Input("New retained value");cut.Find("form").Submit();Assert.True(f.Controller.HasPending);Evidence("notes-unknown",cut.Markup,"uncertain-original-save",nameof(Conflict_and_unknown_acceptance_remain_visible_without_an_extra_write));cut.Find("[aria-label='Uncertain save'] button").Click();Assert.False(f.Controller.HasPending);Assert.Equal(3,f.Controller.Book!.Revision);
    }
    [Fact]public void Long_editor_and_document_content_are_captured_for_theme_and_enlargement_checks()
    {
        var f=Setup();var cut=Render<NotesPanel>();cut.Find("[data-testid=note-title]").Input(new string('T',256));cut.Find("[data-testid=note-body]").Input("# Travel journal\n"+new string('W',500)+"\n| Item | Observations |\n| --- | --- |\n| Jacket | "+new string('W',300)+" |\n```\n"+new string('c',500)+"\n```\n- A related fact\n- Another fact");cut.Find("[data-testid=note-preview]").Click();Evidence("notes-long",cut.Markup,"long-code-table-title",nameof(Long_editor_and_document_content_are_captured_for_theme_and_enlargement_checks));
    }
    [Fact]public void Import_file_is_an_unsaved_exact_draft_and_version_history_can_be_loaded_without_overwriting()
    {
        var f=Setup();var cut=Render<NotesPanel>();var text="# Journal\r\n```\r\nraw 🧥\r\n```\r\n";
        cut.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles(InputFileContent.CreateFromText(text,"Journal.md",contentType:"text/markdown"));
        Assert.Equal(text,f.Controller.Draft.Markdown);Assert.Equal("Journal",f.Controller.Draft.Title);Assert.Null(f.Database.Current.Book);cut.Find("form").Submit();cut.Find("[data-testid=note-body]").Input("revised");cut.Find("form").Submit();
        var history=cut.FindAll("details button").Single(b=>b.TextContent.Contains("Load revision"));history.Click();Assert.Equal(text,f.Controller.Draft.Markdown);Assert.Equal("revised",f.Database.Current.Book!.Notes.Single().Current.Markdown);Assert.True(f.Controller.Dirty);
    }
    [Fact]public void Unavailable_cloud_and_whole_notebook_cleanup_have_explicit_separate_states()
    {
        var f=Setup();f.Client.IsAvailable=false;var cut=Render<NotesPanel>();cut.Find("[data-testid=note-source]").Change("Cloud");Assert.Empty(cut.FindAll("[data-testid=notes-connect]"));Assert.Contains("unavailable in this deployment",cut.Markup);Evidence("notes-unavailable",cut.Markup,"cloud-unavailable-browser-independent",nameof(Unavailable_cloud_and_whole_notebook_cleanup_have_explicit_separate_states));
    }
    [Fact]public void Privacy_page_explains_separate_notes_retention_and_browser_data_deletion()
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var cut=Render<Habitica.WebApp.Pages.PrivacyPage>();Assert.Contains("Personal Notes",cut.Markup);Assert.Contains("export any browser Notes",cut.Markup);Qualification.PageLayoutEvidence.Save("notes-privacy",cut.Markup,"/privacy","notes-data-handling",["disclosure"],nameof(Privacy_page_explains_separate_notes_retention_and_browser_data_deletion));
    }
}
