namespace Contributions.Models
{
    public class ContributionData
    {
        public List<YearData> Years { get; set; } = new();
        public List<Contribution> Contributions { get; set; } = new();
        public List<Contribution> DefaultContributions { get; set; } = new();
    }

    public class YearData
    {
        public string Year { get; set; } = string.Empty;
        public int Total { get; set; }
        public DateRange? Range { get; set; }
        public List<Contribution> Contributions { get; set; } = new();
    }

    public class DateRange
    {
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
    }

    public class Contribution
    {
        public string Date { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Intensity { get; set; }
    }
}
