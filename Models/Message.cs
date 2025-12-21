namespace MamyDashboard.Models;

public class Message
{
    public int Id { get; set; }
    public string Content { get; set; } = "";
    public string Author { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
