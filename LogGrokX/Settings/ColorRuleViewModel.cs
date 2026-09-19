using System;
using System.Text.RegularExpressions;
using System.Windows.Media;
using LogGrokX.Colors.Configuration;

namespace LogGrokX.Settings
{
    public sealed class ColorRuleViewModel : ViewModelBase
    {
        private string _regexString = string.Empty;
        private string _foregroundColor = string.Empty;
        private string _backgroundColor = string.Empty;

        public ColorRuleViewModel()
        {
        }

        public ColorRuleViewModel(ColorRule rule)
        {
            _regexString = rule.RegexString;
            _foregroundColor = rule.ForegroundColor;
            _backgroundColor = rule.BackgroundColor;
        }

        public string RegexString
        {
            get => _regexString;
            set
            {
                if (_regexString == value)
                    return;
                _regexString = value;
                InvokePropertyChanged();
                InvokePropertyChanged(nameof(IsRegexValid));
            }
        }

        public string ForegroundColor
        {
            get => _foregroundColor;
            set
            {
                if (_foregroundColor == value)
                    return;
                _foregroundColor = value;
                InvokePropertyChanged();
                InvokePropertyChanged(nameof(ForegroundBrush));
            }
        }

        public string BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                if (_backgroundColor == value)
                    return;
                _backgroundColor = value;
                InvokePropertyChanged();
                InvokePropertyChanged(nameof(BackgroundBrush));
            }
        }

        public bool IsRegexValid
        {
            get
            {
                try
                {
                    _ = new Regex(_regexString);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
        }

        public Brush? ForegroundBrush => ParseBrush(_foregroundColor);

        public Brush? BackgroundBrush => ParseBrush(_backgroundColor);

        public ColorRuleData ToData() => new()
        {
            RegexString = _regexString,
            ForegroundColor = _foregroundColor,
            BackgroundColor = _backgroundColor
        };

        private static Brush? ParseBrush(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            try
            {
                if (ColorConverter.ConvertFromString(value) is not Color color)
                    return null;

                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}