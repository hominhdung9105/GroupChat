using System;

namespace GroupChat_Client.Models
{
    public class ChatMessage
    {
        public string Sender { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.Now;

        public bool IsOwnMessage { get; set; }
    }
}