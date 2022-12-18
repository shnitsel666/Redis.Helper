namespace Redis.Helper
{
    public static class RedisConstants
    {
        public enum RedisTTLPriorities : int
        {
            VeryLowPriority = 12,
            LowPriority = 8,
            NormalPriority = 4,
            HighPriority = 2,
            VeryHighPriority = 1,
        }
    }
}