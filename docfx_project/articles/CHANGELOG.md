# Changelog
All notable changes to this project will be documented in this file.

## 0.10.1 - 2025/08/24
- Fixed performance issue in `ConvertToPlaintext` when dealing with large inputs (thanks to [Clay Lenhart](https://github.com/xclayl))
- Added fixes from latest updates of Readability up until August 2025
- Parsing of JSON-LD element can now handle arrays
- Improve handling of titles with separators
- Keep Bilibili videos and mathjax

## 0.10.0 - 2025/02/02
- Added fixes from latest updates of Readability up until January 2025
- Improving checking and assignment of byline
- Improve parsing of JSON-LD element
- Keep OL and UL tags in lists
- Add support for automatic language identification

## 0.9.6 - 2024/10/09
- Added fixes from latest updates of Readability up until August 2024
- Allow option to modify link density value
- Small performance improvements
- Fix issue #64, by reducing value of comments containers in different languages (Dutch, Spanish, French)  (thanks to [PeterHagen](https://github.com/PeterHagen))
- Add support for finding articles in alternative languages (thanks to [Andrea Bondanini](https://github.com/AndreBonda))
- Fix vulnerability by updating dependency System.Text.Json to 8.0.5
- Update dependency AngleSharp to 1.1.2

## 0.9.5 - 2024/06/02
- Added fixes from latest updates of Readability up until May 2024
- Fix parsing of JSON-LD element
- Add support for Parsely metadata
- Ensure short links of legitimate contents are preserved
- Make sure elements are not deleted if they contain a data table
- Fix issue #60, unexpected exception thrown for forbidden content (thanks to [doggy8088 ](https://github.com/doggy8088))
- Added performance improvements suggested to conversion to plain text (thanks to [malv007](https://github.com/malv007))

## 0.9.4 - 2023/08/27
- Fix issue #58, data URIs in IMG SRC not preserved, treated as relative URL (thanks to [Acidus](https://github.com/acidus99))
- Added fixes from latest updates of Readability up until August 2023
- Expanded comma detection to non-Latin commas

## 0.9.3 - 2023/04/15
- Fix issue #55, error when parsing certain urls for date detection (thanks to [Ian Smirlis](https://github.com/iansmirlis))
- Fix issue #56, error Readability.CleanTitle() should properly escape string variable siteName with Regex.Escape() before it's applied  (thanks to [Ian Smirlis](https://github.com/iansmirlis))

## 0.9.2 - 2023/02/05
- Added fixes from latest updates of Readability up until January 2023
- Allow lists of images to remain
- Fix articles showing cookie information in reader mode
- Fix bug in TextSimilarity method
- Fix issue #53, error when parsing certain Style attributes (thanks to [Ian Smirlis](https://github.com/iansmirlis))
- Fix issue #54, error when cleaning certain invalid attributes name (thanks to [Ian Smirlis](https://github.com/iansmirlis))
- Add settings AncestorsDepth and ParagraphThreshold to customize algorithm

## 0.9.1 - 2022/10/23
- Fixed memory leaks (thanks to [Joshua Waring](https://github.com/Joshhua5))

## 0.9.0 - 2022/08/28
- Improved recognition of visibility in style attribute (thanks to [Sander Schutten](https://github.com/sschutten))
- Added use of suggested encoding/charset set in the response header. Added setting to force the encoding/charset, thus overcoming the AngleSharp heuristics, that could ignore the setting (thanks to [marhyno](https://github.com/marhyno))
- Changed setting MinContentLengthReaderable from simple integer field to Dictionary with language-based keys (thanks to [Ivan Icin](https://github.com/ivanicin))
- Fixes issue #45, error when parsing articles with noscript tag in head (thanks to [Ward Boumans](https://github.com/wardboumans))

## 0.8.1 - 2022/06/29
- Fixes issue #41, SmartReader.UriExtensions.ToAbsoluteURI throws exception when uriToCheck = "" (Thanks to [mininmaxim](https://github.com/mininmaxim))
- Parse other JSON-LD elements if the first one is not of a recognized type
- Updated IsProbablyReaderable to also check article tags
- Added fixes from latest updates of Readability up until June 2022
- Fixes issue #42, Angle Sharp parsing xml attributes (Thanks to [prestonkell](https://github.com/prestonkell))

## 0.8.0 - 2021/10/21
- Huge thanks to  [Jason Nelson](https://github.com/iamcarbon) for big improvements in optimizing and updating the quality of the code to the latest C# best practices
- Improved code quality (thanks to [Jason Nelson](https://github.com/iamcarbon))
- Updated to support .NET Standard 2.1 (thanks to [Jason Nelson](https://github.com/iamcarbon))
- Improved performance (thanks to [Jason Nelson](https://github.com/iamcarbon))
- Added settings for determining whether the document contains an article, before attempting to do so
- Improvements to header and title detection
- Improvements to handling of link density and added support for hash links
- Added improvements from latest updates of Readability up until April 2021 
- Updated Demo project to .NET 5
- Removed MimeMappings dependency

## 0.7.5 - 2020/10/31
- Fix bug Reader throws DivideByZeroException when articleTitle is empty (Thanks to [DanielEgbers](https://github.com/DanielEgbers))
- Added improvements from latest updates of Readability
- Added functionality to unwrap images that are meant to be lazy loaded
- Remove nodes with role complementary
- Fix lazy-loaded images not visibile in Kinja sites
- Added function to serialize HTML content in article
- Added support to look up metadata in JSON-LD object
- Improved byline parsing

## 0.7.4 - 2020/09/07
- Fixes [issue #22](https://github.com/Strumenta/SmartReader/issues/22), bug regarding disposal of HttpClient (Thanks to [MaratPavlov](https://github.com/MaratPavlov))

## 0.7.3 - 2020/09/05
- Fixes [issue #20](https://github.com/Strumenta/SmartReader/issues/20), bug regarding multi-thread use (Thanks to [theolivenbaum](https://github.com/theolivenbaum))
- Improved efficiency of Regex use (Thanks to [theolivenbaum](https://github.com/theolivenbaum))

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
- Added support for custom operations (Thanks to [G�bor Gergely](https://github.com/kodfodrasz)
- Added support to modify regular expressions used to determine what is part of the article and what is discarded (Thanks to [G�bor Gergely](https://github.com/kodfodrasz)
- Added improvements from latest updates of Readability

## 0.4.0 - 2018/04/01
- Fixed [issue #7](https://github.com/Strumenta/SmartReader/issues/7)
- Added support to attribute xml:lang for language detection (Thanks to [G�bor Gergely](https://github.com/kodfodrasz))
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