namespace PawConnect.Core.Common;

/// <summary>The three roles from the permissions matrix (section 2.1).</summary>
public static class Roles
{
    public const string Adopter = "Adopter";
    public const string Volunteer = "Volunteer";
    public const string Administrator = "Administrator";

    /// <summary>Comma separated form for [Authorize(Roles = ...)].</summary>
    public const string Staff = Volunteer + "," + Administrator;

    public static readonly string[] All = { Adopter, Volunteer, Administrator };
}
