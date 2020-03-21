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

As you can see the custom operation works on an `IElement` and it would normally rely on the AngleSharp API. AngleSharp is the library that SmartReader uses to parse and manipulate HTML. The API of the library follows the standard structure that you can use in JavaScript, so it is intuitive to use. If you need any help to use it, consult [their documentation](https://github.com/AngleSharp/AngleSharp).

## Preserving CSS Classes

Normally the library strips all classes of the elements except for `page`. This is done because classes are used to govern the display of the article, but they are irrelevant to the content itself. However, there is an option to preserve other classes. This is mostly useful if you want to perform custom operations on certain elements and you need CSS classes to identify them.

You can preserve some classes using the property `ClassesToPreserve` which is an array of class names that will be preserved. Note that this has no effect if an element that contains the class is eliminated from the extracted article. This means that the option **does not maintain the element itself**, it just maintains the class, if the element is kept in the extracted article.

```
Reader reader = // ..

// preserve the class info
reader.ClassesToPreserve = new string[] { "info" };
```

The class `page` is always kept, no matter the value you assign to this option.

## Setting Custom User Agent and Custom HttpClient

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