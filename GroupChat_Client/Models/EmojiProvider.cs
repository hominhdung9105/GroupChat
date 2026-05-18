using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GroupChat_Client.Models
{
    public class EmojiModel
    {
        public string Emoji { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }

    public static class EmojiProvider
    {
        public static List<IGrouping<string, EmojiModel>> GetGroupedEmojis()
        {
            try
            {
                // Sử dụng AppDomain để lấy đường dẫn tương đối từ thư mục chạy của file .exe
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db", "emoji.json");

                if (!File.Exists(path))
                    return new List<IGrouping<string, EmojiModel>>();

                string jsonString = File.ReadAllText(path);

                // Khử tính phân biệt chữ hoa/thường khi map JSON
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var emojis = JsonSerializer.Deserialize<List<EmojiModel>>(jsonString, options);

                if (emojis == null)
                    return new List<IGrouping<string, EmojiModel>>();

                // Trả về danh sách đã được Group theo Category
                return emojis.GroupBy(e => e.Category).ToList();
            }
            catch
            {
                // Trả về list rỗng nếu lỗi đọc file/json để app không bị crash
                return new List<IGrouping<string, EmojiModel>>();
            }
        }
    }
}