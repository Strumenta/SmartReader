using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;

namespace SmartReader
{
	/// <summary>
	/// Parsed article
	/// </summary>
	public class Article
	{
		public Uri Uri { get; private set; }
		public String Title { get; private set; }
		public String Byline { get; private set; }
		public String Dir { get; private set; }
		public String Content { get; private set; }
		public String TextContent { get; private set; }
		public String Excerpt { get; private set; }
		public String Language { get; private set; }
		public String Author { get; private set; }
		public int Length { get; private set; }
		public TimeSpan TimeToRead { get; private set; }
		public DateTime? PublicationDate { get; private set; }
		public bool IsReadable { get; private set; }

		public Article(Uri uri, string title, string byline, string dir, string language, string author, IElement article, Metadata metadata)
		{
			Uri = uri;
			Title = title;
			Byline = String.IsNullOrEmpty(byline) ? metadata.Byline : byline;
			Dir = dir;
			Content = article.InnerHtml;
			TextContent = article.TextContent;
			Excerpt = metadata.Excerpt;
			Length = article.TextContent.Length;
			Language = !String.IsNullOrEmpty(metadata.Language) ? metadata.Language : language;
			PublicationDate = metadata.PublicationDate;
			Author = String.IsNullOrEmpty(author) ? metadata.Author : author;
			IsReadable = true;
            // based on http://iovs.arvojournals.org/article.aspx?articleid=2166061
            TimeToRead = TimeSpan.FromMinutes(article.TextContent.Count(x => x != ' ' && !Char.IsPunctuation(x)) / GetWeightTimeToRead());
		}
        
        private int GetWeightTimeToRead()
        {
            CultureInfo culture = new CultureInfo(Language);

            Dictionary<String, int> CharactersMinute = new Dictionary<string, int>()
            {
                { "Arabic", 612 },
                { "Chinese", 255 },
                { "Dutch", 978 },
                { "English", 987 },
                { "Finnish", 1078 },
                { "French", 998 },
                { "German", 920 },
                { "Hebrew", 833 },
                { "Italian", 950 },
                { "Japanese", 357 },               
                { "Polish", 916 },
                { "Portuguese", 913 },
                { "Swedish", 917 },
                { "Slovenian", 885 },
                { "Spanish", 1025 },
                { "Russian", 986 },
                { "Turkish", 1054 }
            };
            
            var cpm = CharactersMinute.FirstOrDefault(x => culture.EnglishName.StartsWith(x.Key));
            
            // 960 is the average excluding the three outliers languages
            int weight = cpm.Value > 0 ? cpm.Value : 960;

            return weight;
        }

		public Article(Uri uri, string title, bool readable)
		{
			IsReadable = false;
			Uri = uri;
			Title = title;
			Dir = "";
			Byline = "";
			Content = "";
			TextContent = "";
			Excerpt = "";
			Length = 0;
			Language = "";
			PublicationDate = new DateTime();
			Author = "";
			TimeToRead = new TimeSpan();
		}
	}
}
