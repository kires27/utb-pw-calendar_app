namespace CalendarApp.Domain.Abstraction
{
    public class UserBasicDto
    {
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }
}
