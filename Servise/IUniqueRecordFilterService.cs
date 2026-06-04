using System.Collections.Generic;
using TestReporter.Models;
using System.IO;
using System.Linq;
using System.Globalization;

namespace TestReporter.Servise
{
    public interface IUniqueRecordFilterService
    {
        List<ImportedRowDto> FilterUniqueRecords(
            IEnumerable<ImportedRowDto> existingRecords,
            IEnumerable<ImportedRowDto> newRecords,
            out List<ImportedRowDto> duplicates);
    }
}
