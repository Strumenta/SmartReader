using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
			Language = !String.IsNullOrEmpty(metadata.Language) ? metadata.Language : Language;
			PublicationDate = metadata.PublicationDate;
			Author = String.IsNullOrEmpty(author) ? metadata.Author : author;
			IsReadable = true;
			// based on http://iovs.arvojournals.org/article.aspx?articleid=2166061
			// this is laughably inaccurate for every language that doesn't have an alphabet
			// for everything else character count seems to be okay            
			TimeToRead = TimeSpan.FromMinutes(article.TextContent.Count(x => x != ' ' && !Char.IsPunctuation(x)) / 1000);
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
