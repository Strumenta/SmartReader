# Notes

This page contains common issues and patterns in using the library. Feel free to suggest any improvement.

### Requesting Web Pages Using .NET HTTP APIs

Any request made with some HTTP APIs of .NET (like HttpClient, WebClient, etc.) follows the permitted values of security protocols that are set in the property `ServicePointManager.SecurityProtocol`. This property determines which versions of the TLS protocol can use. In recent versions of .NET Framework (and other .NETs platforms) the default value of this property has been changed to `SecurityProtocolType.SystemDefault` which basically means whatever combinations of values is deemed the best by the current framework. This is the ideal value, because if any TLS version stops being secure the code does not need to be updated. 

This [might cause some issues](https://github.com/Strumenta/SmartReader/issues/10), because a web server might not be able to fulfill the request. Usually this is because it uses an old version of the SSL/TLS protocol like SSL 3.0. SmartReader neither specifies a `SecurityProtocol` value for the requests made with its internal HttpClient, nor it provides a method to change it. That is because if we did that this would affect all requests made with certain HTTP APIs, even the ones made by other parts of your code. So, if you need to access some article on an old, insecure web server you might set the proper value of `SecurityProtocol` yourself.

```
ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
```

Alternatively, you can retrieve the content yourself in some other way and just use SmartReader to extract the article from the text.