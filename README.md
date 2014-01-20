## F# plugin for Light Table

This plugin is an alpha implementation of a F# plugin for the [Light Table](http://www.lighttable.com) IDE.  It can evaluate the contents of a fsharp file selection.

## Installation and Usage

1.  From LightTable's plugin manager, choose the available tab and double-click the "F#" plugin to install it.
2.  fsi (fsharp interactive) should be in your PATH (fsharpi on mono)
4.  Open up a fsharp file, select some code and use `ctrl/cmd-enter` to eval it.

![Example eval of selection](https://raw.github.com/wiki/enricosada/LightTable-FSharp/images/example-eval-selection.gif)

## Requirements

You'll need .NET Framework 4 and FSharp 3.0 installed

## Acknowledgements

The clojurescript / lighttable side started by using the Python plugin as a template.

The CodeMirror file is from [CodeMirror](https://github.com/marijnh/CodeMirror)

## Changelog

#####  0.1.0

Eval binding of function and values
Mono compatibility

#####  0.0.1

Initial release with eval of selections

###License

Copyright (C) 2014 Enrico Sada

Distributed under the GPLv3, see LICENSE.md for the full text.

CodeMirror file 'codemirror/fsharp.js' is under [CodeMirror license](https://github.com/marijnh/CodeMirror/blob/master/LICENSE)
