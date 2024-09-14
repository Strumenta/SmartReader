# Advanced Usage

This page contains information to use optional features of the library.

## Customizing Regular Expressions

You can customize the regular expressions that are used to determine whether a part of the document will be inside the article. There are two methods to do this:

- `void` **AddOptionToRegularExpression(RegularExpressions expression, string option)**<br>Add an option (i.e., usually a CSS class name) to the regular expression. <br>
- `void` **ReplaceRegularExpression(RegularExpressions expression, string newExpression)**<br>Replace the selected regular expression. <br>

The type `RegularExpression` is an `enum` that can have one of the following values, corresponding to a regular expression:
- UnlikelyCandidates
- PossibleCandidates
- Positive (increases chances to keep the element)
- Negative (decreases chances to keep the element)
- Byline
- Videos
- ShareElements
- Extraneous (note: this regular expression is not used anywhere at the moment. We keep it for alignment with the original library)

Except for the *Videos* regular expression, they all represent values of attributes, classes, etc. of tags. You should look at the code to understand how each of the regular expression is used. For instance, if you add the string `"niceContent"` to `RegularExpression.Positive`, a `div` containing `niceContent` in its `class` attribute, or `id`, will be more likely to be included in the final output.

The *Videos* regular expression represents a domain of origin of an embedded video. Since this is a string representing a regular expression, you have to remember to escape any dot present. This option is used to determine if an embed should be maintained in the article, because people generally want to see videos. If an embed matches one of the domains of this option is maintained, otherwise it is not.

```
// how to add the domain example.com
AddOptionToRegularExpression(RegularExpressions.Videos, "example\.com");
```

## Adding Custom Operations

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

As you can see, the custom operation works on an `IElement` and it would normally rely on the AngleSharp API. AngleSharp is the library that SmartReader uses to parse and manipulate HTML. The API of the library follows the standard structure that you can also see in JavaScript, so it feels intuitive and natural. If you need any help to work with it, consult [their documentation](https://github.com/AngleSharp/AngleSharp).

## Preserving CSS Classes

Normally the library strips all classes of the elements except for `page`. This is done because classes are used to govern the display of the article, but they are irrelevant to the content itself. However, there is an option to preserve other classes. This is mostly useful if you want to perform custom operations on certain elements and you need CSS classes to identify them.

You can preserve some classes using the property `ClassesToPreserve` which is an array of class names that will be preserved. Note that this has no effect if an element that contains the class is eliminated from the extracted article. This means that the option **does not maintain the element itself**, it just maintains the class, if the element is kept in the extracted article.

```
Reader reader = // ..

// preserve the class info
reader.ClassesToPreserve = new string[] { "info" };
```

The class `page` is always kept, no matter the value you assign to this option.

## Setting Custom User Agent and Custom HttpMessageHandler

By default all web requests made by the library use the User Agent *SmartReader Library*. This can be changed by using the function `SetCustomUserAgent(string)`.

```
Reader.SetCustomUserAgent("SuperAwesome App - for any issue contact admin@example.com");
```

This function will change the user agent for **all subsequent web requests** with any object of the class.

If you need to use a custom `HttpMessageHandler`, you can replace the default one, with the function `SetBaseHttpClientHandler(HttpMessageHandler)`.

```
HttpMessageHandler majesticHandler = GetMyTailorMadeHttpMessageHandler();
// ..
Reader.`SetBaseHttpClientHandler(majesticHandler);
```

## Manipulating Content Returned by Article 

By default the HTML content returned in the `Article` object it is just the property InnerHTML of the extracted HTML. You can change that by changing the static property `Serializer` of the `Article` class. This property is a `Func<IElement, string>` method, that is is a method that accepts as an argument an `IElement` and returns a `string`.

Since you have access to the AngleSharp library, this can be quite useful. For instance, in case you want to ensure that the HTML is well-formed or to transform the content in a way that is useful to your application. This is also the ideal place where to make changes to the content, such as formatting text or removing content.

What follows is a terrible example.

```
// example of an alternative serializer that remove spaces outside and inside (unless they are pre) tags
string RemoveSpace(AngleSharp.Dom.IElement element)
{
	return Regex.Replace(
		Regex.Replace(element.InnerHtml, @"(?<endBefore></.*?>)\s+(?<startAfter><[^/]>)", "${endBefore}${startAfter}"),
		@"(?<endBefore><((?!pre).)*?>)\s+",
		"${endBefore}").Trim();
}

[..]

Article.Serializer = RemoveSpace;
```

You can also replace the standard function used to convert HTML content in plain text, that is the function called to calculate the `TextContent` property of an `Article` object. You can do that by changing the property `Converter` which is also of type `Func<IElement, string>`, that is a method that accepts as an argument an `IElement` and returns a `string`. 


Since you have access to the AngleSharp library, this can be quite useful. For example, the standard converter function convert &lt;p&gt; and &lt;br&gt; tags in corresponding line breaks in text.

```
// example of an alternative converter
string MagicConverter(AngleSharp.Dom.IElement element)
{
	[..]
}

[..]

Article.Converter = MagicConverter;
```