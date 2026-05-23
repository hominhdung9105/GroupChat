using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GroupChat_Client.Models
{
    public enum ChatMessageKind
    {
        Text,
        Image,
        File,
        System
    }

    public class ChatMessage : INotifyPropertyChanged
    {
        private string _sender = string.Empty;
        private string _content = string.Empty;
        private DateTime _sentAt = DateTime.Now;
        private bool _isOwnMessage;
        private ChatMessageKind _messageKind = ChatMessageKind.Text;
        private string? _imagePath;
        private string? _fileName;
        private long _fileSize;
        private double _progressPercent;
        private string? _tempFilePath;
        private string? _fileId;
        private bool _isDownloaded;

        public string Sender
        {
            get => _sender;
            set
            {
                _sender = value;
                OnPropertyChanged();
            }
        }

        public string Content
        {
            get => _content;
            set
            {
                _content = value;
                OnPropertyChanged();
            }
        }

        public DateTime SentAt
        {
            get => _sentAt;
            set
            {
                _sentAt = value;
                OnPropertyChanged();
            }
        }

        public bool IsOwnMessage
        {
            get => _isOwnMessage;
            set
            {
                _isOwnMessage = value;
                OnPropertyChanged();
            }
        }

        public ChatMessageKind MessageKind
        {
            get => _messageKind;
            set
            {
                _messageKind = value;
                OnPropertyChanged();
            }
        }

        public string? ImagePath
        {
            get => _imagePath;
            set
            {
                _imagePath = value;
                OnPropertyChanged();
            }
        }

        public string? FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileSizeDisplay));
            }
        }

        public long FileSize
        {
            get => _fileSize;
            set
            {
                _fileSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileSizeDisplay));
            }
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            set
            {
                _progressPercent = value;
                OnPropertyChanged();
            }
        }

        public string? TempFilePath
        {
            get => _tempFilePath;
            set
            {
                _tempFilePath = value;
                OnPropertyChanged();
            }
        }

        public string? FileId
        {
            get => _fileId;
            set
            {
                _fileId = value;
                OnPropertyChanged();
            }
        }

        public bool IsDownloaded
        {
            get => _isDownloaded;
            set
            {
                _isDownloaded = value;
                OnPropertyChanged();
            }
        }

        public string FileSizeDisplay
        {
            get
            {
                if (FileSize <= 0)
                {
                    return string.Empty;
                }

                string[] units = { "B", "KB", "MB", "GB", "TB" };
                double size = FileSize;
                int unitIndex = 0;

                while (size >= 1024 && unitIndex < units.Length - 1)
                {
                    size /= 1024;
                    unitIndex++;
                }

                return $"{size:0.##} {units[unitIndex]}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}