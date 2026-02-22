namespace Vitaent.Api.Auth;

public record SignInRequest(string Email, string Password);

public record AuthUserDto(Guid Id, string Email, string Role);

public record SignInResponse(string AccessToken, AuthUserDto User);

public record RefreshResponse(string AccessToken);
