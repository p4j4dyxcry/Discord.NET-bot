namespace TsDiscordBot.Core.Messaging;

public readonly record struct MessageColor(byte R, byte G, byte B)
{
    public static MessageColor FromRgb(byte r, byte g, byte b) => new(r, g, b);

    public static MessageColor FromHex(uint hex)
    {
        var r = (byte)((hex >> 16) & 0xFF);
        var g = (byte)((hex >> 8) & 0xFF);
        var b = (byte)(hex & 0xFF);
        return new MessageColor(r, g, b);
    }
}
