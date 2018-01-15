# Changelog
All notable changes to this project will be documented in this file.

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