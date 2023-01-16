using SQLite;

namespace NotificationChecker;

public class NotificationItem
{
    [PrimaryKey, AutoIncrement]
    public int Key { get; set; }
    public string Uri { get; set; }
    public string Id { get; set; }
}