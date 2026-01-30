using System;
using System.Collections.Generic;
using System.Linq;
using NeleDesktop.Models;

namespace NeleDesktop.ViewModels;

public sealed class ChatConversationViewModel : ObservableObject
{
    private static readonly IReadOnlyList<string> Gpt5ReasoningOptions = new[] { "minimal", "low", "medium", "high" };
    private static readonly IReadOnlyList<string> Gpt51ReasoningOptions = new[] { "none", "low", "medium", "high" };
    private static readonly IReadOnlyList<string> ClaudeReasoningOptions = new[] { "default", "low", "medium", "high" };
    private static readonly IReadOnlyList<string> GoogleClaudeReasoningOptions = new[] { "low", "medium", "high" };
    public ChatConversationViewModel(ChatConversation model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        EnsureReasoningEffort();
    }

    public ChatConversation Model { get; }

    public string Id => Model.Id;

    public string Title
    {
        get => Model.Title;
        set
        {
            if (Model.Title != value)
            {
                Model.Title = value;
                OnPropertyChanged();
            }
        }
    }

    public string SelectedModel
    {
        get => Model.Model;
        set
        {
            if (Model.Model != value)
            {
                Model.Model = value;
                OnPropertyChanged();
                EnsureReasoningEffort();
                OnPropertyChanged(nameof(ReasoningOptions));
            }
        }
    }

    public IReadOnlyList<string> ReasoningOptions => GetReasoningOptions(Model.Model);

    public string ReasoningEffort
    {
        get => Model.ReasoningEffort;
        set
        {
            if (Model.ReasoningEffort != value)
            {
                Model.ReasoningEffort = value;
                OnPropertyChanged();
            }
        }
    }

    public string? FolderId
    {
        get => Model.FolderId;
        set
        {
            if (Model.FolderId != value)
            {
                Model.FolderId = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsTemporary
    {
        get => Model.IsTemporary;
        set
        {
            if (Model.IsTemporary != value)
            {
                Model.IsTemporary = value;
                OnPropertyChanged();
            }
        }
    }

    public bool UseWebSearch
    {
        get => Model.UseWebSearch;
        set
        {
            if (Model.UseWebSearch != value)
            {
                Model.UseWebSearch = value;
                OnPropertyChanged();
            }
        }
    }

    private void EnsureReasoningEffort()
    {
        var options = GetReasoningOptions(Model.Model);
        if (options.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(Model.ReasoningEffort))
            {
                Model.ReasoningEffort = string.Empty;
                OnPropertyChanged(nameof(ReasoningEffort));
            }

            return;
        }

        if (!options.Contains(Model.ReasoningEffort, StringComparer.OrdinalIgnoreCase))
        {
            Model.ReasoningEffort = options[0];
            OnPropertyChanged(nameof(ReasoningEffort));
        }
    }

    private static IReadOnlyList<string> GetReasoningOptions(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return Array.Empty<string>();
        }

        var normalized = model.Trim().ToLowerInvariant();
        if (normalized.Contains("google-claude-4.5", StringComparison.OrdinalIgnoreCase))
        {
            return GoogleClaudeReasoningOptions;
        }

        if (normalized.Contains("claude-4.5", StringComparison.OrdinalIgnoreCase))
        {
            return ClaudeReasoningOptions;
        }

        if (normalized.Contains("gpt-5.1", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("gpt-5.2", StringComparison.OrdinalIgnoreCase))
        {
            return Gpt51ReasoningOptions;
        }

        if (normalized.Contains("gpt-5", StringComparison.OrdinalIgnoreCase))
        {
            return Gpt5ReasoningOptions;
        }

        return Array.Empty<string>();
    }
}
