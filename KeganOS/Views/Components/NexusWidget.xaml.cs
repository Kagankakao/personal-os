using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeganOS.Core.Models;

namespace KeganOS.Views.Components
{
    public partial class NexusWidget : System.Windows.Controls.UserControl
    {
        private List<NoteItem> _notes = new List<NoteItem>();

        public NexusWidget()
        {
            InitializeComponent();
            LoadDummyNotes();
            RenderNotes();
        }

        private void LoadDummyNotes()
        {
            // For initial dev, let's add some dummy notes like in the screenshot
            _notes.Add(new NoteItem { Title = "Rick and Morty", Content = "Sezon 2 bölüm 5 sondan 10dk kala...", Category = "Ideas", LastModified = DateTime.Now.AddDays(-5) });
            _notes.Add(new NoteItem { Title = "Nokron", Content = "", Category = "Game", IsPinned = true });
            _notes.Add(new NoteItem { Title = "Algoritma", Content = "Kapsülleme ile ilgili güzel banka yönetim uygulaması yap", Category = "Work" });
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
                Tag = note // Store note for reference
            };

            border.MouseLeftButtonDown += (s, e) => ShowNoteDetail(note);

            var stack = new StackPanel();

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
                    MaxHeight = 100 // Limit preview
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

        private void TaskTab_Click(object sender, System.Windows.RoutedEventArgs e) { /* TODO */ }
        private void NeuralTab_Click(object sender, System.Windows.RoutedEventArgs e) { /* TODO */ }
        private void NeuralAsk_Click(object sender, System.Windows.RoutedEventArgs e) { /* TODO */ }
        private void NewNote_Click(object sender, System.Windows.RoutedEventArgs e) { /* TODO */ }

        private void BackToGrid_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            NoteDetailPanel.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void ShowNoteDetail(NoteItem note)
        {
            DetailTitle.Text = note.Title;
            DetailContent.Text = note.Content;
            DetailCategory.Text = note.Category.ToUpper();
            DetailDate.Text = note.LastModified.ToString("yyyy-MM-dd HH:mm");
            
            NoteDetailPanel.Visibility = System.Windows.Visibility.Visible;
        }

        private void NexusSearchInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Trigger AI/Search
                NeuralAsk_Click(null, null);
            }
        }
    }
}
