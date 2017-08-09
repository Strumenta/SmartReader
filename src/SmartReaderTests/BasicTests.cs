using System;
using Xunit;
using SmartReader;
using System.IO;
using Moq;

namespace SmartReaderTests
{
	public interface IArticleTest
	{
		Uri Uri { get; set; }
		String Title { get; set; }
		String Byline { get; set; }
		String Dir { get; set; }
		String Content { get; set; }
		String TextContent { get; set; }
		String Excerpt { get; set; }
		String Language { get; set; }
		String Author { get; set; }
		int Length { get; set; }
		TimeSpan TimeToRead { get; set; }
		DateTime? PublicationDate { get; set; }
		bool IsReadable { get; set; }
	}

	public class BasicTests
	{
		public IArticleTest GetTestArticle(string file)
		{
			using (StreamReader sr = new StreamReader(new FileStream(file, FileMode.Open)))
			{
				//Article expected = new Article(new Uri(uri), title, readable);
				var mockArticle = new Mock<IArticleTest>();
				mockArticle.Setup(x => x.Uri).Returns(new Uri(sr.ReadLine()));
				mockArticle.Setup(x => x.IsReadable).Returns(bool.Parse(sr.ReadLine()));
				mockArticle.Setup(x => x.Title).Returns(sr.ReadLine());
				mockArticle.Setup(x => x.Dir).Returns(sr.ReadLine());
				mockArticle.Setup(x => x.Byline).Returns(sr.ReadLine());
				mockArticle.Setup(x => x.Author).Returns(sr.ReadLine());
				mockArticle.Setup(x => x.PublicationDate).Returns(DateTime.Parse(sr.ReadLine()));
				mockArticle.Setup(x => x.Language).Returns(sr.ReadLine());				
				mockArticle.Setup(x => x.Excerpt).Returns(sr.ReadLine());
				mockArticle.Setup(x => x.TimeToRead).Returns(TimeSpan.Parse(sr.ReadLine()));

				return mockArticle.Object;
			}
		}

		private void AssertProperties(IArticleTest expected, Article found)
		{			
			Assert.Equal(expected.IsReadable, found.IsReadable);
			Assert.Equal(expected.Title, found.Title);
			Assert.Equal(expected.Dir, found.Dir);
			//Assert.Equal(expected.Byline, found.Byline);
			Assert.Equal(expected.Author, found.Author);
			Assert.Equal(expected.PublicationDate, found.PublicationDate);
			Assert.Equal(expected.Language, found.Language);			
			Assert.Equal(expected.Excerpt, found.Excerpt);
			Assert.Equal(expected.TimeToRead, found.TimeToRead);
		}

		[Fact]
		public void TestPages()
		{
			string[] expectedList = Directory.GetFiles(@"..\..\..\test-pages", "*.txt");
			string[] articleList = Directory.GetFiles(@"..\..\..\test-pages", "*.html");
			Array.Sort(expectedList);
			Array.Sort(articleList);

			for (int a = 0; a < expectedList.Length; a++)
			{
				IArticleTest expected = GetTestArticle(expectedList[a]);				
				String text = new StreamReader(new FileStream(articleList[a], FileMode.Open)).ReadToEnd();				
				Article found = Reader.ParseArticle(expected.Uri.ToString(), text);
				Console.WriteLine(found.Byline);

				AssertProperties(expected, found);				
			}
		}
	}
}