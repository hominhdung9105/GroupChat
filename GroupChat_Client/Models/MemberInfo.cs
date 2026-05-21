using System;
using System.Collections.Generic;

using System.Text;

using System.ComponentModel;

namespace GroupChat_Client.Models
{
    public class MemberInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _username = "";
        private bool _isOnline;

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(nameof(Username)); }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(nameof(IsOnline)); }
        }

        public string Initial => Username?.Length > 0
            ? Username[0].ToString().ToUpper() : "?";

        public string StatusText => IsOnline ? "online" : "offline";

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
