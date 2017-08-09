![SmartReader](https://raw.github.com/unosviluppatore/SmartReader/master/logo.png)

**SmartReader** is a [.NET Standard 1.3](https://github.com/dotnet/standard/blob/master/docs/versions/netstandard1.3.md) library to extract the main content of a web page, based on a port of the [Readability](https://github.com/mozilla/readability) library by Mozilla, which in turn is based on the famous original Readability library.

## Installation

You can do it the standard way, by using the [NuGet](https://www.nuget.org/packages/SmartReader/) package.

```
Install-Package SmartReader
```

## Why You May Want To Use It

 There are already other similar good projects, but they don't support .NET Core and they are based on old version of Readability. The original library is already quite stable, but there are always improvement to be made. So by relying on a original library maintained by such a competent organization we can piggyback on their hard work and user base.

 There are also some minor improvements: it returns an author and publication date, together with the default byline, the language of the article and an indication of the time needed to read it. The time is considered accurate for all languages that use an alphabet, so, for instance, it isn't valid for Chinese.

 I plan to add some features, like returning a list of the images in the article or, optionally, trasforming them in data uri. But at the moment the *Smart* in **SmartReader** is more of an aspiration than a statement. Feel free to suggest new features. **Also, since it's an alpha release expect bugs**.

## Usage

There are mainly two ways to use the library. The first is by creating a new `Reader` object, with the URI as the argument, and then calling the `Parse` method to obtain the extracted `Article`. The second one is by using the static method `ParseArticle` of `Reader` directly, to return an `Article`. The advantage of using an object is that it gives you the chance to set some options.

You can also give to the library the text, or stream, directly, but you also need to give the original URI. It will not redownload the text, but it need the URI to make some checks and modifications on the links present on the page.

If the extraction fails, the returned `Article` object will have the field `IsReadable` set to `false`.

The content of the article is unstyled, but it is wrapped in a `div` with the id `readability-content` that you can style yourself.

The library tries to detect the correct encoding of the text, if the correct tags are present in the text.

## Examples

Using the `Parse` method.

```csharp
SmartReader.Reader sr = new SmartReader.Reader("https://arstechnica.co.uk/information-technology/2017/02/humans-must-become-cyborgs-to-survive-says-elon-musk/");

sr.Debug = true;
sr.Logger = new StringWriter();

SmartReader.Article article = sr.Parse();

if(article.IsReadable)
{
	// do something with it
}
```

Using the `ParseArticle` method.

```csharp

SmartReader.Article article = SmartReader.Reader.ParseArticle("https://arstechnica.co.uk/information-technology/2017/02/humans-must-become-cyborgs-to-survive-says-elon-musk/");

if(article.IsReadable)
{
	// do something with it
}
```

## Options

- `int` **MaxElemsToParse**<br>Max number of nodes supported by this parser. <br> *Default: 0 (no limit)*
- `int` **NTopCandidates** <br>The number of top candidates to consider when analysing how tight the competition is among candidates. <br>*Default: 5*
- `int` **MaxPages** <br>The maximum number of pages to loop through before we call it quits and just show a link. <br>*Default: 5*
- `bool` **Debug** <br>Set the Debug option. If set to true the library writes the data on Logger.<br>*Default: false*
- `TextWriter` **Logger** <br> Where the debug data is going to be written. <br> *Default: null*
- `bool` **ContinueIfNotReadable** <br> The library tries to determine if it will find an article before actually trying to do it. This option decides whether to continue if the library heuristics fails. This value is ignored if Debug is set to true <br> *Default: false*

## Article Model

- `Uri` **Uri**<br>Original Uri
- `String` **Title**<br>Title
- `String` **Byline**<br>Byline of the article, usually containing author and publication date
- `String` **Dir**<br>Direction of the text
- `String` **Content**<br>Html content of the article
- `String` **TextContent**<br>The pure text of the article
- `String` **Excerpt**<br>A summary of the article, based on metadata or first paragraph
- `String` **Language**<br>Language string (es. 'en-US')
- `int` **Length**<br>Length of the text of the article
- `TimeSpan` **TimeToRead**<br>Average time needed to read the article
- `DateTime?` **PublicationDate**<br>Date of publication of the article
- `bool` **IsReadable**<br>Indicate whether we successfully find an article

It's important to be aware that the fields **Byline**, **Author** and **PublicationDate** are found independently of each other. So there might be some inconsistencies and unexpected data. For instance, **Byline** may be a string in the form "@Date by @Author" or "@Author, @Date" or any other combination used by the publication. 