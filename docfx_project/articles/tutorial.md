# Tutorial

This tutorial is based on the example console project that you can find in the [GitHub repository](https://github.com/strumenta/SmartReader).

## Setting Up

Once you have installed the package with Nuget, you should add the using directive to shorten the code you need to type.

```
using SmartReader;
```

## Main Method

There is nothing complicated in using this library. 

The first thing to do is calling a `Reader` constructor, to indicate where to find the content, it can either an URI or a string. For this tutorial we opt for a URL.

```csharp
namespace SmartReader.Example
{
    class Program
    {
 		static void Main(string[] args)
        {
            // we set the article
            Reader reader = new Reader("https://arstechnica.com/cars/2020/03/as-covid-19-spreads-truckers-need-to-keep-on-trucking/");
```

Calling the constructor does not retrieve the content. If you use a constructor with a second parameter, like `Reader(string uri, string text)` , you are already providing the content, so the library reads it.

Since we want to do some editing on the final article, we use a custom operation. We are going to see this function later. To improve accuracy, we also edit the `RegularExpression.Positive` used to score the elements. We are not removing anything that is already there, we just add another option. This way there is an higher chance that any element with the class `article-content` will be preserved.

            	// add a custom operation at the end
            	reader.AddCustomOperationEnd(AddInfo);
    
            	// add an option to a regular expression
            	reader.AddOptionToRegularExpression(RegularExpressions.Positive, "article-content");        	
Now we can retrieve the content and print it on the console.

We also retrieve some information about the images.

```
        	// get the article
        	Article article = reader.GetArticle();

        	// get info about images in the article
        	var images = article.GetImagesAsync();
        	images.Wait();

        	Console.WriteLine($"Is Readable: {article.IsReadable}");
        	Console.WriteLine($"Uri: {article.Uri}");
        	Console.WriteLine($"Title: {article.Title}");
        	Console.WriteLine($"Byline: {article.Byline}");
        	Console.WriteLine($"Author: {article.Author}");
        	Console.WriteLine($"Publication Date: {article.PublicationDate}");
        	Console.WriteLine($"Direction of the Text: {article.Dir}");
        	Console.WriteLine($"Language: {article.Language}");
        	Console.WriteLine($"Site Name: {article.SiteName}");
        	Console.WriteLine($"TimeToRead: {article.TimeToRead}");
        	Console.WriteLine($"Excerpt: {article.Excerpt}");
        	Console.WriteLine($"TextContent:\n {article.TextContent}");
        	Console.WriteLine($"Content:\n {article.Content}");
        	Console.WriteLine($"Featured Image: {article.FeaturedImage}");
       		Console.WriteLine($"Images Found: {images.Result?.Count()}");
    	}
```

## AddInfo Method

A custom operation takes in input an element. This element:
- contains the **whole content retrieved**, when it is a custom operation at the start of the process
- or **the extracted article**, when it is a custom operation at the end

To manipulate the element we use the [AngleSharp library](https://anglesharp.github.io/) which is used by SmartReader to parse HTML.

In our example custom operation, we append a notice at the end of the last `<div>` found, in the final part of the article extracted.

```
        static void AddInfo(AngleSharp.Dom.IElement element)
        {       
            element.QuerySelector("div").LastElementChild.InnerHtml += "<p>Article parsed by SmartReader</p>";
        }
    }
}	       	
```

## Summary

As you can see, basic usage of SmartReader is very simple. However, there are a few advanced options and setting you may be interested in, that you can discover reading the rest of the documentation.