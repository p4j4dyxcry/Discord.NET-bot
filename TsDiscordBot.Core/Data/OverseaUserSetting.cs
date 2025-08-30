using System;

namespace TsDiscordBot.Core.Data;

public class OverseaUserSetting
{
    public const string TableName = "oversea_user_setting";

    public int Id { get; set; }
    public ulong UserId { get; set; }
    public bool IsAnonymous { get; set; }
    public string? AnonymousName { get; set; }
    public string? AnonymousAvatarUrl { get; set; }
}

