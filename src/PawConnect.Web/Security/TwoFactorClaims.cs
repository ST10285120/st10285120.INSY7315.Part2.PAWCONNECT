namespace PawConnect.Web.Security;

/// <summary>
/// Claim added at sign-in when the account has two-step verification turned on. Lets the admin
/// filter check MFA on every request without a database call.
/// </summary>
public static class TwoFactorClaims
{
    public const string Type = "pawconnect:2fa";
    public const string Enabled = "on";
}
