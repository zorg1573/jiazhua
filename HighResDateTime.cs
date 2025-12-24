using System;
using System.Diagnostics;

namespace jiazhua
{

    public static class HighResDateTime
    {
        private static readonly DateTime _baseTime;
        private static readonly Stopwatch _stopwatch;
        private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static HighResDateTime()
        {
            _baseTime = DateTime.Now;
            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// 获取高分辨率当前时间（尽可能接近毫秒/微秒级）
        /// </summary>
        public static DateTime Now
        {
            get
            {
                return _baseTime.AddTicks(_stopwatch.Elapsed.Ticks);
            }
        }

        /// <summary>
        /// 获取高分辨率 UTC 时间
        /// </summary>
        public static DateTime UtcNow
        {
            get
            {
                return _baseTime.ToUniversalTime().AddTicks(_stopwatch.Elapsed.Ticks);
            }
        }

        /// <summary>
        /// 返回 Unix 时间戳（毫秒级）
        /// </summary>
        public static long ToUnixMilliseconds(this DateTime dt)
        {
            return (long)(dt.ToUniversalTime() - unixEpoch).TotalMilliseconds;
        }

        /// <summary>
        /// 返回 Unix 时间戳（微秒级）
        /// </summary>
        public static long ToUnixMicroseconds(this DateTime dt)
        {
            return (long)((dt.ToUniversalTime() - unixEpoch).Ticks / 10); // 1 tick = 100ns
        }
    }
}
