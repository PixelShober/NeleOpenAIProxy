using System;
using NeleDesktop.Models;

namespace NeleDesktop.ViewModels;

public sealed class ChatConversationViewModel : ObservableObject
{
    public ChatConversationViewModel(ChatConversation model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
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
}
