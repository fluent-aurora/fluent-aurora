using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using FluentAurora.Core.Indexer;
using FluentAurora.Core.Logging;
using FluentAurora.Controls; // Add this for PlaylistArtwork
using Symbol = FluentIcons.Common.Symbol;
using SymbolIcon = FluentIcons.Avalonia.Fluent.SymbolIcon;

namespace FluentAurora.Services;

public class PlaylistDialogService
{
    // Properties
    private readonly DatabaseManager _databaseManager;
    private readonly Window _mainWindow;

    // Constructor
    public PlaylistDialogService(DatabaseManager databaseManager, Window mainWindow)
    {
        _databaseManager = databaseManager;
        _mainWindow = mainWindow;
    }

    // Methods
    public async Task<long?> ShowPlaylistSelectionDialogAsync(string songTitle)
    {
        List<PlaylistRecord> playlists = _databaseManager.GetAllPlaylists();

        if (playlists.Count == 0)
        {
            // No playlists, prompt to create
            bool createNew = await ShowNoPlaylistsDialogAsync();
            if (createNew)
            {
                return await ShowCreatePlaylistDialogAsync();
            }
            return null;
        }

        // TaskCompletionSource to handle async result
        TaskCompletionSource<long?> resultTcs = new TaskCompletionSource<long?>();
        bool isCreatingNewPlaylist = false;

        // Content of the dialog
        StackPanel stackPanel = new StackPanel
        {
            Spacing = 12,
            MinWidth = 400
        };

        TextBlock instructionText = new TextBlock
        {
            Text = $"Select a playlist to add \"{songTitle}\" to:",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 0, 0, 8)
        };
        stackPanel.Children.Add(instructionText);

        ListBox listBox = new ListBox
        {
            MaxHeight = 400,
            SelectionMode = SelectionMode.Single
        };

        List<ListBoxItem> items = new List<ListBoxItem>();
        ContentDialog dialog = new ContentDialog
        {
            Title = "Add to Playlist",
            Content = stackPanel,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        dialog.Closing += (_, args) =>
        {
            // If we're not creating a new playlist and task isn't completed, user clicked Cancel
            if (!resultTcs.Task.IsCompleted && !isCreatingNewPlaylist)
            {
                resultTcs.TrySetResult(null);
            }
        };

        ListBoxItem createNewItem = new ListBoxItem
        {
            Tag = -1L,
            Content = new Border
            {
                Padding = new Avalonia.Thickness(8),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children =
                    {
                        new Border
                        {
                            Width = 48,
                            Height = 48,
                            CornerRadius = new Avalonia.CornerRadius(6),
                            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                            Child = new SymbolIcon
                            {
                                Symbol = Symbol.Add,
                                FontSize = 24,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        },
                        new StackPanel
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "Create New Playlist",
                                    FontWeight = FontWeight.SemiBold,
                                    FontSize = 14
                                },
                                new TextBlock
                                {
                                    Text = "Create a new playlist and add this song",
                                    FontSize = 12,
                                    Opacity = 0.7
                                }
                            }
                        }
                    }
                }
            }
        };

        createNewItem.Tapped += async (_, _) =>
        {
            if (resultTcs.Task.IsCompleted)
            {
                return;
            }

            isCreatingNewPlaylist = true;
            dialog.Hide();

            // Create new playlist
            try
            {
                long? newPlaylistId = await ShowCreatePlaylistDialogAsync();
                resultTcs.TrySetResult(newPlaylistId); // Returns new playlist ID
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating playlist: {ex}");
                resultTcs.TrySetResult(null);
            }
        };

        items.Add(createNewItem);
        items.Add(new ListBoxItem
        {
            IsEnabled = false,
            Height = 1,
            Content = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Colors.Gray, 0.3),
                Margin = new Avalonia.Thickness(0, 8)
            }
        });
        
        foreach (PlaylistRecord playlist in playlists.OrderBy(p => p.Name))
        {
            byte[]? artwork = null;
            if (playlist.CustomArtwork == null || playlist.CustomArtwork.Length == 0)
            {
                artwork = DatabaseManager.GetPlaylistArtwork(playlist.Id);
            }

            PlaylistArtwork artworkControl = new PlaylistArtwork
            {
                Width = 48,
                Height = 48,
                Artwork = artwork,
                CustomArtwork = playlist.CustomArtwork
            };

            ListBoxItem item = new ListBoxItem
            {
                Tag = playlist.Id,
                Content = new Border
                {
                    Padding = new Avalonia.Thickness(8),
                    Child = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        Children =
                        {
                            artworkControl,
                            new StackPanel
                            {
                                VerticalAlignment = VerticalAlignment.Center,
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Text = playlist.Name,
                                        FontWeight = FontWeight.SemiBold,
                                        FontSize = 14
                                    },
                                    new TextBlock
                                    {
                                        Text = $"{playlist.SongCount} {(playlist.SongCount == 1 ? "song" : "songs")}",
                                        FontSize = 12,
                                        Opacity = 0.7
                                    }
                                }
                            }
                        }
                    }
                }
            };

            item.Tapped += (_, _) =>
            {
                if (resultTcs.Task.IsCompleted)
                {
                    return;
                }

                long playlistId = (long)item.Tag!;
                resultTcs.TrySetResult(playlistId);
                dialog.Hide();
            };

            items.Add(item);
        }

        listBox.ItemsSource = items;
        stackPanel.Children.Add(listBox);

        await dialog.ShowAsync(_mainWindow);

        return await resultTcs.Task;
    }

    public async Task<long?> ShowCreatePlaylistDialogAsync()
    {
        string currentText = $"New Playlist {DateTime.Now:yyyy-MM-dd HH:mm}";
        string? errorMessage = null;

        while (true)
        {
            TextBox textBox = new TextBox
            {
                Watermark = "Enter playlist name...",
                MinWidth = 300,
                Text = currentText // Preserve the text from previous attempt
            };

            TextBlock errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Colors.Red),
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                IsVisible = errorMessage != null,
                Text = errorMessage ?? string.Empty,
                Margin = new Avalonia.Thickness(0, 4, 0, 0)
            };

            StackPanel contentPanel = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Enter a name for your new playlist:",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Children = { textBox, errorText }
                    }
                }
            };

            ContentDialog dialog = new ContentDialog
            {
                Title = "Create New Playlist",
                Content = contentPanel,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            // Focus textbox when the dialog shows
            dialog.Opened += (_, _) =>
            {
                textBox.Focus();
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    dialog.IsPrimaryButtonEnabled = false;
                }
            };

            // Enable/disable create button based on text and clear error on text change
            textBox.TextChanged += (_, _) =>
            {
                dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(textBox.Text);
                if (errorText.IsVisible && textBox.Text != currentText)
                {
                    errorText.IsVisible = false; // Hide error when input is changed
                }
            };

            ContentDialogResult result = await dialog.ShowAsync(_mainWindow);

            if (result != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(textBox.Text))
            {
                // Cancellation
                return null;
            }

            // Saving the text in case we need to show it again
            currentText = textBox.Text.Trim();

            try
            {
                long playlistId = DatabaseManager.CreatePlaylist(currentText);
                Logger.Info($"Created new playlist: {currentText}");
                return playlistId;
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE constraint
            {
                Logger.Warning($"Playlist '{currentText}' already exists");
                errorMessage = $"A playlist named \"{currentText}\" already exists. Please choose a different name.";
                // Loop
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create playlist: {ex}");
                await ShowErrorDialogAsync("Failed to Create Playlist", "An unexpected error occurred while creating the playlist. Please try again.");
                return null;
            }
        }
    }

    private async Task<bool> ShowNoPlaylistsDialogAsync()
    {
        ContentDialog dialog = new ContentDialog
        {
            Title = "No Playlists",
            Content = new TextBlock
            {
                Text = "You don't have any playlists yet. Would you like to create one?",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            },
            PrimaryButtonText = "Create Playlist",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync(_mainWindow);
        return result == ContentDialogResult.Primary;
    }

    public async Task<string?> ShowRenamePlaylistDialogAsync(string currentName)
    {
        string currentText = currentName;
        string? errorMessage = null;

        while (true)
        {
            TextBox textBox = new TextBox
            {
                Text = currentText,
                MinWidth = 300
            };

            TextBlock errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Colors.Red),
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                IsVisible = errorMessage != null,
                Text = errorMessage ?? string.Empty,
                Margin = new Avalonia.Thickness(0, 4, 0, 0)
            };

            StackPanel contentPanel = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Enter a new name for the playlist:",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Children = { textBox, errorText }
                    }
                }
            };

            ContentDialog dialog = new ContentDialog
            {
                Title = "Rename Playlist",
                Content = contentPanel,
                PrimaryButtonText = "Rename",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            dialog.Opened += (_, _) =>
            {
                textBox.Focus();
                textBox.SelectAll();
                dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(textBox.Text);
            };

            textBox.TextChanged += (_, _) =>
            {
                dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(textBox.Text);
                if (errorText.IsVisible && textBox.Text != currentText)
                {
                    errorText.IsVisible = false;
                }
            };

            ContentDialogResult result = await dialog.ShowAsync(_mainWindow);

            if (result != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(textBox.Text))
            {
                // Cancellation
                return null;
            }

            // Save the current text
            currentText = textBox.Text.Trim();

            // If the name hasn't changed, just return it
            if (currentText.Equals(currentName, StringComparison.OrdinalIgnoreCase))
            {
                return currentText;
            }

            // Check if another playlist uses this name
            // If it does return and show an error
            try
            {
                List<PlaylistRecord> existingPlaylists = _databaseManager.GetAllPlaylists();
                if (!existingPlaylists.Any(p => p.Name.Equals(currentText, StringComparison.OrdinalIgnoreCase)))
                {
                    // Valid name
                    return currentText;
                }
                errorMessage = $"A playlist named \"{currentText}\" already exists. Please choose a different name.";
                continue; // Show dialog again with error
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking playlist names: {ex}");
                return currentText;
            }
        }
    }

    public async Task ShowErrorDialogAsync(string title, string message)
    {
        ContentDialog dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            },
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync(_mainWindow);
    }

    public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        ContentDialog dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            },
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync(_mainWindow);
        return result == ContentDialogResult.Primary;
    }
}