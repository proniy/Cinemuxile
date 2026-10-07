using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;

namespace Курсач
{
    public partial class ThemeEditorWindow : Window
    {
        public ThemeEditorWindow(Theme theme = null)
        {
            InitializeComponent();
            ViewModel = new ThemeViewModel(theme ?? new Theme());
            DataContext = ViewModel;
            ApplyTheme(ViewModel.CurrentTheme);
        }

        public ThemeViewModel ViewModel { get; }

        private void ApplyTheme(Theme theme)
        {
            this.Background = new SolidColorBrush(theme.BackgroundColor);
            foreach (var button in FindVisualChildren<Button>(this))
            {
                button.Foreground = new SolidColorBrush(theme.TextColor);
                button.BorderBrush = new SolidColorBrush(theme.BorderColor);
                button.FontSize = theme.FontSize;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var themes = ThemeManager.LoadThemes();
            if (!themes.Exists(t => t.Name == ViewModel.CurrentTheme.Name))
            {
                themes.Add(ViewModel.CurrentTheme);
            }
            ThemeManager.SaveThemes(themes);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    var child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t) yield return t;
                    foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
                }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class ThemeViewModel : INotifyPropertyChanged
    {
        public List<ColorInfo> AvailableColors { get; } = new List<ColorInfo>
        {
            new ColorInfo("Черный", Colors.Black),
            new ColorInfo("Белый", Colors.White),
            new ColorInfo("Синий", Colors.Blue),
            new ColorInfo("Красный", Colors.Red),
            new ColorInfo("Зеленый", Colors.Green),
            new ColorInfo("Фиолетовый", Colors.Purple)
        };

        private Theme _currentTheme;
        public Theme CurrentTheme
        {
            get => _currentTheme;
            set
            {
                _currentTheme = value;
                OnPropertyChanged();
            }
        }

        public ThemeViewModel(Theme theme)
        {
            CurrentTheme = theme;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}