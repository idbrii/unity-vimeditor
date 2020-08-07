# unity-vimeditor

**Requires Unity 2019.2 or later**

Add vim to the Unity's External Tools section in Preferences so all your files open in gvim.

## Features

Double click files to open them, just like built-in Visual Studio support.

Several options are available:

Win: Edit > Preferences > External Tools
Mac: Unity > Preferences > External Tools

![image](https://user-images.githubusercontent.com/43559/89668381-810a9280-d892-11ea-8fb4-7b414c53b05f.png)


## Verified

Tested with Unity 2019.4.3 on multiple platforms:

* On macOS with MacVim installed via [brew](https://brew.sh/).
* On Windows with Gvim installed via [scoop](https://scoop.sh/) (vim-nightly).


## Installation

Add this line to your Packages/manifest.json:

    "com.github.idbrii.unity-vimeditor": "https://github.com/idbrii/unity-vimeditor.git#latest-release",

Win: Edit > Preferences > External Tools
Mac: Unity > Preferences > External Tools

Then select "Vim (gvim.exe)" from the "External Script Editor" dropdown.
"(gvim.exe)" will vary based on your vim setup -- if you have the .bat files
installed, you can choose them.
