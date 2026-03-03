namespace Ravuno.WebAPI.Settings;

public class PasswordSettings
{
    public int MinLength { get; set; } = 8;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSpecialChar { get; set; } = true;
}
