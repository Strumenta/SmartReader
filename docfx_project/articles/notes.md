# Notes

This page contains common issues and patterns in using the library. Feel free to suggest any improvement.

### Requesting Web Pages Using .NET HTTP APIs

Any request made with some HTTP APIs of .NET (like HttpClient, WebClient, etc.) follows the permitted values of security protocols that are set in the property `ServicePointManager.SecurityProtocol`. This property determines which versions of the TLS protocol can use. In recent versions of .NET Framework (and other .NETs platforms) the default value of this property has been changed to `SecurityProtocolType.SystemDefault` which basically means whatever combinations of values is deemed the best by the current framework. This is the ideal value, because if any TLS version stops being secure the code does not need to be updated. 

This [might cause some issues](https://github.com/Strumenta/SmartReader/issues/10), because a web server might not be able to fulfill the request. Usually this is because it uses an old version of the SSL/TLS protocol, like SSL 3.0. SmartReader neither specifies a `SecurityProtocol` value for the requests made with its internal HttpClient, nor it provides a method to change it. That is because if we did that this would affect all requests made with certain HTTP APIs, even the ones made by other parts of your code. So, if you need to access some article on an old, insecure, web server you might set the proper value of `SecurityProtocol` yourself.

```
ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
```

Alternatively, you can retrieve the content yourself in some other way and just use SmartReader to extract the article from the text.

### Potential Thread Issues when Using Synchronous Methods

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