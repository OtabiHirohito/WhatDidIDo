using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WhatDidIDo.Models;

namespace WhatDidIDo.Services
{
    public static class CsvExportService
    {
        public static string Export(IEnumerable<LogEntry> entries, string filePath)
        {
            var sb = new StringBuilder();

            // BOM付きUTF-8 (Excelで文字化けしないように)
            sb.AppendLine("Index,Timestamp,Type,Action,Detail,Position,WindowTitle");

            foreach (var e in entries)
            {
                sb.AppendLine(
                    $"{e.Index}," +
                    $"\"{e.Timestamp}\"," +
                    $"\"{e.Type}\"," +
                    $"\"{e.Action}\"," +
                    $"\"{EscapeCsv(e.Detail)}\"," +
                    $"\"{e.Position}\"," +
                    $"\"{EscapeCsv(e.WindowTitle)}\""
                );
            }

            // BOM付きUTF-8で書き出し
            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
            return filePath;
        }

        private static string EscapeCsv(string value)
            => value.Replace("\"", "\"\"");
    }
}
