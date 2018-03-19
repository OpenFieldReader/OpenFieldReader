# OpenFieldReader
Automatically detect paper-based form fields.

It only focuses on paper-based forms. Because handwriting text represents valuable data. They can help automatically detect entities involved. Printed characters can be processed by tesseract 4.

The algorithm run a ICR cell-detection analysis. So, no need to define a template.

- First, we extract lines and we suppose we have ICR cell corners (line junctions).
- After, we estimate width between cells and we interconnect corners. (horizontally)
- Finally, we join top and bottom corners.

We recommend to:

- use the python wrapper. (Include preprocessing, noise reduction, image resizing to support different resolutions, etc.)
- scan at a resolution of 300dpi for best results

It can be used for other purpose as well.
For example, it can extract cells from a sudoku grid.
If you think about it, you can see it as 9 fields with 9 characters each.

# Segmentation methods
We only support joined frame.

<p align="center">
    <img alt="Example" src="others/images/common_segmentation_methods.png" />
</p>

# Copyright and license
Code released under the MIT license.
