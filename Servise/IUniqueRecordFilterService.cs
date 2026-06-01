using System.Collections.Generic;
using TestReporter.Models;

namespace TestReporter.Servise
{
    /// <summary>
    /// Интерфейс сервиса фильтрации уникальных записей тестирования.
    /// </summary>
    public interface IUniqueRecordFilterService
    {
        /// <summary>
        /// Фильтрует новые записи, оставляя только уникальные по сравнению с существующими.
        /// </summary>
        /// <param name="existingRecords">Уже существующие записи в базе/отчете.</param>
        /// <param name="newRecords">Новые записи для импорта.</param>
        /// <param name="duplicates">Выходной параметр, содержащий список найденных дубликатов.</param>
        /// <returns>Список уникальных записей, готовых к добавлению.</returns>
        List<ImportedRowDto> FilterUniqueRecords(
            IEnumerable<ImportedRowDto> existingRecords,
            IEnumerable<ImportedRowDto> newRecords,
            out List<ImportedRowDto> duplicates);
    }
}