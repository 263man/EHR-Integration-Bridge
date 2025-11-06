// EhrBridge.Api/Utils/CsvExportUtility.cs
using System.Reflection;
using System.Text;

namespace EhrBridge.Api.Utils
{
    /// <summary>
    /// Utility for creating a CSV stream from an IAsyncEnumerable of objects.
    /// This implementation streams directly to the HttpResponse, reducing memory footprint,
    /// crucial when dealing with potentially sensitive PHI data.
    /// </summary>
    public static class CsvExportUtility
    {
        /// <summary>
        /// Writes the header row and then streams the data rows to the provided output stream.
        /// </summary>
        public static async Task WriteCsvToStreamAsync<T>(IAsyncEnumerable<T> records, Stream outputStream) where T : class
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            if (properties.Length == 0)
            {
                // Must ensure the output stream is written to, even if only an error is logged.
                throw new InvalidOperationException($"Cannot generate CSV for type {typeof(T).Name}: no public properties found.");
            }

            // 1. Write Header Row
            var header = string.Join(",", properties.Select(p => p.Name));
            var headerBytes = Encoding.UTF8.GetBytes(header + Environment.NewLine);
            await outputStream.WriteAsync(headerBytes, 0, headerBytes.Length);

            // 2. Stream Data Rows
            await foreach (var record in records)
            {
                var line = string.Join(",", properties.Select(p => FormatCsvValue(p.GetValue(record))));
                var lineBytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);
                await outputStream.WriteAsync(lineBytes, 0, lineBytes.Length);
            }
        }

        /// <summary>
        /// Formats a single object value for CSV, ensuring nulls are empty strings and containing values are quoted.
        /// </summary>
        private static string FormatCsvValue(object? value)
        {
            if (value is null)
            {
                return string.Empty;
            }

            var strValue = value.ToString();
            
            if (string.IsNullOrEmpty(strValue) == false)
            {
                // Check for values that require quoting (commas, quotes, or newlines)
                if (strValue.Contains(',') || strValue.Contains('"') || strValue.Contains('\n'))
                {
                    // Escape double-quotes by doubling them ("""), then wrap the entire value in double-quotes ("...").
                    return $"\"{strValue.Replace("\"", "\"\"")}\"";
                }
            }

            return strValue ?? string.Empty;
        }
    }
}