namespace TsDiscordBot.Discord.Utility
{
    public static class XorShiftUtil
    {
        public static ulong GetValue(ulong userId, DateOnly date, ulong seed = 2463534242u)
        {
            // シードに userId と日付を混ぜる
            ulong state = seed ^ userId ^ (ulong)date.DayNumber;

            // XorShift32
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;

            return state % 10000u; // 0〜9999 の範囲
        }
    }
}