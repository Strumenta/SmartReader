Here is the complete edited text, formatted as a Markdown file. You can copy the code block below and save it as `quickstart.md`.

## Quickstart

This library supports .NET Standard 2.0. The core algorithm is a port of the [Mozilla Readability library](https://github.com/mozilla/readability). The original library is stable and used in production inside Firefox. By relying on a library maintained by a competent organization like Mozilla, we leverage their robust and well-tested work.

SmartReader also adds improvements to the original library, extracting more and better metadata, including:

- Site name
- Author and publication date
- Language
- Article excerpt
- Featured image
- List of images found (can optionally be downloaded and stored as data URIs)
- Estimated time needed to read the article

Feel free to suggest new features.

## Installation

Installation is straightforward using the [NuGet](https://www.nuget.org/packages/SmartReader/) package.


```
PM> Install-Package SmartReader

```

## Usage

There are two main ways to use the library. The first involves creating a new `Reader` object, using the URI as the argument, and then calling the `GetArticle` method to obtain the extracted `Article`. The second uses the static method `ParseArticle` of the `Reader` class directly to return an `Article`. Both approaches are also available via async methods, named `GetArticleAsync` and `ParseArticleAsync` respectively.

The advantage of using an object, rather than the static method, is that it allows you to configure specific options.

You also have the option to directly parse a String or Stream obtained via other means. This is available either through the `ParseArticle` methods or by using the appropriate `Reader` constructor. In either case, you must provide the original URI. The library will not re-download the text, but it requires the URI to perform checks and fix relative links present on the page. If you cannot provide the original URI, you can use a placeholder, such as `https://localhost`.

If the extraction fails, the returned `Article` object will have the `IsReadable` field set to `false`.

The content of the article is unstyled, but it is wrapped in a `div` with the id `readability-content` which you can style yourself.

The library attempts to detect the correct encoding of the text, provided the correct tags are present.

### Getting Images

You can call `GetImagesAsync` on the `Article` object to obtain a Task that returns a list of `Image` objects, representing the images found in the extracted article. This method is async because it makes HEAD requests to determine the size of the images; it only returns those larger than the specified size. The default size is 75KB. This filtering is done to exclude elements such as UI icons.

You can also call `ConvertImagesToDataUriAsync` on the `Article` object to inline the images found in the article using the [data URI scheme](https://en.wikipedia.org/wiki/Data_URI_scheme). The method is async. This inserts the images into the `Content` property of the `Article`, which may significantly increase its size.

The data URI scheme is not efficient because it uses [Base64](https://en.wikipedia.org/wiki/Base64) to encode the image bytes. Base64 encoded data is approximately 33% larger than the original data. The purpose of this method is to provide an offline article suitable for long-term storage. This is useful if the original article becomes inaccessible. The method only converts images larger than the specified size (default 75KB) to exclude UI elements.

Note that this method will not store external elements that are not images, such as embedded videos.

## Examples

Using the `GetArticle` method:

```csharp
SmartReader.Reader sr = new SmartReader.Reader("[https://arstechnica.com/information-technology/2017/02/humans-must-become-cyborgs-to-survive-says-elon-musk/](https://arstechnica.com/information-technology/2017/02/humans-must-become-cyborgs-to-survive-says-elon-musk/)");

sr.Debug = true;
sr.LoggerDelegate = Console.WriteLine;

SmartReader.Article article = sr.GetArticle();
var images = article.GetImagesAsync();

if(article.IsReadable)
{
	// do something with it	
}

```

Using the `ParseArticle` static method:

```csharp
SmartReader.Article article = SmartReader.Reader.ParseArticle("[https://arstechnica.com/information-technology/2017/02/humans-must-become-cyborgs-to-survive-says-elon-musk/](https://arstechnica.com/information-technology/2017/02/humans-must-become-cyborgs-to-survive-says-elon-musk/)");

if(article.IsReadable)
{
	Console.WriteLine($"Article title {article.Title}");
}

```

## Settings

The following settings on the `Reader` class can be modified:

- `int` **MaxElemsToParse**<br>Max number of nodes supported by this parser. <br> *Default: 0 (no limit)*
- `int` **NTopCandidates** <br>The number of top candidates to consider when analyzing how tight the competition is among candidates. <br>*Default: 5*
- `bool` **Debug** <br>Set the Debug option. If set to true the library writes data on Logger.<br>*Default: false*
- `Action<string>` **LoggerDelegate** <br>A delegate function that accepts a string as an argument; it will receive log messages.<br>*Default: does not do anything*
- `ReportLevel` **Logging** <br>Level of information written with the `LoggerDelegate`. Valid values are from the `ReportLevel` enum: `Issue` or `Info`. The first level logs only errors or issues that could prevent correctly obtaining an article. The second level logs all information needed to debug a problematic article.<br>*Default: ReportLevel.Issue*
- `bool` **ContinueIfNotReadable** <br> T The library attempts to determine if it will find an article before actually trying to do so. This option decides whether to continue if the library heuristics fail. This value is ignored if Debug is set to true.<br> *Default: true*
- `int` **CharThreshold** <br>The minimum number of characters an article must have to return a result.<br>*Default: 500*
- `bool` **KeepClasses** <br>Whether to preserve or clean CSS classes.<br>*Default: false*
- `String[]` **ClassesToPreserve** <br>The CSS classes that must be preserved in the article, if we opt to not keep all of them.<br>*Default: ["page"]*
- `bool` **DisableJSONLD** <br> The library looks first at JSON-LD to determine metadata. This setting gives you the option to disable it.<br> *Default: false*
- `Dictionary<string, int>` **MinContentLengthReadearable** <br> The minimum node content length used to decide if the document is readerable (i.e., the library will find something useful)<br> You can provide a dictionary with values based on language.<br> *Default: 140*
- `int` **MinScoreReaderable** <br> The minumum cumulated 'score' used to determine if the document is readerable<br> *Default: 20*
- `Func<IElement, bool>` **IsNodeVisible** <br> The function used to determine if a node is visible. Used in the process of determining if the document is readerable.<br> *Default: NodeUtility.IsProbablyVisible*
- `int` **AncestorsDepth** <br>The default level of depth a node must have to be used for scoring. Nodes without as many ancestors as this level are not counted<br>*Default: 5*
- `int` **ParagraphThreshold** <br>The default number of characters a node must have to be used for scoring.<br>*Default: 25*

### Settings Notes

The settings <code>MinScoreReaderable</code>, <code>CharThreshold</code>, and <code>MinContentLengthReadearable</code> are used in the process of determining if an article is readerable or if the result found is valid.

The scoring algorithm assigns a score to each valid node, then determines the best node based on its relationships (i.e., the score of the node's ancestors and descendants). The settings <code>NTopCandidates</code>, <code>AncestorsDepth</code>, and <code>ParagraphThreshold</code> allow you to customize this process. It is useful to change them if you are targeting sites that use a specific coding style or design.

The settings <code>ParagraphThreshold</code>, <code>MinContentLengthReadearable</code>, and <code>CharThreshold</code> should be customized for content written in non-alphabetical languages.

## Article Model

A brief overview of the Article model returned by the library:

- `Uri` **Uri**<br>Original URI
- `String` **Title**<br>Title
- `String` **Byline**<br>Byline of the article, usually containing author and publication date
- `String` **Dir**<br>Direction of the text
- `String` **FeaturedImage**<br>The main image of the article
- `String` **Content**<br>HTML content of the article
- `String` **TextContent**<br>The plain text of the article with basic formatting
- `String` **Excerpt**<br>A summary of the article, based on metadata or first paragraph
- `String` **Language**<br>Language string (es. 'en-US')
- `String` **Author**<br>Author of the article
- `String` **SiteName**<br>Name of the site that hosts the article
- `int` **Length**<br>Length of the text of the article
- `TimeSpan` **TimeToRead**<br>Average time needed to read the article
- `DateTime?` **PublicationDate**<br>Date of publication of the article
- `bool` **IsReadable**<br>Indicate whether an article was successfully found

It's important to be aware that the fields **Byline**, **Author**, and **PublicationDate** are found independently of each other. Consequently, there might be inconsistencies or unexpected data. For instance, **Byline** may be a string in the form "@Date by @Author", "@Author, @Date", or any other combination used by the publication.

The **TimeToRead** calculation is based on research found in [Standardized Assessment of Reading Performance: The New International Reading Speed Texts IReST](http://iovs.arvojournals.org/article.aspx?articleid=2166061). It should be accurate if the article is written in one of the languages covered by the research, but it is an educated guess for other languages.

The **FeaturedImage** property holds the image indicated by the Open Graph or Twitter meta tags. If neither of these is present, and you called the `GetImagesAsync` method, it will be set to the first image found.

The **TextContent** property is based on the pure text content of the HTML (i.e., the concatenation of [text nodes](https://developer.mozilla.org/en-US/docs/Web/API/Node/nodeType)). We then apply basic formatting, such as removing double spaces or newlines left by HTML formatting. We also add meaningful newlines for P and BR nodes.

