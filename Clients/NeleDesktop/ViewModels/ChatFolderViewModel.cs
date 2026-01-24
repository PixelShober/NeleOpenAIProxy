using System;
using System.Collections.ObjectModel;
using NeleDesktop.Models;

namespace NeleDesktop.ViewModels;

public sealed class ChatFolderViewModel : ObservableObject
{
    public ChatFolderViewModel(ChatFolder model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public ChatFolder Model { get; }

    public string Id => Model.Id;

    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name != value)
            {
                Model.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<ChatConversationViewModel> Chats { get; } = new();
}
