namespace Web.Data;

using System;
using System.Collections.Generic;
using System.Linq;

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

    public static Result AgretgateWithFilter<T>(IEnumerable<T> data, Func<T, decimal?> fieldSelector,
        Func<IEnumerable<T>, Func<T, decimal?>, decimal, IEnumerable<T>> filterFunction, decimal filterScore)
    {
        var filteredData = filterFunction(data, fieldSelector, filterScore);
        return Aggregate(filteredData, fieldSelector);
    }

    public static IEnumerable<T> FilterUsingNormalDistribution<T>(IEnumerable<T> data, Func<T, decimal?> fieldSelector, decimal zScore = 1.96M) // Padrão: 95% de confiança
    {
        var values = data.Select(fieldSelector).Where(o => o is not null).Cast<decimal>().ToList();

        if (values.Count == 0) return data; // Retorna os dados sem alterações se estiverem vazios.

        var mean = values.Average();
        var variance = values.Sum(x => (x - mean) * (x - mean)) / values.Count;
        var stdDev = (decimal)Math.Sqrt((double)variance);

        // Define os limites com base no nível de confiança
        var lowerBound = mean - zScore * stdDev;
        var upperBound = mean + zScore * stdDev;

        // Filtra valores dentro do intervalo
        return data.Where(item =>
        {
            var value = fieldSelector(item);
            return value is not null && value >= lowerBound && value <= upperBound;
        });
    }
    public static IEnumerable<T> FilterUsingStdDev<T>(IEnumerable<T> data, Func<T, decimal?> fieldSelector, decimal numStdDev = 3)
    {
        var values = data.Select(fieldSelector).Where(o => o is not null).Cast<decimal>().ToList();

        if (values.Count == 0) return data; // Retorna os dados sem alterações se estiverem vazios.

        var avg = values.Average();
        var stdDev = (decimal)Math.Sqrt((double)(values.Sum(x => (x - avg) * (x - avg)) / values.Count));

        // Define os limites de exclusão
        decimal lowerBound = avg - numStdDev * stdDev;
        decimal upperBound = avg + numStdDev * stdDev;

        // Filtra valores dentro dos limites
        return data.Where(item =>
        {
            var value = fieldSelector(item);
            return value is not null && value >= lowerBound && value <= upperBound;
        });
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
