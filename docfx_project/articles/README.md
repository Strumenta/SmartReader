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

SmartReader also add some improvements on the original library, mainly to get more and better metadata: 

- it returns an author and publication date
- the language
- the excerpt of the article
- the featured image
- a list of images found (it can optionally also download them and store as data URI)
- an estimate of the time needed to read the article

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

If the extraction fails, the returned `Article` object will have the field `IsReadable` set to `false`.

The content of the article is unstyled, but it is wrapped in a `div` with the id `readability-content` that you can style yourself.

The library tries to detect the correct encoding of the text, if the correct tags are present in the text.

On the `Article` object you can call `GetImagesAsync` to obtain a Task for a list of `Image` objects, representing the images found in the extracted article. The method is async because it makes HEAD Requests, to obtain the size of the images and only returns the ones that are bigger than the specified size. The size by default is 75KB.
This is done to exclude things such as images used in the UI.

On the `Article` object you can also call `ConvertImagesToDataUriAsync` to inline the images found in the article using the [data URI scheme](https://en.wikipedia.org/wiki/Data_URI_scheme). The method is async. This will insert the images into the `Content` property of the `Article`. This may significantly increase the size of `Content`. This data URI scheme is not efficient, because is using [Base64](https://en.wikipedia.org/wiki/Base64) to encode the bytes of the image. Base64 encoded data is approximately 33% larger than the original data. The purpose of this method is to provide an offline article that can be fully stored long term. This is useful in case the original article is not accessible anymore. The method only converts the images that are bigger than the specified size. The size by default is 75KB. This is done to exclude things such as images used in the UI. Notice that this method will not store elements such as embedded videos, so they will still be requested over the network, if present.

### Options

#### Customize Regular Expressions

You can customize the regular expressions that are used to determine whether a part of the document will be inside the article. There are two methods to do this:

- `void` **AddOptionToRegularExpression(RegularExpressions expression, string option)**<br>Add an option (i.e., usually a CSS class name) to the regular expression. <br>
- `void` **ReplaceRegularExpression(RegularExpressions expression, string newExpression)**<br>Replace the selected regular expression. <br>

The type `RegularExpression` is an `enum` that can have one of the following values, corresponding to a regular expression:
- UnlikelyCandidates
- PossibleCandidates
  - Positive (increases chances to keep the element)
  - Negative (decreases chances to keep the element)
  - Extraneous (note: this regular expression is not used anywhere at the moment)
  - Byline
  - Videos
  - ShareElements

Except for the *Videos* regular expression, they all represent values of attributes, classes, etc. of tags. You should look at the code to understand how each of the regular expression is used.

The *Videos* regular expression represents a domain of origin of an embedded video. Since this is a string representing a regular expression, you have to remember to escape any dot present. This option is used to determine if an embed should be maintained in the article, because people generally want to see videos. If an embed matches one of the domains of this option is maintained, otherwise it is not.

```
// how to add the domain example.com
AddOptionToRegularExpression(RegularExpressions.Videos, "example\.com");
```

#### Add Custom Operations

The library allows the user to add custom operations. I.e., to perform arbitrary modifications to the article before is processed or it is returned to the user. A custom operation receives as argument the article (an `IElement`). For custom operations at the beginning, the element is the entire document; for custom operations executed after the processing is complete, the element is the article extracted.

```csharp
// example of custom operation
void AddInfo(AngleSharp.Dom.IElement element)
{       
    // we add a paragraph to the first div we find
	element.QuerySelector("div").LastElementChild.InnerHtml += "<p>Article parsed by SmartReader</p>";
}

static void RemoveElement(AngleSharp.Dom.IElement element)
{
    // we remove the first element with class removeable
    element.QuerySelector(".removeable")?.Remove();
}

[..]
Reader reader = // ..

// add a custom operation at the start
reader.AddCustomOperationStart(RemoveElement);

// add a custom operation at the end
reader.AddCustomOperationEnd(AddInfo);
```

As you can see the custom operation works on an `IElement` and it would normally rely on the AngleSharp API. AngleSharp is the library that SmartReader uses to parse and manipulate HTML. The API of the library follows the standard structure that you can use in JavaScript, so it is intuitive to use. If you need any help to use it, consult [their documentation](https://github.com/AngleSharp/AngleSharp).

#### Preserve CSS Classes

Normally the library strips all classes of the elements except for `page`. This is done because classes are used to govern the display of the article, but they are irrelevant to the content itself. However, there is an option to preserve other classes. This is mostly useful if you want to perform custom operations on certain elements and you need CSS classes to identify them.

You can preserve classes using the property `ClassesToPreserve` which is an array of class names that will be preserved. Note that this has no effect if an element that contains the class is eliminated from the extracted article. This means that the option **does not maintain the element itself**, it just maintains the class if the element is kept in the extracted article.

```
Reader reader = // ..

// preserve the class info
reader.ClassesToPreserve = new string[] { "info" };
```

The class `page` is always kept, no matter the value you assign to this option.

#### Set Custom User Agent and Custom HttpClient

By default all web requests made by the library use the User Agent *SmartReader Library*. This can be changed by using the function `SetCustomUserAgent(string)`.

```
Reader.SetCustomUserAgent("SuperAwesome App - for any issue contact admin@example.com");
```

This function will change the user agent for **all subsequent web requests** with any object of the class.

If you need to use a custom HttpClient, you can replace the default one, with the function `SetCustomHttpClient(HttpClient)`.

```
HttpClient superC = new HttpClient();
// ..
Reader.SetCustomHttpClient(superC);
```

Notice that, if the custom HttpClient does not set a custom User Agent, *SmartReader Library* will be used.

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
- `String` **Author**<br>Author of the article
- `String` **SiteName**<br>Name of the site that hosts the article
- `int` **Length**<br>Length of the text of the article
- `TimeSpan` **TimeToRead**<br>Average time needed to read the article
- `DateTime?` **PublicationDate**<br>Date of publication of the article
- `bool` **IsReadable**<br>Indicate whether we successfully find an article

It's important to be aware that the fields **Byline**, **Author** and **PublicationDate** are found independently of each other. So there might be some inconsistencies and unexpected data. For instance, **Byline** may be a string in the form "@Date by @Author" or "@Author, @Date" or any other combination used by the publication. 

The **TimeToRead** calculation is based on the research found in [Standardized Assessment of Reading Performance: The New International Reading Speed Texts IReST](http://iovs.arvojournals.org/article.aspx?articleid=2166061). It should be accurate if the article is written in one of the languages in the research, but it is just an educated guess for the others languages.

The **FeaturedImage** property holds the image indicated by the Open Graph or Twitter meta tags. If neither of these is present, and you called the `GetImagesAsync` method, it will be set with the first image found. 

The **TextContent** property is based on the pure text content of the HTML (i.e., the concatenations of [text nodes](https://developer.mozilla.org/en-US/docs/Web/API/Node/nodeType). Then we apply some basic formatting, like removing double spaces or the newlines left by the formatting of the HTML code. We also add meaningful newlines for P and BR nodes.

## Demo & Console Projects

The demo project is a simple ASP.NET Core webpage that allows you to input an address and see the results of the library.

The console project is a Console program that allows you to see the results of the library on a random test page.

## Creating The Nuget Package

In case you want to build the Nuget package yourself you can use the following command.

```
 nuget pack .\SmartReader.csproj -OutputDirectory "..\nupkgs\" -Prop Configuration=Release
```

The command must be issued inside the `src/SmartReader` folder.

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

## License

The project uses the **Apache License**.

## Contributors

- [Unosviluppatore](https://github.com/unosviluppatore)
- [Dan Rigby](https://github.com/DanRigby)
- [Yasindn](https://github.com/yasindn)
- [Jamie Lord](https://github.com/jamie-lord)
- [Gábor Gergely](https://github.com/kodfodrasz)
- [AndySchmitt](https://github.com/AndySchmitt)
- [Andrew Lombard](https://github.com/alombard)
- [LatisVlad](https://github.com/latisvlad)

Thanks to all the people involved.