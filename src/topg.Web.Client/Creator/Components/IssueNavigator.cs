using Microsoft.AspNetCore.Components;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Storage;
using topg.Web.Client.Creator.Validation;

namespace topg.Web.Client.Creator.Components;

/// <summary>C-5: go to the target of a validation issue and show its field errors (app bar popover and S-7).</summary>
public class IssueNavigator(ProjectSession session, NavigationManager navigation)
{
    public void GoTo(ValidationIssue issue)
    {
        if (session.Project is not { } project)
        {
            return;
        }

        switch (issue.TargetKind)
        {
            case ValidationTargetKind.Question:
                session.Select(EditorSelection.Question(issue.TargetId), focus: true, showFieldErrors: true);
                break;
            case ValidationTargetKind.Category:
                session.Select(EditorSelection.Category(issue.TargetId), focus: true, showFieldErrors: true);
                break;
            case ValidationTargetKind.Board when project.FindBoard(issue.TargetId) is { } board:
                session.ClearSelection();
                navigation.NavigateTo(CreatorLocation.Board(project.Id, project.BoardNumber(board)));
                return;
            case ValidationTargetKind.Project when issue.Code == "project.noBoards":
                navigation.NavigateTo(CreatorLocation.Board(project.Id, 1));
                return;
            default:
                navigation.NavigateTo(CreatorLocation.QuizSettings(project.Id));
                return;
        }

        // On the board editor the route follows the selection itself; from other pages go there.
        var location = CreatorLocation.Parse(navigation.ToBaseRelativePath(navigation.Uri));
        if (location.Section != CreatorSection.Boards && session.SelectedBoard() is { } selectedBoard)
        {
            navigation.NavigateTo(CreatorLocation.Board(project.Id, project.BoardNumber(selectedBoard)));
        }
    }
}
