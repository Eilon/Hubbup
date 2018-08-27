using System;

namespace Hubbup.IssueMover.Dto
{
    public class CommentData
    {
        public string Author { get; set; }
        public string Text { get; set; }
        public DateTimeOffset Date { get; set; }
    }
}
