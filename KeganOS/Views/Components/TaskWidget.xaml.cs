using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeganOS.Core.Models;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using UserControl = System.Windows.Controls.UserControl;
using TextBox = System.Windows.Controls.TextBox;

namespace KeganOS.Views.Components
{
    public partial class TaskWidget : System.Windows.Controls.UserControl
    {
        private ObservableCollection<TaskItem> _allTasks = new ObservableCollection<TaskItem>();
        private string _currentTab = "Daily";
        private string _tasksFilePath;
        private string _userName;

        public TaskWidget()
        {
            InitializeComponent();
            Loaded += TaskWidget_Loaded;
        }

        private void TaskWidget_Loaded(object sender, RoutedEventArgs e)
        {
            // Resolve user name from parent or global context
            // For now, we'll try to find the UserDisplayName from MainWindow
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                _userName = mainWindow.UserDisplayName.Text;
                InitializePersistence();
            }
        }

        private void InitializePersistence()
        {
            if (string.IsNullOrEmpty(_userName)) return;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string userDir = Path.Combine(baseDir, "texts", "Users", _userName);
            
            if (!Directory.Exists(userDir))
            {
                Directory.CreateDirectory(userDir);
            }

            _tasksFilePath = Path.Combine(userDir, "tasks.json");
            LoadTasks();
            RefreshView();
        }

        private void LoadTasks()
        {
            try
            {
                if (File.Exists(_tasksFilePath))
                {
                    string json = File.ReadAllText(_tasksFilePath);
                    var tasks = JsonSerializer.Deserialize<List<TaskItem>>(json);
                    _allTasks = new ObservableCollection<TaskItem>(tasks ?? new List<TaskItem>());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading tasks: {ex.Message}");
                _allTasks = new ObservableCollection<TaskItem>();
            }
        }

        private void SaveTasks()
        {
            try
            {
                if (string.IsNullOrEmpty(_tasksFilePath)) return;
                
                string json = JsonSerializer.Serialize(_allTasks.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_tasksFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving tasks: {ex.Message}");
            }
        }

        private void RefreshView()
        {
            var filtered = _allTasks.Where(t => t.Category == _currentTab).ToList();
            TasksList.ItemsSource = filtered;
            
            // Update Add button placeholder
            if (_currentTab == "Done")
            {
                NewTaskInput.Visibility = Visibility.Collapsed;
            }
            else
            {
                NewTaskInput.Visibility = Visibility.Visible;
                NewTaskInput.Text = $"[ + Add {_currentTab} Task ]";
            }
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            if (btn == null) return;

            // Update UI tabs
            DailyTabBtn.Tag = null;
            LongTermTabBtn.Tag = null;
            DoneTabBtn.Tag = null;
            
            btn.Tag = "Active";
            _currentTab = btn.Content.ToString() == "Long Term" ? "LongTerm" : btn.Content.ToString();
            
            RefreshView();
        }

        private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var cb = sender as System.Windows.Controls.CheckBox;
            var task = cb?.DataContext as TaskItem;
            
            if (task != null)
            {
                if (task.IsCompleted)
                {
                    task.Category = "Done";
                    task.CompletedAt = DateTime.Now;
                }
                else
                {
                    // If unchecked in 'Done' tab, move back to Daily or wherever it should go
                    // For now, let's just move it back to 'Daily' or 'LongTerm'
                    // We might need to store original category?
                    task.Category = "Daily"; 
                    task.CompletedAt = null;
                }
                
                SaveTasks();
                RefreshView();
            }
        }

        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            var task = btn?.DataContext as TaskItem;
            
            if (task != null)
            {
                _allTasks.Remove(task);
                SaveTasks();
                RefreshView();
            }
        }

        private void NewTaskInput_GotFocus(object sender, RoutedEventArgs e)
        {
            if (NewTaskInput.Text.StartsWith("[ + Add"))
            {
                NewTaskInput.Text = "";
                NewTaskInput.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#CCCCCC");
            }
        }

        private void NewTaskInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewTaskInput.Text))
            {
                NewTaskInput.Text = $"[ + Add {_currentTab} Task ]";
                NewTaskInput.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#888888");
            }
        }

        private void NewTaskInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(NewTaskInput.Text))
            {
                var newTask = new TaskItem
                {
                    Text = NewTaskInput.Text,
                    Category = _currentTab,
                    IsCompleted = false
                };
                
                _allTasks.Add(newTask);
                NewTaskInput.Text = "";
                SaveTasks();
                RefreshView();
            }
        }
    }
}
