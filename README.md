# cgmerge
A command-line tool that imports modified car generators from one or more GTA
savefiles into one target savefile.

## Installation
### Install as .NET Tool (Recommended)
1) Install [.NET Core Runtime 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1)
2) Download the .nupkg from the [latest release](https://github.com/whampson/cgmerge/releases).
3) Open a shell, navigate to where you saved the .nupkg, and run the following:
  ```dotnet install --global --add-source . cgmerge```
4) Verify that the installation was successful by running `cgmerge` from the shell.

### Standalone Installation
1) Download the [latest release](https://github.com/whampson/cgmerge/releases) for your system.
2) Extract the archive.
3) Open a shell, navigate to where you extracted the archive, and run `cgmerge`.

## Usage
```
$ cgmerge --help
GTA Car Generator Merger Tool
Copyright (C) 2019-2020 Wes Hampson

cgmerge [options] target source...

Merges differing car generators from one or more 'source' savefiles into one 'target' savefile.
Merging occurs by first comparing the car generators from the source files against the car
generators in the target file for differences, then replacing the differing car generators in the
target file with car generators from the source files.


  -m, --mode             (Default: GTA3) Set whether to merge car generators from GTA3 or Vice City
                         savefiles. Valid values: GTA3, VC
  -o, --output           Set the path to the resulting savefile. If not specified, the target file
                         will be overwritten.
  -p, --priority-list    A CSV file specifying the order in which to replace car generators. The
                         columns are (priority,index) where 'priority' represents the replacement
                         order and 'index' specifies the index of a car generator in the target
                         save's car generator list. A priority of 0 is the highest priority. A
                         negative priority will exclude row from replacement. If multiple rows share
                         the same priority, one of the rows will be chosen at random and this
                         process will repeat until all have been chosen exactly once. Lines
                         beginning with '#' are treated as comments and ignored. If no priority list
                         is specified, car generators will be replaced at random.
  -r, --radius           (Default: 10) Set the collision radius. If two car generators are found
                         within the collision radius, the merge process will be aborted.
  -t, --title            Set the in-game title of the target savefile.
  -v, --verbose          Enable verbose output for hackers.
  -d, --debug            Pause until a debugger is attached.
  --help                 Display this help screen.
  --version              Display version information.
```
