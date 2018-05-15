# OpenFieldReader [![CircleCI](https://circleci.com/gh/OpenFieldReader/OpenFieldReader/tree/master.svg?style=svg)](https://circleci.com/gh/OpenFieldReader/OpenFieldReader/tree/master)
Automatically detect paper-based form fields.

## Installation

### Prerequisites

`apt-get install libunwind8`

### Download a build

- Press on the badge to consult Circle CI website
- Choose a successful build
- Consult Artifacts
- Expand project/bin
- Download the right version (which depend on ubuntu version)

Or you may want to use the API to get artifacts URL (from the latest build):

https://circleci.com/api/v1.1/project/github/OpenFieldReader/OpenFieldReader/latest/artifacts

### Installation

Ubuntu 14.04 x64

`dpkg -i openfieldreader-ubuntu.14.04-x64.deb`

Ubuntu 16.04 x64

`dpkg -i openfieldreader-ubuntu.16.04-x64.deb`

Ubuntu 16.10 x64

`dpkg -i openfieldreader-ubuntu.16.10-x64.deb`

### Usage

`openfieldreader [args]`

### Uninstallation
apt-get remove -y openfieldreader

# Description

It only focuses on paper-based forms. Because handwriting text represents valuable data. They can help automatically detect entities involved. Printed characters can be processed by tesseract 4.

The algorithm run a ICR cell-detection analysis. So, no need to define a template.

- First, we extract lines and we suppose we have ICR cell corners (line junctions).
- After, we estimate width between cells and we interconnect corners. (horizontally)
- Finally, we join top and bottom corners.

We recommend to:

- run preprocessing, noise reduction, image resizing to support different resolutions, etc.
- scan at a resolution of 300dpi for best results

It can be used for other purpose as well.
For example, it can extract cells from a sudoku grid.
If you think about it, you can see it as 9 fields with 9 characters each.

## Segmentation methods
We only support joined frame.

<p align="center">
    <img alt="Example" src="others/images/common_segmentation_methods.png" />
</p>

# Copyright and license
Code released under the MIT license.
