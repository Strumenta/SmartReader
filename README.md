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
    <img src="https://img.shields.io/nuget/dt/SmartReader" alt="Downloads on Nuget"/>
</a>
<a href="https://ci.appveyor.com/project/GabrieleTomassetti/smartreader">
    <img src="https://ci.appveyor.com/api/projects/status/sdbndj848icahnfq?svg=true" alt="Build status"/>
</a>
<a href="https://github.com/strumenta/smartreader/License">
    <img src="https://img.shields.io/github/license/strumenta/smartreader" alt="Apache License"/>
</a>
</p>

## What and Why

This library supports the .NET Standard 2.0. The core algorithm is a port of the [Mozilla Readability library](https://github.com/mozilla/readability). The original library is stable and used in production inside Firefox. This way we can piggyback on the hard and well-tested work of Mozilla.

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

##  Installation

It is trivial using the [NuGet](https://www.nuget.org/packages/SmartReader/) package.

```
PM> Install-Package SmartReader
```

##  Usage

There are mainly two ways to use the library:

- The first is by creating a new `Reader` object, with the URI as the argument, and then calling the `GetArticle` method to obtain the extracted `Article`

- The second one is by using one of the static methods `ParseArticle` of `Reader` directly, to return an `Article`.

Both ways are available also through an async method, called respectively `GetArticleAsync` and `ParseArticleAsync`.
The advantage of using an object, instead of the static method, is that it gives you the chance to set some options.

There is also the option to parse directly a `String` or `Stream` that you have obtained by some other way. This is available either with one of the `ParseArticle` methods or by using the proper `Reader` constructor. In either case, you also need to give the original URI. It will not re-download the text, but it needs the URI to make some checks and fixing the links present on the page. If you cannot provide the original uri, you can use a fake one, like `https:\\localhost`.

If the extraction fails to extract an article, the returned `Article` object will have the field `IsReadable` set to `false`.
                
If fetching the resource fails, the library will catch the `HttpRequestException`, set `IsReadable` to `false`, `Completed` to `false` and add an Exception to the list of `Errors`.

The content of the article is unstyled, but it is wrapped in a `div` with the id `readability-content` that you can style yourself.

The library tries to detect the correct encoding of the text, if the correct tags are present in the text.

###  Getting Images

On the `Article` object you can call `GetImagesAsync` to obtain a Task for a list of `Image` objects, representing the images found in the extracted article. The method is async because it makes HEAD Requests, to obtain the size of the images and only returns the ones that are bigger than the specified size. The size by default is 75KB.
This is done to exclude things such as images used in the UI.

On the `Article` object you can also call `ConvertImagesToDataUriAsync` to inline the images found in the article using the [data URI scheme](https://en.wikipedia.org/wiki/Data_URI_scheme). The method is async. This will insert the images into the `Content` property of the `Article`. This may significantly increase the size of `Content`.

The data URI scheme is not efficient, because is using [Base64](https://en.wikipedia.org/wiki/Base64) to encode the bytes of the image. Base64 encoded data is approximately 33% larger than the original data. The purpose of this method is to provide an offline article that can be fully stored long term. This is useful in case the original article is not accessible anymore. The method only converts the images that are bigger than the specified size. The size by default is 75KB. This is done to exclude things such as images used in the UI.

Notice that this method will not store other external elements that are not images, such as embedded videos.

If fetching an image fails, the library will throw an `HttpRequestException`, you should handle the exception.

##  Examples

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
	Console.WriteLine($"Article title {article.Title}");
}
```

##  Settings

The following settings on the `Reader` class can be modified.

- `int` **MaxElemsToParse**<br>Max number of nodes supported by this parser. <br> *Default: 0 (no limit)*
- `int` **NTopCandidates** <br>The number of top candidates to consider when analyzing how tight the competition is among candidates. <br>*Default: 5*
- `bool` **Debug** <br>Set the Debug option. If set to true the library writes the data on Logger.<br>*Default: false*
- `Action<string>` **LoggerDelegate** <br>Delegate of a function that accepts as argument a string; it will receive log messages.<br>*Default: does not do anything*
- `ReportLevel` **Logging** <br>Level of information written with the `LoggerDelegate`. The valid values are the ones for the enum `ReportLevel`: Issue or Info. The first level logs only errors or issue that could prevent correctly obtaining an article. The second level logs all the information needed for debugging a problematic article.<br>*Default: ReportLevel.Issue*
- `bool` **ContinueIfNotReadable** <br> The library tries to determine if it will find an article before actually trying to do it. This option decides whether to continue if the library heuristics fails. This value is ignored if Debug is set to true <br> *Default: true*
- `int` **CharThreshold** <br>The minimum number of characters an article must have in order to return a result. <br>*Default: 500*
- `bool` **KeepClasses** <br>Whether to preserve or clean CSS classes.<br>*Default: false*
- `String[]` **ClassesToPreserve** <br>The CSS classes that must be preserved in the article, if we opt to not keep all of them.<br>*Default: ["page"]*
- `bool` **DisableJSONLD** <br> The library look first at JSON-LD to determine metadata. This setting gives you the option of disabling it<br> *Default: false*
- `Dictionary<string, int>` **MinContentLengthReadearable** <br> The minimum node content length used to decide if the document is readerable (i.e., the library will find something useful)<br> You can provide a dictionary with values based on language.<br> *Default: 140*
- `int` **MinScoreReaderable** <br> The minumum cumulated 'score' used to determine if the document is readerable<br> *Default: 20*
- `Func<IElement, bool>` **IsNodeVisible** <br> The function used to determine if a node is visible. Used in the process of determining if the document is readerable<br> *Default: NodeUtility.IsProbablyVisible*
- `bool` **ForceHeaderEncoding** <br>Whether to force the encoding provided in the response header.<br>*Default: false*
- `int` **AncestorsDepth** <br>The default level of depth a node must have to be used for scoring.Nodes without as many ancestors as this level are not counted<br>*Default: 5*
- `int` **ParagraphThreshold** <br>The default number of characters a node must have in order to be used for scoring<br>*Default: 25*
- `double` **linkDensityModifier** <br>A number that is added to the base link density threshold during the shadiness checks. This can be used to penalize nodes with a high link density or vice versa.<br>*Default: 0.0*

### Settings Notes

The settings <code>MinScoreReaderable</code>, <code>CharThreshold</code> and <code>MinContentLengthReadearable</code> are used in the process of determining if an article is readerable or if the result found is valid.

The algorithm for scoring assign some score to each valid node, then it determines the best node depending on their relationships, i.e., what score ancestors and descendants of the node have. The settings <code>NTopCandidates</code>, <code>AncestorsDepth</code> and <code>ParagraphThreshold</code> can help you customize this process. It makes sense to change them if you are interested in some sites that uses a particular style or design of coding.

The settings <code>ParagraphThreshold</code>, <code>MinContentLengthReadearable</code> and <code>CharThreshold</code> should be customized for content written in non-alphabetical languages.

##  Article Model

A brief overview of the Article model returned by the library.

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

### Natural Language Processing Features

Often the webpage containing the article will contain metadata about the language used in the article. Sometimes this is not true, so the library comes with a delegate `LanguageIdentification` in `Article`, that can be used to identify the language based on the text itself. This delegate will be called on the `TextContent` of the `Article`. The default method just returns the language in the metadata. This is the old, standard behavior.

You can use the delegate to implement your own method to identify the language. We also provide a decent implementation, that actually does something, using FastText. This implementation is distributed in a separate nuget package, `SmartReader.NaturalLanguageProcessing`.
To use the language identification feature you will need to call the `Enable` method.

```
NLP.Enable();
```

This will change the delegate `LanguageIdentification`, so it will automatically identify the language and set the property `Language` in every `Article` object created by the library.


To restore the default behavior, instead use `RestoreDefaults`.

```
NLP.RestoreDefaults();
```

There is also a delegate to create a summary of the article : `CreateSummary`. Also in this case the default implementation returns the summary provided by the metadata. In practice this is usually a short summary meant for social media sharing. At this moment we do not provide any better implementation.

## Exceptions

The library could throw some exceptions, that should be caught and reported in the `Errors` property and set `Completed` to false.

If you set a value for `MaxElemsToParse` larger than 0, the library will throw a standard `Exception` if the threshold is passed.

If fetching an HTTP resource fails, the library will throw an `HttpRequestException`. This will happen both when trying to fetch the whole article and when trying to fetch an image.

##  Project Structure

This project has the following directory structure.

| Folder                                     | Description                                                  |
| -------------------------------------------| ------------------------------------------------------------ |
| docfx_project/                             | Contains the DocFx project that generates the documentation website |
| src/                                       | The main source folder                                       |
| src/SmartReader                            | Source for the SmartReader library                           |
| src/SmartReader.NaturalLanguageProcessing  | Source for an implementation for NLP features                |
| src/SmartReaderTests                       | Source for the Tests                                         |
| src/SmartReaderConsole                     | Source for example console project                           |
| src/SmartReader.WebDemo                    | Source for the demo web project                              |

## Demo

You can see the [demo web live](https://smartreader-demo.inre.me/). So you can test for yourself how effective the library can be for you. 

There is also a Docker project for the web demo. You can build and run it with the usual docker commands.

```
docker build -t smartreader-webdemo .
docker run -it -p 5000:5000 smartreader-webdemo
```

The second command will forward traffic from port 5000 on your local host to the port 5000 of the docker container. This means that you will be able to access the web demo by visiting http://localhost:5000.

##  Documentation

This README contains the info to get started in using the library. If you want to know more advanced options, API reference, etc. read the documentation on the [main website](https://smartreader.inre.me).