using System.Collections.Generic;

namespace GroupChat_Client.Models
{
    public static class EmojiProvider
    {
        // Sử dụng mã hóa static để có thể gọi ở bất cứ đâu mà không cần khởi tạo lại
        public static List<string> GetEmojis()
        {
            return new List<string>
            {
                "😀", "😂", "🤣", "😊", "😍", "😒", "😘", "😁", "😉", "😎",
                "😭", "😡", "👍", "👎", "❤️", "🔥", "✨", "🎉", "🤔", "🙌",
                "👀", "💯", "💀", "✌️", "🙏", "💪", "💡", "🚀", "🎵", "☕"
            };
        }
    }
}