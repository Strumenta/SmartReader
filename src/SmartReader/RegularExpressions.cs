namespace SmartReader
{
    /// <summary>The different kinds regular expressions used to filter elements</summary>
    public enum RegularExpressions
    {
        /// <summary>To remove elements unlikely to contain useful content</summary>
        UnlikelyCandidates,
        /// <summary>To find elements likely to contain useful content</summary>
        PossibleCandidates,
        /// <summary>Classes and tags that increases chances to keep the element</summary>
        Positive,
        /// <summary>Classes and tags that decreases chances to keep the element</summary>
        Negative,
        /// <summary>Extraneous elements</summary>
        /// <remarks>Nota that this regular expression is not used anywhere at the moment</remarks>
        Extraneous,
        /// <summary>To detect byline</summary>
        Byline,
        /// <summary>To keep only useful videos</summary>
        Videos,
        /// <summary>To find sharing elements</summary>
        ShareElements
    }
}