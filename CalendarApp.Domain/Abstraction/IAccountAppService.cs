namespace CalendarApp.Domain.Abstraction
{
    public interface IAccountAppService
    {
        Task<IList<string>> RegisterAsync(string username, string email, string password);
        Task<bool> LoginAsync(string username, string password, bool rememberMe);
        Task LogoutAsync();
    }
}
