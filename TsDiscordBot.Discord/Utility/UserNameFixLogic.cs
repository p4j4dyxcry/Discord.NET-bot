namespace TsDiscordBot.Discord.Utility;

public static class UserNameFixLogic
{
    public static string Fix(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var index = name.IndexOf('#');
        return index < 0 ? name : name[..index];
    }
}
