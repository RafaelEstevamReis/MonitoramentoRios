using System;
using System.Collections.Generic;
using System.Linq;

namespace Web.Data;

public class DataAggregator
{
    /// <summary>
    /// Calcula estatísticas agregadas dos dados
    /// Valores devem ser previamente ordenados
    /// </summary>
    public static Result Aggregate<T>(IEnumerable<T> data, Func<T, decimal?> fieldSelector)
    {
        var values = data.Select(fieldSelector).ToList();

        if (values.Count == 0) return new Result
        {
            Count = 0,
            Min = 0,
            Max = 0,
            Avg = 0,
            StdDev = 0,
            Trend = 0
        };

        int count = values.Count;
        decimal? min = values.Min();
        decimal? max = values.Max();
        decimal? avg = values.Average();

        // Calculate standard deviation
        decimal? variance = values.Sum(x => (x - avg) * (x - avg)) / count;
        decimal? stdDev = variance == null ? null : (decimal)Math.Sqrt((double)variance);

        // Calculate trend using a linear regression slope
        decimal? trend = 0;
        if (count > 1)
        {
            decimal avgX = (count - 1) / 2m; // Average of x (0 to count-1)
            decimal? avgY = avg;

            decimal? covariance = values.Select((y, x) => (x - avgX) * (y - avgY)).Sum();
            decimal? varianceX = Enumerable.Range(0, count).Sum(x => (x - avgX) * (x - avgX));

            trend = varianceX != 0 ? covariance / varianceX : 0;
        }

        // Normalize trend to range [-1, 1]
        if (trend != null) trend = Math.Max(-1, Math.Min(1M, trend.Value));

        return new Result
        {
            Count = count,
            Min = min.Round(3),
            Max = max.Round(3),
            Avg = avg.Round(3),
            StdDev = stdDev.Round(3),
            Trend = trend.Round(3),
        };
    }

    public class Result
    {
        public int Count { get; set; }
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public decimal? Avg { get; set; }
        public decimal? StdDev { get; set; }
        /// <summary>
        /// Range from -1 to 1
        /// -1: Steeply downwards
        ///  0: Steady
        /// +1: Steeply upwards
        /// </summary>
        public decimal? Trend { get; set; }
    }
}
