/*
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestReporter.Models;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Servise.Filtrations
{
    internal class FilterRept
    {
        public IEnumerable<StudentAnswer> ApplyFilters
            (IEnumerable<StudentAnswer> source,
            FilterOptions filters)
        {
            if (source == null) return Enumerable.Empty<StudentAnswer>();
            if (filters == null) return source;
            var query = source.AsQueryable();
            
            if (filters.StartDate.HasValue) query = query.Where(a => a.TestDate >= filters.StartDate.Value);
            if (filters.EndDate.HasValue) query = query.Where(a => a.TestDate <= filters.EndDate.Value);
           
            if (filters.SelectedThemes != null && filters.SelectedThemes.Any())
                query = query.Where(a => filters.SelectedThemes.Contains(a.TopicName));
            
            if (filters.SelectedGroups != null && filters.SelectedGroups.Any())
                query = query.Where(a => !string.IsNullOrEmpty(a.GroupName) && filters.SelectedGroups.Contains(a.GroupName));
            
            if (filters.SelectedStudents != null && filters.SelectedStudents.Any())
                query = query.Where(a => filters.SelectedStudents.Contains(a.StudentName));
            return query.ToList();
        }
    }
}

*/