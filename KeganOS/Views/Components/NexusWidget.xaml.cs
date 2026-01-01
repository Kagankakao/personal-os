using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
        private string _activeTagFilterValue = null; // null means "All"
        
        // Auto-save timer (debounce)
        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;
        private const int AUTO_SAVE_DELAY_MS = 1500;
        private int _populatingCount = 0;
        private bool IsPopulating => _populatingCount > 0;

        public NexusWidget()
        {
            InitializeComponent();
            
            // Initialize auto-save timer
            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(AUTO_SAVE_DELAY_MS);
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            
            // Hook up auto-save on content changes
            DetailContent.TextChanged += OnNoteChanged;
            DetailTitle.TextChanged += OnTitleChanged;
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
            
            UpdateGlobalTagsList();
            RenderNotes();
        }

        private void UpdateGlobalTagsList()
        {
            if (TagsPanel == null) return;
            TagsPanel.Children.Clear();

            // "All" filter
            AddTagFilterChip(null);

            // Get all unique tags from notes
            var allTags = _notes.Where(n => n.Tags != null)
                                .SelectMany(n => n.Tags)
                                .Distinct()
                                .OrderBy(t => t)
                                .ToList();

            foreach (var tag in allTags)
            {
                AddTagFilterChip(tag);
            }
        }

        private void AddTagFilterChip(string tagValue)
        {
            var isAll = tagValue == null;
            var isActive = _activeTagFilterValue == tagValue;

            var text = new TextBlock
            {
                Text = isAll ? "[All]" : (tagValue.StartsWith("#") ? tagValue : "#" + tagValue),
                Foreground = isActive ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(135, 206, 235)) : new SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 5),
                FontSize = 11,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal
            };

            text.MouseLeftButtonDown += (s, e) =>
            {
                _activeTagFilterValue = tagValue;
                UpdateGlobalTagsList(); // Refresh highlighting
                RenderNotes();
            };

            TagsPanel.Children.Add(text);
        }

        public void RenderNotes()
        {
            Column1.Children.Clear();
            Column2.Children.Clear();

            double col1Height = 0;
            double col2Height = 0;

            // Filter notes by active tag
            var filteredNotes = _activeTagFilterValue == null 
                ? _notes 
                : _notes.Where(n => n.Tags != null && n.Tags.Contains(_activeTagFilterValue)).ToList();

            foreach (var note in filteredNotes)
            {
                var card = CreateNoteCard(note);
                double estimatedHeight = GetEstimateHeight(note);

                // Add to the shorter column
                if (col1Height <= col2Height)
                {
                    Column1.Children.Add(card);
                    col1Height += estimatedHeight;
                }
                else
                {
                    Column2.Children.Add(card);
                    col2Height += estimatedHeight;
                }
            }
        }

        private double GetEstimateHeight(NoteItem note)
        {
            double height = 40; // Base padding + date row

            // Thumbnail
            if (note.ImagePaths?.Count > 0 && System.IO.File.Exists(note.ImagePaths[0]))
                height += 130; // Image height + margin

            // Title
            if (!string.IsNullOrEmpty(note.Title))
            {
                // Approx 20 units per 20 chars
                height += 25 + (Math.Min(note.Title.Length, 100) / 20.0 * 15);
            }

            // Content Preview
            if (!string.IsNullOrEmpty(note.Content))
            {
                int previewLen = Math.Min(note.Content.Length, 80);
                height += 15 + (previewLen / 30.0 * 15);
            }

            return height;
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

            // Show first image as large thumbnail if available
            if (note.ImagePaths?.Count > 0 && System.IO.File.Exists(note.ImagePaths[0]))
            {
                try
                {
                    var thumbImg = new System.Windows.Controls.Image
                    {
                        Height = 120,
                        Stretch = Stretch.UniformToFill,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
                    };
                    
                    var thumbBitmap = new BitmapImage();
                    thumbBitmap.BeginInit();
                    thumbBitmap.UriSource = new Uri(note.ImagePaths[0], UriKind.Absolute);
                    thumbBitmap.DecodePixelWidth = 280;
                    thumbBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    thumbBitmap.EndInit();
                    thumbImg.Source = thumbBitmap;
                    
                    // Wrap in border for rounded corners
                    var imgBorder = new Border
                    {
                        CornerRadius = new CornerRadius(6),
                        ClipToBounds = true,
                        Margin = new Thickness(0, 0, 0, 10),
                        Child = thumbImg
                    };
                    
                    stack.Children.Add(imgBorder);
                }
                catch { /* Skip if image fails to load */ }
            }

            stack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(note.Title) ? "Untitled" : note.Title,
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            // Content preview or "No text"
            var contentPreview = string.IsNullOrWhiteSpace(note.Content) ? "No text" : note.Content;
            if (contentPreview.Length > 80) contentPreview = contentPreview.Substring(0, 77) + "...";
            stack.Children.Add(new TextBlock
            {
                Text = contentPreview,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 100
            });

            // Date + Pin row at bottom
            var bottomRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            bottomRow.Children.Add(new TextBlock
            {
                Text = note.LastModified.ToString("MMM dd"),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 120)),
                FontSize = 11,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            
            if (note.IsPinned)
            {
                bottomRow.Children.Add(new TextBlock
                {
                    Text = "  📌",
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 200, 80)),
                    FontSize = 12,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                });
            }
            stack.Children.Add(bottomRow);

            border.Child = stack;
            return border;
        }

        // --- Tab Management ---
        private void TaskTab_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            TaskView.Visibility = System.Windows.Visibility.Visible;
            NeuralView.Visibility = System.Windows.Visibility.Collapsed;
            
            TasksTabBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(135, 206, 235));
            NeuralTabBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170));
        }

        private void NeuralTab_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            TaskView.Visibility = System.Windows.Visibility.Collapsed;
            NeuralView.Visibility = System.Windows.Visibility.Visible;
            
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
        }

        private void BackToGrid_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SetEditMode(false);
            NoteDetailPanel.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void ShowNoteDetail(NoteItem note)
        {
            _populatingCount++;
            try
            {
                _currentNote = note;
                DetailTitle.Text = note.Title;
                DetailCategory.Text = note.Category.ToUpper();
                DetailDate.Text = note.LastModified.ToString("yyyy-MM-dd HH:mm");
                
                PinNoteButton.Content = note.IsPinned ? "[ 📌 Unpin ]" : "[ 📌 Pin ]";
                
                // Populate FlowDocument with content and images
                PopulateFlowDocument(note);
                
                RenderDetailTags(note);
                SetEditMode(true);  // Enter edit mode immediately
                NoteDetailPanel.Visibility = System.Windows.Visibility.Visible;
            }
            finally
            {
                _populatingCount--;
            }
        }

        private void PopulateFlowDocument(NoteItem note)
        {
            _populatingCount++;
            try
            {
                DetailContent.Document.Blocks.Clear();
                
                // Add text content
                if (!string.IsNullOrEmpty(note.Content))
                {
                    var para = new Paragraph(new Run(note.Content))
                    {
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204))
                    };
                    DetailContent.Document.Blocks.Add(para);
                }
                else
                {
                    // Add an empty paragraph so the user can start typing immediately
                    DetailContent.Document.Blocks.Add(new Paragraph(new Run("")));
                }
                
                // Add inline images
                if (note.ImagePaths?.Count > 0)
                {
                    var imgPara = new Paragraph();
                    imgPara.Inlines.Add(new Run(" ")); // Initial space
                    
                    foreach (var path in note.ImagePaths)
                    {
                        if (!System.IO.File.Exists(path)) continue;
                        try
                        {
                            var img = CreateInlineImage(path);
                            imgPara.Inlines.Add(new InlineUIContainer(img));
                            imgPara.Inlines.Add(new Run(" ")); // Space between images
                        }
                        catch { }
                    }
                    
                    if (imgPara.Inlines.Count > 1) // More than just the initial space
                        DetailContent.Document.Blocks.Add(imgPara);
                }
            }
            finally
            {
                _populatingCount--;
            }
        }

        private UIElement CreateInlineImage(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = 300;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            
            var img = new System.Windows.Controls.Image
            {
                Source = bitmap,
                MaxWidth = 280,
                MaxHeight = 200,
                Stretch = Stretch.Uniform,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            // Click to view full size
            img.MouseLeftButtonDown += (s, e) => 
            {
                if (e.ClickCount == 2) // Double-click to open
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            };
            
            // Delete button
            var deleteBtn = new System.Windows.Controls.Button
            {
                Content = "✕",
                FontSize = 12,
                Width = 24,
                Height = 24,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 200, 50, 50)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 2, 2, 0),
                Visibility = System.Windows.Visibility.Collapsed
            };
            
            deleteBtn.Click += async (s, e) =>
            {
                _populatingCount++;
                try
                {
                    _currentNote.ImagePaths.Remove(path);
                    _currentNote.LastModified = DateTime.Now;
                    
                    // Update local _notes list too
                    var existingNote = _notes.FirstOrDefault(n => n.Id == _currentNote.Id);
                    if (existingNote != null)
                    {
                        existingNote.ImagePaths = new List<string>(_currentNote.ImagePaths);
                        existingNote.LastModified = _currentNote.LastModified;
                    }
                    
                    // Save to database
                    await _noteService.SaveNoteAsync(_currentUser.Id, _currentNote);
                    
                    // Refresh display
                    PopulateFlowDocument(_currentNote);
                    RenderNotes();
                }
                finally
                {
                    _populatingCount--;
                }
            };
            
            // Container grid
            var container = new Grid
            {
                Margin = new Thickness(0, 5, 10, 5),
                Background = System.Windows.Media.Brushes.Transparent, // Essential for hover detection
                Tag = path // Store path for extraction during auto-save
            };
            container.Children.Add(img);
            container.Children.Add(deleteBtn);
            
            // Show delete button on hover
            container.MouseEnter += (s, e) => deleteBtn.Visibility = System.Windows.Visibility.Visible;
            container.MouseLeave += (s, e) => deleteBtn.Visibility = System.Windows.Visibility.Collapsed;
            
            return container;
        }

        private void EditNote_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SetEditMode(true);
        }

        private async void SaveNote_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _currentNote.Title = DetailTitle.Text;
            
            // Extract text from FlowDocument
            var textRange = new TextRange(DetailContent.Document.ContentStart, DetailContent.Document.ContentEnd);
            _currentNote.Content = textRange.Text.Trim();
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
        }

        private void OnNoteChanged(object sender, TextChangedEventArgs e)
        {
            if (IsPopulating) return;

            // Restart auto-save timer on each change (debounce)
            if (_isEditing && _currentNote != null)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer.Start();
            }
        }

        private void OnTitleChanged(object sender, TextChangedEventArgs e)
        {
            if (IsPopulating) return;

            // Same logic - restart auto-save timer
            if (_isEditing && _currentNote != null)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer.Start();
            }
        }

        private async void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            _autoSaveTimer.Stop();
            
            if (_currentNote == null || _currentUser == null || _noteService == null) return;
            
            // Save current state
            _currentNote.Title = DetailTitle.Text;
            var textRange = new TextRange(DetailContent.Document.ContentStart, DetailContent.Document.ContentEnd);
            _currentNote.Content = textRange.Text.Trim();
            
            // Sync ImagePaths from document (handles keyboard deletion)
            var currentImages = new List<string>();
            foreach (var block in DetailContent.Document.Blocks)
            {
                if (block is Paragraph para)
                {
                    foreach (var inline in para.Inlines)
                    {
                        if (inline is InlineUIContainer container && container.Child is FrameworkElement element && element.Tag is string path)
                        {
                            currentImages.Add(path);
                        }
                    }
                }
            }
            _currentNote.ImagePaths = currentImages;
            
            _currentNote.LastModified = DateTime.Now;
            
            await _noteService.SaveNoteAsync(_currentUser.Id, _currentNote);
            
            // Update the local _notes list and refresh the preview
            var existingNote = _notes.FirstOrDefault(n => n.Id == _currentNote.Id);
            if (existingNote != null)
            {
                existingNote.Title = _currentNote.Title;
                existingNote.Content = _currentNote.Content;
                existingNote.LastModified = _currentNote.LastModified;
                existingNote.ImagePaths = _currentNote.ImagePaths;
            }
            else
            {
                // New note! Add it to the local cache so it appears in the grid
                _notes.Insert(0, _currentNote);
            }
            RenderNotes();
        }

        private void DetailContent_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (!_isEditing)
            {
                e.Effects = System.Windows.DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                bool hasImage = files.Any(f => IsImageFile(f));
                e.Effects = hasImage ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DetailContent_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (!_isEditing || _currentNote == null) return;

            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                
                // Get drop position
                var dropPoint = e.GetPosition(DetailContent);
                TextPointer insertPosition = DetailContent.GetPositionFromPoint(dropPoint, true);
                
                foreach (var file in files)
                {
                    if (IsImageFile(file))
                    {
                        // Add to image paths for persistence
                        if (!_currentNote.ImagePaths.Contains(file))
                            _currentNote.ImagePaths.Add(file);
                        
                        // Insert image inline at drop position
                        try
                        {
                            var img = CreateInlineImage(file);
                            var container = new InlineUIContainer(img, insertPosition);
                            insertPosition = container.ContentEnd;
                            
                            // Add a space after the image
                            insertPosition.InsertTextInRun(" ");
                        }
                        catch { }
                    }
                }
            }
            e.Handled = true;
        }

        private bool IsImageFile(string path)
        {
            var ext = System.IO.Path.GetExtension(path)?.ToLower();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp";
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
            TagsPanel.Visibility = System.Windows.Visibility.Collapsed;
            SelectModeBtn.Visibility = System.Windows.Visibility.Collapsed;
            BulkActionsPanel.Visibility = System.Windows.Visibility.Visible;
            RenderNotes();
        }

        private void CancelSelect_Click(object sender, RoutedEventArgs e)
        {
            _isSelectionMode = false;
            _selectedNoteIds.Clear();
            TagsPanel.Visibility = System.Windows.Visibility.Visible;
            SelectModeBtn.Visibility = System.Windows.Visibility.Visible;
            BulkActionsPanel.Visibility = System.Windows.Visibility.Collapsed;
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

            var result = System.Windows.MessageBox.Show($"Delete {_selectedNoteIds.Count} notes permanently?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (result == System.Windows.MessageBoxResult.Yes)
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

            var result = System.Windows.MessageBox.Show("Delete this note permanently?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                await _noteService.DeleteNoteAsync(_currentNote.Id);
                NoteDetailPanel.Visibility = System.Windows.Visibility.Collapsed;
                await RefreshNotes();
            }
        }
        private void RenderDetailTags(NoteItem note)
        {
            if (DetailTagsPanel == null) return;
            DetailTagsPanel.Children.Clear();

            if (note.Tags == null) note.Tags = new List<string>();

            foreach (var tag in note.Tags)
            {
                var chip = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 26)),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 3, 5, 3),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 0, 5, 5)
                };

                var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                
                stack.Children.Add(new TextBlock 
                { 
                    Text = tag.StartsWith("#") ? tag : "#" + tag, 
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(135, 206, 235)),
                    FontSize = 10,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                });

                var deleteBtn = new System.Windows.Controls.Button
                {
                    Content = "×",
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170)),
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = 12,
                    Margin = new Thickness(5, -2, 0, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                string tagToRemove = tag;
                deleteBtn.Click += async (s, e) =>
                {
                    note.Tags.Remove(tagToRemove);
                    RenderDetailTags(note);
                    UpdateGlobalTagsList(); // Refresh main categories
                    await _noteService.SaveNoteAsync(_currentUser.Id, note);
                };

                stack.Children.Add(deleteBtn);
                chip.Child = stack;
                DetailTagsPanel.Children.Add(chip);
            }
        }

        private async void AddTagInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                string tag = AddTagInput.Text.Trim();
                if (string.IsNullOrWhiteSpace(tag)) return;

                if (!tag.StartsWith("#")) tag = "#" + tag;

                if (_currentNote.Tags == null) _currentNote.Tags = new List<string>();

                if (!_currentNote.Tags.Contains(tag))
                {
                    _currentNote.Tags.Add(tag);
                    AddTagInput.Text = "";
                    RenderDetailTags(_currentNote);
                    UpdateGlobalTagsList();
                    await _noteService.SaveNoteAsync(_currentUser.Id, _currentNote);
                }
            }
        }
    }
}
