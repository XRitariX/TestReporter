namespace TestReporter.Models
{
    public enum AggregationMode
    {
        Sum = 0,        // Сумма всех баллов
        Average = 1,    // Среднее значение (доля правильных ответов)
        Maximum = 2     // Максимальный балл (не применяется, но добавляем)
    }
}