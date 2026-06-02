using System;
using System.Collections.Generic;
using TestReporter.Models;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Servise
{
    /// <summary>
    /// Реализация сервиса фильтрации, использующая HashSet для скорости O(1).
    /// </summary>
    public class UniqueRecordFilterService : IUniqueRecordFilterService
    {
        public List<ImportedRowDto> FilterUniqueRecords(
            IEnumerable<ImportedRowDto> existingRecords,
            IEnumerable<ImportedRowDto> newRecords,
            out List<ImportedRowDto> duplicates)
        {
            duplicates = new List<ImportedRowDto>();
            var uniqueNewRecords = new List<ImportedRowDto>();

            // Используем наш кастомный компаратор для игнорирования регистра (например: Иванов и иванов)
            var existingKeysSet = new HashSet<TestRecordKey>(new TestRecordKeyComparer());

            // 1. Заполняем кэш уже существующими записями
            if (existingRecords != null)
            {
                foreach (var record in existingRecords)
                {
                    var key = CreateKey(record);
                    existingKeysSet.Add(key);
                }
            }

            // 2. Проверяем новые записи
            if (newRecords != null)
            {
                foreach (var record in newRecords)
                {
                    var key = CreateKey(record);

                    // Если ключ уже есть в старых данных или уже встречался в текущей пачке новых файлов
                    if (existingKeysSet.Contains(key))
                    {
                        duplicates.Add(record);
                    }
                    else
                    {
                        existingKeysSet.Add(key);       // Добавляем, чтобы предотвратить дубли в самих новых файлах
                        uniqueNewRecords.Add(record);  // Отправляем на добавление
                    }
                }
            }

            return uniqueNewRecords;
        }

        private TestRecordKey CreateKey(ImportedRowDto row)
        {
            // Безопасно обрезаем пробелы по краям, обрабатываем возможные null-значения
            return new TestRecordKey(
                row.Student?.Trim() ?? string.Empty,
                row.Date?.Date, // Берем только дату, отбрасывая время, если оно случайно попало при парсинге Excel
                row.Theme?.Trim() ?? string.Empty,
                row.Question?.Trim() ?? string.Empty
            );
        }
    }
}