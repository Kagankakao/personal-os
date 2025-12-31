using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using KeganOS.Core.Models;
using KeganOS.Core.Interfaces;

namespace KeganOS.Views.Components
{
    public partial class NexusWidget : System.Windows.Controls.UserControl
    {
        private INoteService _noteService;
        private IPrometheusService _prometheusService;
        private User _currentUser;
        private List<NoteItem> _notes = new List<NoteItem>();
        private NoteItem _currentNote;
        private bool _isEditing = false;
        private bool _isSelectionMode = false;
        private HashSet<string> _selectedNoteIds = new HashSet<string>();

        public NexusWidget()
        {
            InitializeComponent();
        }

        public void Initialize(INoteService noteService, IPrometheusService prometheusService)
        {
            _noteService = noteService;
            _prometheusService = prometheusService;
        }

        public async void SetUser(User user)
        {
            _currentUser = user;
            if (NexusTaskWidget != null)
            {
                NexusTaskWidget.SetCurrentUser(user);
            }
            await RefreshNotes();
        }

        public async Task RefreshNotes()
        {
            if (_currentUser == null || _noteService == null) return;
            
            var notes = await _noteService.GetNotesAsync(_currentUser.Id);
            _notes = notes.OrderByDescending(n => n.IsPinned)
                          .ThenByDescending(n => n.LastModified)
                          .ToList();
            RenderNotes();
        }

        public void RenderNotes()
        {
            Column1.Children.Clear();
            Column2.Children.Clear();

            int count = 0;
            foreach (var note in _notes)
            {
                var card = CreateNoteCard(note);
                if (count % 2 == 0) Column1.Children.Add(card);
                else Column2.Children.Add(card);
                count++;
            }
        }

        private Border CreateNoteCard(NoteItem note)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 26)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = note
            };

            // Fluent Hover Effect
            border.MouseEnter += (s, e) => border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(135, 206, 235));
            border.MouseLeave += (s, e) => border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51));

            border.MouseLeftButtonDown += (s, e) => 
            {
                if (_isSelectionMode)
                {
                    ToggleNoteSelection(note.Id);
                }
                else
                {
                    ShowNoteDetail(note);
                }
            };

            var stack = new StackPanel();

            // Selection Checkbox (Simulated)
            if (_isSelectionMode)
            {
                var isSelected = _selectedNoteIds.Contains(note.Id);
                var checkText = new TextBlock
                {
                    Text = isSelected ? "[▣]" : "[□]",
                    Foreground = isSelected ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(135, 206, 235)) : System.Windows.Media.Brushes.Gray,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                stack.Children.Add(checkText);
                
                if (isSelected)
                {
                    border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 45, 55));
                    border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(135, 206, 235));
                }
            }

            // Show first image as thumbnail if available
            if (note.ImagePaths?.Count > 0 && System.IO.File.Exists(note.ImagePaths[0]))
            {
                try
                {
                    var thumbImg = new System.Windows.Controls.Image
                    {
                        Height = 60,
                        Stretch = Stretch.UniformToFill,
                        Margin = new Thickness(0, 0, 0, 8),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
                    };
                    
                    var thumbBitmap = new BitmapImage();
                    thumbBitmap.BeginInit();
                    thumbBitmap.UriSource = new Uri(note.ImagePaths[0], UriKind.Absolute);
                    thumbBitmap.DecodePixelWidth = 200;
                    thumbBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    thumbBitmap.EndInit();
                    thumbImg.Source = thumbBitmap;
                    
                    stack.Children.Add(thumbImg);
                }
                catch { /* Skip if image fails to load */ }
            }

            if (!string.IsNullOrEmpty(note.Title))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = note.Title,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 8),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            if (!string.IsNullOrEmpty(note.Content))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = note.Content,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170)),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 100 
                });
            }

            if (note.IsPinned)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "📌 Pinned",
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 179, 71)),
                    FontSize = 10,
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }

            border.Child = stack;
            return border;
        }

        // --- Tab Management ---
        private void TaskTab_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            TaskView.Visibility = Visibility.Visible;
            NeuralView.Visibility = Visibility.Collapsed;
            
            TasksTabBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(135, 206, 235));
            NeuralTabBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170));
        }

        private void NeuralTab_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            TaskView.Visibility = Visibility.Collapsed;
            NeuralView.Visibility = Visibility.Visible;
            
            NeuralTabBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(135, 206, 235));
            TasksTabBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170));
        }

        public event Action<string> PrometheusRequested;

        public async Task SearchNotesAsync(string query)
        {
            if (_currentUser == null || _noteService == null) return;
            var notes = await _noteService.SearchNotesAsync(_currentUser.Id, query);
            _notes = notes.OrderByDescending(n => n.IsPinned)
                          .ThenByDescending(n => n.LastModified)
                          .ToList();
            RenderNotes();
            
            // Show a visual indicator that we are in "AI Filter" mode
            if (NexusSearchInput.Text != query)
                NexusSearchInput.Text = query;
        }

        private async void NeuralAsk_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            string query = NexusSearchInput.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                await RefreshNotes();
                return;
            }

            if (query.StartsWith("/prometheus"))
            {
                string aiQuery = query.Replace("/prometheus", "").Trim();
                PrometheusRequested?.Invoke(aiQuery);
                return;
            }

            // Local SQL Search
            await SearchNotesAsync(query);
        }

        private void NewNote_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _currentNote = new NoteItem { Category = "General" };
            ShowNoteDetail(_currentNote);
            SetEditMode(true);
        }

        private void BackToGrid_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SetEditMode(false);
            NoteDetailPanel.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void ShowNoteDetail(NoteItem note)
        {
            _currentNote = note;
            DetailTitle.Text = note.Title;
            DetailContent.Text = note.Content;
            DetailCategory.Text = note.Category.ToUpper();
            DetailDate.Text = note.LastModified.ToString("yyyy-MM-dd HH:mm");
            
            PinNoteButton.Content = note.IsPinned ? "[ 📌 Unpin ]" : "[ 📌 Pin ]";
            
            // Populate images
            RenderDetailImages();
            
            SetEditMode(false);
            NoteDetailPanel.Visibility = System.Windows.Visibility.Visible;
        }

        private void EditNote_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SetEditMode(true);
        }

        private async void SaveNote_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _currentNote.Title = DetailTitle.Text;
            _currentNote.Content = DetailContent.Text;
            _currentNote.LastModified = DateTime.Now;

            await _noteService.SaveNoteAsync(_currentUser.Id, _currentNote);
            await RefreshNotes();
            
            SetEditMode(false);
        }

        private async void PinNote_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _currentNote.IsPinned = !_currentNote.IsPinned;
            await _noteService.SaveNoteAsync(_currentUser.Id, _currentNote);
            await RefreshNotes();
            PinNoteButton.Content = _currentNote.IsPinned ? "[ 📌 Unpin ]" : "[ 📌 Pin ]";
        }

        private void SetEditMode(bool editing)
        {
            _isEditing = editing;
            DetailTitle.IsReadOnly = !editing;
            DetailContent.IsReadOnly = !editing;
            
            SaveNoteButton.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
            EditNoteButton.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
            AddImageBtn.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RenderDetailImages()
        {
            DetailImagesPanel.Children.Clear();
            
            if (_currentNote?.ImagePaths == null || _currentNote.ImagePaths.Count == 0)
            {
                ImagesLabel.Visibility = Visibility.Collapsed;
                return;
            }

            ImagesLabel.Visibility = Visibility.Visible;
            
            foreach (var path in _currentNote.ImagePaths)
            {
                if (!System.IO.File.Exists(path)) continue;

                try
                {
                    var img = new System.Windows.Controls.Image
                    {
                        Width = 120,
                        Height = 90,
                        Stretch = Stretch.UniformToFill,
                        Margin = new Thickness(0, 0, 8, 8),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Tag = path
                    };
                    
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.DecodePixelWidth = 240; // Thumbnail optimization
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    img.Source = bitmap;
                    
                    // Click to view full size (future: lightbox)
                    img.MouseLeftButtonDown += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                    
                    var border = new Border
                    {
                        BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Child = img,
                        Margin = new Thickness(0, 0, 8, 8)
                    };
                    
                    DetailImagesPanel.Children.Add(border);
                }
                catch { /* Skip invalid images */ }
            }
        }

        private void AddImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Image",
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!_currentNote.ImagePaths.Contains(file))
                    {
                        _currentNote.ImagePaths.Add(file);
                    }
                }
                RenderDetailImages();
            }
        }

        private void OnNoteChanged(object sender, TextChangedEventArgs e)
        {
            // Optional: Auto-save or visual feedback
        }

        private void NexusSearchInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NeuralAsk_Click(null, null);
            }
        }

        private async void NexusSearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NexusSearchInput == null) return;
            string query = NexusSearchInput.Text;
            
            // Slash commands (/prometheus) still require Enter to prevent spam
            if (query.StartsWith("/")) return;

            if (string.IsNullOrWhiteSpace(query))
            {
                await RefreshNotes();
                return;
            }

            // Live Search local notes
            var notes = await _noteService.SearchNotesAsync(_currentUser.Id, query);
            _notes = notes.OrderByDescending(n => n.IsPinned)
                          .ThenByDescending(n => n.LastModified)
                          .ToList();
            RenderNotes();
        }

        // --- Selection & Deletion Logic ---
        private void SelectMode_Click(object sender, RoutedEventArgs e)
        {
            _isSelectionMode = true;
            _selectedNoteIds.Clear();
            TagsPanel.Visibility = Visibility.Collapsed;
            SelectModeBtn.Visibility = Visibility.Collapsed;
            BulkActionsPanel.Visibility = Visibility.Visible;
            RenderNotes();
        }

        private void CancelSelect_Click(object sender, RoutedEventArgs e)
        {
            _isSelectionMode = false;
            _selectedNoteIds.Clear();
            TagsPanel.Visibility = Visibility.Visible;
            SelectModeBtn.Visibility = Visibility.Visible;
            BulkActionsPanel.Visibility = Visibility.Collapsed;
            RenderNotes();
        }

        private void ToggleNoteSelection(string noteId)
        {
            if (_selectedNoteIds.Contains(noteId))
                _selectedNoteIds.Remove(noteId);
            else
                _selectedNoteIds.Add(noteId);
            
            RenderNotes();
            
            DeleteSelectedBtn.Content = _selectedNoteIds.Count > 0 
                ? $"[ DELETE ({_selectedNoteIds.Count}) ]" 
                : "[ DELETE ]";
        }

        private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNoteIds.Count == 0) return;

            var result = System.Windows.MessageBox.Show($"Delete {_selectedNoteIds.Count} notes permanently?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await _noteService.DeleteNotesAsync(_selectedNoteIds);
                _isSelectionMode = false;
                _selectedNoteIds.Clear();
                
                TagsPanel.Visibility = Visibility.Visible;
                SelectModeBtn.Visibility = Visibility.Visible;
                BulkActionsPanel.Visibility = Visibility.Collapsed;
                
                await RefreshNotes();
            }
        }

        private async void DeleteDetailNote_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNote == null) return;

            var result = System.Windows.MessageBox.Show("Delete this note permanently?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await _noteService.DeleteNoteAsync(_currentNote.Id);
                NoteDetailPanel.Visibility = Visibility.Collapsed;
                await RefreshNotes();
            }
        }
    }
}
