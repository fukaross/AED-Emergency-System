namespace AedBackend.Core.Services.WeChat;

public interface ISessionTokenService
{
    string CreateToken();

    string HashToken(string token);
}
