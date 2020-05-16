# Changelog
All notable changes to this project will be documented in this file.

## 0.7.2 - 2020/05/09
- Improved documentation 
- Now we pass to the LoggerDelegate also the original body of source during Debug
- Fixed visibility of internal methods
- Moved RegularExpressions enum outside of the Reader class for consistency
- Updated demo application
- Updated dependencies of console example application
- Updated AngleSharp dependency to 0.13. This should also fix [issue #18](https://github.com/Strumenta/SmartReader/issues/18)

## 0.7.1 - 2020/03/08
- Added Readability update to preserve children when removing javascript: links
- Added Readability update to add exception to probably readable for Wikimedia Math images
- Added function to download images using the data URI scheme
- Added function to use a custom HttpClient
- Improved extraction of text content

## 0.7.0 - 2019/10/29
- Added Readability update to fix missing Wikipedia content
- Added Readability update to remove aria-hidden nodes
- Added Readability update of adding 'content' as an indicator of readable content
- Applied remaining suggestion in [issue #6](https://github.com/Strumenta/SmartReader/issues/6)
- Improved organization of code
- Merged pull-request #12 for dealing with problems when retrieving content (Thanks to [LatisVlad](https://github.com/latisvlad))
- Improved testing

## 0.6.3 - 2019/08/18
- Fixed [issue #11](https://github.com/Strumenta/SmartReader/issues/11)

## 0.6.2 - 2019/05/25
- Fixed [issue #9](https://github.com/Strumenta/SmartReader/issues/9)
- Added Readability update to transform lazy images
- Added Readability update regarding share elements

## 0.6.1 - 2019/04/20
- Fixed bug in dependency listing for the nuget package

## 0.6.0 - 2019/04/20
- Updated AngleSharp dependency. Now the minimum version is .NETStandard 2.0 (this is because of AngleSharp.Css)
- Added improvements from latest updates of Readability
- Fixed bug for property recognition
- Changed minimum time to read from 0 to 1 minute
- Improved tests

## 0.5.2 - 2019/01/12
- Added metadata for site name
- Fixed bugs for recognition of title and author metadata
- Added improvements from latest updates of Readability
- Improved documentation

## 0.5.1 - 2018/08/27
- Added support for custom operations before processing
- Added fix to preserve CSS classes when removing a DIV with only one P
- Improved testing
- Added improvements from August updates of Readability

## 0.5.0 - 2018/08/13
- Added support for custom operations (Thanks to [Gábor Gergely](https://github.com/kodfodrasz)
- Added support to modify regular expressions used to determine what is part of the article and what is discarded (Thanks to [Gábor Gergely](https://github.com/kodfodrasz)
- Added improvements from latest updates of Readability

## 0.4.0 - 2018/04/01
- Fixed [issue #7](https://github.com/Strumenta/SmartReader/issues/7)
- Added support to attribute xml:lang for language detection (Thanks to [Gábor Gergely](https://github.com/kodfodrasz))
- Added new test pages for language detection
- Added improvements from March updates of Readability

## 0.3.1 - 2018/03/03
- Fixed [issue #5](https://github.com/Strumenta/SmartReader/issues/5)
- Added improvements from February updates of Readability
- Added new test page
- Fixed comparison bugs in readability scores

## 0.3.0 - 2018/02/17 
- Cleanup of the code and naming issues (Thanks to [jamie-lord](https://github.com/jamie-lord))
- Improved testing
- Added improvement from January update of Readability
- Fixed bug for the detection of the readability of article
- Fixed bug for the fixing of relative URIs
- Fixed bug in elimination of certain nodes
- Added detection of featured image and images found in the article

## 0.2.0 - 2018/01/15
- Added improvements from December updates of Readability
- Solved [issue #2](https://github.com/Strumenta/SmartReader/issues/2) (Thanks to [Yasindn](https://github.com/yasindn))
- Breaking Changes to the API method names to improve clarity and solve issue #2. The Parse() method is now private, so if you were using it, now instead you should use the GetArticle/GetArticleAsync method. If you were using the ParseArticle method you can keep using it or choose the async version: ParseArticleAsync.
- Merged pull-request #3 for the caching of HttpClient (Thanks to [DanRigby](https://github.com/DanRigby))

## 0.1.3 - 2017/11/27
- Added improvements from November updates of Readability
- Added reading of itemprop properties for metadata extraction
- Integrated tests from Readability

## 0.1.2 - 2017/10/17

- Improved the accuracy of the calculation for reading time

## 0.1.1 - 2017/09/26

- Release based on September updates of Readability.

## 0.1.0 - 2017/08/09

- Initial release, based on a February release of Readability.