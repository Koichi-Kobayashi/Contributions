namespace Contributions.Models
{
    /// <summary>
    /// 取得したコントリビューション情報をまとめたデータ。
    /// </summary>
    public class ContributionData
    {
        public List<YearData> Years { get; set; } = new();
        public List<Contribution> Contributions { get; set; } = new();
        public List<Contribution> DefaultContributions { get; set; } = new();
        public int DefaultTotal { get; set; }
    }

    /// <summary>
    /// 年単位のコントリビューション情報。
    /// </summary>
    public class YearData
    {
        public string Year { get; set; } = string.Empty;
        public int Total { get; set; }
        public DateRange? Range { get; set; }
        public List<Contribution> Contributions { get; set; } = new();
    }

    /// <summary>
    /// 日付範囲を表す。
    /// </summary>
    public class DateRange
    {
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
    }

    /// <summary>
    /// 1日分のコントリビューション情報。
    /// </summary>
    public class Contribution
    {
        public string Date { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Intensity { get; set; }
        public string TooltipText { get; set; } = string.Empty;
    }

    /// <summary>
    /// 既定表示用キャッシュの内容。
    /// </summary>
    public class DefaultContributionCache
    {
        public int Total { get; set; }
        public List<Contribution> Contributions { get; set; } = new();
    }
}
