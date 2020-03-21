# API Reference

This is where you find the API reference for the public elements of **SmartReader**.

You can navigate this reference using the sidebar on the left.

## Overview

The [Reader](xref:SmartReader.Reader) class is the main one, which contains the methods you will use to get the content and parse the article.

An [Article](xref:SmartReader.Article) object contains the article found by the library, if any. This class has a method that will get [Image]](xref:SmartReader.Image) objects, containing metadata about the images found in the parsed article.

The enums [Flags](xref:SmartReader.Flags) and [RegularExpressions](xref:SmartReader.RegularExpressions) are used to customize settings governing the parsing of the article: filtering more content, changing the evaluation of certain elements, etc. You should use them, if you encounter some problem in recognizing too little or too much content
      
The enum [ReportLevel](xref:SmartReader.ReportLevel) is used in setting the level of information that will be passed to the `LoggerDelegate` method.

