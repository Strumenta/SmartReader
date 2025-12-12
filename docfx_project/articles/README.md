<h1 align="center">
  <br>
  <img src="https://raw.github.com/strumenta/SmartReader/master/logo.png" width="256" alt="SmartReader">
  <br>
  SmartReader
  <br>
</h1>
<h5 align="center">A library to extract the main content of a web page, removing ads, sidebars, etc.</h5>

<p align="center">
<a href="https://www.nuget.org/packages/SmartReader/">
    <img src="https://img.shields.io/nuget/dt/SmartReader" alt="Downloads on Nuget">
</a>
<a href="https://github.com/strumenta/smartreader/License">
    <img src="https://img.shields.io/github/license/strumenta/smartreader" alt="Apache License">
</a>
</p>

[TOC]

## What and Why

This library supports the .NET Standard 2.0. It is a port of [Mozilla Readability](https://github.com/mozilla/readability). The original library is stable and used in production inside Firefox. By relying on a library maintained by a competent organization like Mozilla we can piggyback on their hard and well-tested work.

SmartReader also added some improvements on the original library, getting more and better metadata: 

- site name
- an author and publication date
- the language
- the excerpt of the article
- the featured image
- a list of images found (it can optionally also download them and store as data URI)
- an estimate of the time needed to read the article

Some of these fields are now present in the original library.

It also allows to perform custom operations before and after extracting the article.

Feel free to suggest new features. 

## Installation

It is trivial using the [NuGet](https://www.nuget.org/packages/SmartReader/) package.

```
PM> Install-Package SmartReader
```

## Usage

There are mainly two ways to use the library. The first is by creating a new `Reader` object, with the URI as the argument, and then calling the `GetArticle` method to obtain the extracted `Article`. The second one is by using one of the static methods `ParseArticle` of `Reader` directly, to return an `Article`. Both ways are available also through an async method, called respectively `GetArticleAsync` and `ParseArticleAsync`. 
The advantage of using an object, instead of the static method, is that it gives you the chance to set some options.

There is also the option to parse directly a String or Stream that you have obtained by some other way. This is available either with `ParseArticle` methods or by using the proper `Reader` constructor. In either case, you also need to give the original URI. It will not re-download the text, but it needs the URI to make some checks and fixing the links present on the page. If you cannot provide the original uri, you can use a fake one, like `https:\\localhost`.

If the extraction fails to extract an article, the returned `Article` object will have the field `IsReadable` set to `false`.
                
If fetching the resource fails, the library will catch the `HttpRequestException`, set `IsReadable` to `false`, `Completed` to `false` and add an Exception to the list of `Errors`. The async methods to extract an article support `CancellationToken` arguments to gracefully request for the interruption of the extraction. For instance, if the extraction takes too much time. If cancelled, they throw a `OperationCanceledException`, but the exception is caught by the library and added to the list of `Errors`.

The content of the article is unstyled, but it is wrapped in a `div` with the id `readability-content` that you can style yourself.

The library tries to detect the correct encoding of the text, if the correct tags are present in the text.

On the `Article` object you can call `GetImagesAsync` to obtain a Task for a list of `Image` objects, representing the images found in the extracted article. The method is async because it makes HEAD Requests, to obtain the size of the images and only returns the ones that are bigger than the specified size. The size by default is 75KB.
This is done to exclude things such as images used in the UI.

On the `Article` object you can also call `ConvertImagesToDataUriAsync` to inline the images found in the article using the [data URI scheme](https://en.wikipedia.org/wiki/Data_URI_scheme). The method is async. This will insert the images into the `Content` property of the `Article`. This may significantly increase the size of `Content`. This data URI scheme is not efficient, because is using [Base64](https://en.wikipedia.org/wiki/Base64) to encode the bytes of the image. Base64 encoded data is approximately 33% larger than the original data. The purpose of this method is to provide an offline article that can be fully stored long term. This is useful in case the original article is not accessible anymore. The method only converts the images that are bigger than the specified size. The size by default is 75KB. This is done to exclude things such as images used in the UI. Notice that this method will not store elements such as embedded videos, so they will still be requested over the network, if present.


## Examples

Using the `GetArticle` method.

```csharp
SmartReader.Reader sr = new SmartReader.Reader("https://arstechnica.com/information-technology/2017/02/humans-must-become-cyborgs-to-survive-says-elon-musk/");

sr.Debug = true;
sr.LoggerDelegate = Console.WriteLine;

SmartReader.Article article = sr.GetArticle();
var images = article.GetImagesAsync();

if(article.IsReadable)
{
	// do something with it	
}
```

Using the `ParseArticle` static method.

```csharp

SmartReader.Article article = SmartReader.Reader.ParseArticle("https://arstechnica.com/information-technology/2017/02/humans-must-become-cyborgs-to-survive-says-elon-musk/");

if(article.IsReadable)
{
	// do something with it
}
```
## Settings

- `int` **MaxElemsToParse**<br>Max number of nodes supported by this parser. <br> *Default: 0 (no limit)*
- `int` **NTopCandidates** <br>The number of top candidates to consider when analyzing how tight the competition is among candidates. <br>*Default: 5*
- `bool` **Debug** <br>Set the Debug option. If set to true the library writes the data on Logger.<br>*Default: false*
- `Action<string>` **LoggerDelegate** <br>Delegate of a function that accepts as argument a string; it will receive log messages.<br>*Default: does not do anything*
- `ReportLevel` **Logging** <br>Level of information written with the `LoggerDelegate`. The valid values are the ones for the enum `ReportLevel`: Issue or Info. The first level logs only errors or issue that could prevent correctly obtaining an article. The second level logs all the information needed for debugging a problematic article.<br>*Default: ReportLevel.Issue*
- `bool` **ContinueIfNotReadable** <br> The library tries to determine if it will find an article before actually trying to do it. This option decides whether to continue if the library heuristics fails. This value is ignored if Debug is set to true <br> *Default: true*
- `int` **CharThreshold** <br>The minimum number of characters an article must have in order to return a result. <br>*Default: 500*
- `String[]` **ClassesToPreserve** <br>The CSS classes that must be preserved in the article. <br>*Default: ["page"]*
- `bool` **DisableJSONLD** <br> The library look first at JSON-LD to determine metadata. This setting gives you the option of disabling it<br> *Default: false*
- `Dictionary<string, int>` **MinContentLengthReadearable** <br> The minimum node content length used to decide if the document is readerable (i.e., the library will find something useful).<br> You can provide a dictionary with values based on language.<br> *Default: 140*
- `int` **MinScoreReaderable** <br> The minumum cumulated 'score' used to determine if the document is readerable<br> *Default: 20*
- `Func<IElement, bool>` **IsNodeVisible** <br> The function used to determine if a node is visible. Used in the process of determining if the document is readerable<br> *Default: NodeUtility.IsProbablyVisible*
- `bool` **ForceHeaderEncoding** <br>Whether to force the encoding provided in the response header. This will convert the stream to the encoding set in the header before passing it to the HTML parser<br>*Default: false*
- `int` **AncestorsDepth** <br>The default level of depth a node must have to be used for scoring.Nodes without as many ancestors as this level are not counted<br>*Default: 5*
- `int` **ParagraphThreshold** <br>The default number of characters a node must have in order to be used for scoring<br>*Default: 25*
- `linkDensityModifier` (number, default `0.0`): a number that is added to the base link density threshold during the shadiness checks. This can be used to penalize nodes with a high link density or vice versa.
- `bool` **PreCleanPage** <br>Some pages have structural issues that harms performance, such as hundred of thousands of empty paragraph nodes. This flag activates heuristics to pre-clean the page before it is analyzed by the library. In practice, the current implementation
just eliminates empty paragraph nodes.<br>*Default: false*

### Settings Notes

The settings <code>MinScoreReaderable</code>, <code>CharThreshold</code> and <code>MinContentLengthReadearable</code> are used in the process of determining if an article is readerable or if the result found is valid.

The algorithm for scoring assign some score to each valid node, then it determines the best node depending on their relationships, i.e., what score ancestors and descendants of the node have. The settings <code>NTopCandidates</code>, <code>AncestorsDepth</code> and <code>ParagraphThreshold</code> can help you customize this process. It makes sense to change them if you are interested in some sites that uses a particular style or design of coding.

The settings <code>ParagraphThreshold</code>, <code>MinContentLengthReadearable</code> and <code>CharThreshold</code> should be customized for content written in non-alphabetical languages.

## Article Model

- `Uri` **Uri**<br>Original Uri
- `String` **Title**<br>Title
- `String` **Byline**<br>Byline of the article, usually containing author and publication date
- `String` **Dir**<br>Direction of the text
- `String` **FeaturedImage**<br>The main image of the article
- `String` **Content**<br>Html content of the article
- `String` **TextContent**<br>The plain text of the article with basic formatting
- `String` **Excerpt**<br>A summary of the article, based on metadata or first paragraph
- `String` **Language**<br>Language string (es. 'en-US')
- `Dictionary<string, Uri>` **AlternativeLanguageUris**<br>Contains URIs for pages in alternative languages, where the key is the language code (es. 'en-US': 'https://www.example.com/en')
- `String` **Author**<br>Author of the article
- `String` **SiteName**<br>Name of the site that hosts the article
- `int` **Length**<br>Length of the text of the article
- `TimeSpan` **TimeToRead**<br>Average time needed to read the article
- `DateTime?` **PublicationDate**<br>Date of publication of the article
- `bool` **IsReadable**<br>Indicate whether we successfully find an article
- `bool` **Completed**<br>Indicate whether we completed the process without getting an Exception (for instance, the HTTP request returned 403 Forbidden)
- `List<Exception>` **Errors**<br>The list of errors generated during the process

It's important to be aware that the fields **Byline**, **Author** and **PublicationDate** are found independently of each other. So there might be some inconsistencies and unexpected data. For instance, **Byline** may be a string in the form "@Date by @Author" or "@Author, @Date" or any other combination used by the publication. 

The **TimeToRead** calculation is based on the research found in [Standardized Assessment of Reading Performance: The New International Reading Speed Texts IReST](http://iovs.arvojournals.org/article.aspx?articleid=2166061). It should be accurate if the article is written in one of the languages in the research, but it is just an educated guess for the others languages.

The **FeaturedImage** property holds the image indicated by the Open Graph or Twitter meta tags. If neither of these is present, and you called the `GetImagesAsync` method, it will be set with the first image found. 

The **TextContent** property is based on the pure text content of the HTML (i.e., the concatenations of [text nodes](https://developer.mozilla.org/en-US/docs/Web/API/Node/nodeType). Then we apply some basic formatting, like removing double spaces or the newlines left by the formatting of the HTML code. We also add meaningful newlines for P and BR nodes.

The **IsReadable** property will be false if no article was extracted, whatever the reason (i.e., the algorithm did not found anything valuable or the request failed). The property **Completed** just indicated whether the process completed correctly or not. Previously we left to the user of the library to manage exceptions, but now we try to handle them ourselves.

## Demo & Console Projects

The demo project is a simple ASP.NET Core webpage that allows you to input an address and see the results of the library.

The console project is a Console program that allows you to see the results of the library on a random test page.

## Notes

### Requesting Web Pages Using .NET HTTP APIs

Any request made with certain HTTP APIs of .NET (like HttpClient, WebClient, etc.) follows the permitted values of security protocols that are set in the property `ServicePointManager.SecurityProtocol`. So, it determines which versions of the TLS protocol can use. In recent versions of .NET Framework (and other .NETs platforms) the default value of this property has been changed to `SecurityProtocolType.SystemDefault` which basically means whatever combinations of values is deemed the best by the current framework. This is the ideal value because if any TLS version stops being secure the code does not need to be updated. 

This [might cause some issues](https://github.com/Strumenta/SmartReader/issues/10), because a web server might not be able to fulfill the request. Usually this is because it uses an old version of the SSL/TLS protocol like SSL 3.0. SmartReader neither specifies a `SecurityProtocol` value for the requests made with its internal HttpClient, nor it provides a method to change it. That's because if we did that this would affect all requests made with certain HTTP APIs, even the ones made by other parts of your code. So, if you need to access some article on an old, insecure web server you might set the proper value of `SecurityProtocol` yourself.

```
ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
```

Alternatively, you can retrieve the content yourself in some other way and just use SmartReader to extract the article from the text.

### Security of Untrusted Input

SmartReader does not perform any security check on the input. If you are using SmartReader with untrusted input and you are displaying the content to the user, it is your responsibility to make sure that nothing bad happens.

The Readability team suggests using a sanitizer library. On .NET you could the [HTML Sanitizer](https://github.com/mganss/HtmlSanitizer) library. They also recommend using
[CSP](https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP) to add further defense-in-depth restrictions to what you allow the resulting content to do.

### Potential Thread Issues When Using Synchronous Methods

There are potential issues in using synchronous methods. That is because the underlying methods to request HTTP content provided by .NET are all asynchronous. So when you call a synchronous method of SmartReader, behind the scene we actually have still to call an asynchronous method to download the content and wait for the call to finish. 

As [pointed out by theolivenbaum](https://github.com/Strumenta/SmartReader/pull/21#issuecomment-687591716), this can lead to issues:

> you can easily get on thread starvation issues when using synchronous methods over asynchronous.
>

### Keys and Values for MinContentLengthReaderable

The keys of `MinContentLengthReaderable` should be the English name of languages, as defined for the `CultureInfo` class. The dictionary should also contain a `Default` key. For example, something like the following.

```
{
    { "Default", 140 },
	{ "English", 140 },
	{ "Italian", 160 }
};
```

If you need to provide language-specific minimum content length, a good starting point may be the characters per minute settings in the `TimeToReadCalculator` class. These values represent how many characters per minute a reader of the language is typically able to read. We do not provide default values ourselves, because we do not have example articles to calculate meaningful values for each language.

### Dealing with Long Extraction Time

Even webpages that are large in terms of size can usually be dealt with in a reasonable time. Aside from bugs solved during the years, the only issue reported from pages in the wild has been a page with hundreds of thousands of `<p>&nbsp;<p>` nodes. The initial parsing of the pages was handled well by AngleSharp, the library we use to parse HTML. However, it then took a long time trying to dynamically create the `InnerHTML` property. The algorithm to extract the main content also took a long time, since it loops through all the nodes. The execution did actually concluded but it took hours.
We found a fix for that particular problem, that can be activated by setting the property `PreCleanPage` to `true`. Unfortunately this is not a general fix, so that is why the flag is `false` by default.

This solution does not solve the general problem, but we added support for `CancellationToken` to our async methods (like `GetArticleAsync`). This allow users of the library to cancel the request whenever they want and therefore lead however they prefer with long running time.

## License

The project uses the **Apache License**.
