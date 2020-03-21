# Security Considerations

The library uses standard methods provided by the .NET platform to retrieve HTML content, if you ask the library to retrieve it directly. It uses AngleSharp, a well-tested library, to handle HTML. It does not execute any operation based on the content of the input (e.g. launching a script), it just reads it and perform analysis on the content.

This means that the added security risks of using SmartReader are fairly limited. However, as any software, there are things to keep in mind to use it safely. This is especially important if you display any content to the public.

## Security of Untrusted Input

**SmartReader does not perform any security check on the input**. If you are using SmartReader with untrusted input and you are displaying the content to the user and the public at large, it is your responsibility to make sure that nothing bad happens.

The Readability team suggests using a sanitizer library. On .NET you could the [HTML Sanitizer](https://github.com/mganss/HtmlSanitizer) library. They also recommend using [CSP](https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP) on your website to add further defense-in-depth restrictions to what you allow the resulting content to do.

## Security of Embedding Images with Data URI Scheme

The library can optionally download and embed images into the HTML content, using the Data URI Scheme. 

There are no [known risks](https://github.com/mganss/HtmlSanitizer/issues/187#issuecomment-536270416) in using the Data URI Scheme specifically in img tags, with modern browsers. There are risks in using Data URI in other contexts. Follow the link for a discussion on the issue.

