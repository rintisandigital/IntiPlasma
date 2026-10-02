namespace Application.Users.SignIn;

public sealed record SignedInUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string SecurityStamp);
