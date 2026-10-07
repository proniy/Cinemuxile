using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Курсач
{
    public class Theme : INotifyPropertyChanged
    {
        private string _name;
        private Color _backgroundColor;
        private Color _borderColor;
        private Color _textColor;
        private double _fontSize = 14;
        private bool _neonEnabled;
        private Color _neonBorderColor;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                OnPropertyChanged();
            }
        }

        public Color AccentColor { get; set; }

        public Color BorderColor
        {
            get => _borderColor;
            set
            {
                _borderColor = value;
                OnPropertyChanged();
            }
        }

        public Color TextColor
        {
            get => _textColor;
            set
            {
                _textColor = value;
                OnPropertyChanged();
            }
        }

        public double FontSize
        {
            get => _fontSize;
            set
            {
                _fontSize = value;
                OnPropertyChanged();
            }
        }

        public bool NeonEnabled
        {
            get => _neonEnabled;
            set
            {
                _neonEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BorderColor));
            }
        }

        public Color NeonBorderColor
        {
            get => _neonBorderColor;
            set
            {
                _neonBorderColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BorderColor));
            }
        }

        public Color HeaderBackgroundColor { get; set; }
        public Color DetailBackgroundColor { get; set; }
        public Color CardBackgroundColor { get; set; }
        public Color CardBorderColor { get; set; }
        public Color ButtonBackgroundColor { get; set; }
        public Color ButtonForegroundColor { get; set; }
        public Color ButtonBorderColor { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}