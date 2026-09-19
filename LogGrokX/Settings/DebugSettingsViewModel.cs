using System;
using System.Globalization;

namespace LogGrokX.Settings
{
    public sealed class DebugSettingsViewModel : ViewModelBase
    {
        private bool _enableCrashDumps;
        private string _maxDumpsCountText;

        public DebugSettingsViewModel(DebugSettings settings)
        {
            _enableCrashDumps = settings.EnableCrashDumps;
            _maxDumpsCountText = settings.MaxDumpsCount.ToString(CultureInfo.InvariantCulture);
        }

        public bool EnableCrashDumps
        {
            get => _enableCrashDumps;
            set
            {
                if (_enableCrashDumps == value)
                    return;
                _enableCrashDumps = value;
                InvokePropertyChanged();
            }
        }

        public string MaxDumpsCountText
        {
            get => _maxDumpsCountText;
            set
            {
                if (_maxDumpsCountText == value)
                    return;
                _maxDumpsCountText = value;
                InvokePropertyChanged();
            }
        }
    }
}