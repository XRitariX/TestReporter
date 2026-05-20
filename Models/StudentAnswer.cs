using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestReporter.Models
{
    public class StudentAnswer
    {
        public string StudentName { get; set; } = string.Empty;
        public string TopicName { get; set; } = string.Empty; //тема берётся автоматически из названия файла

        
        public string QuestionName { get; set; } = string.Empty; //назывнание вопроса 

       
        public int Score { get; set; } //балл

       
        public string GroupName { get; set; } = string.Empty;

       
        public DateTime? TestDate { get; set; }

        public string UniqueKey //для уникальности имя+дата+тема+вопрос
        {
            get
            {
                string dateStr = TestDate.HasValue ? TestDate.Value.ToString("yyyy-MM-dd") : "NoDate";
                return $"{StudentName}_{dateStr}_{TopicName}_{QuestionName}";
            }
        }
    }
}

