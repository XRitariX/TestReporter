using System;
using System.Collections.Generic;
using TestReporter.Models;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Servise
{
    public class UniqueRecordFilterService : IUniqueRecordFilterService
    {
        public List<ImportedRowDto> FilterUniqueRecords(
            IEnumerable<ImportedRowDto> existingRecords,
            IEnumerable<ImportedRowDto> newRecords,
            out List<ImportedRowDto> duplicates)
        {
            duplicates = new List<ImportedRowDto>();
            var uniqueNewRecords = new List<ImportedRowDto>();

            var existingKeysSet = new HashSet<TestRecordKey>(new TestRecordKeyComparer());

            if (existingRecords != null)
            {
                foreach (var record in existingRecords)
                {
                    var key = CreateKey(record);
                    existingKeysSet.Add(key);
                }
            }

            if (newRecords != null)
            {
                foreach (var record in newRecords)
                {
                    var key = CreateKey(record);

                    if (existingKeysSet.Contains(key))
                    {
                        duplicates.Add(record);
                    }
                    else
                    {
                        existingKeysSet.Add(key);
                        uniqueNewRecords.Add(record);
                    }
                }
            }

            return uniqueNewRecords;
        }

        private TestRecordKey CreateKey(ImportedRowDto row)
        {
            return new TestRecordKey(
                row.Student?.Trim() ?? string.Empty,
                row.Date?.Date,
                row.Theme?.Trim() ?? string.Empty,
                row.Question?.Trim() ?? string.Empty
            );
        }
    }
}